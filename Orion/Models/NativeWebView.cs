using System;

namespace Orion
{
    /// <summary>
    /// Owns a native WebKit window handle and its lifecycle.
    /// Creates the underlying HWND on construction; destroys it on disposal.
    /// Exposes C# events so consumers don't need to know about OrionBackend callbacks.
    /// Stored on BrowserTab to prevent the callback delegates from being garbage collected.
    /// </summary>
    public class NativeWebView : IDisposable
    {
        public Win32.HWND Hwnd { get; private set; } = Win32.HWND.Zero;

        // Events — subscribe after construction, before first navigation
        public event Action<double>? ProgressChanged;

        /// <summary>
        /// Fired when the main-frame load starts (true) or ends (false):
        /// finished, failed, or reset by a web-process crash. Read IsLoading
        /// after subscribing: the engine can report the first change during
        /// construction, before any subscriber exists.
        /// </summary>
        public event Action<bool>? IsLoadingChanged;

        public event Action<string>? ActiveURLChanged;
        public event Action<string>? TitleChanged;

        /// <summary>
        /// Called by the engine when the page did not handle a key event.
        /// Parameters: (vkCode, modifiers) matching VirtualKey/VirtualKeyModifiers.
        /// Return true if handled as a browser shortcut.
        /// </summary>
        public event Func<uint, uint, bool>? UnhandledKeyEvent;

        /// <summary>
        /// Called before a key event reaches the engine.
        /// Return true if the key is a reserved browser shortcut that should not
        /// be forwarded to the web page (e.g. Ctrl+T, Ctrl+W).
        /// </summary>
        public event Func<uint, uint, bool>? ReservedKeyEvent;

        /// <summary>
        /// Called when a page's favicon URL is discovered after navigation completes.
        /// Parameters: (pageUrl, iconUrl).
        /// </summary>
        public event Action<string, string>? FaviconUrlChanged;

        /// <summary>
        /// Called when a main-frame navigation fails.
        /// Parameters: (errorCategory, rawErrorCode, failingUrl, description).
        /// Returns error page HTML, or null to use the engine default.
        /// </summary>
        public event Func<int, int, string, string, string?>? NavigationError;

        public event Action<string, uint, string>? WebProcessTerminated;

        public event Action<string>? InputFocus;
        public event Action<string>? FormSubmit;

        /// <summary>
        /// Fired when the page enters or leaves HTML5 element fullscreen
        /// (e.g. a fullscreen video). Also raised with false from Dispose()
        /// when the view is destroyed while fullscreen, since the engine can
        /// no longer confirm the exit.
        /// </summary>
        public event Action<bool>? ElementFullscreenChanged;

        /// <summary>
        /// Fired when the engine wants to show a context menu. Return true
        /// to suppress the engine's native menu (the app shows its own),
        /// false to let the engine show its default menu.
        /// </summary>
        public event Func<WebContextMenuContext, bool>? ContextMenuRequested;

        /// <summary>
        /// Fired with the result of a find-in-page operation.
        /// Parameters: (matchCount, activeMatchOrdinal). matchCount 0 = no
        /// matches, negative = more than |matchCount| matches; ordinal is
        /// 1-based, 0 = unknown.
        /// </summary>
        public event Action<int, int>? FindResultChanged;

        /// <summary>
        /// Fired with the result of a RequestSnapshot call: the visible
        /// viewport as encoded JPEG bytes, or null when the capture failed
        /// (e.g. the view is hidden).
        /// </summary>
        public event Action<byte[]?>? SnapshotTaken;

        /// <summary>
        /// Fired when the page requests media capture access (getUserMedia /
        /// getDisplayMedia). Parameters: (pageUrl, requested types, requestId).
        /// The handler must resolve the request exactly once via
        /// OrionBackend.RespondToMediaAccessPermissionRequest; with no
        /// handler subscribed the request is denied.
        /// </summary>
        public event Action<string, WebMediaPermission, long>? MediaAccessPermissionRequested;

        /// <summary>
        /// Fired when back/forward availability changes: (canGoBack, canGoForward).
        /// </summary>
        public event Action<bool, bool>? NavigationStateChanged;

        /// <summary>
        /// Fired when the engine creates or destroys this view's DevTools
        /// frontend view: (previous, current) HWNDs, zero = none. The current
        /// handle is a hidden child of the top-level window for the host to
        /// place; it repeats (previous == current) when the frontend asks to
        /// be brought to the front. Read DevToolsHwnd after subscribing.
        /// </summary>
        public event Action<Win32.HWND, Win32.HWND>? DevToolsViewChanged;

