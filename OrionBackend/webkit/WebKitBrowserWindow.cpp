/*
 * Copyright (C) 2018 Sony Interactive Entertainment Inc.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY APPLE INC. ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 * PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL APPLE INC. OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
 * OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#include "pch.h"

#include "WebKitBrowserWindow.h"
#include "browser_engine_types.h"
#include "host_api.h"
#include "utils.h"
#include "generated/orion_version.h"

#include "../extension_scheme_webkit.h"
#include "../download_bridge.h"
#include <shlobj.h>
#include <shobjidl.h>

#include "src/log.h"

#include "Common.h"
#include "Common2.h"
#include "MiniBrowserLibResource.h"
#include <WebKit/WKAuthenticationChallenge.h>
#include <WebKit/WKAuthenticationDecisionListener.h>
#include <WebKit/WKCertificateInfoCurl.h>
#include <WebKit/WKContextConfigurationRef.h>
#include <WebKit/WKContextMenuItem.h>
#include <WebKit/WKContextPrivate.h>
#include <WebKit/WKCredential.h>
#include <WebKit/WKDownloadClient.h>
#include <WebKit/WKDownloadRef.h>
#include <WebKit/WKFrameInfoRef.h>
#include <WebKit/WKHTTPCookieStoreRef.h>
#include <WebKit/WKInspector.h>
// Kagi fork addition: embedder hosting of the inspector frontend view. Absent
// from the vcpkg-cached WebKit until WEBKIT_REF moves past it; without it the
// inspector attaches itself to the view as before.
#if __has_include(<WebKit/WKInspectorWin.h>)
#include <WebKit/WKInspectorWin.h>
#define ORION_HAS_WK_INSPECTOR_WIN 1
#else
#define ORION_HAS_WK_INSPECTOR_WIN 0
#endif
#include <WebKit/WKNavigationResponseRef.h>
#include <WebKit/WKPageFullScreenClient.h>  // WKCompletionListenerComplete
#include <WebKit/WKPagePrivate.h>
#include <WebKit/WKPreferencesRefPrivate.h>
#include <WebKit/WKProtectionSpace.h>
#include <WebKit/WKProtectionSpaceCurl.h>
#include <WebKit/WKSecurityOriginRef.h>
#include <WebKit/WKURLRequest.h>
#include <WebKit/WKURLResponse.h>
#include <WebKit/WKWebsiteDataStoreConfigurationRef.h>
#include <WebKit/WKWebsiteDataStoreRef.h>
#include <WebKit/WKWebsiteDataStoreRefCurl.h>
#include <WebKit/WKURLSchemeHandlerRef.h>
#include <WebKit/WKWindowFeaturesRef.h>
#include <WebKit/WKUserContentControllerRef.h>
#include <WebKit/WKUserScriptRef.h>
#include <WebKit/WKScriptMessageRef.h>
#include <WebKit/WKNavigationActionRef.h>

#include "window/orion_webview_tabview_widgets/scripts/autofill_scripts.h"

#include <WebKit/WKData.h>

#include <filesystem>
#include <memory>
#include <sstream>
#include <vector>

std::wstring createString(WKURLRef wkURL)
{
    if (!wkURL)
        return { };
    WKPtr<WKStringRef> url = adoptWK(WKURLCopyString(wkURL));
    return createString(url.get());
}

std::string createUTF8String(const wchar_t* src, size_t srcLength)
{
    int length = WideCharToMultiByte(CP_UTF8, 0, src, static_cast<int>(srcLength), 0, 0, nullptr, nullptr);
    std::vector<char> buffer(length);
    size_t actualLength = WideCharToMultiByte(CP_UTF8, 0, src, static_cast<int>(srcLength), buffer.data(), length, nullptr, nullptr);
    return { buffer.data(), actualLength };
}

std::wstring createPEMString(WKProtectionSpaceRef protectionSpace)
{
    auto chain = adoptWK(WKProtectionSpaceCopyCertificateChain(protectionSpace));

    std::wstring pems;

    for (size_t i = 0; i < WKArrayGetSize(chain.get()); i++) {
        auto item = WKArrayGetItemAtIndex(chain.get(), i);
        assert(WKGetTypeID(item) == WKDataGetTypeID());
        auto certificate = static_cast<WKDataRef>(item);
        auto size = WKDataGetSize(certificate);
        auto data = WKDataGetBytes(certificate);

        for (size_t i = 0; i < size; i++)
            pems.push_back(data[i]);
    }

    return replaceString(pems, L"\n", L"\r\n");
}

WKPtr<WKStringRef> createWKString(const std::wstring& str)
{
    auto utf8 = createUTF8String(str.c_str(), str.length());
    return adoptWK(WKStringCreateWithUTF8CString(utf8.data()));
}

WKPtr<WKStringRef> createWKString(const wchar_t* str)
{
    return createWKString(std::wstring(str));
}

WKPtr<WKURLRef> createWKURL(const std::wstring& str)
{
    auto utf8 = createUTF8String(str.c_str(), str.length());
    return adoptWK(WKURLCreateWithUTF8CString(utf8.data()));
}

WKPtr<WKStringRef> injectedBundlePath()
{
    // The bundle ships next to this DLL (the engine directory), not the exe.
    HMODULE module = nullptr;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(&injectedBundlePath), &module);
    std::wstring path;
    for (;;) {
        path.resize(path.size() + MAX_PATH);
        auto copied = GetModuleFileName(module, path.data(), static_cast<DWORD>(path.size()));
        if (copied < path.size()) {
            path.resize(copied);
            break;
        }
    }
    path = std::filesystem::path(path).replace_filename("MiniBrowserInjectedBundle.dll");
    return createWKString(path);
}

struct CachedWebKitProfile {
    WKPtr<WKWebsiteDataStoreRef> dataStore;
    WKPtr<WKContextRef> context;
    WKPtr<WKPreferencesRef> preferences;
};

static std::unordered_map<std::wstring, CachedWebKitProfile> s_profileCache;

void clearWebKitProfileCache() { s_profileCache.clear(); }

// Minimum page font size in px; 0 = no minimum. WKPreferences changes
// propagate to open pages, so applying to the cached profiles is live.
static uint32_t s_minimumFontSize = 0;

void setWebKitMinimumFontSize(uint32_t size)
{
    s_minimumFontSize = size;
    for (auto& [key, profile] : s_profileCache)
        WKPreferencesSetMinimumFontSize(profile.preferences.get(), size);
}

// settings::websites::javascript_setting_for_all_websites. Like the minimum
// font size, live via WKPreferences (affects loads from the next navigation;
// already-running scripts are unaffected).
static bool s_javaScriptEnabled = true;

void setWebKitJavaScriptEnabled(bool enabled)
{
    s_javaScriptEnabled = enabled;
    for (auto& [key, profile] : s_profileCache)
        WKPreferencesSetJavaScriptEnabled(profile.preferences.get(), enabled);
}

static std::wstring s_defaultUserAgent;

void setWebKitDefaultUserAgent(const std::wstring& userAgent)
{
    s_defaultUserAgent = userAgent;
}

static CachedWebKitProfile& getOrCreateProfile(const std::filesystem::path& profileDataPath)
{
    std::wstring key = profileDataPath.empty() ? L"" : profileDataPath.wstring();
    auto it = s_profileCache.find(key);
    if (it != s_profileCache.end())
        return it->second;

    auto websiteDataStoreConf = adoptWK(WKWebsiteDataStoreConfigurationCreate());

    if (!profileDataPath.empty()) {
        auto setDir = [&](auto setter, const wchar_t* subdir) {
            auto dir = (profileDataPath / subdir).wstring();
            auto wkStr = adoptWK(WKStringCreateWithUTF8CString(narrow(dir).c_str()));
            setter(websiteDataStoreConf.get(), wkStr.get());
        };
        setDir(WKWebsiteDataStoreConfigurationSetGeneralStorageDirectory, L"storage");
        setDir(WKWebsiteDataStoreConfigurationSetNetworkCacheDirectory, L"cache");
        setDir(WKWebsiteDataStoreConfigurationSetIndexedDBDatabaseDirectory, L"indexeddb");
        setDir(WKWebsiteDataStoreConfigurationSetLocalStorageDirectory, L"localstorage");
        setDir(WKWebsiteDataStoreConfigurationSetCacheStorageDirectory, L"cachestorage");
        setDir(WKWebsiteDataStoreConfigurationSetServiceWorkerRegistrationDirectory, L"serviceworkers");
        setDir(WKWebsiteDataStoreConfigurationSetResourceLoadStatisticsDirectory, L"itp");

        auto cookiePath = (profileDataPath / L"cookies.db").wstring();
        auto wkCookie = adoptWK(WKStringCreateWithUTF8CString(narrow(cookiePath).c_str()));
        WKWebsiteDataStoreConfigurationSetCookieStorageFile(websiteDataStoreConf.get(), wkCookie.get());
    }

    CachedWebKitProfile profile;
    profile.dataStore = adoptWK(WKWebsiteDataStoreCreateWithConfiguration(websiteDataStoreConf.get()));

    auto contextConf = adoptWK(WKContextConfigurationCreate());
    WKContextConfigurationSetInjectedBundlePath(contextConf.get(), injectedBundlePath().get());
    profile.context = adoptWK(WKContextCreateWithConfiguration(contextConf.get()));

    profile.preferences = adoptWK(WKPreferencesCreate());
    WKPreferencesSetMediaCapabilitiesEnabled(profile.preferences.get(), false);
    WKPreferencesSetDeveloperExtrasEnabled(profile.preferences.get(), true);
    WKPreferencesSetMinimumFontSize(profile.preferences.get(), s_minimumFontSize);
    WKPreferencesSetJavaScriptEnabled(profile.preferences.get(), s_javaScriptEnabled);
    // HTML5 element fullscreen (requestFullscreen); default is off for the
    // Win port. The window transition is driven by the WKPageFullScreenClient
    // registered in the constructor.
    WKPreferencesSetFullScreenEnabled(profile.preferences.get(), true);

    // Disable service workers. WebKit's curl/Windows port has a broken SW
    // navigation-preload path (canceled mid-load on youtube.com), after which
    // the page shell renders but SW-routed fetches return no content; direct
    // fetching works. This stable preference has no dedicated C API setter, so
    // set it by key — the same preference store the generated setter writes to.
    auto swKey = adoptWK(WKStringCreateWithUTF8CString("ServiceWorkersEnabled"));
    WKPreferencesSetBoolValueForKeyForTesting(profile.preferences.get(), false, swKey.get());

    auto [inserted, _] = s_profileCache.emplace(key, std::move(profile));
    return inserted->second;
}

#ifdef _DEBUG
static std::string getOrionVersionString()
{
    return ORION_VERSION;
}

static void orionDebugSchemeStart(WKPageConfigurationRef, WKURLSchemeTaskRef task, const void*)
{
    auto wkUrl = adoptWK(WKURLSchemeTaskCopyRequestURL(task));
    auto urlStr = createString(wkUrl.get());

    std::string body;
    if (urlStr == L"orion-debug://info" || urlStr == L"orion-debug://info/") {
        auto version = getOrionVersionString();
        body =
            "<!DOCTYPE html><html><head><meta charset='utf-8'>"
            "<title>Orion Debug Info</title>"
            "<style>"
            "body{font-family:system-ui,sans-serif;max-width:600px;margin:40px auto;"
            "color:#e0e0e0;background:#1a1a2e}"
            "h1{color:#7eb8da}dt{font-weight:600;margin-top:12px}dd{margin:2px 0 0 0;color:#b0b0b0}"
            "</style></head><body>"
            "<h1>Orion Debug Info</h1><dl>"
            "<dt>Version</dt><dd>" + version + "</dd>"
            "<dt>Engine</dt><dd>WebKit</dd>"
            "<dt>Build</dt><dd>Debug</dd>"
            "</dl></body></html>";
    } else {
        body = "<!DOCTYPE html><html><body><h1>404</h1><p>Unknown debug page.</p></body></html>";
    }

    auto mimeStr = createWKString(L"text/html");
    WKURLSchemeTaskDidReceiveResponse(task, 200, mimeStr.get(), body.size());
    WKURLSchemeTaskDidReceiveData(task, body.data(), body.size());
    WKURLSchemeTaskDidFinish(task);
}

static void registerDebugScheme(WKContextRef context, WKPageConfigurationRef pageConf)
{
    auto scheme = createWKString(L"orion-debug");
    WKContextRegisterURLSchemeAsSecure(context, scheme.get());
    WKContextRegisterURLSchemeAsBypassingContentSecurityPolicy(context, scheme.get());
    WKPageConfigurationSetURLSchemeHandlerForURLScheme(
        pageConf, scheme.get(), orionDebugSchemeStart, nullptr, nullptr);
}
#endif // _DEBUG

std::shared_ptr<BrowserWindow> WebKitBrowserWindow::create(BrowserWindowClient& client, HWND mainWnd, bool,
    const std::filesystem::path& profileDataPath)
{
    auto& profile = getOrCreateProfile(profileDataPath);

    auto pageConf = adoptWK(WKPageConfigurationCreate());
    WKPageConfigurationSetWebsiteDataStore(pageConf.get(), profile.dataStore.get());
    WKPageConfigurationSetContext(pageConf.get(), profile.context.get());
    WKPageConfigurationSetPreferences(pageConf.get(), profile.preferences.get());

#ifdef _DEBUG
    registerDebugScheme(profile.context.get(), pageConf.get());
#endif
    orion::registerExtensionSchemeHandlers(profile.context.get(), pageConf.get());

    return std::shared_ptr<BrowserWindow>(new WebKitBrowserWindow(client, pageConf.get(), mainWnd));
}

// ---------------------------------------------------------------------------
// Autofill script message handlers
// ---------------------------------------------------------------------------

// The GTK scripts pass JS objects to postMessage(). WebKit's C API delivers
// them as serialized script values that require JSC to deserialize. To avoid
// a JSC dependency, we wrap postMessage so the native handler receives a
// pre-stringified JSON string instead.
static const char* s_autofillStringifyShim = R"js(
(function() {
    var handler = window.webkit.messageHandlers['inputFocus'];
    var orig = handler.postMessage.bind(handler);
    handler.postMessage = function(data) {
        orig(JSON.stringify(data));
    };
})();
)js";

extern WebKitBrowserWindow* browserWindowForPage(WKPageRef page);

static void onInputFocusMessage(WKScriptMessageRef message, WKCompletionListenerRef listener, const void*)
{
    // The listener wraps an IPC reply the web process is waiting for; it must
    // be completed on every path.
    WKCompletionListenerComplete(listener, nullptr);
    WKTypeRef body = WKScriptMessageGetBody(message);
    if (!body || WKGetTypeID(body) != WKStringGetTypeID())
        return;
    // The user content controller carrying this handler is shared by every
    // window created from the same configuration (window.open popups share
    // their opener's) and outlives any one of them, so the destination window
    // must be resolved from the sending page at message time — a window
    // pointer in the registration context would go stale when that window
    // closes before the others.
    WKFrameInfoRef frameInfo = WKScriptMessageGetFrameInfo(message);
    WKPageRef page = frameInfo ? WKFrameInfoGetPage(frameInfo) : nullptr;
    auto* self = browserWindowForPage(page);
    if (!self)
        return;
    std::wstring wjson = createString(static_cast<WKStringRef>(body));
    std::string json = createUTF8String(wjson.c_str(), wjson.length());
    self->client().inputFocus(std::move(json));
}

static void addAutofillUserScript(WKUserContentControllerRef ucc, const std::string& source)
{
    auto wkSource = adoptWK(WKStringCreateWithUTF8CString(source.c_str()));
    auto script = adoptWK(WKUserScriptCreateWithSource(wkSource.get(), kWKInjectAtDocumentEnd, true));
    WKUserContentControllerAddUserScript(ucc, script.get());
}

static void setupAutofillScripts(WKUserContentControllerRef ucc)
{
    auto inputFocusName = adoptWK(WKStringCreateWithUTF8CString("inputFocus"));
    WKUserContentControllerAddScriptMessageHandler(ucc, inputFocusName.get(), onInputFocusMessage, nullptr);

    addAutofillUserScript(ucc, orion::gtk::get_autofill_script());
    addAutofillUserScript(ucc, orion::gtk::get_input_focus_script());
    addAutofillUserScript(ucc, s_autofillStringifyShim);
}

// ---------------------------------------------------------------------------

WebKitBrowserWindow::WebKitBrowserWindow(BrowserWindowClient& client, WKPageConfigurationRef pageConf, HWND mainWnd)
    : m_client(client)
    , m_hMainWnd(mainWnd)
{
    // Set up autofill scripts + message handlers on the content controller
    // before creating the view, so they're active from the first page load.
    // Only when creating a fresh controller: a configuration that already has
    // one (a window.open popup shares its opener's) already carries the
    // scripts — re-adding would duplicate the user scripts, and WebKit
    // silently ignores duplicate handler names anyway. The handler resolves
    // the destination window from the sending page, so one registration
    // serves every window sharing the controller.
    if (!WKPageConfigurationGetUserContentController(pageConf)) {
        auto ucc = adoptWK(WKUserContentControllerCreate());
        WKPageConfigurationSetUserContentController(pageConf, ucc.get());
        setupAutofillScripts(ucc.get());
    }

    RECT rect = { };
    m_view = adoptWK(WKViewCreate(rect, pageConf, mainWnd));
    WKViewSetIsInWindow(m_view.get(), true);
	WKViewSetUsesOffscreenRendering(m_view.get(), false);

    auto page = WKViewGetPage(m_view.get());
    m_isControlledByAutomation = false;//WKPageGetIsControlledByAutomation(page);

    // Profile-default UA, applied before any load so pages WebKit itself
    // creates (window.open popups) and loads that start before the app's
    // per-view setUserAgent also honor the setting.
    if (!s_defaultUserAgent.empty()) {
        auto ua = createWKString(s_defaultUserAgent);
        WKPageSetCustomUserAgent(page, ua.get());
    }

    WKPageNavigationClientV3 navigationClient = { };
    navigationClient.base.version = 3;
    navigationClient.base.clientInfo = this;
    navigationClient.decidePolicyForNavigationAction = decidePolicyForNavigationAction;
    navigationClient.decidePolicyForNavigationResponse = decidePolicyForNavigationResponse;
    navigationClient.didFinishNavigation = didFinishNavigation;
    navigationClient.didFailProvisionalNavigation = didFailProvisionalNavigation;
    navigationClient.webProcessDidTerminate = webProcessDidTerminate;
    navigationClient.didReceiveAuthenticationChallenge = didReceiveAuthenticationChallenge;
    navigationClient.navigationActionDidBecomeDownload = navigationActionDidBecomeDownload;
    navigationClient.navigationResponseDidBecomeDownload = navigationResponseDidBecomeDownload;
    navigationClient.contextMenuDidCreateDownload = contextMenuDidCreateDownload;
    WKPageSetPageNavigationClient(page, &navigationClient.base);

    WKPageUIClientV13 uiClient = { };
    uiClient.base.version = 13;
    uiClient.base.clientInfo = this;
    uiClient.createNewPage = createNewPage;
    uiClient.close = close;
    uiClient.didNotHandleKeyEvent = didNotHandleKeyEvent;
    uiClient.getWindowFrame = getWindowFrame;
    uiClient.runJavaScriptAlert = runJavaScriptAlert;
    uiClient.runJavaScriptConfirm = runJavaScriptConfirm;
    uiClient.runJavaScriptPrompt = runJavaScriptPrompt;
    WKPageSetPageUIClient(page, &uiClient.base);

    WKPageStateClientV0 stateClient = { };
    stateClient.base.version = 0;
    stateClient.base.clientInfo = this;
    stateClient.didChangeTitle = didChangeTitle;
    stateClient.didChangeIsLoading = didChangeIsLoading;
    stateClient.didChangeEstimatedProgress = didChangeEstimatedProgress;
    stateClient.didChangeActiveURL = didChangeActiveURL;
    stateClient.didChangeCanGoBack = didChangeCanGoBackOrForward;
    stateClient.didChangeCanGoForward = didChangeCanGoBackOrForward;
    WKPageSetPageStateClient(page, &stateClient.base);

    // Element fullscreen (requestFullscreen). WKPageSetFullScreenClient is a
    // kagi fork addition (upstream only has the ForTesting variant used by
    // WebKitTestRunner).
    WKPageFullScreenClientV0 fullScreenClient = { };
    fullScreenClient.base.version = 0;
    fullScreenClient.base.clientInfo = this;
    fullScreenClient.willEnterFullScreen = willEnterFullScreen;
    fullScreenClient.beganEnterFullScreen = beganEnterFullScreen;
    fullScreenClient.exitFullScreen = exitFullScreen;
    fullScreenClient.beganExitFullScreen = beganExitFullScreen;
    WKPageSetFullScreenClient(page, &fullScreenClient.base);

    WKPageContextMenuClientV3 contextMenuClient = { };
    contextMenuClient.base.version = 3;
    contextMenuClient.base.clientInfo = this;
    contextMenuClient.getContextMenuFromProposedMenu = getContextMenuFromProposedMenu;
    contextMenuClient.showContextMenu = showContextMenu;
    WKPageSetPageContextMenuClient(page, &contextMenuClient.base);

    WKPageFindClientV0 findClient = { };
    findClient.base.version = 0;
    findClient.base.clientInfo = this;
    findClient.didFindString = didFindString;
    findClient.didFailToFindString = didFailToFindString;
    WKPageSetPageFindClient(page, &findClient.base);

#if ORION_HAS_WK_INSPECTOR_WIN
    // With a client set, WebKit creates the inspector frontend as a hidden
    // child of our top-level window and hands it over instead of attaching
    // it to the view or opening its own window; the app hosts it.
    WKInspectorWinClientV0 inspectorClient = { };
    inspectorClient.base.version = 0;
    inspectorClient.base.clientInfo = this;
    inspectorClient.didOpenFrontend = inspectorDidOpenFrontend;
    inspectorClient.didClose = inspectorDidClose;
    inspectorClient.bringToFront = inspectorBringToFront;
    WKInspectorSetWinClient(WKPageGetInspector(page), &inspectorClient.base);
#endif

    updateProxySettings();

    adjustScaleFactors();
}

struct ScrapeContext {
    WebKitBrowserWindow* window;
    WKPtr<WKFramePolicyListenerRef> listener;
};

static void onScrapeCompleted(WKTypeRef result, WKErrorRef error, void* context)
{
    auto* ctx = static_cast<ScrapeContext*>(context);

    if (!error && result && WKGetTypeID(result) == WKStringGetTypeID()) {
        std::wstring wjson = createString(static_cast<WKStringRef>(result));
        std::string json = createUTF8String(wjson.c_str(), wjson.length());
        ctx->window->client().formSubmit(std::move(json));
    }

    if (ctx->listener)
        WKFramePolicyListenerUse(ctx->listener.get());

    delete ctx;
}

void WebKitBrowserWindow::scrapeCredentials()
{
    scrapeCredentialsAndAllow(nullptr);
}

void WebKitBrowserWindow::scrapeCredentialsAndAllow(WKFramePolicyListenerRef listener)
{
    auto page = pageRef();
    if (!page) {
        if (listener) WKFramePolicyListenerUse(listener);
        return;
    }

    static std::string scrapeJs = [] {
        std::string s = orion::gtk::get_scrape_username_password_script();
        while (!s.empty() && (s.back() == '\n' || s.back() == ';' || s.back() == ' '))
            s.pop_back();
        return "JSON.stringify((" + s + "))";
    }();

    // The listener ref is borrowed: the C API glue's stack Ref is the only
    // reference and dies when decidePolicyForNavigationAction returns, so it
    // must be retained (not adopted) to survive until onScrapeCompleted.
    auto* ctx = new ScrapeContext{this, WKPtr<WKFramePolicyListenerRef>(listener)};
    auto wkScript = adoptWK(WKStringCreateWithUTF8CString(scrapeJs.c_str()));
    WKPageEvaluateJavaScriptInMainFrame(page, wkScript.get(), ctx, onScrapeCompleted);
}

void WebKitBrowserWindow::updateProxySettings()
{
    auto page = WKViewGetPage(m_view.get());
    auto websiteDataStore = WKPageGetWebsiteDataStore(page);

    if (!m_proxy.enable) {
        WKWebsiteDataStoreDisableNetworkProxySettings(websiteDataStore);
        return;
    }

    if (!m_proxy.custom) {
        WKWebsiteDataStoreEnableDefaultNetworkProxySettings(websiteDataStore);
        return;
    }

    auto url = createWKURL(m_proxy.url);
    auto excludeHosts = createWKString(m_proxy.excludeHosts);
    WKWebsiteDataStoreEnableCustomNetworkProxySettings(websiteDataStore, url.get(), excludeHosts.get());
}

// FIXME: The current design of WebKit produces too many noises on fractional device scale factor.
// This rounds device scale factor for tantative workarond.
void WebKitBrowserWindow::adjustScaleFactors()
{
    WKPageRef page = WKViewGetPage(m_view.get());
    WKPageSetCustomBackingScaleFactor(page, std::round(static_cast<float>(GetDpiForWindow(hwnd())) / 96.0f));
    //WKPageSetCustomBackingScaleFactor(page, 1);
}

HRESULT WebKitBrowserWindow::init()
{
    return S_OK;
}

void WebKitBrowserWindow::resetFeatureMenu(FeatureType featureType, HMENU menu, bool resetsSettingsToDefaults)
{
    auto page = WKViewGetPage(m_view.get());
    auto configuration = adoptWK(WKPageCopyPageConfiguration(page));
    auto pref = WKPageConfigurationGetPreferences(configuration.get());
    switch (featureType) {
    case FeatureType::Experimental: {
        auto features = adoptWK(WKPreferencesCopyExperimentalFeatures(pref));
        auto size = WKArrayGetSize(features.get());
        for (size_t i = 0; i < size; i++) {
            auto item = WKArrayGetItemAtIndex(features.get(), i);
            assert(WKGetTypeID(item) == WKFeatureGetTypeID());
            auto feature = static_cast<WKFeatureRef>(item);
            auto name = createString(adoptWK(WKFeatureCopyName(feature)).get());
            auto key = adoptWK(WKFeatureCopyKey(feature));
            bool defaultValue = WKFeatureDefaultValue(feature);
            if (resetsSettingsToDefaults) {
                auto flag = MF_BYCOMMAND | (defaultValue ? MF_CHECKED : MF_UNCHECKED);
                CheckMenuItem(menu, IDM_EXPERIMENTAL_FEATURES_BEGIN + static_cast<UINT>(i), flag);
                WKPreferencesSetExperimentalFeatureForKey(pref, defaultValue, key.get());
            } else {
                auto flag = MF_STRING | (defaultValue ? MF_CHECKED : MF_UNCHECKED);
                AppendMenu(menu, flag, IDM_EXPERIMENTAL_FEATURES_BEGIN + i, name.c_str());
                m_experimentalFeatureKeys.push_back(std::move(key));
            }
        }
        break;
    }
    case FeatureType::InternalDebug: {
        auto features = adoptWK(WKPreferencesCopyInternalDebugFeatures(pref));
        auto size = WKArrayGetSize(features.get());
        for (size_t i = 0; i < size; i++) {
            auto item = WKArrayGetItemAtIndex(features.get(), i);
            assert(WKGetTypeID(item) == WKFeatureGetTypeID());
            auto feature = static_cast<WKFeatureRef>(item);
            auto name = createString(adoptWK(WKFeatureCopyName(feature)).get());
            auto key = adoptWK(WKFeatureCopyKey(feature));
            bool defaultValue = WKFeatureDefaultValue(feature);
            if (resetsSettingsToDefaults) {
                auto flag = MF_BYCOMMAND | (defaultValue ? MF_CHECKED : MF_UNCHECKED);
                CheckMenuItem(menu, IDM_INTERNAL_DEBUG_FEATURES_BEGIN + static_cast<UINT>(i), flag);
                WKPreferencesSetInternalDebugFeatureForKey(pref, defaultValue, key.get());
            } else {
                auto flag = MF_STRING | (defaultValue ? MF_CHECKED : MF_UNCHECKED);
                AppendMenu(menu, flag, IDM_INTERNAL_DEBUG_FEATURES_BEGIN + i, name.c_str());
                m_internalDebugFeatureKeys.push_back(std::move(key));
            }
        }
        break;
    }
    }
}

HWND WebKitBrowserWindow::hwnd()
{
    return WKViewGetWindow(m_view.get());
}

HRESULT WebKitBrowserWindow::loadURL(const std::wstring& url)
{
    auto page = WKViewGetPage(m_view.get());
    WKPageLoadURL(page, createWKURL(url).get());
    return true;
}

void WebKitBrowserWindow::loadHTMLString(const std::wstring& html, const std::wstring& unreachableUrl)
{
    auto page = WKViewGetPage(m_view.get());
    auto htmlStr = createWKString(html);
    auto baseUrl = createWKURL(unreachableUrl.empty() ? L"about:error" : unreachableUrl);
    WKPageLoadHTMLString(page, htmlStr.get(), baseUrl.get());
}

void WebKitBrowserWindow::reload()
{
    auto page = WKViewGetPage(m_view.get());
    WKPageReload(page);
}

void WebKitBrowserWindow::reloadBypassingCache()
{
    auto page = WKViewGetPage(m_view.get());
    WKPageReloadFromOrigin(page);
}

void WebKitBrowserWindow::navigateForwardOrBackward(bool forward)
{
    auto page = WKViewGetPage(m_view.get());
    if (forward)
        WKPageGoForward(page);
    else
        WKPageGoBack(page);
}

void WebKitBrowserWindow::navigateToHistory(UINT)
{
    // Not implemented
}

void WebKitBrowserWindow::setPreference(UINT menuID, bool enable)
{
    auto page = WKViewGetPage(m_view.get());
    auto configuration = adoptWK(WKPageCopyPageConfiguration(page));
    auto pref = WKPageConfigurationGetPreferences(configuration.get());
    if (IDM_EXPERIMENTAL_FEATURES_BEGIN <= menuID && menuID <= IDM_EXPERIMENTAL_FEATURES_END) {
        int index = menuID - IDM_EXPERIMENTAL_FEATURES_BEGIN;
        WKPreferencesSetExperimentalFeatureForKey(pref, enable, m_experimentalFeatureKeys[index].get());
        return;
    }
    if (IDM_INTERNAL_DEBUG_FEATURES_BEGIN <= menuID && menuID <= IDM_INTERNAL_DEBUG_FEATURES_END) {
        int index = menuID - IDM_INTERNAL_DEBUG_FEATURES_BEGIN;
        WKPreferencesSetInternalDebugFeatureForKey(pref, enable, m_internalDebugFeatureKeys[index].get());
        return;
    }
    switch (menuID) {
    case IDM_ACC_COMPOSITING:
        WKPreferencesSetAcceleratedCompositingEnabled(pref, enable);
        break;
    case IDM_COMPOSITING_BORDERS:
        WKPreferencesSetCompositingBordersVisible(pref, enable);
        WKPreferencesSetCompositingRepaintCountersVisible(pref, enable);
        break;
    case IDM_DEBUG_INFO_LAYER:
        WKPreferencesSetTiledScrollingIndicatorVisible(pref, enable);
        break;
    case IDM_DISABLE_IMAGES:
        WKPreferencesSetLoadsImagesAutomatically(pref, !enable);
        break;
    case IDM_DISABLE_JAVASCRIPT:
        WKPreferencesSetJavaScriptEnabled(pref, !enable);
        break;
    }
}

void WebKitBrowserWindow::print()
{
    // Not implemented
}

void WebKitBrowserWindow::launchInspector()
{
    auto page = WKViewGetPage(m_view.get());
    auto inspector = WKPageGetInspector(page);
    WKInspectorShow(inspector);
}

void WebKitBrowserWindow::closeInspector()
{
    auto page = WKViewGetPage(m_view.get());
    WKInspectorClose(WKPageGetInspector(page));
}

void WebKitBrowserWindow::setUseDarkAppearance(bool useDarkAppearance)
{
    WKViewSetUseDarkAppearance(m_view.get(), useDarkAppearance);
#if ORION_HAS_WK_INSPECTOR_WIN
    WKInspectorSetUseDarkAppearance(WKPageGetInspector(WKViewGetPage(m_view.get())), useDarkAppearance);
#endif
}

void WebKitBrowserWindow::openProxySettings()
{
    if (askProxySettings(m_hMainWnd, m_proxy))
        updateProxySettings();

}

void WebKitBrowserWindow::setUserAgent(const std::wstring& customUAString)
{
    auto page = WKViewGetPage(m_view.get());
    auto ua = createWKString(customUAString);
    WKPageSetCustomUserAgent(page, ua.get());
}

std::wstring WebKitBrowserWindow::userAgent()
{
    auto page = WKViewGetPage(m_view.get());
    auto ua = adoptWK(WKPageCopyUserAgent(page));
    return createString(ua.get());
}

void WebKitBrowserWindow::showLayerTree()
{
    auto page = WKViewGetPage(m_view.get());
    auto name = createWKString(L"DumpLayerTree");
    WKPagePostMessageToInjectedBundle(page, name.get(), nullptr);
}

void WebKitBrowserWindow::updateStatistics(HWND)
{
    // Not implemented
}


void WebKitBrowserWindow::resetZoom()
{
    auto page = WKViewGetPage(m_view.get());
    WKPageSetPageZoomFactor(page, 1);
}

void WebKitBrowserWindow::zoomIn()
{
    auto page = WKViewGetPage(m_view.get());
    double s = WKPageGetPageZoomFactor(page);
    WKPageSetPageZoomFactor(page, s * 1.25);
}

void WebKitBrowserWindow::zoomOut()
{
    auto page = WKViewGetPage(m_view.get());
    double s = WKPageGetPageZoomFactor(page);
    WKPageSetPageZoomFactor(page, s * 0.8);
}

static void deleteAllCookiesCallback(void*)
{

}

void WebKitBrowserWindow::clearCookies()
{
    auto page = WKViewGetPage(m_view.get());
    auto websiteDataStore = WKPageGetWebsiteDataStore(page);

    WKHTTPCookieStoreRef cookieStore = WKWebsiteDataStoreGetHTTPCookieStore(websiteDataStore);
    if (cookieStore)
        WKHTTPCookieStoreDeleteAllCookies(cookieStore, this, deleteAllCookiesCallback);
}

static void removeAllFetchCachesCallback(void*)
{
}

static void removeNetworkCacheCallback(void*)
{
}

static void removeMemoryCachesCallback(void*)
{
}

static void removeAllServiceWorkerRegistrationsCallback(void*)
{
}

static void removeAllIndexedDatabasesCallback(void*)
{
}

static void removeLocalStorageCallback(void*)
{
}
    
void WebKitBrowserWindow::clearWebsiteData()
{
    auto page = WKViewGetPage(m_view.get());
    auto websiteDataStore = WKPageGetWebsiteDataStore(page);

    WKWebsiteDataStoreRemoveAllFetchCaches(websiteDataStore, this, removeAllFetchCachesCallback);
    WKWebsiteDataStoreRemoveNetworkCache(websiteDataStore, this, removeNetworkCacheCallback);
    WKWebsiteDataStoreRemoveMemoryCaches(websiteDataStore, this, removeMemoryCachesCallback);
    WKWebsiteDataStoreRemoveAllServiceWorkerRegistrations(websiteDataStore, this, removeAllServiceWorkerRegistrationsCallback);
    WKWebsiteDataStoreRemoveAllIndexedDatabases(websiteDataStore, this, removeAllIndexedDatabasesCallback);
    WKWebsiteDataStoreRemoveLocalStorage(websiteDataStore, this, removeLocalStorageCallback);
}

static WebKitBrowserWindow& toWebKitBrowserWindow(const void *clientInfo)
{
    return *const_cast<WebKitBrowserWindow*>(static_cast<const WebKitBrowserWindow*>(clientInfo));
}

#if ORION_HAS_WK_INSPECTOR_WIN
void WebKitBrowserWindow::inspectorDidOpenFrontend(WKInspectorRef, HWND frontendWindow, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    thisWindow.m_inspectorViewHwnd = frontendWindow;
    thisWindow.m_client.devToolsViewChanged(frontendWindow);
}

void WebKitBrowserWindow::inspectorDidClose(WKInspectorRef, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    thisWindow.m_inspectorViewHwnd = nullptr;
    thisWindow.m_client.devToolsViewChanged(nullptr);
}

void WebKitBrowserWindow::inspectorBringToFront(WKInspectorRef, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    if (thisWindow.m_inspectorViewHwnd)
        thisWindow.m_client.devToolsViewChanged(thisWindow.m_inspectorViewHwnd);
}
#endif

void WebKitBrowserWindow::didChangeTitle(const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto page = WKViewGetPage(thisWindow.m_view.get());
    WKPtr<WKStringRef> title = adoptWK(WKPageCopyTitle(page));
    std::wstring titleString = createString(title.get());// + L" [WebKit]";
    SetWindowText(thisWindow.m_hMainWnd, titleString.c_str());
    thisWindow.m_client.titleChanged(titleString);
}

void WebKitBrowserWindow::didChangeIsLoading(const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto page = WKViewGetPage(thisWindow.m_view.get());
    thisWindow.m_isLoading = !thisWindow.m_isLoading;
    thisWindow.m_client.loadingStateChanged(thisWindow.m_isLoading);

    if (WKPageGetEstimatedProgress(page) >= 1.0)
        extractFavicon(page, clientInfo);
}

void WebKitBrowserWindow::didChangeEstimatedProgress(const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto page = WKViewGetPage(thisWindow.m_view.get());
    thisWindow.m_client.progressChanged(WKPageGetEstimatedProgress(page));
}

void WebKitBrowserWindow::didChangeActiveURL(const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto page = WKViewGetPage(thisWindow.m_view.get());
    WKPtr<WKURLRef> url = adoptWK(WKPageCopyActiveURL(page));
    thisWindow.m_client.activeURLChanged(createString(url.get()));
}

void WebKitBrowserWindow::didChangeCanGoBackOrForward(const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto page = WKViewGetPage(thisWindow.m_view.get());
    thisWindow.m_client.navigationStateChanged(
        WKPageCanGoBack(page), WKPageCanGoForward(page));
}

// ---------------------------------------------------------------------------
// Element fullscreen (WKPageFullScreenClient)
//
// willEnterFullScreen / beganExitFullScreen carry a completion listener the
// web process waits on mid-transition; it must be completed on every path.
// The window/chrome transition itself happens in the C# handler behind
// elementFullscreenChanged; the fullscreen element renders into the normal
// view, which follows the window size.
// ---------------------------------------------------------------------------

void WebKitBrowserWindow::willEnterFullScreen(WKPageRef, WKCompletionListenerRef listener, const void* clientInfo)
{
    WKCompletionListenerComplete(listener, nullptr);
    toWebKitBrowserWindow(clientInfo).m_client.elementFullscreenChanged(true);
}

void WebKitBrowserWindow::beganEnterFullScreen(WKPageRef, WKRect, WKRect, const void*)
{
    // Notification only; the frames are unused (zero rects on the Win port).
}

void WebKitBrowserWindow::exitFullScreen(WKPageRef, const void* clientInfo)
{
    toWebKitBrowserWindow(clientInfo).m_client.elementFullscreenChanged(false);
}

void WebKitBrowserWindow::beganExitFullScreen(WKPageRef, WKRect, WKRect, WKCompletionListenerRef listener, const void*)
{
    WKCompletionListenerComplete(listener, nullptr);
}

// ---------------------------------------------------------------------------
// Find in page (WKPageFindClient)
// ---------------------------------------------------------------------------

// Bounds match counting/marking work in the web process (FindController stops
// at cap + 1 and reports the more-than-cap sentinel). Same limit as AppKit's
// NSTextFinder.
static constexpr unsigned kMaxFindMatches = 1000;

void WebKitBrowserWindow::findInPage(const char* text, bool forward, bool findNext)
{
    if (text != m_findText) {
        m_findText = text;
        findNext = false;
    }
    m_findPendingNewSearch = !findNext;
    m_findPendingBackwards = !forward;

    WKFindOptions options = kWKFindOptionsCaseInsensitive | kWKFindOptionsWrapAround
        | kWKFindOptionsShowOverlay | kWKFindOptionsShowFindIndicator | kWKFindOptionsShowHighlight;
    if (!forward)
        options |= kWKFindOptionsBackwards;

    auto string = adoptWK(WKStringCreateWithUTF8CString(m_findText.c_str()));
    WKPageFindString(pageRef(), string.get(), options, kMaxFindMatches);
}

void WebKitBrowserWindow::stopFindInPage()
{
    m_findText.clear();
    m_findOrdinal = 0;
    WKPageHideFindUI(pageRef());
}

void WebKitBrowserWindow::didFindString(WKPageRef, WKStringRef, unsigned matchCount, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    if (matchCount > kMaxFindMatches) {
        // kWKMoreThanMaximumMatchCount sentinel: more matches than the cap;
        // the exact count is unknown, and so is the ordinal.
        thisWindow.m_findOrdinal = 0;
        thisWindow.m_client.findResult(-static_cast<int32_t>(kMaxFindMatches), 0);
        return;
    }
    auto count = static_cast<int32_t>(matchCount);
    int32_t& ordinal = thisWindow.m_findOrdinal;
    if (thisWindow.m_findPendingNewSearch || ordinal == 0)
        ordinal = 1;
    else if (thisWindow.m_findPendingBackwards)
        ordinal = ordinal > 1 ? ordinal - 1 : count;
    else
        ordinal = ordinal < count ? ordinal + 1 : 1;
    if (ordinal > count) // count shrank under a stale ordinal (page mutated)
        ordinal = count;
    thisWindow.m_client.findResult(count, ordinal);
}

void WebKitBrowserWindow::didFailToFindString(WKPageRef, WKStringRef, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    thisWindow.m_findOrdinal = 0;
    thisWindow.m_client.findResult(0, 0);
}

static const char* s_faviconScript = R"js(
    (function() {
        var icons = document.querySelectorAll('link[rel="icon"], link[rel="shortcut icon"]');
        var urls = [];
        for (var i = 0; i < icons.length; i++) {
            if (icons[i].href) urls.push(icons[i].href);
        }
        if (urls.length === 0) urls.push(new URL('/favicon.ico', location.href).href);
        return urls.join('\n');
    })()
)js";

void WebKitBrowserWindow::extractFavicon(WKPageRef page, const void* clientInfo)
{
    auto script = adoptWK(WKStringCreateWithUTF8CString(s_faviconScript));
    WKPageEvaluateJavaScriptInMainFrame(page, script.get(),
        const_cast<void*>(clientInfo), faviconScriptCompleted);
}

// ---------------------------------------------------------------------------
// Tab snapshot capture — visible-viewport JPEG via the Kagi-fork
// WKPageTakeSnapshotData C API; the bytes are delivered through the
// client's snapshotTaken callback.
// ---------------------------------------------------------------------------

void WebKitBrowserWindow::takeSnapshot(int32_t maxPixelWidth)
{
    auto page = WKViewGetPage(m_view.get());
    // The context holds only a weak reference: the window (and its client)
    // may be destroyed before the async result arrives.
    auto* context = new std::weak_ptr<BrowserWindow>(shared_from_this());
    WKPageTakeSnapshotData(page, maxPixelWidth, /*jpegQuality=*/60,
        context, [](WKDataRef data, WKErrorRef, void* ctx) {
            std::unique_ptr<std::weak_ptr<BrowserWindow>> weak(
                static_cast<std::weak_ptr<BrowserWindow>*>(ctx));
            std::shared_ptr<BrowserWindow> window = weak->lock();
            if (!window)
                return;
            auto& client = static_cast<WebKitBrowserWindow&>(*window).client();
            size_t size = data ? WKDataGetSize(data) : 0;
            if (size)
                client.snapshotTaken(WKDataGetBytes(data), static_cast<int32_t>(size));
            else
                client.snapshotTaken(nullptr, 0);
        });
}

