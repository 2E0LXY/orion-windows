#pragma once

#include <stdint.h>

#include "context_menu_types.h"
#include "media_permission_types.h"

// Browser engine identity. Values cross the P/Invoke boundary
// (GetBrowserEngine) and select per-engine profile data directories.
enum class EngineId : int32_t {
    WebKit = 0,
    Blink = 1,
};

// Function pointer types for browser engine callbacks.
typedef void(__cdecl* ProgressChangedCallback)(double progress);
// Called when the view's main-frame load starts (1) or ends (0) — finished,
// failed, or reset by a web-process crash. Drives the tab loading indicator.
typedef void(__cdecl* LoadingStateChangedCallback)(int32_t isLoading);
typedef void(__cdecl* ActiveURLChangedCallback)(const wchar_t* url);
typedef void(__cdecl* TitleChangedCallback)(const wchar_t* title);

// Mirrors Windows.System.VirtualKeyModifiers (C# WinUI enum).
// Values must stay in sync with the C# definition.
namespace VirtualKeyModifiers {
    constexpr uint32_t None    = 0;
    constexpr uint32_t Control = 1;
    constexpr uint32_t Menu    = 2; // Alt
    constexpr uint32_t Shift   = 4;
    constexpr uint32_t Windows = 8;
}

// Converts per-modifier bools to a VirtualKeyModifiers bitmask.
inline uint32_t toVirtualKeyModifiers(bool ctrl, bool alt, bool shift)
{
    return (ctrl  ? VirtualKeyModifiers::Control : 0)
         | (alt   ? VirtualKeyModifiers::Menu    : 0)
         | (shift ? VirtualKeyModifiers::Shift   : 0);
}

// Called when the web page did not handle a key event.
// vkCode: Win32 virtual key code (matches Windows.System.VirtualKey).
// modifiers: bitmask matching Windows.System.VirtualKeyModifiers.
// Returns 1 if handled as a browser shortcut, 0 otherwise.
typedef int(__cdecl* UnhandledKeyEventCallback)(uint32_t vkCode, uint32_t modifiers);

// Called before a key event reaches the engine.
// Returns 1 if the key is a reserved browser shortcut (handled), 0 otherwise.
// Reserved shortcuts are never forwarded to the web page.
typedef int(__cdecl* ReservedKeyEventCallback)(uint32_t vkCode, uint32_t modifiers);

// Called when a page's favicon candidate URLs are discovered after navigation.
// iconUrls contains one or more URLs separated by newlines.
typedef void(__cdecl* FaviconUrlChangedCallback)(const wchar_t* pageUrl, const wchar_t* iconUrls);

// Unified error categories for error page selection (must match C# ErrorPageType enum).
namespace ErrorCategory {
    constexpr int32_t Unknown           = 0;
    constexpr int32_t NetworkError      = 1;
    constexpr int32_t DnsError          = 2;
    constexpr int32_t Timeout           = 3;
    constexpr int32_t SslError          = 4;
    constexpr int32_t ConnectionRefused = 5;
}

// Called when a main-frame navigation fails.
// Returns error page HTML (allocated with CoTaskMemAlloc) or nullptr to
// use the engine's default error page.  Caller frees with CoTaskMemFree.
typedef wchar_t*(__cdecl* NavigationErrorCallback)(
    int32_t errorCategory, int32_t rawErrorCode,
    const wchar_t* failingUrl, const wchar_t* description);

// Called when the web content process terminates unexpectedly.
// processName: executable that terminated (e.g. "WebKitWebProcess.exe").
// reason: engine-specific termination reason code.
// reasonString: human-readable reason (e.g. "Crash", "OutOfMemory").
typedef void(__cdecl* WebProcessTerminatedCallback)(const char* processName, uint32_t reason, const char* reasonString);

// Called when a username or password field receives focus.
// jsonPayload: JSON string with type, position, dimensions (from autofill script).
typedef void(__cdecl* InputFocusCallback)(const char* jsonPayload);

// Called when a form with credentials is submitted.
// jsonPayload: JSON string with username and password fields.
typedef void(__cdecl* FormSubmitCallback)(const char* jsonPayload);

// Called when the page enters or leaves HTML5 element fullscreen
// (e.g. a fullscreen video). The embedder makes the window fullscreen
// and hides its chrome while active; the engine renders the fullscreen
// element to fill the view.
typedef void(__cdecl* ElementFullscreenChangedCallback)(int32_t isFullscreen);

// Called when the engine wants to show a context menu. Returns 1 if the
// app will show its own menu (the engine suppresses its native one),
// 0 to let the engine show its default menu.
typedef int32_t(__cdecl* ContextMenuRequestedCallback)(const ContextMenuRequest* request);

// Called with the result of a find-in-page operation.
// matchCount: 0 = no matches; negative = more than |matchCount| matches
// (the engine's counting cap; the exact count is unknown).
// activeMatchOrdinal: 1-based index of the current match, 0 = unknown.
typedef void(__cdecl* FindResultCallback)(int32_t matchCount, int32_t activeMatchOrdinal);

// Called with the result of a RequestTabSnapshot call: the view's visible
// viewport as encoded JPEG bytes, or nullptr/0 when the capture failed
// (e.g. the view is hidden). data is only valid for the duration of the
// call — copy to keep.
typedef void(__cdecl* SnapshotTakenCallback)(const uint8_t* data, int32_t size);

