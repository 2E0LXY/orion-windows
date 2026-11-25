using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace Orion
{
    public enum DevToolsDockSide
    {
        Bottom,
        Right,
    }

    /// <summary>
    /// Hosts an engine's DevTools frontend HWND beside the page: a header
    /// (title, dock-side toggle, close) over an HwndHostPanel, with a drag
    /// handle on the page-facing edge. Both engines run their frontend in
    /// undocked mode inside it, with no dock or close controls of their own,
    /// so this header is the only DevTools chrome. Register one HWND per tab;
    /// Show switches between them and Hide collapses the pane.
    ///
    /// Place directly in a Grid with rows [*, Auto] and columns [*, Auto]:
    /// the pane positions itself in the trailing row (bottom dock, spanning
    /// both columns) or the trailing column (right dock, spanning both rows).
    /// </summary>
    public sealed partial class DevToolsPane : UserControl
    {
        private const double MinPaneSize = 120;
        private const double MinPageSize = 100;
        private const double DefaultPaneSize = 320;

        // Last-used choices seed new panes so windows agree within a session.
        private static DevToolsDockSide s_lastDockSide = DevToolsDockSide.Bottom;
        private static double s_lastPaneSize = DefaultPaneSize;

        private DevToolsDockSide _dockSide = s_lastDockSide;
        private double _paneSize = s_lastPaneSize;

        public event Action? CloseRequested;

        public DevToolsPane()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
            ApplyDockSide();
        }

        /// <summary>Top-level window the hosted HWNDs are parented to; set before Add.</summary>
        public Win32.HWND WindowHandle
        {
            get => Host.WindowHandle;
            set => Host.WindowHandle = value;
        }

        public void Add(Win32.HWND hwnd)
        {
            Host.EnsureAttached();
            _ = Host.Add(hwnd);
        }

        public void Remove(Win32.HWND hwnd)
        {
            _ = Host.Remove(hwnd);
            if (Host.VisibleHwnd == Win32.HWND.Zero)
                Hide();
        }

        public void Show(Win32.HWND hwnd)
        {
            Visibility = Visibility.Visible;
            // HwndHostPanel.Add selects the first handle it receives without
            // revealing it, and Show is a no-op for the selected handle.
            if (Host.VisibleHwnd == hwnd)
                _ = Win32.ShowWindow(hwnd, Win32.SW_SHOW);
            else
                _ = Host.Show(hwnd);
            Host.UpdatePosition();
        }

        public void Hide()
        {
            Host.HideAll();
            Visibility = Visibility.Collapsed;
        }

        private void TopSplitter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e) =>
            ResizeBy(-e.Delta.Translation.Y);

        private void LeftSplitter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e) =>
            ResizeBy(-e.Delta.Translation.X);

        private void ResizeBy(double delta)
        {
            double available = Parent is FrameworkElement parent
                ? (_dockSide == DevToolsDockSide.Bottom ? parent.ActualHeight : parent.ActualWidth)
                : double.PositiveInfinity;
            double max = Math.Max(MinPaneSize, available - MinPageSize);
            _paneSize = Math.Clamp(_paneSize + delta, MinPaneSize, max);
            s_lastPaneSize = _paneSize;
            ApplySize();
        }

        private void DockSideButton_Click(object sender, RoutedEventArgs e)
        {
            _dockSide = _dockSide == DevToolsDockSide.Bottom ? DevToolsDockSide.Right : DevToolsDockSide.Bottom;
            s_lastDockSide = _dockSide;
            ApplyDockSide();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

        private void ApplyDockSide()
        {
            bool bottom = _dockSide == DevToolsDockSide.Bottom;
            TopSplitter.Visibility = bottom ? Visibility.Visible : Visibility.Collapsed;
            LeftSplitter.Visibility = bottom ? Visibility.Collapsed : Visibility.Visible;
            // The button offers the other side.
            DockRightIcon.Visibility = bottom ? Visibility.Visible : Visibility.Collapsed;
            DockBottomIcon.Visibility = bottom ? Visibility.Collapsed : Visibility.Visible;
            ToolTipService.SetToolTip(DockSideButton, bottom ? "Dock to right" : "Dock to bottom");
            Grid.SetRow(this, bottom ? 1 : 0);
            Grid.SetRowSpan(this, bottom ? 1 : 2);
            Grid.SetColumn(this, bottom ? 0 : 1);
            Grid.SetColumnSpan(this, bottom ? 2 : 1);
            ApplySize();
        }

        private void ApplySize()
        {
            if (_dockSide == DevToolsDockSide.Bottom)
            {
                Height = _paneSize;
                Width = double.NaN;
            }
            else
            {
                Width = _paneSize;
                Height = double.NaN;
            }
        }
    }
}
