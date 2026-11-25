using DirectShow;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Orion
{
    /// <summary>
    /// Minimal popup UI: title bar + optional non-editable location bar +
    /// embedded WKView HWND.  No tabs, no history, no DB persistence.
    ///
    /// Construction flow:
    ///  1. Native's createNewPage callback invokes the registered
    ///     CreatePopupCallback.  The handler creates a PopupWindow +
    ///     PopupWindowContent, builds the (detached) NativeWebView, and
    ///     returns the content's host HWND + the callbacks struct.
    ///  2. Native creates the WKView on that HWND using the WebKit-supplied
    ///     WKPageConfigurationRef (so window.opener / same-origin survive).
    ///  3. Native invokes PopupReadyCallback which routes to
    ///     AttachNativeWebView — we call AttachHwnd + register the HWND.
    /// </summary>
    public sealed partial class PopupWindowContent : UserControl
    {
        public Window? ParentWindow { get; set; }

        /// <summary>Exposed for PopupWindow.SetTitleBar().</summary>
        internal UIElement TitleBarElementRef => TitleBarElement;

        public bool ShowLocationBar
        {
            get => LocationBar.Visibility == Visibility.Visible;
            set => LocationBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        internal Windows.Graphics.SizeInt32 ComputeOuterSize(
            int viewportWidthCss, int viewportHeightCss, IntPtr hwnd)
        {
            uint dpi = Win32.GetDpiForWindow(hwnd);
            double scale = dpi / 96.0;

            double trimHeightDips = TitleBarElement.Height;
            if (ShowLocationBar)
            {
                LocationBar.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                trimHeightDips += LocationBar.DesiredSize.Height;
            }

            // ExtendsContentIntoTitleBar reclaims the caption as client
            // area, so the non-client overhead is just the resize border.
            var frame = new RECT(0, 0, 0, 0);
            Win32.AdjustWindowRectExForDpi(ref frame, Win32.WS_THICKFRAME, false, 0, dpi);

            return new Windows.Graphics.SizeInt32
            {
                Width = (int)Math.Ceiling(viewportWidthCss * scale) + (frame.right - frame.left),
                Height = (int)Math.Ceiling(viewportHeightCss * scale)
                    + (int)Math.Ceiling(trimHeightDips * scale) + (frame.bottom - frame.top),
            };
        }

        private NativeWebView? _webView;
        private WebContextMenuPresenter? _webContextMenu;

        /// <summary>The MainWindow HWND that opened this popup — context-menu
        /// link items open tabs there.</summary>
        internal Win32.HWND OriginatingMainHwnd { get; set; }

        public PopupWindowContent()
        {
            InitializeComponent();
            WebViewHost.Loaded += (_, _) =>
            {
                LogDebug($"WebViewHost.Loaded: IsAttached={WebViewHost.IsAttached} VisibleHwnd={WebViewHost.VisibleHwnd} Count={WebViewHost.Count} ActualSize={WebViewHost.ActualWidth}x{WebViewHost.ActualHeight}");
                WebViewHost.UpdateLayout();
            };
            SizeChanged += (_, _) => ResizeWebView();
            WebViewHost.SizeChanged += (_, _) => ResizeWebView();
            DevToolsPane.CloseRequested += () => _webView?.CloseDevTools();
            ThemeService.Register(this);
        }

        /// <summary>
        /// Creates the detached NativeWebView and returns the parent HWND +
        /// callbacks struct that native code needs.  Called synchronously
        /// from the popup creation callback.
        /// </summary>
        internal (Win32.HWND parentHwnd, OrionBackend.BrowserCallbacks callbacks) BuildForNative()
        {
            _webView = NativeWebView.CreateDetached();
            WireWebViewEvents(_webView);

            if (ParentWindow == null)
                throw new InvalidOperationException("PopupWindow must be activated before BuildForNative");

            Win32.HWND parentHwnd = WinRT.Interop.WindowNative.GetWindowHandle(ParentWindow);
            WebViewHost.WindowHandle = parentHwnd;
            DevToolsPane.WindowHandle = parentHwnd;
            return (parentHwnd, _webView.Callbacks);
        }

        /// <summary>
        /// Called by PopupReadyCallback after native has created the WKView
        /// child HWND under our PopupWindow's HWND.
        /// </summary>
        internal void AttachNativeWebView(Win32.HWND webViewHwnd)
        {
            if (_webView == null)
            {
                LogError("PopupWindowContent.AttachNativeWebView: no NativeWebView");
                return;
            }
            LogDebug($"AttachNativeWebView: hwnd={webViewHwnd} IsLoaded={WebViewHost.IsLoaded} IsAttached={WebViewHost.IsAttached} XamlRoot={WebViewHost.XamlRoot != null}");
            _webView.AttachHwnd(webViewHwnd);
            WebViewHost.EnsureAttached();
            LogDebug($"AttachNativeWebView: after EnsureAttached IsAttached={WebViewHost.IsAttached}");
            bool added = WebViewHost.Add(webViewHwnd);
            LogDebug($"AttachNativeWebView: Add={added} IsAttached={WebViewHost.IsAttached} VisibleHwnd={WebViewHost.VisibleHwnd}");
            _ = WebViewHost.Show(webViewHwnd);
            LogDebug($"AttachNativeWebView: Show done, VisibleHwnd={WebViewHost.VisibleHwnd} ActualSize={WebViewHost.ActualWidth}x{WebViewHost.ActualHeight}");
            ResizeWebView();
        }

        private void WireWebViewEvents(NativeWebView webView)
        {
            webView.TitleChanged += title =>
            {
                TitleBarText.Text = string.IsNullOrEmpty(title) ? "Popup" : title;
                ParentWindow?.Title = string.IsNullOrEmpty(title) ? "Popup" : title;
                // Deliberately NOT calling OrionBackend.AddHistoryEntry —
                // popups do not contribute to history.
            };
            webView.ActiveURLChanged += url =>
            {
                LocationBarText.Text = url ?? "";
                _webContextMenu?.Close();
                PermissionPromptControl.DismissForNavigation(url ?? "");
                // Deliberately NOT persisting to the db.
            };
            webView.MediaAccessPermissionRequested += (pageUrl, types, requestId) =>
                PermissionPromptControl.HandleRequest(TitleBarElement, webView,
                    OrionBackend.GetCurrentProfileId(), pageUrl, types, requestId);
            // window.close() on a script-opened popup closes its window.
            // Fires from inside an engine callback — defer the teardown to
            // the next dispatcher tick.
            webView.CloseRequested += () =>
                _ = DispatcherQueue.TryEnqueue(() => ParentWindow?.Close());
            // Reserved/unhandled key events are ignored for popups — no
            // tab/window shortcuts apply in a plain popup.
            webView.WebProcessTerminated += (processName, reason, reasonStr) =>
            {
                LogError($"{processName} terminated: {reasonStr} ({reason})");
                _webContextMenu?.Close();
            };
            webView.ContextMenuRequested += context => EnsureWebContextMenu().HandleRequest(webView, context);
            webView.DevToolsViewChanged += (previous, current) =>
            {
                if (previous != Win32.HWND.Zero && previous != current)
                    DevToolsPane.Remove(previous);
                if (current == Win32.HWND.Zero)
                {
                    DevToolsPane.Hide();
                }
                else
                {
                    DevToolsPane.Add(current);
                    DevToolsPane.Show(current);
                }
            };
        }

        // No search item (popups have no profile context for the engine
        // choice) and no save-page item (the dialog lives in MainWindowContent).
        private WebContextMenuPresenter EnsureWebContextMenu() =>
            _webContextMenu ??= new WebContextMenuPresenter(
                WebViewHost,
                (url, disposition) => ((App)Application.Current).OpenUrlFrom(OriginatingMainHwnd, url, disposition));

        internal void DeferredResizeWebView()
        {
            LogDebug($"DeferredResizeWebView: IsAttached={WebViewHost.IsAttached} VisibleHwnd={WebViewHost.VisibleHwnd} ActualSize={WebViewHost.ActualWidth}x{WebViewHost.ActualHeight}");
            WebViewHost.UpdatePosition();
            ResizeWebView();
        }

        private void ResizeWebView()
        {
            if (_webView == null || _webView.Hwnd == Win32.HWND.Zero)
                return;
            double scale = WebViewHost.XamlRoot?.RasterizationScale ?? 1.0;
            int width = (int)(WebViewHost.ActualWidth * scale);
            int height = (int)(WebViewHost.ActualHeight * scale);
            LogDebug($"ResizeWebView: hwnd={_webView.Hwnd} size={width}x{height} scale={scale}");
            if (width > 0 && height > 0)
                _webView.Resize(width, height);
        }

        internal void Cleanup()
        {
            _webContextMenu?.Close();
            _webView?.Dispose();
            _webView = null;
        }
    }
}