// Called when the page requests media capture access (getUserMedia /
// getDisplayMedia). pageUrl: the view's main-frame URL (permission decisions
// are keyed by its authority, matching orion-linux); mediaPermissions:
// WebMediaPermission bitmask; requestId: token to resolve exactly once via
// RespondToMediaAccessPermissionRequest. The engine holds the request
// pending until the response arrives and denies it when the view goes away.
typedef void(__cdecl* MediaAccessPermissionRequestedCallback)(
    const wchar_t* pageUrl, int32_t mediaPermissions, int64_t requestId);

// Called whenever the view's back/forward availability changes (navigation
// commits, back-forward list mutations). Drives the toolbar button state.
typedef void(__cdecl* NavigationStateChangedCallback)(int32_t canGoBack, int32_t canGoForward);

// Called when the view's DevTools frontend view appears or goes away.
// devToolsHwnd: a hidden child of the view's top-level window that the
// embedder hosts and positions from then on (repeated when the frontend asks
// to be brought to the front); nullptr once the frontend has closed.
typedef void(__cdecl* DevToolsViewChangedCallback)(HWND devToolsHwnd);

// Called when the page requests to close itself (window.close()). Engines
// only fire this for closes the page is allowed to make (a script-opened
// window, or a tab with a single-entry session history); the embedder
// closes the hosting tab or popup window — never the whole browser window.
typedef void(__cdecl* CloseRequestedCallback)();

// Called when a request needs HTTP authentication (a 401 challenge from the
// server, or a 407 from a proxy when isProxy is 1). realm may be empty. The
// embedder shows a credentials dialog and resolves exactly once via
// RespondToAuthCredentials; the engine cancels the request when the view
// goes away unanswered.
typedef void(__cdecl* AuthCredentialsRequestedCallback)(
    const wchar_t* host, int32_t port, int32_t isProxy,
    const wchar_t* realm, int64_t requestId);

// Callbacks supplied by the caller when creating a browser view.
// Layout is mirrored by hand in C# (OrionBackend.BrowserCallbacks);
// only append new fields at the end, and keep both sides in sync.
struct BrowserCallbacks
{
    ProgressChangedCallback progressChanged;
    LoadingStateChangedCallback loadingStateChanged;
    ActiveURLChangedCallback activeURLChanged;
    TitleChangedCallback titleChanged;
    UnhandledKeyEventCallback unhandledKeyEvent;
    ReservedKeyEventCallback reservedKeyEvent;
    FaviconUrlChangedCallback faviconUrlChanged;
    NavigationErrorCallback navigationError;
    WebProcessTerminatedCallback webProcessTerminated;
    InputFocusCallback inputFocus;
    FormSubmitCallback formSubmit;
    ElementFullscreenChangedCallback elementFullscreenChanged;
    ContextMenuRequestedCallback contextMenuRequested;
    FindResultCallback findResult;
    SnapshotTakenCallback snapshotTaken;
    MediaAccessPermissionRequestedCallback mediaAccessPermissionRequested;
    NavigationStateChangedCallback navigationStateChanged;
    DevToolsViewChangedCallback devToolsViewChanged;
    CloseRequestedCallback closeRequested;
    AuthCredentialsRequestedCallback authCredentialsRequested;
};

// ──────────────────────────────────────────────────────────────────────────
//  Popup support (window.open)
// ──────────────────────────────────────────────────────────────────────────

// Result of the popup-creation callback to C#. C# creates a PopupWindow
// (a separate WinUI window), returns its HwndHostPanel's parent HWND plus
// the per-popup BrowserCallbacks struct (kept alive by C# PopupWindow so
// delegates aren't GC'd while native code holds them).
//
// Returning {nullptr, {}} means "popup blocked / denied"; native side
// should return nullptr to WebKit which cancels the window.open.
struct PopupCreateResult
{
    HWND parentHwnd;
    BrowserCallbacks callbacks;
};

// Called by native when window.open is triggered.  C# must create the
// popup window synchronously (popups are UI-thread only).
//
// wantsPopup: 1 if the page specified window features (size, bars) →
//             C# should open a separate PopupWindow.
//             0 if no features → C# should open a new tab.
// originatingHwnd: the parent HWND of the page that called window.open,
//                  so C# can find which MainWindow to add the tab to.
// userGesture: 1 when the window.open was backed by a user gesture (click,
//              key press); 0 for script-initiated opens — popup blocking
//              (C#-side) only ever blocks the latter.
typedef PopupCreateResult (__cdecl *CreatePopupCallback)(
    int32_t requestedWidth, int32_t requestedHeight,
    int32_t requestedX, int32_t requestedY,
    int8_t wantLocationBar, int8_t wantResizable,
    int8_t wantsPopup, int8_t userGesture,
    HWND originatingHwnd,
    const wchar_t* targetUrl);

// Called after native creates the WKView child HWND. C# must
// register the child HWND in its HwndHostPanel so it becomes visible.
typedef void (__cdecl *PopupReadyCallback)(HWND parentHwnd, HWND webViewHwnd);

// Where a navigation diverted out of the current view should open.
// Values must stay in sync with the C# OrionBackend.OpenDisposition enum.
namespace OpenDisposition {
    constexpr int32_t NewForegroundTab = 0;
    constexpr int32_t NewBackgroundTab = 1;
    constexpr int32_t NewWindow        = 2;
}

// Called when a navigation should open outside the current view: a
// modifier-clicked link (Ctrl/Shift/middle, both engines) or a featureless
// window.open / target=_blank that CEF routes to a tab. C# opens a tab or
// window in the MainWindow identified by originatingHwnd and navigates it
// to targetUrl.  The resulting view is standalone (no opener link).
typedef void (__cdecl *OpenURLCallback)(HWND originatingHwnd, const wchar_t* targetUrl, int32_t disposition);