void WebKitBrowserWindow::didFinishNavigation(WKPageRef page, WKNavigationRef, WKTypeRef, const void* clientInfo)
{
    extractFavicon(page, clientInfo);
}

void WebKitBrowserWindow::faviconScriptCompleted(WKTypeRef result, WKErrorRef error, void* context)
{
    if (error || !result || WKGetTypeID(result) != WKStringGetTypeID())
        return;

    auto iconUrls = createString(static_cast<WKStringRef>(result));
    if (iconUrls.empty())
        return;

    auto& thisWindow = toWebKitBrowserWindow(context);
    auto page = WKViewGetPage(thisWindow.m_view.get());
    WKPtr<WKURLRef> wkUrl = adoptWK(WKPageCopyActiveURL(page));
    auto pageUrl = createString(wkUrl.get());

    if (!pageUrl.empty())
        thisWindow.m_client.faviconUrlChanged(std::move(pageUrl), std::move(iconUrls));
}

// Maps a modifier-clicked link (Ctrl/Shift/middle) to an OpenDisposition,
// following the same gesture mapping as the macOS and GTK Orion ports plus
// Windows-convention Shift+click. Plain clicks return false and navigate
// normally; Alt+click keeps its download meaning.
static bool linkClickOpenDisposition(WKNavigationActionRef navigationAction, int32_t& disposition)
{
    if (WKNavigationActionShouldPerformDownload(navigationAction))
        return false;

    WKEventModifiers modifiers = WKNavigationActionGetModifiers(navigationAction);
    WKEventMouseButton button = WKNavigationActionGetMouseButton(navigationAction);
    bool ctrl = modifiers & kWKEventModifiersControlKey;
    bool shift = modifiers & kWKEventModifiersShiftKey;

    if (ctrl || button == kWKEventMouseButtonMiddleButton) {
        disposition = shift ? OpenDisposition::NewForegroundTab : OpenDisposition::NewBackgroundTab;
        return true;
    }
    if (shift && button == kWKEventMouseButtonLeftButton) {
        disposition = OpenDisposition::NewWindow;
        return true;
    }
    return false;
}

