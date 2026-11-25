#include "pch.h"
#include "script_bridge.h"
#include "extension_scheme_webkit.h"

#include "webkit/WKPtr.h"
#include "webkit/Common2.h"
#include "webkit/WebKitBrowserWindow.h"
#include "utils.h"

#include <WebKit/WebKit2_C.h>
#include <WebKit/WKPage.h>
#include <WebKit/WKPageConfigurationRef.h>
#include <WebKit/WKPageFullScreenClient.h>  // WKCompletionListenerComplete
#include <WebKit/WKUserContentControllerRef.h>
#include <WebKit/WKUserScriptRef.h>
#include <WebKit/WKScriptMessageRef.h>
#include <WebKit/WKMutableArray.h>
#include <WebKit/WKMutableDictionary.h>
#include <WebKit/WKString.h>
#include <WebKit/WKURL.h>

#include <core/webextensions/web_view.h>
#include <src/log.h>
#include <unordered_map>

extern std::unordered_map<HWND, std::shared_ptr<BrowserWindow>> temp_browserWindows;

namespace {

// Named script worlds: the stock WebKit C API operates on the page world only.
// Orion's WebKit fork adds WKUserScriptCreateWithSourceAndMatchPatterns and
// WKUserContentControllerAddScriptMessageHandlerInWorld (advertised by the
// WK_HAVE_* macros in their headers); until the pinned WebKit carries them, warn
// when core asks for a world so the gap stays visible. WKPageCallAsyncJavaScript
// (web_view_call) has no in-world variant either way.
void warnIfScriptWorldUnsupported(const char* function, const std::string& script_world)
{
    if (script_world.empty())
        return;
    orion::log_warning(
        "ScriptBridge> %s: named script world '%s' is not supported by this WebKit C API — using the page world",
        function, script_world.c_str());
}

#if defined(WK_HAVE_USER_SCRIPT_MATCH_PATTERNS_AND_WORLDS) || defined(WK_HAVE_SCRIPT_MESSAGE_HANDLER_IN_WORLD)
// Empty world name -> null ref -> the page world, per the fork's API contract.
WKPtr<WKStringRef> toWKWorldName(const std::string& script_world)
{
    if (script_world.empty())
        return {};
    return adoptWK(WKStringCreateWithUTF8CString(script_world.c_str()));
}
#endif

#if defined(WK_HAVE_USER_SCRIPT_MATCH_PATTERNS_AND_WORLDS)
WKPtr<WKMutableArrayRef> toWKStringArray(const std::vector<std::string>& strings)
{
    auto array = adoptWK(WKMutableArrayCreateWithCapacity(strings.size()));
    for (const auto& s : strings) {
        auto wkString = adoptWK(WKStringCreateWithUTF8CString(s.c_str()));
        WKArrayAppendItem(array.get(), wkString.get());
    }
    return array;
}
#endif

WKPageRef pageForWebView(HWND hwnd)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) {
        orion::log_warning("ScriptBridge> webview hwnd=%p not found in browser windows (%zu tracked) — bridge call dropped",
            hwnd, temp_browserWindows.size());
        return nullptr;
    }
    return static_cast<WebKitBrowserWindow*>(it->second.get())->pageRef();
}

WKUserContentControllerRef contentControllerForPage(WKPageRef page)
{
    auto config = adoptWK(WKPageCopyPageConfiguration(page));
    return WKPageConfigurationGetUserContentController(config.get());
}

struct MessageHandlerData {
    orion::ScriptBridge::WebViewMessageHandler handler;
    void* context;
    void(*destroy_context)(void*);
};

void scriptMessageCallback(WKScriptMessageRef message, WKCompletionListenerRef listener, const void* ctx)
{
    auto* data = static_cast<const MessageHandlerData*>(ctx);
    WKTypeRef body = WKScriptMessageGetBody(message);
    if (!body || WKGetTypeID(body) != WKStringGetTypeID()) {
        orion::log_warning("ScriptBridge> script message body missing or not a string — passing empty payload");
        data->handler("", data->context);
    } else {
        std::wstring wstr = createString(static_cast<WKStringRef>(body));
        data->handler(narrow(wstr), data->context);
    }
    // The listener wraps an IPC reply the web process is waiting for; it MUST
    // be completed. Dropping it destroys the reply handler uninvoked.
    WKCompletionListenerComplete(listener, nullptr);
}

class WebKitScriptBridge final : public orion::ScriptBridge {
public:
    void web_view_call(
        HWND hwnd,
        const std::string& body,
        const std::string& argument0_json,
        const std::string& script_world) override
    {
        warnIfScriptWorldUnsupported("web_view_call", script_world);

        WKPageRef page = pageForWebView(hwnd);
        if (!page) return;

        auto wkScript = adoptWK(WKStringCreateWithUTF8CString(body.c_str()));
        auto wkKey = adoptWK(WKStringCreateWithUTF8CString("arg0"));
        auto wkValue = adoptWK(WKStringCreateWithUTF8CString(argument0_json.c_str()));

        auto args = adoptWK(WKMutableDictionaryCreate());
        WKDictionarySetItem(args.get(), wkKey.get(), wkValue.get());

        WKPageCallAsyncJavaScript(page, wkScript.get(),
            args.get(), nullptr, nullptr, nullptr);
    }

