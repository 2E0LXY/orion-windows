#include "pch.h"
#include "extension_scheme_webkit.h"
#include "utils.h"
#include "webkit/Common2.h"
#include "webkit/WKPtr.h"

#include <WebKit/WKArray.h>
#include <WebKit/WKContextPrivate.h>
#include <WebKit/WKData.h>
#include <WebKit/WKDictionary.h>
#include <WebKit/WKMutableDictionary.h>
#include <WebKit/WKPage.h>
#include <WebKit/WKString.h>
#include <WebKit/WKType.h>
#include <WebKit/WKURL.h>
#include <WebKit/WKURLSchemeHandlerRef.h>

#include <src/log.h>

#include <cctype>
#include <map>
#include <string>

using WebExtensions::WebExtensionHttpHeaders;
using WebExtensions::WebExtensionRequestStatus;
using WebExtensions::WebExtensionSchemeRegistration;
using WebExtensions::WebExtensionSchemeRequest;
using WebExtensions::WebExtensionSchemeRequestHandler;
using WebExtensions::WebExtensionSchemeResponse;

namespace {

// Schemes the core registers through WebViewFactoryHost::register_scheme
// (WebViewFactoryBridge::register_schemes in the core's web_view.cpp; keep in
// sync), with the flags it asks for. They are wired to every page
// configuration up front (the core registers them only once the world is
// activated, which happens after the first view exists), and each request is
// dispatched to the handler installed for its scheme.
struct FixedScheme {
    const char* name;
    bool secure;
    bool corsEnabled;
};
constexpr FixedScheme kExtensionSchemes[] = {
    { "chrome-extension", true, false },
    { "moz-extension", true, false },
    { "extension-proxy", true, true },
};

// The core's resource responses carry no content type (GTK guesses it from
// the path too); WebKit would otherwise treat text/html as a download and
// interrupt the navigation.
const char* mimeTypeForPath(const std::string& path)
{
    const auto dot = path.find_last_of('.');
    std::string ext = dot == std::string::npos ? std::string() : path.substr(dot + 1);
    for (auto& c : ext) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    if (ext == "html" || ext == "htm") return "text/html";
    if (ext == "js" || ext == "mjs") return "text/javascript";
    if (ext == "css") return "text/css";
    if (ext == "json") return "application/json";
    if (ext == "svg") return "image/svg+xml";
    if (ext == "png") return "image/png";
    if (ext == "jpg" || ext == "jpeg") return "image/jpeg";
    if (ext == "gif") return "image/gif";
    if (ext == "webp") return "image/webp";
    if (ext == "ico") return "image/x-icon";
    if (ext == "woff") return "font/woff";
    if (ext == "woff2") return "font/woff2";
    if (ext == "txt") return "text/plain";
    if (ext == "xml") return "application/xml";
    if (ext == "wasm") return "application/wasm";
    return "application/octet-stream";
}

struct SchemeEntry {
    WebExtensionSchemeRegistration registration;
    WebExtensionSchemeRequestHandler handler;
};

// scheme -> core handler. UI thread only (WebKit C API callbacks and the
// core's bridge calls both run there). Per plugin because a Windows process
// hosts exactly one profile world; the GTK build, where one process hosts
// every profile and both worlds, keeps this per WebViewFactoryHost. A second
// world here (private mode) would need per-world dispatch.
std::map<std::string, SchemeEntry>& schemeTable()
{
    static std::map<std::string, SchemeEntry> table;
    return table;
}

void sendResponse(WKURLSchemeTaskRef task, int status, const std::string& reason,
    const std::string& mimeType, const WebExtensionHttpHeaders& headers, const std::string& body)
{
    auto wkMime = adoptWK(WKStringCreateWithUTF8CString(
        mimeType.empty() ? "application/octet-stream" : mimeType.c_str()));
#if defined(WK_HAVE_URL_SCHEME_TASK_REQUEST_DETAILS)
    auto wkHeaders = adoptWK(WKMutableDictionaryCreate());
    for (const auto& [name, value] : headers) {
        auto wkName = adoptWK(WKStringCreateWithUTF8CString(name.c_str()));
        auto wkValue = adoptWK(WKStringCreateWithUTF8CString(value.c_str()));
        WKDictionarySetItem(wkHeaders.get(), wkName.get(), wkValue.get());
    }
    auto wkReason = adoptWK(WKStringCreateWithUTF8CString(reason.c_str()));
    WKURLSchemeTaskDidReceiveResponseWithHeaders(
        task, status, wkReason.get(), wkMime.get(), body.size(), wkHeaders.get());
#else
    // Stock C API: status, MIME type and length only; response.headers (the
    // CORS headers extension-proxy adds) and the reason phrase are dropped.
    (void)reason; (void)headers;
    WKURLSchemeTaskDidReceiveResponse(task, status, wkMime.get(), body.size());
#endif
    if (!body.empty())
        WKURLSchemeTaskDidReceiveData(task, body.data(), body.size());
    WKURLSchemeTaskDidFinish(task);
}

void sendErrorPage(WKURLSchemeTaskRef task, int status, const std::string& reason)
{
    const std::string body =
        "<!DOCTYPE html><html><body><h1>" + std::to_string(status) + "</h1><p>"
        + (reason.empty() ? std::string("Extension request failed.") : reason)
        + "</p></body></html>";
    sendResponse(task, status, reason, "text/html", {}, body);
}

// Translates the core's response onto the WebKit task.
void completeTask(WKURLSchemeTaskRef task, const std::string& path, const WebExtensionSchemeResponse& response)
{
    switch (response.status) {
    case WebExtensionRequestStatus::ok:
        sendResponse(task, response.http_status_code > 0 ? response.http_status_code : 200,
            response.reason_phrase,
            response.content_type.empty() ? mimeTypeForPath(path) : response.content_type,
            response.headers, response.body);
        return;
    case WebExtensionRequestStatus::forbidden:
        sendErrorPage(task, 403, response.reason_phrase);
        return;
    case WebExtensionRequestStatus::not_found:
        sendErrorPage(task, 404, response.reason_phrase);
        return;
    case WebExtensionRequestStatus::failed:
        break;
    }
    if (!response.error_message.empty())
        orion::log_warning("ExtensionScheme> request failed: %s", response.error_message.c_str());
    sendErrorPage(task, response.http_status_code >= 400 ? response.http_status_code : 500,
        response.reason_phrase);
}

void extensionSchemeStart(WKPageConfigurationRef, WKURLSchemeTaskRef task, const void*)
{
    auto wkUrl = adoptWK(WKURLSchemeTaskCopyRequestURL(task));
    auto wkUrlString = wkUrl.get() ? adoptWK(WKURLCopyString(wkUrl.get())) : WKPtr<WKStringRef>();
    auto wkScheme = wkUrl.get() ? adoptWK(WKURLCopyScheme(wkUrl.get())) : WKPtr<WKStringRef>();
    auto wkHost = wkUrl.get() ? adoptWK(WKURLCopyHostName(wkUrl.get())) : WKPtr<WKStringRef>();
    auto wkPath = wkUrl.get() ? adoptWK(WKURLCopyPath(wkUrl.get())) : WKPtr<WKStringRef>();
    if (!wkUrlString.get() || !wkScheme.get() || !wkHost.get()) {
        orion::log_warning("ExtensionScheme> request URL missing scheme/host -> 404");
        sendErrorPage(task, 404, "Malformed extension URL.");
        return;
    }
    const std::string url = narrow(createString(wkUrlString.get()));
    const std::string scheme = narrow(createString(wkScheme.get()));
    const std::string host = narrow(createString(wkHost.get()));
    const std::string path = wkPath.get() ? narrow(createString(wkPath.get())) : std::string();
    orion::log_debug("ExtensionScheme> %s", url.c_str());

    const auto it = schemeTable().find(scheme);
    if (it == schemeTable().end()) {
        orion::log_warning(
            "ExtensionScheme> no core handler for scheme '%s' yet (profile world not activated; "
            "ConfigureWebViewExtensions not called?) -> 503", scheme.c_str());
        sendErrorPage(task, 503, "Extension runtime not ready.");
        return;
    }

    std::string method = "GET";
    if (auto wkMethod = adoptWK(WKURLSchemeTaskCopyRequestMethod(task)); wkMethod.get()) {
        std::string m = narrow(createString(wkMethod.get()));
        if (!m.empty()) method = std::move(m);
    }

    WebExtensionSchemeRequest request;
    request.uri = url;
    request.security_origin.request_host = host;
    request.method = std::move(method);
#if defined(WK_HAVE_URL_SCHEME_TASK_REQUEST_DETAILS)
    // Requesting origin: the page's active URL, which during a navigation to
    // the extension page is already that page's URL (same source GTK uses via
    // webkit_web_view_get_uri). A page on another origin therefore gets 403
    // from the core's same-origin check; a vanished page yields an empty host,
    // which the check also rejects.
    if (WKPageRef page = WKURLSchemeTaskGetPage(task)) {
        auto wkPageUrl = adoptWK(WKPageCopyActiveURL(page));
        auto wkPageHost = wkPageUrl.get() ? adoptWK(WKURLCopyHostName(wkPageUrl.get())) : WKPtr<WKStringRef>();
        if (wkPageHost.get())
            request.security_origin.web_view_host = narrow(createString(wkPageHost.get()));
    }
    if (auto wkHeaders = adoptWK(WKURLSchemeTaskCopyRequestHTTPHeaders(task)); wkHeaders.get()) {
        auto wkKeys = adoptWK(WKDictionaryCopyKeys(wkHeaders.get()));
        for (size_t i = 0, n = WKArrayGetSize(wkKeys.get()); i < n; ++i) {
            auto wkKey = static_cast<WKStringRef>(WKArrayGetItemAtIndex(wkKeys.get(), i));
            WKTypeRef wkValue = WKDictionaryGetItemForKey(wkHeaders.get(), wkKey);
            if (!wkValue || WKGetTypeID(wkValue) != WKStringGetTypeID())
                continue;
            std::string name = narrow(createString(wkKey));
            std::string value = narrow(createString(static_cast<WKStringRef>(wkValue)));
            if (_stricmp(name.c_str(), "Content-Type") == 0)
                request.body.content_type = value;
            request.headers.emplace_back(std::move(name), std::move(value));
        }
    }
    if (auto wkBody = adoptWK(WKURLSchemeTaskCopyRequestHTTPBody(task)); wkBody.get()) {
        request.body.content.assign(
            reinterpret_cast<const char*>(WKDataGetBytes(wkBody.get())), WKDataGetSize(wkBody.get()));
        request.body.total_content_length = request.body.content.size();
    }
#else
    // Stock C API: neither the requesting page nor the request headers or body
    // are exposed. Reporting the request's own host as the web view host makes
    // the core's same-origin check pass for every requester (the pre-port
    // Windows behaviour: any page could fetch an installed extension's
    // resources), and extension-proxy sees header-less GET requests only.
    request.security_origin.web_view_host = host;
#endif

    // `task` is a borrowed callback parameter; the core may complete the
    // request later (extension-proxy is asynchronous), so retain it.
    WKPtr<WKURLSchemeTaskRef> retainedTask(task);
    it->second.handler(request, [retainedTask, path](WebExtensionSchemeResponse response) {
        completeTask(retainedTask.get(), path, response);
    });
}

} // namespace

