using DirectShow;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

namespace Orion
{
    /// <summary>
    /// F11 window fullscreen: presenter switch, top-chrome hiding per the
    /// always_show_toolbar_in_fullscreen setting, sidebar auto-hide per
    /// auto_show_sidebar_in_fullscreen, and hover-at-top-edge chrome reveal.
    /// HTML5 element fullscreen (requestFullscreen) is a separate, deferred feature.
    /// </summary>
    public sealed partial class MainWindowContent
    {
        private bool _isFullscreen;
        private bool _wasMaximizedBeforeFullscreen;
        private bool _sidebarHiddenForFullscreen;
        private bool _inFullscreenSidebarChange;
        private DispatcherQueueTimer? _fullscreenHoverTimer;

        // HTML5 element fullscreen (requestFullscreen, e.g. fullscreen video).
        // The view that holds it, and whether F11 fullscreen was already
        // active when it began (to know what to restore on exit).
        private NativeWebView? _elementFullscreenWebView;
        private bool _wasWindowFullscreenBeforeElement;
        private bool _displaySleepBlocked;

        // Core-module setting observers for live-applying fullscreen settings.
        // The delegate fields keep the callbacks alive while registered.
        private OrionBackend.SettingChangedCallback? _alwaysShowToolbarObserver;
        private OrionBackend.SettingChangedCallback? _autoShowSidebarObserver;
        private long _alwaysShowToolbarObserverId;
        private long _autoShowSidebarObserverId;

        // Transient "Press X to exit full screen" toast shown on entering
        // element fullscreen. Hosted in an unconstrained Popup so it renders
        // above the native web view HWND (same technique as PopupDialog).
        private Popup? _fullscreenToast;
        private Border? _fullscreenToastPill;
        private TextBlock? _fullscreenToastText;
        private DispatcherQueueTimer? _fullscreenToastTimer;

        // Pointer within the top edge of the screen reveals hidden chrome; pointer
        // below the revealed chrome (plus margin) hides it again. In DIPs.
        private const double RevealEdgeDips = 4;
        private const double HideMarginDips = 10;

