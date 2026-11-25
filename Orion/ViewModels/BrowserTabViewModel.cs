using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Orion
{
    /// <summary>
    /// Per-window ViewModel wrapper for BrowserTab.
    /// Holds the NativeWebView (WebKit HWND) and navigation commands,
    /// which are inherently per-window since HWNDs have a single parent.
    /// </summary>
    public partial class BrowserTabViewModel(BrowserTab model) : ObservableObject
    {
        public BrowserTab Model { get; } = model;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNativeWebView))]
        [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
        [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
        [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
        [NotifyCanExecuteChangedFor(nameof(ReloadBypassingCacheCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleDevToolsCommand))]
        public partial NativeWebView? NativeWebView { get; set; }

        public bool HasNativeWebView => NativeWebView != null;

        /// <summary>
        /// Back/forward availability, pushed by the engine's
        /// NavigationStateChanged event (wired in WireWebViewEvents).
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
        public partial bool CanGoBack { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
        public partial bool CanGoForward { get; set; }

        /// <summary>
        /// Whether this tab had the find bar open; restored on tab switch.
        /// Read/written only by the view (no binding).
        /// </summary>
        public bool FindBarVisible { get; set; }

        private bool CanExecuteGoBack() => HasNativeWebView && CanGoBack;

        private bool CanExecuteGoForward() => HasNativeWebView && CanGoForward;

        [RelayCommand(CanExecute = nameof(CanExecuteGoBack))]
        private void GoBack() => NativeWebView?.GoBack();

        [RelayCommand(CanExecute = nameof(CanExecuteGoForward))]
        private void GoForward() => NativeWebView?.GoForward();

        // Clearing IsCrashed dismisses the crash banner and re-enables the
        // title/URL callbacks it gates.
        [RelayCommand(CanExecute = nameof(HasNativeWebView))]
        private void Reload()
        {
            Model.IsCrashed = false;
            NativeWebView?.Reload();
        }

        [RelayCommand(CanExecute = nameof(HasNativeWebView))]
        private void ReloadBypassingCache()
        {
            Model.IsCrashed = false;
            NativeWebView?.ReloadBypassingCache();
        }

        [RelayCommand(CanExecute = nameof(HasNativeWebView))]
        private void ToggleDevTools() => NativeWebView?.ToggleDevTools();
    }
}
