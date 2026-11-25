#pragma once

#include "browser_engine_types.h"

#include <string>

// Free functions implemented by the engine translation units (webkit.cpp /
// cef.cpp). The plugin entry point adapts them to the BrowserEngine
// interface (browser_engine.h).

// Returns -1 if this is the main browser process (continue normal startup).
// Returns >= 0 if this is an engine subprocess (caller should exit with this code).
int checkBrowserSubprocess();

// External message pump support (CEF only; no-ops for WebKit).
// Returns a Win32 auto-reset event HANDLE signalled when CEF has work.
HANDLE getCefWakeupEventHandle();
// Calls CefDoMessageLoopWork(). Must be called on the UI thread.
void pumpCefMessageLoop();

HWND createBrowserView(HWND parentHwnd, BrowserCallbacks callbacks, int64_t profileId, const char* initialUrl = nullptr);
void destroyBrowserView(HWND hwnd);
void cleanBrowserEngine();

HRESULT loadURL(HWND hwnd, const std::string& url);
void startDownload(const char* url);
// Asks the page to leave HTML5 element fullscreen (UI-initiated exit, e.g.
// F11 or tab switch). The engine confirms via elementFullscreenChanged(false).
void requestExitElementFullscreen(HWND hwnd);
void executeJavaScript(HWND hwnd, const char* script);
void loadHTMLString(HWND hwnd, const std::string& html, const std::string& unreachableUrl);
void reload(HWND hwnd);
void reloadBypassingCache(HWND hwnd);
void forward(HWND hwnd);
void backward(HWND hwnd);
void setMinimumFontSize(int32_t size);
void setJavaScriptEnabled(bool enabled);
void setNativePopupBlocking(bool enabled);
void setCustomUserAgent(HWND hwnd, const wchar_t* userAgent);
// Process-default user agent, resolved from the profile's setting; covers
// pages the app never reaches per-view (window.open popups, loads racing
// setCustomUserAgent). CEF latches it at engine initialization; WebKit
// applies it to every page at creation. Call before the first view.
void setDefaultUserAgent(const wchar_t* userAgent);
// Executes a context-menu command chosen from the app-drawn menu.
// arg: URL for SaveUrl; x/y: the request's coordinates for InspectElement.
void executeContextMenuCommand(HWND hwnd, WebContextMenuCommand command, const char* arg, int32_t x, int32_t y);
// Opens the view's DevTools frontend, or focuses it when already open. The
// frontend view arrives via the view's devToolsViewChanged callback. x/y >= 0
// inspect the element at those view coordinates (CEF only; WebKit's C API has
// no inspect-at-point).
void showDevTools(HWND hwnd, int32_t x, int32_t y);
// Closes the view's DevTools frontend; devToolsViewChanged(nullptr) follows.
void closeDevTools(HWND hwnd);
// Find in page. findNext=false starts/restarts a search for text; true
// advances from the current match. Results arrive via the view's findResult
// callback. text must be non-empty (stopFindInPage otherwise).
void findInPage(HWND hwnd, const char* text, bool forward, bool findNext);
// Ends the find session and hides match highlights.
void stopFindInPage(HWND hwnd, bool clearSelection);
// Requests an async snapshot of the view's visible viewport (JPEG, at most
// maxPixelWidth wide; 0 = native size). Result arrives via the view's
// snapshotTaken callback; nullptr/0 on failure. Only reliable while the
// view is (or was just) visible.
void requestTabSnapshot(HWND hwnd, int32_t maxPixelWidth);
// Pushes the embedder's effective UI appearance (drives prefers-color-scheme).
// Applied to every live and future view; web content follows this value, not
// the OS.
void setUseDarkAppearance(bool useDarkAppearance);
// Resolves a pending media-access request raised via the view's
// mediaAccessPermissionRequested callback. allow grants exactly what was
// requested; unknown ids (view already gone) are ignored.
void respondToMediaAccessPermission(int64_t requestId, bool allow);
void respondToAuthCredentials(int64_t requestId, bool allow,
    const wchar_t* username, const wchar_t* password);