        private void InitializeFullscreen()
        {
            // A user sidebar toggle during fullscreen takes precedence over our
            // auto-hide: stop tracking so exit doesn't fight the user's choice.
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.ShowSidebar)
                    && _isFullscreen && !_inFullscreenSidebarChange)
                {
                    _sidebarHiddenForFullscreen = false;
                }
            };

            // Live-apply fullscreen settings on any committed write (View menu,
            // Settings page, other windows). Booleans are stored as "1"/"0".
            _alwaysShowToolbarObserver = (profileId, _, newValue) =>
                OnAlwaysShowToolbarSettingChanged(profileId, newValue == "1");
            _alwaysShowToolbarObserverId = OrionBackend.AddSettingObserver(
                "settings::appearance::always_show_toolbar_in_fullscreen", _alwaysShowToolbarObserver);

            _autoShowSidebarObserver = (profileId, _, newValue) =>
                OnAutoShowSidebarSettingChanged(profileId, newValue == "1");
            _autoShowSidebarObserverId = OrionBackend.AddSettingObserver(
                "settings::appearance::auto_show_sidebar_in_fullscreen", _autoShowSidebarObserver);
        }

        private void UnregisterFullscreenSettingObservers()
        {
            OrionBackend.RemoveSettingObserver(_alwaysShowToolbarObserverId);
            _alwaysShowToolbarObserverId = 0;
            _alwaysShowToolbarObserver = null;
            OrionBackend.RemoveSettingObserver(_autoShowSidebarObserverId);
            _autoShowSidebarObserverId = 0;
            _autoShowSidebarObserver = null;
        }

        private void OnAlwaysShowToolbarSettingChanged(long profileId, bool value)
        {
            if (profileId == ViewModel.ActiveProfileId)
                ApplyFullscreenChromePolicy(value);
        }

        private void OnAutoShowSidebarSettingChanged(long profileId, bool value)
        {
            if (profileId != ViewModel.ActiveProfileId || !_isFullscreen)
                return;
            if (value)
            {
                if (ViewModel.ShowSidebar)
                {
                    _sidebarHiddenForFullscreen = true;
                    SetSidebarTransientGuarded(false);
                }
            }
            else
            {
                // Not during element fullscreen — that hides the sidebar
                // regardless of this setting.
                if (_sidebarHiddenForFullscreen && !ViewModel.ShowSidebar && _elementFullscreenWebView == null)
                    SetSidebarTransientGuarded(true);
                _sidebarHiddenForFullscreen = false;
            }
        }

        private void ToggleFullscreen()
        {
            // F11 during element fullscreen exits element fullscreen; the
            // engine confirms via ElementFullscreenChanged(false), which
            // restores the window state.
            if (_elementFullscreenWebView != null)
            {
                _elementFullscreenWebView.RequestExitElementFullscreen();
                return;
            }

            if (_isFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        }

        /// <summary>
        /// Handles the engine's element-fullscreen transitions for a web view
        /// (entered via requestFullscreen, exited via Esc, exitFullscreen(),
        /// our RequestExitElementFullscreen, or view disposal). Element
        /// fullscreen implies window fullscreen with all chrome hidden and no
        /// hover reveal, regardless of the fullscreen appearance settings.
        /// </summary>
        internal void OnElementFullscreenChanged(NativeWebView webView, bool isFullscreen)
        {
            if (isFullscreen)
            {
                if (_elementFullscreenWebView == webView)
                    return;

                // A different view already holds element fullscreen (e.g. a
                // background tab): kick it out, the new request wins.
                if (_elementFullscreenWebView != null)
                    _elementFullscreenWebView.RequestExitElementFullscreen();
                else
                    _wasWindowFullscreenBeforeElement = _isFullscreen;
                _elementFullscreenWebView = webView;

                if (!_isFullscreen)
                    EnterFullscreen();
                else
                    ApplyFullscreenChromePolicy();

                // Hide the sidebar regardless of auto_show_sidebar_in_fullscreen
                // (EnterFullscreen only hides it when that setting is on).
                if (ViewModel.ShowSidebar)
                {
                    _sidebarHiddenForFullscreen = true;
                    SetSidebarTransientGuarded(false);
                }

                ShowFullscreenToast();
                BlockDisplaySleep();
                SyncDevToolsPaneToActiveTab();
            }
            else
            {
                if (_elementFullscreenWebView != webView)
                    return;
                _elementFullscreenWebView = null;
                HideFullscreenToast();
                UnblockDisplaySleep();
                SyncDevToolsPaneToActiveTab();

                if (!_wasWindowFullscreenBeforeElement)
                {
                    ExitFullscreen();
                    return;
                }

                // Stay in F11 fullscreen: recompute chrome/hover per settings,
                // and re-show the sidebar only if the F11 rules would not have
                // hidden it themselves.
                ApplyFullscreenChromePolicy();
                if (_sidebarHiddenForFullscreen && !GetAutoShowSidebarInFullscreen() && !ViewModel.ShowSidebar)
                {
                    SetSidebarTransientGuarded(true);
                    _sidebarHiddenForFullscreen = false;
                }
            }
        }

        /// <summary>
        /// Element fullscreen belongs to one tab; switching away exits it
        /// (matching mainstream browsers). Called from OnActiveTabChanged.
        /// </summary>
        private void ExitElementFullscreenOnTabSwitch(BrowserTab tab)
        {
            if (_elementFullscreenWebView == null)
                return;
            if (ViewModel.GetOrCreateTabViewModel(tab).NativeWebView != _elementFullscreenWebView)
                _elementFullscreenWebView.RequestExitElementFullscreen();
        }

        private void EnterFullscreen()
        {
            AppWindow? appWindow = ParentWindow?.AppWindow;
            if (appWindow == null || _isFullscreen)
                return;

            _wasMaximizedBeforeFullscreen =
                appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };

            try
            {
                appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            }
            catch (Exception ex)
            {
                OrionBackend.LogError($"EnterFullscreen: SetPresenter failed: {ex.Message}");
                return;
            }

            _isFullscreen = true;
            ApplyFullscreenChromePolicy();

            if (GetAutoShowSidebarInFullscreen() && ViewModel.ShowSidebar)
            {
                _sidebarHiddenForFullscreen = true;
                SetSidebarTransientGuarded(false);
            }
        }

        private void ExitFullscreen()
        {
            AppWindow? appWindow = ParentWindow?.AppWindow;
            if (appWindow == null || !_isFullscreen)
                return;

            StopHoverTimer();
            ViewModel.IsChromeHiddenForFullscreen = false;

            if (_sidebarHiddenForFullscreen && !ViewModel.ShowSidebar)
                SetSidebarTransientGuarded(true);
            _sidebarHiddenForFullscreen = false;

            try
            {
                appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
                if (_wasMaximizedBeforeFullscreen)
                    (appWindow.Presenter as OverlappedPresenter)?.Maximize();
            }
            catch (Exception ex)
            {
                OrionBackend.LogError($"ExitFullscreen: SetPresenter failed: {ex.Message}");
            }

            _isFullscreen = false;
        }

        private void ApplyFullscreenChromePolicy() =>
            ApplyFullscreenChromePolicy(GetAlwaysShowToolbarInFullscreen());

        /// <summary>
        /// Applies the always_show_toolbar_in_fullscreen value to the current
        /// fullscreen state: hides/shows the top chrome and runs the hover-reveal
        /// timer only while chrome is hideable. No-op when windowed.
        /// During element fullscreen all chrome is hidden with no hover reveal,
        /// regardless of the setting.
        /// Callers that already know the value pass it directly — the setting
        /// observer callback must not re-read it (GetSetting pumps the run
        /// loop, which is not allowed inside a core callback).
        /// </summary>
        private void ApplyFullscreenChromePolicy(bool alwaysShowToolbar)
        {
            if (!_isFullscreen)
                return;
            bool hideChrome = _elementFullscreenWebView != null || !alwaysShowToolbar;
            ViewModel.IsChromeHiddenForFullscreen = hideChrome;
            if (hideChrome && _elementFullscreenWebView == null)
                StartHoverTimer();
            else
                StopHoverTimer();
        }

        /// <summary>
        /// Sets ShowSidebar without persisting, flagged so the PropertyChanged
        /// watcher can tell our fullscreen changes from user toggles.
        /// </summary>
        private void SetSidebarTransientGuarded(bool value)
        {
            _inFullscreenSidebarChange = true;
            try { ViewModel.SetShowSidebarTransient(value); }
            finally { _inFullscreenSidebarChange = false; }
        }

        private bool GetAlwaysShowToolbarInFullscreen() =>
            OrionBackend.GetBoolSetting(ViewModel.ActiveProfileId,
                "settings::appearance::always_show_toolbar_in_fullscreen", "settings::appearance",
                "always_show_toolbar_in_fullscreen", true);

        private bool GetAutoShowSidebarInFullscreen() =>
            OrionBackend.GetBoolSetting(ViewModel.ActiveProfileId,
                "settings::appearance::auto_show_sidebar_in_fullscreen", "settings::appearance",
                "auto_show_sidebar_in_fullscreen", true);

        private bool GetPreventEscapeFromExitingFullscreen() =>
            OrionBackend.GetBoolSetting(ViewModel.ActiveProfileId,
                "settings::browsing::prevent_escape_from_exiting_fullscreen", "settings::browsing",
                "prevent_escape_from_exiting_fullscreen", false);

        private void ShowFullscreenToast()
        {
            if (XamlRoot == null)
                return;

            if (_fullscreenToast == null)
            {
                _fullscreenToastText = new TextBlock
                {
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                    FontSize = 14,
                };
                _fullscreenToastPill = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xD0, 0x20, 0x20, 0x20)),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(18, 9, 18, 9),
                    Child = _fullscreenToastText,
                };
                // Offsets are set once the pill has a measured size.
                _fullscreenToastPill.SizeChanged += (s, e) =>
                {
                    if (_fullscreenToast != null && XamlRoot != null)
                    {
                        _fullscreenToast.HorizontalOffset = (XamlRoot.Size.Width - e.NewSize.Width) / 2;
                        _fullscreenToast.VerticalOffset = 48;
                    }
                };
                _fullscreenToast = new Popup
                {
                    ShouldConstrainToRootBounds = false,
                    IsHitTestVisible = false,
                    Child = _fullscreenToastPill,
                };
            }

            _fullscreenToastText!.Text = GetPreventEscapeFromExitingFullscreen()
                ? "Press F11 to exit full screen"
                : "Press Esc to exit full screen";

            _fullscreenToast.XamlRoot = XamlRoot;
            _fullscreenToast.IsOpen = true;

            if (_fullscreenToastTimer == null)
            {
                _fullscreenToastTimer = DispatcherQueue.CreateTimer();
                _fullscreenToastTimer.Interval = TimeSpan.FromSeconds(4);
                _fullscreenToastTimer.IsRepeating = false;
                _fullscreenToastTimer.Tick += (s, e) => HideFullscreenToast();
            }
            _fullscreenToastTimer.Stop();
            _fullscreenToastTimer.Start();
        }

        private void HideFullscreenToast()
        {
            _fullscreenToastTimer?.Stop();
            _fullscreenToast?.IsOpen = false;
        }

        // Idempotent per window: the shared blocker is reference-counted, so a
        // window must not acquire twice (e.g. fullscreen handed between views).
        private void BlockDisplaySleep()
        {
            if (_displaySleepBlocked)
                return;
            DisplaySleepBlocker.Acquire();
            _displaySleepBlocked = true;
        }

        private void UnblockDisplaySleep()
        {
            if (!_displaySleepBlocked)
                return;
            DisplaySleepBlocker.Release();
            _displaySleepBlocked = false;
        }

        private void StartHoverTimer()
        {
            if (_fullscreenHoverTimer == null)
            {
                _fullscreenHoverTimer = DispatcherQueue.CreateTimer();
                _fullscreenHoverTimer.Interval = TimeSpan.FromMilliseconds(120);
                _fullscreenHoverTimer.IsRepeating = true;
                _fullscreenHoverTimer.Tick += OnFullscreenHoverTick;
            }
            if (!_fullscreenHoverTimer.IsRunning)
                _fullscreenHoverTimer.Start();
        }

        private void StopHoverTimer() => _fullscreenHoverTimer?.Stop();

        private void OnFullscreenHoverTick(DispatcherQueueTimer sender, object args)
        {
            if (!_isFullscreen || ParentWindow == null || XamlRoot == null)
            {
                sender.Stop();
                return;
            }
            if (!Win32.GetCursorPos(out Win32.POINT pt))
                return;
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(ParentWindow);
            if (!Win32.GetWindowRect(hwnd, out RECT rect))
                return;

            bool inside = pt.x >= rect.left && pt.x < rect.right
                       && pt.y >= rect.top && pt.y < rect.bottom;
            // Fullscreen has no non-client area, so the window rect is the client rect.
            double yDips = (pt.y - rect.top) / XamlRoot.RasterizationScale;

            if (ViewModel.IsChromeHiddenForFullscreen)
            {
                if (inside && yDips <= RevealEdgeDips)
                    ViewModel.IsChromeHiddenForFullscreen = false;
            }
            else
            {
                // Don't collapse chrome under an open flyout/menu/popup.
                if (VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot).Count > 0)
                    return;

                // The content area's Y offset is the revealed chrome height,
                // correct in every tab layout / bookmark bar combination.
                double chromeBottom = ContentAreaGrid
                    .TransformToVisual(null).TransformPoint(new Point(0, 0)).Y;
                if (chromeBottom < RevealEdgeDips + 1)
                    return; // layout hasn't settled after the reveal yet

                if (!inside || yDips > chromeBottom + HideMarginDips)
                    ViewModel.IsChromeHiddenForFullscreen = true;
            }
        }
    }
}