// Sends a diverted navigation's URL to C# to open per `disposition`.
static void openNavigationActionURL(WKNavigationActionRef navigationAction, HWND originatingHwnd, int32_t disposition, OpenURLCallback cb)
{
    auto request = adoptWK(WKNavigationActionCopyRequest(navigationAction));
    auto url = adoptWK(WKURLRequestCopyURL(request.get()));
    cb(originatingHwnd, createString(url.get()).c_str(), disposition);
}

void WebKitBrowserWindow::decidePolicyForNavigationAction(WKPageRef page, WKNavigationActionRef navigationAction, WKFramePolicyListenerRef listener, WKTypeRef, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);

    auto targetFrame = adoptWK(WKNavigationActionCopyTargetFrameInfo(navigationAction));
    bool isMainFrame = targetFrame && WKFrameInfoGetIsMainFrame(targetFrame.get());
    auto navType = WKNavigationActionGetNavigationType(navigationAction);

    // A modifier-clicked link (Ctrl/Shift/middle) opens in a new tab or
    // window instead of navigating this view. Checked before the frame
    // gates so links inside subframes divert too.
    if (navType == kWKFrameNavigationTypeLinkClicked) {
        int32_t disposition;
        if (linkClickOpenDisposition(navigationAction, disposition)) {
            if (OpenURLCallback cb = getOpenURLCallback()) {
                openNavigationActionURL(navigationAction, thisWindow.m_hMainWnd, disposition, cb);
                WKFramePolicyListenerIgnore(listener);
                return;
            }
        }
    }

    if (!isMainFrame) {
        WKFramePolicyListenerUse(listener);
        return;
    }

    if (navType != kWKFrameNavigationTypeFormSubmitted &&
        navType != kWKFrameNavigationTypeFormResubmitted &&
        navType != kWKFrameNavigationTypeLinkClicked &&
        navType != kWKFrameNavigationTypeOther) {
        WKFramePolicyListenerUse(listener);
        return;
    }

    thisWindow.scrapeCredentialsAndAllow(listener);
}