        /// <summary>
        /// The page closed itself via window.close() (engine-vetted: only
        /// script-opened windows / single-entry tabs may). Fires from inside
        /// an engine callback — handlers must defer any view teardown to the
        /// next dispatcher tick.
        /// </summary>
        public event Action? CloseRequested;

        /// <summary>
        /// A request hit an HTTP auth challenge:
        /// (host, port, isProxy, realm, requestId). The handler must resolve
        /// the request exactly once via OrionBackend.RespondToAuthCredentials;
        /// with no handler subscribed the challenge is cancelled.
        /// </summary>
        public event Action<string, int, bool, string, long>? AuthCredentialsRequested;

        /// <summary>True while the page holds HTML5 element fullscreen.</summary>
        public bool IsElementFullscreen { get; private set; }

        /// <summary>Latest main-frame loading state reported by the engine.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>The DevTools frontend view; zero while closed.</summary>
        public Win32.HWND DevToolsHwnd { get; private set; } = Win32.HWND.Zero;

        // Prevent GC of the delegates passed to native code

        public NativeWebView(Win32.HWND parentHwnd, long profileId, string? initialUrl = null)
        {
            Callbacks = BuildCallbacks();

            LogDebug($"NativeWebView: Creating with parent HWND = {parentHwnd}");
            Hwnd = string.IsNullOrEmpty(initialUrl)
                ? OrionBackend.CreateOrionWindow(parentHwnd, Callbacks, profileId)
                : OrionBackend.CreateOrionWindowWithUrl(parentHwnd, Callbacks, profileId, initialUrl);
            LogDebug($"NativeWebView: Created HWND = {Hwnd}");
            if (Hwnd == Win32.HWND.Zero)
                throw new InvalidOperationException("Failed to create native browser window.");
            OrionBackend.ConfigureWebViewExtensions(Hwnd, profileId);
        }

        /// <summary>
        /// Creates a detached NativeWebView for a popup.  Native code calls
        /// WebKit's createNewPage with a pre-supplied WKPageConfigurationRef
        /// and allocates the WKView HWND itself; this wrapper exists only
        /// to hold the callback delegates (keeping them alive) and to expose
        /// navigation commands once AttachHwnd has been called.
        /// </summary>
        public static NativeWebView CreateDetached() => new();

        private NativeWebView()
        {
            // Build callbacks that forward to THIS instance's events.  The
            // caller passes them to native via the popup creation callback;
            // events start firing once AttachHwnd wires up the HWND.
            Callbacks = BuildCallbacks();
        }

        public void AttachHwnd(Win32.HWND hwnd)
        {
            if (Hwnd != Win32.HWND.Zero)
                throw new InvalidOperationException("NativeWebView HWND already set");
            Hwnd = hwnd;
            LogDebug($"NativeWebView: Attached popup HWND = {hwnd}");
        }

        /// <summary>
        /// The callbacks struct created during construction.  Pass this to
        /// native code when adopting a pre-created WKView HWND.  The
        /// delegates are alive for as long as this NativeWebView is alive.
        /// </summary>
        public OrionBackend.BrowserCallbacks Callbacks { get; }

