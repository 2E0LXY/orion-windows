#pragma once

#include "browser_engine_types.h"

#include <string>

namespace orion { class ScriptBridge; }
struct HostServices;

// Runtime interface to a browser engine. Implemented by the engine plugin
// DLLs; the facade obtains an instance through the plugin's OrionCreateEngine
// export. At most one engine is ever loaded into a process.
class BrowserEngine {
public:
    virtual ~BrowserEngine() = default;

    virtual EngineId engineId() const = 0;

    // Returns -1 if this is the main browser process (continue normal
    // startup), >= 0 if this is an engine subprocess (exit with this code).
    virtual int checkBrowserSubprocess() = 0;

    // External message pump support (CEF only; nullptr/no-op for WebKit).
    // The event is a Win32 auto-reset event signalled when CEF has work;
    // pumpCefMessageLoop() must be called on the UI thread.
    virtual HANDLE getCefWakeupEventHandle() = 0;
    virtual void pumpCefMessageLoop() = 0;

    virtual HWND createBrowserView(HWND parentHwnd, BrowserCallbacks callbacks, int64_t profileId, const char* initialUrl) = 0;
    virtual void destroyBrowserView(HWND hwnd) = 0;
    virtual void cleanBrowserEngine() = 0;

    virtual HRESULT loadURL(HWND hwnd, const std::string& url) = 0;
    virtual void loadHTMLString(HWND hwnd, const std::string& html, const std::string& unreachableUrl) = 0;
    virtual void executeJavaScript(HWND hwnd, const char* script) = 0;
    virtual void reload(HWND hwnd) = 0;
    virtual void reloadBypassingCache(HWND hwnd) = 0;
    virtual void forward(HWND hwnd) = 0;
    virtual void backward(HWND hwnd) = 0;
    virtual void setCustomUserAgent(HWND hwnd, const wchar_t* userAgent) = 0;
    // Process-default user agent from the profile's setting; call before the
    // first view (CEF latches it at engine initialization; WebKit applies it
    // per page at creation). Per-view setCustomUserAgent overrides it.
    virtual void setDefaultUserAgent(const wchar_t* userAgent) = 0;
    // Minimum page font size in px; 0 = no minimum. Applies to all profiles.
    // WebKit applies to open pages immediately; CEF only to views created after.
    virtual void setMinimumFontSize(int32_t size) = 0;
    virtual void setJavaScriptEnabled(bool enabled) = 0;
    // Engine-native popup blocking (Chromium's built-in blocker). Disabled
    // when the app's popup setting owns the policy, so every window.open
    // reaches the CreatePopupCallback gate. No-op on WebKit (no native
    // blocker).
    virtual void setNativePopupBlocking(bool enabled) = 0;
    virtual void startDownload(const char* url) = 0;
    // Asks the page to leave HTML5 element fullscreen (UI-initiated exit,
    // e.g. F11 or tab switch). The engine confirms via
    // elementFullscreenChanged(false).
    virtual void requestExitElementFullscreen(HWND hwnd) = 0;
    // Executes a context-menu command chosen from the app-drawn menu.
    // arg: URL for SaveUrl; x/y: the request's coordinates for InspectElement.
    virtual void executeContextMenuCommand(HWND hwnd, WebContextMenuCommand command, const char* arg, int32_t x, int32_t y) = 0;
    // Opens the view's DevTools frontend, or focuses it when already open.
    // The frontend view arrives via the view's devToolsViewChanged callback.
    // x/y >= 0 inspect the element at those view coordinates (CEF only).
    virtual void showDevTools(HWND hwnd, int32_t x, int32_t y) = 0;
    // Closes the view's DevTools frontend; devToolsViewChanged(nullptr) follows.
    virtual void closeDevTools(HWND hwnd) = 0;
    // Find in page. findNext=false starts/restarts a search for text;
    // true advances from the current match. Results arrive via the view's
    // findResult callback. text must be non-empty (stopFindInPage otherwise).
    virtual void findInPage(HWND hwnd, const char* text, bool forward, bool findNext) = 0;
    // Ends the find session and hides match highlights.
    virtual void stopFindInPage(HWND hwnd, bool clearSelection) = 0;
    // Requests an async snapshot of the view's visible viewport, downscaled
    // to at most maxPixelWidth (0 = native size) and encoded as JPEG. The
    // result arrives via the view's snapshotTaken callback (nullptr/0 on
    // failure). Only reliable while the view is (or was just) visible.
    virtual void requestTabSnapshot(HWND hwnd, int32_t maxPixelWidth) = 0;
    // Pushes the embedder's effective UI appearance (drives CSS
    // prefers-color-scheme). Applied to every live and future view; web
    // content follows this value, not the OS.
    virtual void setUseDarkAppearance(bool useDarkAppearance) = 0;
    // Resolves a pending media-access request raised via the view's
    // mediaAccessPermissionRequested callback. allow grants exactly what
    // was requested; unknown ids (view already gone) are ignored.
    virtual void respondToMediaAccessPermission(int64_t requestId, bool allow) = 0;
    // Resolves a pending HTTP-auth challenge raised via the view's
    // authCredentialsRequested callback. allow=false cancels (the site's 401
    // body renders); unknown ids (view already gone) are ignored.
    virtual void respondToAuthCredentials(int64_t requestId, bool allow,
        const wchar_t* username, const wchar_t* password) = 0;

    // The engine's half of the core module's WebExtensions host contract
    // (script_bridge.h; the facade's bridge hosts forward to it). WebKit
    // returns its bridge; CEF returns nullptr (extensions disabled).
    virtual orion::ScriptBridge* createScriptBridge() = 0;
};

// The plugin's single export:
//   extern "C" __declspec(dllexport) BrowserEngine* __cdecl
//   OrionCreateEngine(const HostServices* host);
// Returns nullptr if the host's HostServices ABI does not match what the
// plugin was built against (see host_services.h).
using OrionCreateEngineFn = BrowserEngine* (__cdecl*)(const HostServices* host);