namespace orion {

void setExtensionSchemeHandler(
    const WebExtensionSchemeRegistration& registration,
    WebExtensionSchemeRequestHandler handler)
{
#if !defined(WK_HAVE_URL_SCHEME_TASK_REQUEST_DETAILS)
    if (registration.cors_enabled) {
        // The stock C API has no WKContextRegisterURLSchemeAsCORSEnabled; the
        // scheme still works for same-origin extension pages.
        orion::log_debug("ExtensionScheme> '%s': CORS registration not available in this WebKit C API",
            registration.scheme.c_str());
    }
#endif
    schemeTable()[registration.scheme] = SchemeEntry{ registration, std::move(handler) };
    orion::log_debug("ExtensionScheme> core handler installed for '%s'", registration.scheme.c_str());
}

void registerExtensionSchemeHandlers(WKContextRef context, WKPageConfigurationRef pageConf)
{
    // The flags mirror the core's registrations (see kExtensionSchemes); the
    // core's register_scheme calls arrive only after the world is activated,
    // i.e. after the first page configuration was built.
    for (const FixedScheme& scheme : kExtensionSchemes) {
        auto wkScheme = adoptWK(WKStringCreateWithUTF8CString(scheme.name));
        if (scheme.secure)
            WKContextRegisterURLSchemeAsSecure(context, wkScheme.get());
#if defined(WK_HAVE_URL_SCHEME_TASK_REQUEST_DETAILS)
        if (scheme.corsEnabled)
            WKContextRegisterURLSchemeAsCORSEnabled(context, wkScheme.get());
#endif
        WKPageConfigurationSetURLSchemeHandlerForURLScheme(
            pageConf, wkScheme.get(), extensionSchemeStart, nullptr, nullptr);
    }
}

} // namespace orion