        /// <summary>
        /// Builds a BrowserCallbacks struct whose delegates forward into
        /// this NativeWebView's events.  Exposed so popup creation can build
        /// the struct early (before the native HWND exists) and pass it to
        /// native, then adopt the resulting HWND via AdoptForPopup.
        /// </summary>
        public OrionBackend.BrowserCallbacks BuildCallbacks() => new()
        {
            ProgressChanged = progress => ProgressChanged?.Invoke(progress),
            LoadingStateChanged = isLoading =>
            {
                IsLoading = isLoading != 0;
                IsLoadingChanged?.Invoke(IsLoading);
            },
            ActiveURLChanged = url =>
            {
                if (_sessionMediaSite != null && MediaPermissions.WebsiteForUrl(url) != _sessionMediaSite)
                    ClearSessionMediaDecisions();
                ActiveURLChanged?.Invoke(url);
            },
            TitleChanged = title => TitleChanged?.Invoke(title),
            UnhandledKeyEvent = (vkCode, modifiers) =>
                UnhandledKeyEvent?.Invoke(vkCode, modifiers) == true ? 1 : 0,
            ReservedKeyEvent = (vkCode, modifiers) =>
                ReservedKeyEvent?.Invoke(vkCode, modifiers) == true ? 1 : 0,
            FaviconUrlChanged = (pageUrl, iconUrl) =>
                FaviconUrlChanged?.Invoke(pageUrl, iconUrl),
            NavigationError = (category, rawCode, url, desc) =>
                NavigationError?.Invoke(category, rawCode, url, desc),
            WebProcessTerminated = (processName, reason, reasonStr) =>
                WebProcessTerminated?.Invoke(processName, reason, reasonStr),
            InputFocus = json => InputFocus?.Invoke(json),
            FormSubmit = json => FormSubmit?.Invoke(json),
            ElementFullscreenChanged = isFullscreen =>
            {
                IsElementFullscreen = isFullscreen != 0;
                ElementFullscreenChanged?.Invoke(isFullscreen != 0);
            },
            ContextMenuRequested = request =>
            {
                // Copy the borrowed native struct before returning. An
                // exception escaping a reverse-P/Invoke delegate is undefined
                // behavior, so nothing may throw past this frame.
                try
                {
                    var context = WebContextMenuContext.FromNative(request);
                    return ContextMenuRequested?.Invoke(context) == true ? 1 : 0;
                }
                catch (Exception ex)
                {
                    LogError($"ContextMenuRequested handler failed: {ex}");
                    return 0;
                }
            },
            FindResult = (matchCount, activeMatchOrdinal) =>
                FindResultChanged?.Invoke(matchCount, activeMatchOrdinal),
            SnapshotTaken = (data, size) =>
            {
                byte[]? jpeg = null;
                if (data != IntPtr.Zero && size > 0)
                {
                    jpeg = new byte[size];
                    System.Runtime.InteropServices.Marshal.Copy(data, jpeg, 0, size);
                }
                SnapshotTaken?.Invoke(jpeg);
            },
            MediaAccessPermissionRequested = (pageUrl, mediaPermissions, requestId) =>
            {
                if (MediaAccessPermissionRequested is { } handler)
                    handler(pageUrl, (WebMediaPermission)mediaPermissions, requestId);
                else
                    OrionBackend.RespondToMediaAccessPermissionRequest(requestId, false);
            },
            NavigationStateChanged = (canGoBack, canGoForward) =>
                NavigationStateChanged?.Invoke(canGoBack != 0, canGoForward != 0),
            DevToolsViewChanged = SetDevToolsHwnd,
            CloseRequested = () => CloseRequested?.Invoke(),
            AuthCredentialsRequested = (host, port, isProxy, realm, requestId) =>
            {
                if (AuthCredentialsRequested is { } handler)
                    handler(host, port, isProxy != 0, realm, requestId);
                else
                    OrionBackend.RespondToAuthCredentials(requestId, 0, null, null);
            },
        };

        private void SetDevToolsHwnd(Win32.HWND hwnd)
        {
            Win32.HWND previous = DevToolsHwnd;
            if (hwnd == Win32.HWND.Zero && previous == Win32.HWND.Zero)
                return;
            DevToolsHwnd = hwnd;
            DevToolsViewChanged?.Invoke(previous, hwnd);
        }

        // Session-scoped ("this time") media-capture decisions. One site at a
        // time — cleared when the view navigates cross-site and dropped with
        // the view. Needed because the engine raises a permission request for
        // every getUserMedia call (CEF has no engine-side grant cache).
        private string? _sessionMediaSite;
        private WebMediaPermission _sessionMediaAllowed;
        private WebMediaPermission _sessionMediaDenied;

        /// <summary>
        /// Answers a media-capture request from this visit's "this time"
        /// prompt choices: Allow when every requested type was allowed, Deny
        /// when any requested type was denied, Ask otherwise.
        /// </summary>
        public MediaPermissions.Decision ResolveSessionMediaDecision(string website, WebMediaPermission types)
        {
            if (website != _sessionMediaSite)
                return MediaPermissions.Decision.Ask;
            if ((types & _sessionMediaDenied) != 0)
                return MediaPermissions.Decision.Deny;
            return (types & ~_sessionMediaAllowed) == 0
                ? MediaPermissions.Decision.Allow
                : MediaPermissions.Decision.Ask;
        }

