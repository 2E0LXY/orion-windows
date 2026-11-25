using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

namespace Orion
{
    public sealed partial class MainWindowContent
    {
        /// <summary>
        /// Hover preview card for tab strip / sidebar tab items, gated on
        /// settings::tabs::show_tab_preview_on_hover. Shows the tab's last
        /// captured thumbnail (see MainWindowContent.Thumbnails.cs) with its
        /// title and URL in a Popup, which renders above the web HWND.
        /// </summary>
        private const double HoverPreviewWidth = 258;
        private const double HoverPreviewMaxHeight = 216;
        // Matches Firefox's tab card preview delay
        // (browser.tabs.cardPreview.delayMs default).
        private const int HoverPreviewDelayMs = 1000;

        private bool _tabHoverPreviewEnabled;
        private OrionBackend.SettingChangedCallback? _tabHoverPreviewObserver;
        private long _tabHoverPreviewObserverId;
        private DispatcherTimer? _tabHoverPreviewTimer;
        private BrowserTab? _hoverPreviewTab;
        private FrameworkElement? _hoverPreviewAnchor;

        private void InitializeTabHoverPreview()
        {
            _tabHoverPreviewEnabled = OrionBackend.GetBoolSetting(ViewModel.ActiveProfileId,
                "settings::tabs::show_tab_preview_on_hover", "settings::tabs",
                "show_tab_preview_on_hover", true);

            _tabHoverPreviewObserver = (profileId, _, newValue) =>
            {
                if (profileId != ViewModel.ActiveProfileId)
                    return;
                _tabHoverPreviewEnabled = newValue == "1";
                if (!_tabHoverPreviewEnabled)
                    HideTabHoverPreview();
            };
            _tabHoverPreviewObserverId = OrionBackend.AddSettingObserver(
                "settings::tabs::show_tab_preview_on_hover", _tabHoverPreviewObserver);

            // The card describes the hovered tab; switching tabs (click,
            // Ctrl+Tab, close) repositions content under the pointer.
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.ActiveTab))
                    HideTabHoverPreview();
            };
        }

        private void UnregisterTabHoverPreview()
        {
            HideTabHoverPreview();
            OrionBackend.RemoveSettingObserver(_tabHoverPreviewObserverId);
            _tabHoverPreviewObserverId = 0;
            _tabHoverPreviewObserver = null;
        }

        /// <summary>
        /// PointerEntered on a tab item (horizontal/compact OrionTabViewItem
        /// via Tag, vertical TreeViewItem via DataContext).
        /// </summary>
        private void Tab_PointerEntered(object sender, PointerRoutedEventArgs _e)
        {
            if (!_tabHoverPreviewEnabled || sender is not FrameworkElement element)
                return;
            if ((element.Tag as BrowserTab ?? element.DataContext as BrowserTab) is not BrowserTab tab)
                return;

            _hoverPreviewTab = tab;
            _hoverPreviewAnchor = element;

            // Once a card is open, moving along the strip follows the pointer
            // without re-applying the initial delay.
            if (TabHoverPreviewPopup.IsOpen)
            {
                ShowTabHoverPreview();
                return;
            }

            if (_tabHoverPreviewTimer == null)
            {
                _tabHoverPreviewTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(HoverPreviewDelayMs),
                };
                _tabHoverPreviewTimer.Tick += (_, _) => ShowTabHoverPreview();
            }
            _tabHoverPreviewTimer.Stop();
            _tabHoverPreviewTimer.Start();
        }

        /// <summary>Also wired to PointerCaptureLost (drag start).</summary>
        private void Tab_PointerExited(object sender, PointerRoutedEventArgs _e)
        {
            if (ReferenceEquals(sender, _hoverPreviewAnchor))
                HideTabHoverPreview();
        }

        private void ShowTabHoverPreview()
        {
            _tabHoverPreviewTimer?.Stop();
            BrowserTab? tab = _hoverPreviewTab;
            FrameworkElement? anchor = _hoverPreviewAnchor;
            if (tab == null || anchor == null || !anchor.IsLoaded)
                return;

            HoverPreviewTitle.Text = string.IsNullOrEmpty(tab.Title) ? tab.Url : tab.Title;
            HoverPreviewUrl.Text = tab.Url;
            HoverPreviewThumbBorder.Visibility = tab.ThumbnailImage != null
                ? Visibility.Visible : Visibility.Collapsed;
            HoverPreviewThumbBrush.ImageSource = tab.ThumbnailImage;
            HoverPreviewFooterFavicon.Source = tab.FaviconImage;

            // Below horizontal/compact tab items, right of sidebar items;
            // clamped so the card stays inside the window.
            GeneralTransform transform = anchor.TransformToVisual(RootGrid);
            Point origin = anchor is TabViewItem
                ? transform.TransformPoint(new Point(0, anchor.ActualHeight + 4))
                : transform.TransformPoint(new Point(anchor.ActualWidth + 8, 0));
            TabHoverPreviewPopup.HorizontalOffset = Math.Clamp(origin.X,
                8, Math.Max(8, RootGrid.ActualWidth - HoverPreviewWidth - 8));
            TabHoverPreviewPopup.VerticalOffset = Math.Clamp(origin.Y,
                8, Math.Max(8, RootGrid.ActualHeight - HoverPreviewMaxHeight - 8));
            TabHoverPreviewPopup.IsOpen = true;
        }

        private void HideTabHoverPreview()
        {
            _tabHoverPreviewTimer?.Stop();
            _hoverPreviewTab = null;
            _hoverPreviewAnchor = null;
            TabHoverPreviewPopup.IsOpen = false;
        }
    }
}