void WebKitBrowserWindow::decidePolicyForNavigationResponse(WKPageRef, WKNavigationResponseRef navigationResponse, WKFramePolicyListenerRef listener, WKTypeRef, const void*)
{
    // An HTTP error status (403, 404, 500, ...) does not gate rendering: such a
    // response carrying an HTML body is a normal page and must render, as it
    // does in every other WebKit port. Transport-level failures never reach
    // this callback — they go through didFailProvisionalNavigation and get
    // Orion's error page instead.
    auto response = adoptWK(WKNavigationResponseCopyResponse(navigationResponse));
    auto isMainFrame = WKFrameInfoGetIsMainFrame(adoptWK(WKNavigationResponseCopyFrameInfo(navigationResponse)).get());

    auto urlStr = createString(adoptWK(WKURLResponseCopyURL(response.get())).get());
    if (isMainFrame && bridgeHasPendingRestart(narrow(urlStr))) {
        WKFramePolicyListenerDownload(listener);
        return;
    }

    // Route to download only a genuine HTTP response whose body WebKit can't
    // render. Three responses must render (Use) instead — choosing Download
    // (or Ignore) would needlessly interrupt the main-frame provisional load
    // via frameLoadInterruptedByPolicyChange:
    //   * 204 No Content — legitimately body-less (matches WebKitGTK's
    //     SOUP_STATUS_NO_CONTENT special-casing).
    //   * A response with no HTTP status (status 0) — not a real HTTP response,
    //     so there is nothing to download.
    //   * An error status declaring an empty body (e.g. a bodiless 401 after a
    //     cancelled auth challenge) — offering a zero-byte download is noise;
    //     render blank. Restricted to declared Content-Length 0 (unknown
    //     lengths report as huge) and status >= 400, so a deliberate
    //     empty-body download on a success status is left alone.
    auto statusCode = WKURLResponseHTTPStatusCode(response.get());
    bool emptyErrorBody = statusCode >= 400
        && WKURLResponseGetExpectedContentLength64(response.get()) == 0;
    if (WKNavigationResponseCanShowMIMEType(navigationResponse) || !isMainFrame
        || statusCode == 204 || statusCode == 0 || emptyErrorBody)
        WKFramePolicyListenerUse(listener);
    else
        WKFramePolicyListenerDownload(listener);
}

