#pragma once

#include "browser_engine.h"
#include "engine_impl.h"

// Shared adapter between the BrowserEngine plugin interface and the engine
// implementation's free functions (engine_impl.h). Engine plugins derive from
// this and override only engineId() and createScriptBridge(); keeping the
// mechanical forwarders here makes a BrowserEngine interface change a single
// edit instead of a per-plugin synchronized one (where a missed copy compiles
// cleanly and silently diverges engine behavior).
class ForwardingEngine : public BrowserEngine {
public:
    int checkBrowserSubprocess() override { return ::checkBrowserSubprocess(); }
    HANDLE getCefWakeupEventHandle() override { return ::getCefWakeupEventHandle(); }
    void pumpCefMessageLoop() override { ::pumpCefMessageLoop(); }
    HWND createBrowserView(HWND parentHwnd, BrowserCallbacks callbacks, int64_t profileId, const char* initialUrl) override
    {
        return ::createBrowserView(parentHwnd, callbacks, profileId, initialUrl);
    }
    void destroyBrowserView(HWND hwnd) override { ::destroyBrowserView(hwnd); }
    void cleanBrowserEngine() override { ::cleanBrowserEngine(); }
    HRESULT loadURL(HWND hwnd, const std::string& url) override { return ::loadURL(hwnd, url); }
    void loadHTMLString(HWND hwnd, const std::string& html, const std::string& unreachableUrl) override
    {
        ::loadHTMLString(hwnd, html, unreachableUrl);
    }
    void executeJavaScript(HWND hwnd, const char* script) override { ::executeJavaScript(hwnd, script); }
    void reload(HWND hwnd) override { ::reload(hwnd); }
    void reloadBypassingCache(HWND hwnd) override { ::reloadBypassingCache(hwnd); }
    void forward(HWND hwnd) override { ::forward(hwnd); }
    void backward(HWND hwnd) override { ::backward(hwnd); }
    void setCustomUserAgent(HWND hwnd, const wchar_t* userAgent) override { ::setCustomUserAgent(hwnd, userAgent); }
    void setDefaultUserAgent(const wchar_t* userAgent) override { ::setDefaultUserAgent(userAgent); }
    void setMinimumFontSize(int32_t size) override { ::setMinimumFontSize(size); }
    void setJavaScriptEnabled(bool enabled) override { ::setJavaScriptEnabled(enabled); }
    void setNativePopupBlocking(bool enabled) override { ::setNativePopupBlocking(enabled); }
    void startDownload(const char* url) override { ::startDownload(url); }
    void requestExitElementFullscreen(HWND hwnd) override { ::requestExitElementFullscreen(hwnd); }
    void executeContextMenuCommand(HWND hwnd, WebContextMenuCommand command, const char* arg, int32_t x, int32_t y) override
    {
        ::executeContextMenuCommand(hwnd, command, arg, x, y);
    }
    void showDevTools(HWND hwnd, int32_t x, int32_t y) override { ::showDevTools(hwnd, x, y); }
    void closeDevTools(HWND hwnd) override { ::closeDevTools(hwnd); }
    void findInPage(HWND hwnd, const char* text, bool forward, bool findNext) override
    {
        ::findInPage(hwnd, text, forward, findNext);
    }
    void stopFindInPage(HWND hwnd, bool clearSelection) override { ::stopFindInPage(hwnd, clearSelection); }
    void requestTabSnapshot(HWND hwnd, int32_t maxPixelWidth) override { ::requestTabSnapshot(hwnd, maxPixelWidth); }
    void setUseDarkAppearance(bool useDarkAppearance) override { ::setUseDarkAppearance(useDarkAppearance); }
    void respondToMediaAccessPermission(int64_t requestId, bool allow) override
    {
        ::respondToMediaAccessPermission(requestId, allow);
    }
    void respondToAuthCredentials(int64_t requestId, bool allow,
        const wchar_t* username, const wchar_t* password) override
    {
        ::respondToAuthCredentials(requestId, allow, username, password);
    }
};
