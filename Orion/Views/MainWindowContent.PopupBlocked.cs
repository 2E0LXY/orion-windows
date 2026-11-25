using Microsoft.UI.Xaml;
using System;

namespace Orion
{
    public sealed partial class MainWindowContent
    {
        /// <summary>
        /// Transient "Pop-up blocked" banner for the Block-and-Notify popup
        /// mode (settings::websites::popup_windows_default...): shown in the
        /// window whose page's gesture-less window.open was blocked, with an
        /// Open button that loads the blocked target in a new tab.
        /// </summary>
        private const int PopupBlockedBannerSeconds = 6;

        private DispatcherTimer? _popupBlockedTimer;
        private string _blockedPopupUrl = "";

        internal void ShowPopupBlockedBanner(string targetUrl)
        {
            _blockedPopupUrl = targetUrl;

            string host;
            try
            {
                host = new Uri(targetUrl).Host;
            }
            catch (UriFormatException)
            {
                host = "";
            }
            PopupBlockedInfoBar.Message = host.Length > 0
                ? $"This page tried to open a window on {host}."
                : "This page tried to open a window.";
            // Without a URL (about:blank targets) there is nothing to open.
            PopupBlockedOpenButton.Visibility = targetUrl.Length > 0
                ? Visibility.Visible : Visibility.Collapsed;

            // Top-right of the content area, offset from its origin (the
            // popup's parent). Transient, so window resizes are ignored.
            PopupBlockedInfoBar.IsOpen = true;
            PopupBlockedBanner.HorizontalOffset =
                Math.Max(8, ContentAreaGrid.ActualWidth - 436);
            PopupBlockedBanner.VerticalOffset = 8;
            PopupBlockedBanner.IsOpen = true;

            if (_popupBlockedTimer == null)
            {
                _popupBlockedTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(PopupBlockedBannerSeconds),
                };
                _popupBlockedTimer.Tick += (_, _) => HidePopupBlockedBanner();
            }
            _popupBlockedTimer.Stop();
            _popupBlockedTimer.Start();
        }

        private void HidePopupBlockedBanner()
        {
            _popupBlockedTimer?.Stop();
            PopupBlockedBanner.IsOpen = false;
        }

        private void PopupBlockedOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_blockedPopupUrl.Length > 0)
                CreateNewTab(_blockedPopupUrl);
            HidePopupBlockedBanner();
        }

        /// <summary>The InfoBar's own ✕ closes the InfoBar; close the hosting
        /// Popup with it (and re-open the InfoBar for the next show).</summary>
        private void PopupBlockedInfoBar_Closed(Microsoft.UI.Xaml.Controls.InfoBar sender, object args)
        {
            HidePopupBlockedBanner();
        }
    }
}