void WebKitBrowserWindow::didFailProvisionalNavigation(WKPageRef, WKNavigationRef, WKErrorRef error, WKTypeRef, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    int rawCode = WKErrorGetErrorCode(error);
    auto domain = createString(adoptWK(WKErrorCopyDomain(error)).get());
    auto failingUrl = createString(adoptWK(WKErrorCopyFailingURL(error)).get());
    auto description = createString(adoptWK(WKErrorCopyLocalizedDescription(error)).get());

    orion::log_info("WebKit didFailProvisionalNavigation: code=%d domain=%ls url=%ls desc=%ls",
        rawCode, domain.c_str(), failingUrl.c_str(), description.c_str());

    // A superseded provisional load (the user navigates again, hits stop, or a
    // redirect replaces the pending load before it commits) is cancelled rather
    // than failed: WebKit reports kWKErrorCodeCancelled, and Orion must not paint
    // its error page over it. Real transport failures use a curl error code and
    // still render the error page below.
    if (rawCode == kWKErrorCodeCancelled)
        return;

    // WebKit on Windows uses curl — map curl error codes to unified categories.
    int32_t category = ErrorCategory::Unknown;
    switch (rawCode) {
    case 6:  category = ErrorCategory::DnsError; break;
    case 7:  category = ErrorCategory::ConnectionRefused; break;
    case 28: category = ErrorCategory::Timeout; break;
    case 35: case 51: case 60:
        category = ErrorCategory::SslError; break;
    default: category = ErrorCategory::NetworkError; break;
    }

    std::wstring html = thisWindow.m_client.navigationError(category, rawCode,
        failingUrl, description);
    if (!html.empty()) {
        // WebKit silently drops loadHTMLString calls from inside
        // didFailProvisionalNavigation.  Defer to the next message loop tick.
        struct ErrorPageData { std::weak_ptr<BrowserWindow> window; std::wstring html; std::wstring url; };
        auto* data = new ErrorPageData{thisWindow.shared_from_this(), std::move(html), std::move(failingUrl)};
        SetTimer(thisWindow.m_hMainWnd, reinterpret_cast<UINT_PTR>(data), 0,
            [](HWND hwnd, UINT, UINT_PTR id, DWORD) {
                KillTimer(hwnd, id);
                auto* d = reinterpret_cast<ErrorPageData*>(id);
                if (auto win = std::dynamic_pointer_cast<WebKitBrowserWindow>(d->window.lock()))
                    win->loadHTMLString(d->html, d->url);
                delete d;
            });
    }
}