        /// <summary>Records a "this time" prompt choice for the current site.</summary>
        public void RecordSessionMediaDecision(string website, WebMediaPermission types, bool allow)
        {
            if (website != _sessionMediaSite)
            {
                ClearSessionMediaDecisions();
                _sessionMediaSite = website;
            }
            if (allow)
            {
                _sessionMediaAllowed |= types;
                _sessionMediaDenied &= ~types;
            }
            else
            {
                _sessionMediaDenied |= types;
                _sessionMediaAllowed &= ~types;
            }
        }

        private void ClearSessionMediaDecisions()
        {
            _sessionMediaSite = null;
            _sessionMediaAllowed = WebMediaPermission.None;
            _sessionMediaDenied = WebMediaPermission.None;
        }

        public void LoadHTMLString(string html, string unreachableUrl) => OrionBackend.LoadHTMLString(Hwnd, html, unreachableUrl);

        public void GoBack() => OrionBackend.Backward(Hwnd);

        public void GoForward() => OrionBackend.Forward(Hwnd);

        public void Reload() => OrionBackend.Reload(Hwnd);

        public void ReloadBypassingCache() => OrionBackend.ReloadBypassingCache(Hwnd);

        public void LoadUrl(string url) => _ = OrionBackend.LoadURL(Hwnd, url);

        public void Resize(int width, int height) => OrionBackend.ResizeOrionWindow(Hwnd, width, height);

        public void SetUserAgent(string? userAgent) => OrionBackend.SetCustomUserAgent(Hwnd, userAgent);

        public void ExecuteJavaScript(string script) => OrionBackend.ExecuteJavaScript(Hwnd, script);

        /// <summary>
        /// Executes a context-menu command chosen from the app-drawn menu.
        /// arg: URL for SaveUrl; x/y: the request's coordinates for InspectElement.
        /// </summary>
        public void ExecuteContextMenuCommand(WebContextMenuCommand command, string? arg = null, int x = 0, int y = 0) =>
            OrionBackend.ExecuteContextMenuCommand(Hwnd, command, arg, x, y);

        /// <summary>
        /// Opens the DevTools frontend, or focuses it when already open. The
        /// frontend view arrives via DevToolsViewChanged. x/y >= 0 inspect the
        /// element at those view coordinates.
        /// </summary>
        public void ShowDevTools(int x = -1, int y = -1) => OrionBackend.ShowDevTools(Hwnd, x, y);

        /// <summary>Closes the DevTools frontend; DevToolsViewChanged(zero) follows.</summary>
        public void CloseDevTools() => OrionBackend.CloseDevTools(Hwnd);

        public void ToggleDevTools()
        {
            if (DevToolsHwnd != Win32.HWND.Zero)
                CloseDevTools();
            else
                ShowDevTools();
        }

        /// <summary>
        /// Asks the page to leave HTML5 element fullscreen. The engine
        /// confirms via ElementFullscreenChanged(false).
        /// </summary>
        public void RequestExitElementFullscreen() => OrionBackend.RequestExitElementFullscreen(Hwnd);

        /// <summary>
        /// Find in page. findNext=false starts/restarts a search for text;
        /// true advances from the current match. Results arrive via
        /// FindResultChanged. text must be non-empty (use StopFindInPage).
        /// </summary>
        public void FindInPage(string text, bool forward, bool findNext) =>
            OrionBackend.FindInPage(Hwnd, text, forward, findNext);

        /// <summary>Ends the find session and hides match highlights.</summary>
        public void StopFindInPage(bool clearSelection) => OrionBackend.StopFindInPage(Hwnd, clearSelection);

        /// <summary>
        /// Requests an async snapshot of the visible viewport (JPEG, at most
        /// maxPixelWidth wide; 0 = native size). The result arrives via
        /// SnapshotTaken. Only reliable while the view is (or was just)
        /// visible — hidden views can't paint.
        /// </summary>
        public void RequestSnapshot(int maxPixelWidth) => OrionBackend.RequestTabSnapshot(Hwnd, maxPixelWidth);

        public void Dispose()
        {
            if (IsElementFullscreen)
            {
                IsElementFullscreen = false;
                ElementFullscreenChanged?.Invoke(false);
            }
            // The engine's own closed notice can arrive after the host has let
            // go of this view (CEF closes DevTools asynchronously), so report
            // the closure here while the delegates are still rooted.
            SetDevToolsHwnd(Win32.HWND.Zero);
            if (Hwnd != Win32.HWND.Zero)
                OrionBackend.DestroyOrionWindow(Hwnd);
        }
    }
}