    void web_view_add_script(
        HWND hwnd,
        const std::vector<std::string>& matches,
        const std::vector<std::string>& exclude_matches,
        const std::string& script,
        const std::string& script_world,
        orion::WebViewScriptInjectionTime injection_time,
        orion::WebViewScriptFrames injected_frames
    ) override
    {
        // Contract (script_bridge.h): an empty match list means "inject nowhere".
        if (matches.empty()) {
            orion::log_warning("ScriptBridge> web_view_add_script: empty matches — script not injected");
            return;
        }

        WKPageRef page = pageForWebView(hwnd);
        if (!page) return;

        WKUserContentControllerRef controller = contentControllerForPage(page);
        if (!controller) {
            orion::log_warning("ScriptBridge> web_view_add_script: page has no user content controller — script not injected");
            return;
        }

        auto wkSource = adoptWK(WKStringCreateWithUTF8CString(script.c_str()));
        const _WKUserScriptInjectionTime wkInjectionTime =
            injection_time == orion::WebViewScriptInjectionTime::document_end
                ? kWKInjectAtDocumentEnd
                : kWKInjectAtDocumentStart;
        const bool forMainFrameOnly = injected_frames == orion::WebViewScriptFrames::top_frame;
#if defined(WK_HAVE_USER_SCRIPT_MATCH_PATTERNS_AND_WORLDS)
        // The fork's API takes WebCore match patterns as allow/block lists, so
        // the web process runs the script only in documents that match: each
        // extension's runtime script (with its baked-in id and manifest) stays
        // on that extension's origin, and content scripts get their manifest
        // patterns and isolated world.
        auto allowList = toWKStringArray(matches);
        auto blockList = toWKStringArray(exclude_matches);
        auto wkWorld = toWKWorldName(script_world);
        auto userScript = adoptWK(WKUserScriptCreateWithSourceAndMatchPatterns(
            wkSource.get(), wkInjectionTime, forMainFrameOnly,
            allowList.get(), blockList.get(), wkWorld.get()));
#else
        // Stock C API: WKUserScriptCreateWithSource has no match-pattern
        // parameters, so matches/exclude_matches cannot be enforced — the
        // script is injected into every page of this web view. Native dispatch
        // is still gated by web_view_security_origin_get_host, but with several
        // extensions installed every extension page also runs the *other*
        // extensions' runtime scripts, and the last one wins for the JS-side
        // constants (runtime.id, getManifest(), getURL()). Fixed once the
        // pinned WebKit carries WKUserScriptCreateWithSourceAndMatchPatterns.
        warnIfScriptWorldUnsupported("web_view_add_script", script_world);
        auto userScript = adoptWK(WKUserScriptCreateWithSource(
            wkSource.get(), wkInjectionTime, forMainFrameOnly));
#endif
        WKUserContentControllerAddUserScript(controller, userScript.get());
    }

    bool web_view_register_script_message_handler(
        HWND hwnd,
        const std::string& message_handler_name,
        WebViewMessageHandler message_handler,
        void* context,
        void(*destroy_context)(void*),
        const std::string& script_world) override
    {
        WKPageRef page = pageForWebView(hwnd);
        if (!page) return false;

        WKUserContentControllerRef controller = contentControllerForPage(page);
        if (!controller) {
            orion::log_warning("ScriptBridge> register_script_message_handler('%s'): page has no user content controller",
                message_handler_name.c_str());
            return false;
        }

        // Leak-free: WebKit C API does not expose a remove-by-name callback,
        // so the data lives until RemoveAllUserMessageHandlers is called.
        // The core module only registers handlers once per webview lifetime.
        auto* data = new MessageHandlerData {
            .handler = message_handler,
            .context = context,
            .destroy_context = destroy_context
        };

        auto wkName = adoptWK(WKStringCreateWithUTF8CString(message_handler_name.c_str()));
#if defined(WK_HAVE_SCRIPT_MESSAGE_HANDLER_IN_WORLD)
        auto wkWorld = toWKWorldName(script_world);
        WKUserContentControllerAddScriptMessageHandlerInWorld(
            controller, wkName.get(), wkWorld.get(), scriptMessageCallback, data);
#else
        warnIfScriptWorldUnsupported("web_view_register_script_message_handler", script_world);
        WKUserContentControllerAddScriptMessageHandler(
            controller, wkName.get(), scriptMessageCallback, data);
#endif
        return true;
    }

    std::string web_view_security_origin_get_host(HWND hwnd) override
    {
        WKPageRef page = pageForWebView(hwnd);
        if (!page) return "";

        auto wkUrl = adoptWK(WKPageCopyActiveURL(page));
        if (!wkUrl.get()) return "";

        auto wkHost = adoptWK(WKURLCopyHostName(wkUrl.get()));
        if (!wkHost.get()) return "";

        std::wstring host = createString(wkHost.get());
        return narrow(host);
    }

    std::string web_view_uri(HWND hwnd) override
    {
        WKPageRef page = pageForWebView(hwnd);
        if (!page) return "";

        auto wkUrl = adoptWK(WKPageCopyActiveURL(page));
        if (!wkUrl.get()) return "";

        auto wkUrlString = adoptWK(WKURLCopyString(wkUrl.get()));
        if (!wkUrlString.get()) return "";

        return narrow(createString(wkUrlString.get()));
    }

    // ---- per profile world ----

    void register_scheme(
        int64_t,
        const WebExtensions::WebExtensionSchemeRegistration& registration,
        WebExtensions::WebExtensionSchemeRequestHandler handler) override
    {
        // One world per profile process: the plugin-wide table is enough.
        orion::setExtensionSchemeHandler(registration, std::move(handler));
    }

    void update_background_pages(
        int64_t profile_id,
        const WebExtensions::WebExtensionBackgroundPagesChange& change) override
    {
        orion::log_warning(
            "ScriptBridge> update_background_pages(profile %lld): not implemented on Windows "
            "(%zu created, %zu deleted background page(s) dropped)",
            profile_id, change.created.size(), change.deleted.size());
    }
};

} // namespace

namespace orion {

ScriptBridge* create_webkit_script_bridge()
{
    static WebKitScriptBridge bridge;
    return &bridge;
}

} // namespace orion