void WebKitBrowserWindow::webProcessDidTerminate(WKPageRef page, WKProcessTerminationReason reason, const void* clientInfo)
{
    const char* reasonStr = "Unknown";
    switch (reason) {
    case kWKProcessTerminationReasonExceededMemoryLimit: reasonStr = "ExceededMemoryLimit"; break;
    case kWKProcessTerminationReasonExceededCPULimit:    reasonStr = "ExceededCPULimit"; break;
    case kWKProcessTerminationReasonRequestedByClient:   return;
    case kWKProcessTerminationReasonCrash:               reasonStr = "Crash"; break;
    }
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    thisWindow.m_client.webProcessTerminated("WebKitWebProcess.exe", reason, reasonStr);
}

// HTTP-auth challenges parked while the app shows its credentials dialog,
// resolved exactly once via respondToWebKitAuthChallenge. The challenge
// arrives as a borrowed callback parameter, so parking RETAINS it (the WKPtr
// constructor, never adoptWK); the retained challenge keeps its decision
// listener alive until the answer.
struct PendingWebKitAuthChallenge
{
    WKPtr<WKAuthenticationChallengeRef> challenge;
    HWND viewHwnd = nullptr;
};

static std::unordered_map<int64_t, PendingWebKitAuthChallenge> s_pendingAuthChallenges;
static int64_t s_nextAuthRequestId = 1;

void respondToWebKitAuthChallenge(int64_t requestId, bool allow,
    const wchar_t* username, const wchar_t* password)
{
    auto node = s_pendingAuthChallenges.extract(requestId);
    if (node.empty())
        return; // view already gone, or resolved by a lifecycle sweep
    PendingWebKitAuthChallenge& pending = node.mapped();
    auto listener = WKAuthenticationChallengeGetDecisionListener(pending.challenge.get());
    if (allow) {
        WKPtr<WKStringRef> user = createWKString(username ? username : L"");
        WKPtr<WKStringRef> pass = createWKString(password ? password : L"");
        WKPtr<WKCredentialRef> credential = adoptWK(WKCredentialCreate(
            user.get(), pass.get(), kWKCredentialPersistenceForSession));
        WKAuthenticationDecisionListenerUseCredential(listener, credential.get());
    } else {
        // Continue without credentials — the site's 401 body renders,
        // matching the CEF path's cancel semantics.
        WKAuthenticationDecisionListenerUseCredential(listener, nullptr);
    }
}

void cancelWebKitAuthChallengesForView(HWND viewHwnd)
{
    std::erase_if(s_pendingAuthChallenges, [viewHwnd](auto& entry) {
        if (entry.second.viewHwnd != viewHwnd)
            return false;
        auto listener = WKAuthenticationChallengeGetDecisionListener(entry.second.challenge.get());
        WKAuthenticationDecisionListenerUseCredential(listener, nullptr);
        return true;
    });
}

void cancelAllWebKitAuthChallenges()
{
    for (auto& [id, pending] : s_pendingAuthChallenges) {
        auto listener = WKAuthenticationChallengeGetDecisionListener(pending.challenge.get());
        WKAuthenticationDecisionListenerUseCredential(listener, nullptr);
    }
    s_pendingAuthChallenges.clear();
}

void WebKitBrowserWindow::didReceiveAuthenticationChallenge(WKPageRef, WKAuthenticationChallengeRef challenge, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto protectionSpace = WKAuthenticationChallengeGetProtectionSpace(challenge);
    auto decisionListener = WKAuthenticationChallengeGetDecisionListener(challenge);
    auto authenticationScheme = WKProtectionSpaceGetAuthenticationScheme(protectionSpace);

    if (thisWindow.m_isControlledByAutomation) {
        WKPtr<WKStringRef> username = createWKString(L"accept server trust");
        WKPtr<WKStringRef> password = createWKString(L"");
        WKPtr<WKCredentialRef> wkCredential = adoptWK(WKCredentialCreate(username.get(), password.get(), kWKCredentialPersistenceForSession));
        WKAuthenticationDecisionListenerUseCredential(decisionListener, wkCredential.get());
        return;
    }

    if (authenticationScheme == kWKProtectionSpaceAuthenticationSchemeServerTrustEvaluationRequested) {
        if (thisWindow.canTrustServerCertificate(protectionSpace)) {
            WKPtr<WKStringRef> username = createWKString(L"accept server trust");
            WKPtr<WKStringRef> password = createWKString(L"");
            WKPtr<WKCredentialRef> wkCredential = adoptWK(WKCredentialCreate(username.get(), password.get(), kWKCredentialPersistenceForSession));
            WKAuthenticationDecisionListenerUseCredential(decisionListener, wkCredential.get());
            return;
        }
    } else {
        // HTTP auth (a 401, or 407 when the protection space is a proxy):
        // park the challenge and let the app show its credentials dialog —
        // the same one the CEF path uses. Resolved exactly once via
        // respondToWebKitAuthChallenge; unanswered challenges are continued
        // credential-less by the view-close/engine-shutdown sweeps.
        auto host = createString(adoptWK(WKProtectionSpaceCopyHost(protectionSpace)).get());
        auto realm = createString(adoptWK(WKProtectionSpaceCopyRealm(protectionSpace)).get());
        int32_t port = WKProtectionSpaceGetPort(protectionSpace);
        bool isProxy = WKProtectionSpaceGetIsProxy(protectionSpace);

        int64_t requestId = s_nextAuthRequestId++;
        s_pendingAuthChallenges[requestId] =
            { WKPtr<WKAuthenticationChallengeRef>(challenge), thisWindow.hwnd() };
        if (thisWindow.m_client.authCredentialsRequested(host, port, isProxy, realm, requestId))
            return;
        // No app handler wired — continue without credentials immediately.
        respondToWebKitAuthChallenge(requestId, false, nullptr, nullptr);
        return;
    }

    WKAuthenticationDecisionListenerUseCredential(decisionListener, nullptr);
}

bool WebKitBrowserWindow::canTrustServerCertificate(WKProtectionSpaceRef protectionSpace)
{
    auto host = createString(adoptWK(WKProtectionSpaceCopyHost(protectionSpace)).get());
    auto verificationError = WKProtectionSpaceGetCertificateVerificationError(protectionSpace);
    auto description = createString(adoptWK(WKProtectionSpaceCopyCertificateVerificationErrorDescription(protectionSpace)).get());
    auto pem = createPEMString(protectionSpace);

    auto it = m_acceptedServerTrustCerts.find(host);
    if (it != m_acceptedServerTrustCerts.end() && it->second == pem)
        return true;

    std::wstring textString = L"[HOST] " + host + L"\r\n";
    textString.append(L"[ERROR] " + std::to_wstring(verificationError) + L"\r\n");
    textString.append(L"[DESCRIPTION] " + description + L"\r\n");
    textString.append(pem);

    if (askServerTrustEvaluation(hwnd(), textString)) {
        m_acceptedServerTrustCerts.emplace(host, pem);
        return true;
    }

    return false;
}

void WebKitBrowserWindow::navigationActionDidBecomeDownload(WKPageRef, WKNavigationActionRef, WKDownloadRef download, const void* clientInfo)
{
    setDownloadClient(download, clientInfo);
}

void WebKitBrowserWindow::navigationResponseDidBecomeDownload(WKPageRef, WKNavigationResponseRef, WKDownloadRef download, const void* clientInfo)
{
    setDownloadClient(download, clientInfo);
}

void WebKitBrowserWindow::contextMenuDidCreateDownload(WKPageRef, WKDownloadRef download, const void* clientInfo)
{
    setDownloadClient(download, clientInfo);
}

static std::string toUTF8(WKStringRef str)
{
    if (!str)
        return { };
    std::wstring wide = createString(str);
    return createUTF8String(wide.c_str(), wide.length());
}

static std::string toUTF8(WKURLRef url)
{
    if (!url)
        return { };
    std::wstring wide = createString(url);
    return createUTF8String(wide.c_str(), wide.length());
}

