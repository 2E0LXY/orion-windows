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
#pragma once

#include "WebkitCommon.h"
#include "BrowserWindow.h"
#include "Common.h"
#include "WKPtr.h"
#include <WebKit/WebKit2_C.h>
#include <filesystem>
#include <unordered_map>

void clearWebKitProfileCache();

// Minimum page font size in px; 0 = no minimum. Stored for profiles created
// later and applied immediately to already-cached profiles' preferences.
void setWebKitMinimumFontSize(uint32_t size);

// JavaScript on/off for all websites; same storage/application pattern.
void setWebKitJavaScriptEnabled(bool enabled);

// HTTP-auth challenges parked for the app's credentials dialog (raised via
// BrowserWindowClient::authCredentialsRequested). allow=false continues
// credential-less (the 401 body renders). The sweeps continue any challenge
// still pending when its view closes or the engine shuts down.
void respondToWebKitAuthChallenge(int64_t requestId, bool allow,
    const wchar_t* username, const wchar_t* password);
void cancelWebKitAuthChallengesForView(HWND viewHwnd);
void cancelAllWebKitAuthChallenges();
// Profile-default UA applied to every page at creation (window.open popups
// included); per-view setUserAgent overwrites it. Empty = WebKit's own UA.
void setWebKitDefaultUserAgent(const std::wstring& userAgent);

class WebKitBrowserWindow : public BrowserWindow {
public:
    static std::shared_ptr<BrowserWindow> create(BrowserWindowClient&, HWND mainWnd, bool useLayeredWebView = false,
        const std::filesystem::path& profileDataPath = {});

    // Public so webkit.cpp's popup plumbing can construct a view bound to
    // an externally-supplied WKPageConfigurationRef (window.open case).
    WebKitBrowserWindow(BrowserWindowClient&, WKPageConfigurationRef, HWND mainWnd);

    // Borrowed WKPageRef for the view; used to hand the page back to WebKit
    // from the createNewPage UI client callback.
    WKPageRef pageRef() const { return m_view.get() ? WKViewGetPage(m_view.get()) : nullptr; }

    BrowserWindowClient& client() { return m_client; }
    void scrapeCredentials();
    void scrapeCredentialsAndAllow(WKFramePolicyListenerRef listener);

    void loadHTMLString(const std::wstring& html, const std::wstring& unreachableUrl);

    // Executes a command chosen from the app-drawn context menu.
    // arg: URL for SaveUrl (matched against the active menu's hit test).
    void executeContextMenuCommand(WebContextMenuCommand, const char* arg);

    // Find in page. findNext=false starts/restarts a search for text; true
    // advances from the current match. Results arrive via the client's
    // findResult callback.
    void findInPage(const char* text, bool forward, bool findNext);
    // Ends the find session and hides match highlights.
    void stopFindInPage();
    // Async snapshot of the visible viewport (JPEG, at most maxPixelWidth
    // wide; 0 = native size); result arrives via the client's snapshotTaken.
    void takeSnapshot(int32_t maxPixelWidth);

    // Applies the app-wide effective appearance to this view and its Web
    // Inspector (drives prefers-color-scheme; sticky across web-process
    // relaunch).
    void setUseDarkAppearance(bool useDarkAppearance);

    // Closes the Web Inspector; the client's devToolsViewChanged(nullptr)
    // follows.
    void closeInspector();

private:

    HRESULT init() override;
    HWND hwnd() override;

    HRESULT loadURL(const std::wstring& url) override;
    void reload() override;
    void reloadBypassingCache() override;
    void navigateForwardOrBackward(bool forward) override;
    void navigateToHistory(UINT menuID) override;
    void setPreference(UINT menuID, bool enable) override;

    void resetFeatureMenu(FeatureType, HMENU, bool resetsSettingsToDefaults) override;

    void print() override;
    void launchInspector() override;
    void openProxySettings() override;

    std::wstring userAgent() override;
    void setUserAgent(const std::wstring&) override;

    void showLayerTree() override;
    void updateStatistics(HWND dialog) override;

    void resetZoom() override;
    void zoomIn() override;
    void zoomOut() override;

    void clearCookies() override;
    void clearWebsiteData() override;

    void updateProxySettings();
    void adjustScaleFactors() override;

    bool canTrustServerCertificate(WKProtectionSpaceRef);

