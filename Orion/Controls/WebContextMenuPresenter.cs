using DirectShow;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Orion
{
    /// <summary>
    /// Shows an app-drawn web context menu (MenuFlyout) above the embedded
    /// browser HWND and dispatches the chosen item: engine commands go back
    /// through the NativeWebView, app actions run via host delegates.
    /// Light dismiss cannot see clicks landing inside the native HWND, so a
    /// thread-scoped WH_MOUSE hook (one shared instance for all windows on
    /// the UI thread) closes the menu on mouse activity in the browser
    /// subtree while it is open; resize/move dismissal is self-managed too.
    /// Hosts additionally Close() on their own triggers (navigation, tab
    /// switch, crash, cleanup).
    /// </summary>
    /// <param name="anchor">The web view host panel the menu positions against.</param>
    /// <param name="openUrl">Opens a URL with the given disposition (new tab/window).</param>
    /// <param name="savePageAs">Host's save-page dialog; null omits the Save Page item.</param>
    /// <param name="searchEngine">Default-engine provider; null omits the Search item.</param>
    public sealed class WebContextMenuPresenter(
        HwndHostPanel anchor,
        Action<string, OrionBackend.OpenDisposition> openUrl,
        Action? savePageAs = null,
        Func<SearchEngineInfo>? searchEngine = null)
    {
        private readonly HwndHostPanel _anchor = anchor;
        private readonly Action<string, OrionBackend.OpenDisposition> _openUrl = openUrl;
        private readonly Action? _savePageAs = savePageAs;
        private readonly Func<SearchEngineInfo>? _searchEngine = searchEngine;

        private MenuFlyout? _flyout;
        private NativeWebView? _webView;
        private WebContextMenuContext? _context;
        private bool _dismissTriggersInstalled;

        /// <summary>
        /// Handles NativeWebView.ContextMenuRequested: composes the menu and
        /// defers display (the callback arrives inside the engine's message
        /// pump — no XAML popup logic on that stack). Returns true when the
        /// app menu will be shown, suppressing the engine's native menu.
        /// </summary>
        public bool HandleRequest(NativeWebView webView, WebContextMenuContext context)
        {
            SearchEngineInfo? engine = _searchEngine?.Invoke();
            IReadOnlyList<WebContextMenuItem> items = WebContextMenu.Build(
                context, engine?.Name, engine?.UrlTemplate, canSavePage: _savePageAs != null);
            if (items.Count == 0)
                return false;
            _ = _anchor.DispatcherQueue.TryEnqueue(() =>
            {
                // The view may have been switched away between the engine's
                // request and this dispatch.
                if (_anchor.VisibleHwnd == webView.Hwnd)
                    Show(webView, context, items);
            });
            return true;
        }

        private void Show(NativeWebView webView, WebContextMenuContext context, IReadOnlyList<WebContextMenuItem> items)
        {
            Close();
            InstallDismissTriggers();

            // ShouldConstrainToRootBounds=false is honored at open time only;
            // the unconstrained (windowed) popup renders above the native HWND.
            var flyout = new MenuFlyout
            {
                ShouldConstrainToRootBounds = false,
                AreOpenCloseAnimationsEnabled = false,
            };
            foreach (WebContextMenuItem item in items)
            {
                if (item.IsSeparator)
                {
                    flyout.Items.Add(new MenuFlyoutSeparator());
                    continue;
                }
                var menuItem = new MenuFlyoutItem { Text = item.Label, IsEnabled = item.IsEnabled };
                WebContextMenuItem captured = item;
                menuItem.Click += (_, _) => OnItemClick(captured);
                flyout.Items.Add(menuItem);
            }
            flyout.Closed += OnFlyoutClosed;

            _flyout = flyout;
            _webView = webView;
            _context = context;
            DismissHook.Activate(this, webView.Hwnd);
            flyout.ShowAt(_anchor, new FlyoutShowOptions { Position = ComputePosition(context, webView.Hwnd) });
        }

        public void Close() => _flyout?.Hide();

        // An open menu doesn't track the view: close on resize and window move.
        private void InstallDismissTriggers()
        {
            if (_dismissTriggersInstalled)
                return;
            _dismissTriggersInstalled = true;
            _anchor.SizeChanged += (_, _) => Close();
            if (_anchor.XamlRoot?.ContentIslandEnvironment is { } island)
            {
                AppWindow.GetFromWindowId(island.AppWindowId).Changed += (_, args) =>
                {
                    if (args.DidPositionChange)
                        Close();
                };
            }
        }

        // The cursor position has unambiguous units; the request's x/y
        // (expected DIPs) covers keyboard-invoked menus where the cursor may
        // be outside the view.
        private Point ComputePosition(WebContextMenuContext context, Win32.HWND browserHwnd)
        {
            double scale = _anchor.XamlRoot?.RasterizationScale ?? 1.0;
            if (Win32.GetCursorPos(out Win32.POINT pt)
                && Win32.ScreenToClient(browserHwnd, ref pt)
                && Win32.GetClientRect(browserHwnd, out RECT client)
                && pt.x >= 0 && pt.y >= 0 && pt.x < client.right && pt.y < client.bottom)
            {
                LogDebug($"WebContextMenu: cursor px=({pt.x},{pt.y}) request=({context.X},{context.Y}) scale={scale}");
                return new Point(pt.x / scale, pt.y / scale);
            }
            return new Point(context.X, context.Y);
        }

        private void OnItemClick(WebContextMenuItem item)
        {
            NativeWebView? webView = _webView;
            WebContextMenuContext? context = _context;
            if (webView == null || context == null)
                return;
            // Deferred so dispatch runs after the flyout's own dismissal
            // handling, independent of Click-vs-Closed ordering.
            _ = _anchor.DispatcherQueue.TryEnqueue(() =>
            {
                if (!Win32.IsWindow(webView.Hwnd))
                    return;
                _ = Win32.SetFocus(webView.Hwnd);
                if (item.EngineCommand != 0)
                {
                    webView.ExecuteContextMenuCommand(item.EngineCommand,
                        item.Arg.Length > 0 ? item.Arg : null, context.X, context.Y);
                    return;
                }
                switch (item.AppAction)
                {
                    case WebContextMenuAppAction.OpenInNewTab:
                        _openUrl(item.Arg, OrionBackend.OpenDisposition.NewBackgroundTab);
                        break;
                    case WebContextMenuAppAction.OpenInNewWindow:
                        _openUrl(item.Arg, OrionBackend.OpenDisposition.NewWindow);
                        break;
                    case WebContextMenuAppAction.CopyText:
                        var package = new DataPackage();
                        package.SetText(item.Arg);
                        Clipboard.SetContent(package);
                        break;
                    case WebContextMenuAppAction.SavePageAs:
                        _savePageAs?.Invoke();
                        break;
                    case WebContextMenuAppAction.None:
                        break;
                    default:
                        break;
                }
            });
        }

        private void OnFlyoutClosed(object? sender, object e)
        {
            if (!ReferenceEquals(sender, _flyout))
                return; // a replaced flyout closing late
            NativeWebView? webView = _webView;
            _flyout = null;
            _webView = null;
            _context = null;
            DismissHook.Deactivate(this);
            // Win32 focus otherwise lands back in the XAML island and typing
            // goes nowhere until the user clicks the page.
            if (webView != null && Win32.IsWindow(webView.Hwnd))
                _ = Win32.SetFocus(webView.Hwnd);
        }

        // One hook serves every window on the UI thread (CEF creates all its
        // input HWNDs there); only the presenter whose flyout is open owns it,
        // so a stale flyout's late Closed can't unhook the active one.
        private static class DismissHook
        {
            private static readonly Win32.HOOKPROC s_proc = HookProc; // rooted while the hook is installed
            private static IntPtr s_hook;
            private static WebContextMenuPresenter? s_owner;
            private static Win32.HWND s_browserHwnd;

            public static void Activate(WebContextMenuPresenter owner, Win32.HWND browserHwnd)
            {
                s_owner = owner;
                s_browserHwnd = browserHwnd;
                if (s_hook == IntPtr.Zero)
                    s_hook = Win32.SetWindowsHookExW(Win32.WH_MOUSE, s_proc, IntPtr.Zero, Win32.GetCurrentThreadId());
            }

            public static void Deactivate(WebContextMenuPresenter owner)
            {
                if (s_owner != owner)
                    return;
                s_owner = null;
                if (s_hook != IntPtr.Zero)
                {
                    _ = Win32.UnhookWindowsHookEx(s_hook);
                    s_hook = IntPtr.Zero;
                }
            }

            private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode >= 0 && s_owner is { } owner)
                {
                    int msg = (int)wParam;
                    bool buttonDown = msg is Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN
                        or Win32.WM_MBUTTONDOWN or Win32.WM_XBUTTONDOWN;
                    bool wheel = msg is Win32.WM_MOUSEWHEEL or Win32.WM_MOUSEHWHEEL;
                    if (buttonDown || wheel)
                    {
                        Win32.MOUSEHOOKSTRUCT info = Marshal.PtrToStructure<Win32.MOUSEHOOKSTRUCT>(lParam);
                        if (info.hwnd == s_browserHwnd.Value || Win32.IsChild(s_browserHwnd.Value, info.hwnd))
                        {
                            // Deferred: closing runs XAML logic; don't re-enter
                            // it from inside the message hook.
                            _ = owner._anchor.DispatcherQueue.TryEnqueue(owner.Close);
                            // Swallow the dismissing click like a native menu.
                            // Right-click passes through so the engine opens
                            // the next menu; wheel passes through and scrolls.
                            if (msg is Win32.WM_LBUTTONDOWN or Win32.WM_MBUTTONDOWN or Win32.WM_XBUTTONDOWN)
                                return 1;
                        }
                    }
                }
                return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }
        }
    }
}