void WebKitBrowserWindow::getContextMenuFromProposedMenu(WKPageRef page, WKArrayRef proposedMenu, WKArrayRef* newMenu, WKHitTestResultRef hitTestResult, WKTypeRef /*userData*/, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto& ctx = thisWindow.m_contextMenuContext;
    ctx = {};

    auto linkUrl = adoptWK(WKHitTestResultCopyAbsoluteLinkURL(hitTestResult));
    auto imageUrl = adoptWK(WKHitTestResultCopyAbsoluteImageURL(hitTestResult));
    auto linkLabel = adoptWK(WKHitTestResultCopyLinkLabel(hitTestResult));
    auto lookupText = adoptWK(WKHitTestResultCopyLookupText(hitTestResult));
    auto pageUrl = adoptWK(WKPageCopyActiveURL(page));

    ctx.linkUrl = toUTF8(linkUrl.get());
    ctx.sourceUrl = toUTF8(imageUrl.get());
    ctx.linkText = toUTF8(linkLabel.get());
    ctx.selectionText = toUTF8(lookupText.get());
    ctx.pageUrl = toUTF8(pageUrl.get());
    ctx.mediaType = ctx.sourceUrl.empty() ? WebContextMenuMediaTypeNone : WebContextMenuMediaTypeImage;
    ctx.typeFlags = WebContextMenuTypeFlagsPage;
    if (!ctx.linkUrl.empty())
        ctx.typeFlags |= WebContextMenuTypeFlagsLink;
    if (!ctx.sourceUrl.empty())
        ctx.typeFlags |= WebContextMenuTypeFlagsMedia;
    if (!ctx.selectionText.empty())
        ctx.typeFlags |= WebContextMenuTypeFlagsSelection;
    if (WKHitTestResultIsContentEditable(hitTestResult))
        ctx.typeFlags |= WebContextMenuTypeFlagsEditable;

    // Pass the proposed items through unchanged; WebKit adopts *newMenu.
    WKRetain(proposedMenu);
    *newMenu = proposedMenu;
}

// Enablement of the standard edit actions, read from WebKit's proposed
// items (the C API exposes no editor state query). Absent tags stay 0 —
// WebKit menus carry no Undo/Redo/Delete items.
static uint32_t editFlagsFromMenuItems(WKArrayRef menuItems)
{
    uint32_t flags = 0;
    size_t count = WKArrayGetSize(menuItems);
    for (size_t i = 0; i < count; ++i) {
        auto item = static_cast<WKContextMenuItemRef>(WKArrayGetItemAtIndex(menuItems, i));
        if (!item || !WKContextMenuItemGetEnabled(item))
            continue;
        switch (WKContextMenuItemGetTag(item)) {
        case kWKContextMenuItemTagCut:       flags |= WebContextMenuEditFlagsCanCut; break;
        case kWKContextMenuItemTagCopy:      flags |= WebContextMenuEditFlagsCanCopy; break;
        case kWKContextMenuItemTagPaste:     flags |= WebContextMenuEditFlagsCanPaste; break;
        case kWKContextMenuItemTagSelectAll: flags |= WebContextMenuEditFlagsCanSelectAll; break;
        }
    }
    return flags;
}

void WebKitBrowserWindow::showContextMenu(WKPageRef page, WKPoint menuLocation, WKArrayRef menuItems, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto& ctx = thisWindow.m_contextMenuContext;

    // Strings are borrowed from the stash, which outlives the callback.
    ContextMenuRequest request = {};
    request.x = static_cast<int32_t>(menuLocation.x);
    request.y = static_cast<int32_t>(menuLocation.y);
    request.typeFlags = ctx.typeFlags;
    request.editFlags = editFlagsFromMenuItems(menuItems);
    request.mediaType = ctx.mediaType;
    request.hasImageContents = ctx.mediaType == WebContextMenuMediaTypeImage ? 1 : 0;
    request.canGoBack = WKPageCanGoBack(page) ? 1 : 0;
    request.canGoForward = WKPageCanGoForward(page) ? 1 : 0;
    request.canPrint = 0;  // WebKitBrowserWindow::print() is unimplemented
    request.linkUrl = ctx.linkUrl.c_str();
    request.unfilteredLinkUrl = ctx.linkUrl.c_str();
    request.sourceUrl = ctx.sourceUrl.c_str();
    request.pageUrl = ctx.pageUrl.c_str();
    request.selectionText = ctx.selectionText.c_str();
    request.linkText = ctx.linkText.c_str();

    // Unhandled → no menu: the native path was already skipped for this view.
    thisWindow.m_client.contextMenuRequested(request);
}

void WebKitBrowserWindow::executeContextMenuCommand(WebContextMenuCommand command, const char* arg)
{
    auto page = WKViewGetPage(m_view.get());
    if (!page)
        return;

    auto executeEditCommand = [page](const char* name) {
        auto commandName = adoptWK(WKStringCreateWithUTF8CString(name));
        auto value = adoptWK(WKStringCreateWithUTF8CString(""));
        // Despite the name, not gated on a testing harness — it runs
        // WebPageProxy::executeEditCommand unconditionally.
        WKPageExecuteCommandForTesting(page, commandName.get(), value.get());
    };

    switch (command) {
    case WebContextMenuCommandBack:      WKPageGoBack(page); break;
    case WebContextMenuCommandForward:   WKPageGoForward(page); break;
    case WebContextMenuCommandReload:    WKPageReload(page); break;
    case WebContextMenuCommandUndo:      executeEditCommand("Undo"); break;
    case WebContextMenuCommandRedo:      executeEditCommand("Redo"); break;
    case WebContextMenuCommandCut:       executeEditCommand("Cut"); break;
    case WebContextMenuCommandCopy:      executeEditCommand("Copy"); break;
    case WebContextMenuCommandPaste:     executeEditCommand("Paste"); break;
    case WebContextMenuCommandDelete:    executeEditCommand("Delete"); break;
    case WebContextMenuCommandSelectAll: executeEditCommand("SelectAll"); break;
    case WebContextMenuCommandPrint:     break;  // print() unimplemented; the item is hidden via canPrint=0
    case WebContextMenuCommandSaveUrl:   downloadFromActiveContextMenu(arg); break;
    case WebContextMenuCommandInspectElement: launchInspector(); break;
    }
}

// The C API has no start-download-from-URL. Selecting one of the engine's
// own Download* actions makes WebPageProxy pull the URL from the still-live
// context-menu hit test and start the download through the normal pipeline
// (contextMenuDidCreateDownload). The tag is picked by matching the URL
// against the stashed hit test.
void WebKitBrowserWindow::downloadFromActiveContextMenu(const char* url)
{
    if (!url || !*url)
        return;
    auto page = WKViewGetPage(m_view.get());
    WKContextMenuItemTag tag;
    if (m_contextMenuContext.linkUrl == url)
        tag = kWKContextMenuItemTagDownloadLinkToDisk;
    else if (m_contextMenuContext.sourceUrl == url)
        tag = kWKContextMenuItemTagDownloadImageToDisk;
    else
        return;
    auto title = adoptWK(WKStringCreateWithUTF8CString(""));
    auto item = adoptWK(WKContextMenuItemCreateAsAction(tag, title.get(), true));
#pragma warning(suppress: 4996)  // WKFrameCreateFrameInfo: no non-deprecated way to make a WKFrameInfoRef
    auto frameInfo = adoptWK(WKFrameCreateFrameInfo(WKPageGetMainFrame(page)));
    WKPageSelectContextMenuItem(page, item.get(), frameInfo.get());
}

struct DownloadContext : ActiveDownload {
    int64_t profileId;
    WKPtr<WKDownloadRef> downloadRef;
};

void WebKitBrowserWindow::setDownloadClient(WKDownloadRef download, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    auto* ctx = new DownloadContext();
    ctx->download_id = 0;
    ctx->profileId = thisWindow.client().profileId();
    ctx->downloadRef = WKPtr<WKDownloadRef>(download);

    WKDownloadClientV0 client = { };
    client.base = { 0, ctx };
    client.decideDestinationWithResponse = downloadDecideDestinationWithResponse;
    client.didWriteData = downloadDidWriteData;
    client.didFinish = downloadDidFinish;
    client.didFailWithError = downloadDidFailWithError;

    WKDownloadSetClient(download, &client.base);
}

WKStringRef WebKitBrowserWindow::downloadDecideDestinationWithResponse(WKDownloadRef, WKURLResponseRef response, WKStringRef suggestedFilename, const void* clientInfo)
{
    auto* ctx = const_cast<DownloadContext*>(static_cast<const DownloadContext*>(clientInfo));

    auto filename = createString(suggestedFilename);
    if (filename.empty())
        filename = L"unknown";

    std::wstring folderPath;
    if (!bridgeGetDownloadFolder(ctx->profileId, folderPath)) {
        orion::log_error("Could not get Downloads folder path");
        return nullptr;
    }

    auto urlRef = adoptWK(WKURLResponseCopyURL(response));
    auto originUri = narrow(createString(urlRef.get()));

    auto restart = bridgeConsumePendingRestart(originUri);
    if (restart.downloadId > 0) {
        ctx->download_id = restart.downloadId;
        auto destPath = widen(restart.filePath);

        WKPtr<WKDownloadRef> ref = ctx->downloadRef;
        bridgeRegisterCancelHandler(ctx->download_id, [ref]() {
            WKDownloadCancel(ref.get(), nullptr, nullptr);
        });

        return createWKString(destPath).leakRef();
    }

    bool askUser = bridgeShouldAskDownloadLocation(ctx->profileId);

    int64_t contentLength = static_cast<int64_t>(WKURLResponseGetExpectedContentLength64(response));

    auto destPath = folderPath + L"\\" + filename;

    if (askUser) {
        IFileSaveDialog* pDialog = nullptr;
        if (SUCCEEDED(CoCreateInstance(CLSID_FileSaveDialog, nullptr,
                CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&pDialog)))) {
            pDialog->SetFileName(filename.c_str());

            auto dotPos = filename.rfind(L'.');
            if (dotPos != std::wstring::npos) {
                auto ext = filename.substr(dotPos);
                std::wstring filter = L"*" + ext;
                COMDLG_FILTERSPEC spec = { L"File", filter.c_str() };
                pDialog->SetFileTypes(1, &spec);
            }

            IShellItem* pFolder = nullptr;
            if (SUCCEEDED(SHCreateItemFromParsingName(folderPath.c_str(), nullptr, IID_PPV_ARGS(&pFolder)))) {
                pDialog->SetFolder(pFolder);
                pFolder->Release();
            }

            if (SUCCEEDED(pDialog->Show(nullptr))) {
                IShellItem* pItem = nullptr;
                if (SUCCEEDED(pDialog->GetResult(&pItem))) {
                    PWSTR pszPath = nullptr;
                    if (SUCCEEDED(pItem->GetDisplayName(SIGDN_FILESYSPATH, &pszPath))) {
                        destPath = pszPath;
                        auto sep = destPath.rfind(L'\\');
                        if (sep != std::wstring::npos)
                            filename = destPath.substr(sep + 1);
                        CoTaskMemFree(pszPath);
                    }
                    pItem->Release();
                }
            } else {
                pDialog->Release();
                return nullptr;
            }
            pDialog->Release();
        }
    } else {
        // Resolve filename conflicts by appending (1), (2), etc.
        for (int i = 1; i < 100; i++) {
            DWORD attrs = GetFileAttributesW(destPath.c_str());
            if (attrs == INVALID_FILE_ATTRIBUTES)
                break;
            auto dotPos = filename.rfind(L'.');
            if (dotPos != std::wstring::npos && dotPos > 0)
                destPath = folderPath + L"\\" + filename.substr(0, dotPos)
                    + L"(" + std::to_wstring(i) + L")"
                    + filename.substr(dotPos);
            else
                destPath = folderPath + L"\\" + filename + L"(" + std::to_wstring(i) + L")";
        }
    }

    // WebKit's curl backend opens the file with failIfFileExists=true.
    // Delete the existing file so the download can proceed.
    DeleteFileW(destPath.c_str());

    ctx->download_id = bridgeCreateDownload(
        ctx->profileId, narrow(filename), narrow(destPath), originUri, contentLength);

    if (ctx->download_id > 0) {
        WKPtr<WKDownloadRef> ref = ctx->downloadRef;
        bridgeRegisterCancelHandler(ctx->download_id, [ref]() {
            WKDownloadCancel(ref.get(), nullptr, nullptr);
        });
    }

    return createWKString(destPath).leakRef();
}