    static void didChangeTitle(const void*);
    static void didChangeIsLoading(const void*);
    static void didChangeCanGoBackOrForward(const void*);
    static void didChangeEstimatedProgress(const void*);
    static void didChangeActiveURL(const void*);
    static void willEnterFullScreen(WKPageRef, WKCompletionListenerRef, const void*);
    static void beganEnterFullScreen(WKPageRef, WKRect initialFrame, WKRect finalFrame, const void*);
    static void exitFullScreen(WKPageRef, const void*);
    static void beganExitFullScreen(WKPageRef, WKRect initialFrame, WKRect finalFrame, WKCompletionListenerRef, const void*);
    static void extractFavicon(WKPageRef, const void* clientInfo);
    static void didFinishNavigation(WKPageRef, WKNavigationRef, WKTypeRef, const void*);
    static void faviconScriptCompleted(WKTypeRef result, WKErrorRef error, void* context);
    static void decidePolicyForNavigationAction(WKPageRef, WKNavigationActionRef, WKFramePolicyListenerRef, WKTypeRef, const void*);
    static void decidePolicyForNavigationResponse(WKPageRef, WKNavigationResponseRef, WKFramePolicyListenerRef, WKTypeRef, const void*);
    static void didFailProvisionalNavigation(WKPageRef, WKNavigationRef, WKErrorRef, WKTypeRef, const void*);
    static void webProcessDidTerminate(WKPageRef, WKProcessTerminationReason, const void*);
    static void didReceiveAuthenticationChallenge(WKPageRef, WKAuthenticationChallengeRef, const void*);
    static void navigationActionDidBecomeDownload(WKPageRef, WKNavigationActionRef, WKDownloadRef, const void*);
    static void navigationResponseDidBecomeDownload(WKPageRef, WKNavigationResponseRef, WKDownloadRef, const void*);
    static void contextMenuDidCreateDownload(WKPageRef, WKDownloadRef, const void*);
    static void setDownloadClient(WKDownloadRef, const void*);
    static WKStringRef downloadDecideDestinationWithResponse(WKDownloadRef, WKURLResponseRef, WKStringRef, const void*);
    static void downloadDidWriteData(WKDownloadRef, long long, long long, long long, const void*);
    static void downloadDidFinish(WKDownloadRef, const void*);
    static void downloadDidFailWithError(WKDownloadRef, WKErrorRef, WKDataRef, const void*);
    static void close(WKPageRef, const void*);
    static WKPageRef createNewPage(WKPageRef, WKPageConfigurationRef, WKNavigationActionRef, WKWindowFeaturesRef, const void *);
    static void didNotHandleKeyEvent(WKPageRef, WKNativeEventPtr, const void*);
    static void getContextMenuFromProposedMenu(WKPageRef, WKArrayRef proposedMenu, WKArrayRef* newMenu, WKHitTestResultRef, WKTypeRef userData, const void*);
    static void showContextMenu(WKPageRef, WKPoint menuLocation, WKArrayRef menuItems, const void*);
    static void didFindString(WKPageRef, WKStringRef, unsigned matchCount, const void*);
    static void didFailToFindString(WKPageRef, WKStringRef, const void*);
    static void inspectorDidOpenFrontend(WKInspectorRef, HWND frontendWindow, const void*);
    static void inspectorDidClose(WKInspectorRef, const void*);
    static void inspectorBringToFront(WKInspectorRef, const void*);

    void downloadFromActiveContextMenu(const char* url);
    static WKRect getWindowFrame(WKPageRef, const void*);
    static void runJavaScriptAlert(WKPageRef, WKStringRef, WKFrameRef, WKSecurityOriginRef, WKPageRunJavaScriptAlertResultListenerRef, const void *);
    static void runJavaScriptConfirm(WKPageRef, WKStringRef, WKFrameRef, WKSecurityOriginRef, WKPageRunJavaScriptConfirmResultListenerRef, const void *);
    static void runJavaScriptPrompt(WKPageRef, WKStringRef, WKStringRef, WKFrameRef, WKSecurityOriginRef, WKPageRunJavaScriptPromptResultListenerRef, const void *);

    BrowserWindowClient& m_client;
    WKPtr<WKViewRef> m_view;

    // Mirror of PageLoadState::isLoading: the C API has no getter, and the
    // state client's didChangeIsLoading fires only on actual flips.
    bool m_isLoading { false };

    // Hit-test context stashed by getContextMenuFromProposedMenu for the
    // showContextMenu call that follows synchronously on the same stack;
    // SaveUrl later matches its URL against this to pick the download tag.
    struct {
        uint32_t typeFlags = 0;
        int32_t mediaType = 0;
        std::string linkUrl;
        std::string sourceUrl;
        std::string pageUrl;
        std::string selectionText;
        std::string linkText;
    } m_contextMenuContext;

    // Find-in-page session. The C find client reports only a match count, so
    // the active-match ordinal is maintained here with the same ±1 modulo rule
    // WebKit applies internally (FindController::updateMatchIndex).
    std::string m_findText;
    int32_t m_findOrdinal { 0 };        // 1-based; 0 = unknown
    bool m_findPendingNewSearch { false };
    bool m_findPendingBackwards { false };

    // The Web Inspector frontend view while open (embedder-hosted).
    HWND m_inspectorViewHwnd { nullptr };

    HWND m_hMainWnd { nullptr };
    ProxySettings m_proxy { };
    std::unordered_map<std::wstring, std::wstring> m_acceptedServerTrustCerts;
    std::vector<WKPtr<WKStringRef>> m_experimentalFeatureKeys;
    std::vector<WKPtr<WKStringRef>> m_internalDebugFeatureKeys;
    bool m_isControlledByAutomation { false };
};
