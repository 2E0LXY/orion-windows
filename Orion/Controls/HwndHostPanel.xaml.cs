using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace Orion
{
    /// <summary>
    /// A panel that hosts native Win32 HWNDs, properly composited with WinUI content.
    /// Supports multiple HWNDs with only one visible at a time.
    /// Installs a WinEvent hook to apply WS_EX_LAYERED to any child HWNDs
    /// created internally by the browser engine (tooltips, popups, inspector, etc.).
    /// </summary>
    public sealed partial class HwndHostPanel : UserControl
    {
        private readonly HashSet<Win32.HWND> _hwnds = [];
        public Win32.HWND VisibleHwnd { get; private set; }
        private Win32.HWND _parentHwnd;
        public bool IsAttached { get; private set; }

        // WinEvent hook to catch engine-created child HWNDs
        private IntPtr _winEventHook;
        private Win32.WinEventDelegate? _winEventDelegate; // prevent GC

        /// <summary>
        /// The top-level window handle that child HWNDs are parented to.
        /// Must be set before any HWNDs are added (typically in the View's Loaded handler).
        /// </summary>
        public Win32.HWND WindowHandle { get; set; }

        public HwndHostPanel()
        {
            InitializeComponent();

            // Subscribe to lifecycle events
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            LayoutUpdated += OnLayoutUpdated;
        }

        /// <summary>
        /// Gets the number of HWNDs managed by this panel.
        /// </summary>
        public int Count => _hwnds.Count;

        /// <summary>
        /// Adds an HWND to be managed by this panel.
        /// The HWND is attached as a child but hidden until Show() is called.
        /// </summary>
        /// <param name="hwnd">The HWND to add.</param>
        /// <returns>True if added, false if already exists.</returns>
        public bool Add(Win32.HWND hwnd)
        {
            if (hwnd == Win32.HWND.Zero || _hwnds.Contains(hwnd))
                return false;

            _ = _hwnds.Add(hwnd);

            // If we're already attached to a parent, attach this HWND too
            if (IsAttached && _parentHwnd != Win32.HWND.Zero)
            {
                AttachHwnd(hwnd);
                // Hide it initially (unless it's the first one and nothing is visible)
                if (VisibleHwnd != Win32.HWND.Zero)
                {
                    _ = Win32.ShowWindow(hwnd, Win32.SW_HIDE);
                }
                else
                {
                    // First HWND added - make it visible
                    VisibleHwnd = hwnd;
                    UpdatePosition();
                }
            }

            return true;
        }

        /// <summary>
        /// Removes an HWND from this panel.
        /// </summary>
        /// <param name="hwnd">The HWND to remove.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool Remove(Win32.HWND hwnd)
        {
            if (hwnd == Win32.HWND.Zero || !_hwnds.Contains(hwnd))
                return false;

            // If this is the visible one, hide it first
            if (VisibleHwnd == hwnd)
            {
                _ = Win32.ShowWindow(hwnd, Win32.SW_HIDE);
                VisibleHwnd = Win32.HWND.Zero;
            }

            // Detach from parent
            if (IsAttached)
                DetachHwnd(hwnd);

            _ = _hwnds.Remove(hwnd);
            return true;
        }

        /// <summary>
        /// Shows the specified HWND and hides the currently visible one.
        /// </summary>
        /// <param name="hwnd">The HWND to show.</param>
        /// <returns>True if shown, false if HWND not found.</returns>
        public bool Show(Win32.HWND hwnd)
        {
            if (hwnd == Win32.HWND.Zero || !_hwnds.Contains(hwnd))
                return false;

            if (VisibleHwnd == hwnd)
            {
                return true; // Already visible
            }

            // Hide current
            if (VisibleHwnd != Win32.HWND.Zero)
                _ = Win32.ShowWindow(VisibleHwnd, Win32.SW_HIDE);

            // Show new
            VisibleHwnd = hwnd;
            UpdatePosition();
            _ = Win32.ShowWindow(hwnd, Win32.SW_SHOW);

            return true;
        }

        /// <summary>
        /// Hides all HWNDs.
        /// </summary>
        public void HideAll()
        {
            if (VisibleHwnd != Win32.HWND.Zero)
            {
                _ = Win32.ShowWindow(VisibleHwnd, Win32.SW_HIDE);
                VisibleHwnd = Win32.HWND.Zero;
            }
        }

        /// <summary>
        /// Checks if an HWND is managed by this panel.
        /// </summary>
        public bool Contains(Win32.HWND hwnd) => _hwnds.Contains(hwnd);

        /// <summary>
        /// Forces early attachment when WindowHandle is set but Loaded
        /// hasn't fired yet.  Safe to call multiple times — no-ops if
        /// already attached.
        /// </summary>
        public void EnsureAttached()
        {
            if (IsAttached)
                return;
            DoAttach();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (IsAttached)
                return;
            DoAttach();
        }

        private void DoAttach()
        {
            // Use explicitly set WindowHandle, fall back to Utils for compat
            _parentHwnd = WindowHandle != Win32.HWND.Zero
                ? WindowHandle
                : Utils.GetWindowHandle(this);

            if (_parentHwnd != Win32.HWND.Zero)
            {
                // Attach all HWNDs that were added before we loaded
                foreach (Win32.HWND hwnd in _hwnds)
                {
                    AttachHwnd(hwnd);
                    // Hide all except the visible one
                    if (hwnd != VisibleHwnd)
                        _ = Win32.ShowWindow(hwnd, Win32.SW_HIDE);
                }
                IsAttached = true;

                // Position the visible HWND
                if (VisibleHwnd != Win32.HWND.Zero)
                {
                    UpdatePosition();
                    _ = Win32.ShowWindow(VisibleHwnd, Win32.SW_SHOW);
                }

                InstallChildHwndHook();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UninstallChildHwndHook();

            // Hide all HWNDs when unloaded
            foreach (Win32.HWND hwnd in _hwnds)
            {
                _ = Win32.ShowWindow(hwnd, Win32.SW_HIDE);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePosition();

        private void OnLayoutUpdated(object? sender, object e) => UpdatePosition();

        /// <summary>
        /// Attaches an HWND as a child of the parent window.
        /// </summary>
        private void AttachHwnd(Win32.HWND hwnd)
        {
            if (hwnd == Win32.HWND.Zero || _parentHwnd == Win32.HWND.Zero)
                return;

            // Add WS_EX_LAYERED for WinUI composition compatibility
            IntPtr exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            _ = Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, (IntPtr)((long)exStyle | Win32.WS_EX_LAYERED));

            // Make fully opaque
            _ = Win32.SetLayeredWindowAttributes(hwnd, 0, 255, Win32.LWA_ALPHA);

            // Set child window styles
            IntPtr style = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);
            _ = Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, (IntPtr)((long)style | Win32.WS_CHILD | Win32.WS_CLIPCHILDREN | Win32.WS_CLIPSIBLINGS));

            // Set parent
            _ = Win32.SetParent(hwnd, _parentHwnd);
        }

        /// <summary>
        /// Detaches an HWND from the parent window.
        /// </summary>
        private void DetachHwnd(Win32.HWND hwnd)
        {
            if (hwnd == Win32.HWND.Zero)
                return;

            // Hide first
            _ = Win32.ShowWindow(hwnd, Win32.SW_HIDE);

            // Remove parent relationship
            _ = Win32.SetParent(hwnd, IntPtr.Zero);

            // Remove child style
            IntPtr style = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);
            _ = Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, (IntPtr)((long)style & ~Win32.WS_CHILD));
        }

        /// <summary>
        /// Updates the position and size of the visible HWND to match this control.
        /// </summary>
        public void UpdatePosition()
        {
            if (VisibleHwnd == Win32.HWND.Zero || !IsAttached || !IsLoaded || XamlRoot == null)
                return;

            // Get position relative to the window
            GeneralTransform transform = TransformToVisual(null);
            Point position = transform.TransformPoint(new Point(0, 0));

            // Get DPI scale factor
            double scale = XamlRoot.RasterizationScale;

            // Calculate pixel coordinates
            int x = (int)(position.X * scale);
            int y = (int)(position.Y * scale);
            int width = (int)(ActualWidth * scale);
            int height = (int)(ActualHeight * scale);

            // Position only — never SWP_SHOWWINDOW here. SetWindowPos flips
            // WS_VISIBLE without sending WM_SHOWWINDOW, which starves the
            // engine's visibility tracking (WebKit's WebView only learns
            // visibility from WM_SHOWWINDOW, so the page's document would
            // stay "hidden" after a tab switch). Show() owns the reveal via
            // ShowWindow(SW_SHOW), which does send the message.
            _ = Win32.SetWindowPos(VisibleHwnd, Win32.HWND.Zero, x, y, width, height,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        }

        public void Clear()
        {
            UninstallChildHwndHook();

            if (VisibleHwnd != Win32.HWND.Zero)
                _ = Win32.ShowWindow(VisibleHwnd, Win32.SW_HIDE);
            foreach (Win32.HWND hwnd in _hwnds)
            {
                // Remove parent relationship
                _ = Win32.SetParent(hwnd, IntPtr.Zero);
            }
        }

        /// <summary>
        /// Installs a WinEvent hook to detect HWNDs created internally by the
        /// browser engine (tooltips, &lt;select&gt; popups, inspector, context
        /// menus, scrollbar overlays, etc.) and applies WS_EX_LAYERED so they
        /// composite correctly with WinUI content.
        /// </summary>
        private void InstallChildHwndHook()
        {
            // CEF renders to texture and the windows have WS_EX_NOREDIRECTIONBITMAP, which causes race
            // conditions with WS_EX_LAYERED. WebKit uses the old BeginPaint/GetDC style which doesn't
            // work with WS_EX_NOREDIRECTIONBITMAP.
            if (OrionBackend.IsBlinkEngine)
                return;

            if (_winEventHook != IntPtr.Zero)
                return;

            // prevent delegate from being garbage collected
            _winEventDelegate = OnWinEvent;
            _winEventHook = Win32.SetWinEventHook(
                Win32.EVENT_OBJECT_CREATE,
                Win32.EVENT_OBJECT_CREATE,
                IntPtr.Zero,
                _winEventDelegate,
                Win32.GetCurrentProcessId(),
                0, // all threads in this process
                Win32.WINEVENT_OUTOFCONTEXT);
        }

        private void UninstallChildHwndHook()
        {
            if (_winEventHook != IntPtr.Zero)
            {
                _ = Win32.UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
            _winEventDelegate = null;
        }

        private void OnWinEvent(
            IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild,
            uint idEventThread, uint dwmsEventTime)
        {
            if (idObject != Win32.OBJID_WINDOW || idChild != Win32.CHILDID_SELF
                || hwnd == IntPtr.Zero)
            {
                return;
            }

            long exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);

            // Skip DComp windows (WinUI framework)
            if ((exStyle & Win32.WS_EX_NOREDIRECTIONBITMAP) != 0)
                return;

            // Skip windows that already have WS_EX_LAYERED
            if ((exStyle & Win32.WS_EX_LAYERED) != 0)
                return;

            long style = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);

            // Only process visible child windows that are descendants of
            // our main window. This catches the docked inspector (WS_CHILD
            // | WS_VISIBLE, child of _parentHwnd) but skips popups,
            // hidden transient windows, and orphans that WebKit doesn't
            // intend to be visible yet.
            if ((style & Win32.WS_CHILD) == 0)
                return;
            if ((style & Win32.WS_VISIBLE) == 0)
                return;
            if (_parentHwnd == Win32.HWND.Zero || !Win32.IsChild(_parentHwnd, hwnd))
                return;

            _ = Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE,
                (IntPtr)(exStyle | Win32.WS_EX_LAYERED));
            _ = Win32.SetLayeredWindowAttributes(hwnd, 0, 255, Win32.LWA_ALPHA);

            // HACK: SetParent (even to the same parent) triggers DWM to
            // rebuild the compositing surface so the docked Web Inspector
            // becomes visible immediately. Remove once we patch WebKit to
            // allow us to layout the inspector in C#. Deferred to avoid
            // WinEvent recursion. Still causes a brief flash of the
            // undocked inspector popup.
            IntPtr captured = hwnd;
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                IntPtr parent = Win32.GetParent(captured);
                if (parent != IntPtr.Zero)
                    _ = Win32.SetParent(captured, parent);
            });
        }
    }
}