void WebKitBrowserWindow::downloadDidWriteData(WKDownloadRef, long long bytesWritten,
    long long totalBytesWritten, long long totalBytesExpectedToWrite, const void* clientInfo)
{
    auto* ctx = const_cast<DownloadContext*>(static_cast<const DownloadContext*>(clientInfo));
    if (!ctx->download_id) return;

    ctx->total_bytes_written += bytesWritten;

    auto now = std::chrono::steady_clock::now();
    if (now - ctx->last_progress_flush < std::chrono::milliseconds(400))
        return;
    ctx->last_progress_flush = now;

    bridgeUpdateDownloadProgress(ctx->download_id,
        totalBytesWritten > 0 ? totalBytesWritten : ctx->total_bytes_written,
        totalBytesExpectedToWrite > 0 ? totalBytesExpectedToWrite : 0);
}

void WebKitBrowserWindow::downloadDidFinish(WKDownloadRef, const void* clientInfo)
{
    auto* ctx = const_cast<DownloadContext*>(static_cast<const DownloadContext*>(clientInfo));
    if (ctx->download_id) {
        bridgeUnregisterCancelHandler(ctx->download_id);
        bridgeFinishDownload(ctx->download_id, ctx->total_bytes_written);
    }
    delete ctx;
}

void WebKitBrowserWindow::downloadDidFailWithError(WKDownloadRef, WKErrorRef error, WKDataRef, const void* clientInfo)
{
    auto* ctx = const_cast<DownloadContext*>(static_cast<const DownloadContext*>(clientInfo));
    if (ctx->download_id) {
        if (bridgeUnregisterCancelHandler(ctx->download_id))
            bridgeFailDownload(ctx->download_id);
    }

    auto desc = createString(adoptWK(WKErrorCopyLocalizedDescription(error)).get());
    orion::log_error("Download failed: %ls (code %d)", desc.c_str(), WKErrorGetErrorCode(error));

    delete ctx;
}

void WebKitBrowserWindow::close(WKPageRef, const void* clientInfo)
{
    // WebKit only fires this for closes the page may make (script-opened
    // window / single-entry tab). The client closes the hosting tab or
    // popup window — posting WM_CLOSE to the main window here would close
    // the whole browser window for any tab calling window.close().
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    thisWindow.m_client.closeRequested();
}

// Forward declarations from webkit.cpp
extern HWND createBrowserViewFromPageConf(HWND parentHwnd, WKPageConfigurationRef pageConf, BrowserCallbacks callbacks);
extern WKPageRef retainPageFromViewHwnd(HWND hwnd);

WKPageRef WebKitBrowserWindow::createNewPage(WKPageRef, WKPageConfigurationRef pageConf,
    WKNavigationActionRef navigationAction, WKWindowFeaturesRef windowFeaturesRef, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);

    // A modifier-clicked target=_blank link opens a standalone tab or window
    // (no opener), same as a modifier-click on a plain link. Returning
    // nullptr cancels WebKit's linked-page creation.
    if (WKNavigationActionGetNavigationType(navigationAction) == kWKFrameNavigationTypeLinkClicked) {
        int32_t disposition;
        if (linkClickOpenDisposition(navigationAction, disposition)) {
            if (OpenURLCallback cb = getOpenURLCallback()) {
                openNavigationActionURL(navigationAction, thisWindow.m_hMainWnd, disposition, cb);
                return nullptr;
            }
        }
    }

    CreatePopupCallback createCb = getCreatePopupCallback();
    if (!createCb)
        return nullptr;

    // Extract window features via C API to decide popup vs new tab.
    int8_t wantsPopup = 1;
    int32_t width = 800, height = 600;
    int32_t x = -1, y = -1;
    int8_t wantLocationBar = 1, wantResizable = 1;

    if (windowFeaturesRef) {
        wantsPopup = WKWindowFeaturesGetWantsPopup(windowFeaturesRef) ? 1 : 0;
        double val;
        if (WKWindowFeaturesGetWidth(windowFeaturesRef, &val))  width  = static_cast<int32_t>(val);
        if (WKWindowFeaturesGetHeight(windowFeaturesRef, &val)) height = static_cast<int32_t>(val);
        if (WKWindowFeaturesGetX(windowFeaturesRef, &val)) x = static_cast<int32_t>(val);
        if (WKWindowFeaturesGetY(windowFeaturesRef, &val)) y = static_cast<int32_t>(val);
        bool bval;
        if (WKWindowFeaturesGetLocationBarVisible(windowFeaturesRef, &bval)) wantLocationBar = bval ? 1 : 0;
        if (WKWindowFeaturesGetResizable(windowFeaturesRef, &bval)) wantResizable = bval ? 1 : 0;
    }

    HWND originatingHwnd = thisWindow.m_hMainWnd;
    int8_t userGesture =
        WKNavigationActionHasUnconsumedUserGesture(navigationAction) ? 1 : 0;

    auto request = adoptWK(WKNavigationActionCopyRequest(navigationAction));
    auto targetUrl = adoptWK(WKURLRequestCopyURL(request.get()));

    PopupCreateResult result = createCb(
        width, height, x, y,
        wantLocationBar, wantResizable,
        wantsPopup, userGesture, originatingHwnd,
        createString(targetUrl.get()).c_str());

    if (!result.parentHwnd)
        return nullptr;

    HWND webViewHwnd = createBrowserViewFromPageConf(result.parentHwnd, pageConf, result.callbacks);
    if (!webViewHwnd)
        return nullptr;

    if (auto readyCb = getPopupReadyCallback())
        readyCb(result.parentHwnd, webViewHwnd);

    // WebKit's createNewPage contract: return a +1-retained WKPageRef.
    return retainPageFromViewHwnd(webViewHwnd);
}

void WebKitBrowserWindow::didNotHandleKeyEvent(WKPageRef, WKNativeEventPtr event, const void* clientInfo)
{
    if (event->message != WM_KEYDOWN && event->message != WM_SYSKEYDOWN)
        return;

    // WKNativeEventPtr is a raw Win32 MSG — it only carries the virtual key code
    // in wParam, not modifier state. Query modifiers via GetKeyState().
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    uint32_t vkCode = static_cast<uint32_t>(event->wParam);
    uint32_t modifiers = toVirtualKeyModifiers(
        GetKeyState(VK_CONTROL) & 0x8000,
        GetKeyState(VK_MENU)    & 0x8000,
        GetKeyState(VK_SHIFT)   & 0x8000);
    thisWindow.m_client.unhandledKeyEvent(vkCode, modifiers);
}

WKRect WebKitBrowserWindow::getWindowFrame(WKPageRef, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    RECT rect;
    GetWindowRect(thisWindow.hwnd(), &rect);
    return WKRectMake(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
}

void WebKitBrowserWindow::runJavaScriptAlert(WKPageRef, WKStringRef alertText, WKFrameRef, WKSecurityOriginRef securityOrigin, WKPageRunJavaScriptAlertResultListenerRef listener, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    std::wstring title = L"Alert: ";
    title += createString(adoptWK(WKSecurityOriginCopyToString(securityOrigin)).get());
    auto text = createString(alertText);
    MessageBox(thisWindow.m_hMainWnd, text.c_str(), title.c_str(), MB_OK);
    WKPageRunJavaScriptAlertResultListenerCall(listener);
}

void WebKitBrowserWindow::runJavaScriptConfirm(WKPageRef, WKStringRef message, WKFrameRef, WKSecurityOriginRef securityOrigin, WKPageRunJavaScriptConfirmResultListenerRef listener, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    std::wstring title = L"Confirm: ";
    title += createString(adoptWK(WKSecurityOriginCopyToString(securityOrigin)).get());
    auto text = createString(message);
    bool result = MessageBox(thisWindow.m_hMainWnd, text.c_str(), title.c_str(), MB_OKCANCEL) == IDOK;
    WKPageRunJavaScriptConfirmResultListenerCall(listener, result);
}

void WebKitBrowserWindow::runJavaScriptPrompt(WKPageRef, WKStringRef message, WKStringRef defaultValue, WKFrameRef, WKSecurityOriginRef securityOrigin, WKPageRunJavaScriptPromptResultListenerRef listener, const void* clientInfo)
{
    auto& thisWindow = toWebKitBrowserWindow(clientInfo);
    std::wstring title = L"Prompt: ";
    title += createString(adoptWK(WKSecurityOriginCopyToString(securityOrigin)).get());
    auto text = createString(message);
    text += L"\nDefault Value: " + createString(defaultValue);
    MessageBox(thisWindow.m_hMainWnd, text.c_str(), title.c_str(), MB_OK);
    WKPageRunJavaScriptPromptResultListenerCall(listener, defaultValue);
}

