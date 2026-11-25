namespace Orion
{
    /// <summary>
    /// Pushes engine-level preference settings into the loaded browser engine
    /// and keeps them live via setting observers. Initialized once per process
    /// after LoadBrowserEngine, before the first browser view exists.
    ///
    /// Values are cached because observers must not call GetSetting (it can
    /// pump the run loop, which is not allowed from inside a core callback).
    /// </summary>
    internal static class EnginePreferences
    {
        private static long _profileId;
        private static bool _minimumFontSizeEnabled;
        private static int _minimumFontSize;
        private static bool _javaScriptDisabled;

        /// <summary>
        /// settings::websites::popup_windows_default_setting_for_all_websites
        /// (Allow=1, Block=2, BlockAndNotify=3; null = never set — no gating).
        /// Cached here for App.OnCreatePopup, which runs inside an engine
        /// callback where GetSetting must not be called (it can pump the
        /// run loop).
        /// </summary>
        internal static int? PopupWindowsMode { get; private set; }

        // Rooted for process lifetime — native holds these delegates.
        private static OrionBackend.SettingChangedCallback? _enableObserver;
        private static OrionBackend.SettingChangedCallback? _sizeObserver;
        private static OrionBackend.SettingChangedCallback? _javaScriptObserver;
        private static OrionBackend.SettingChangedCallback? _popupObserver;

        internal static void Initialize(long profileId)
        {
            _profileId = profileId;

            // Process-default UA, latched by CEF at engine initialization
            // (User-Agent header + navigator.userAgent for every view).
            // Runtime setting changes still apply per view through
            // ApplyUserAgentToAllViews.
            string? userAgent = MainWindowViewModel.GetUserAgentString(profileId);
            if (userAgent != null)
                OrionBackend.SetDefaultUserAgent(userAgent);

            // An unset setting applies nothing to the engine (whose own
            // default is no minimum) rather than inventing a value.
            _minimumFontSizeEnabled = OrionBackend.GetBoolSettingOrNull(profileId,
                "settings::browsing::enable_minimum_font_size", "settings::browsing",
                "enable_minimum_font_size") ?? false;
            _minimumFontSize = OrionBackend.GetIntSettingOrNull(profileId,
                "settings::browsing::minimum_font_size", "settings::browsing",
                "minimum_font_size") ?? 0;
            ApplyMinimumFontSize();

            _enableObserver = (pid, _, newValue) =>
            {
                if (pid != _profileId) return;
                _minimumFontSizeEnabled = newValue == "1";
                ApplyMinimumFontSize();
            };
            _ = OrionBackend.AddSettingObserver(
                "settings::browsing::enable_minimum_font_size", _enableObserver);

            _sizeObserver = (pid, _, newValue) =>
            {
                if (pid != _profileId) return;
                if (int.TryParse(newValue, out int size))
                {
                    _minimumFontSize = size;
                    ApplyMinimumFontSize();
                }
            };
            _ = OrionBackend.AddSettingObserver(
                "settings::browsing::minimum_font_size", _sizeObserver);

            // JavaScript for all websites: On=1, Off=2; unset = engine
            // default (on).
            _javaScriptDisabled = OrionBackend.GetIntSettingOrNull(profileId,
                "settings::websites::javascript_setting_for_all_websites", "settings::websites",
                "javascript_setting_for_all_websites") == 2;
            ApplyJavaScriptEnabled();

            _javaScriptObserver = (pid, _, newValue) =>
            {
                if (pid != _profileId) return;
                _javaScriptDisabled = newValue == "2";
                ApplyJavaScriptEnabled();
            };
            _ = OrionBackend.AddSettingObserver(
                "settings::websites::javascript_setting_for_all_websites", _javaScriptObserver);

            PopupWindowsMode = OrionBackend.GetIntSettingOrNull(profileId,
                "settings::websites::popup_windows_default_setting_for_all_websites",
                "settings::websites", "popup_windows_default_setting_for_all_websites");
            ApplyPopupPolicyOwnership();

            _popupObserver = (pid, _, newValue) =>
            {
                if (pid != _profileId) return;
                if (int.TryParse(newValue, out int mode))
                {
                    PopupWindowsMode = mode;
                    ApplyPopupPolicyOwnership();
                }
            };
            _ = OrionBackend.AddSettingObserver(
                "settings::websites::popup_windows_default_setting_for_all_websites", _popupObserver);
        }

        private static void ApplyMinimumFontSize() =>
            OrionBackend.SetMinimumFontSize(_minimumFontSizeEnabled ? _minimumFontSize : 0);

        private static void ApplyJavaScriptEnabled() =>
            OrionBackend.SetJavaScriptEnabled(_javaScriptDisabled ? 0 : 1);

        /// <summary>
        /// Once the popup setting has any value, the App.OnCreatePopup gate
        /// owns popup policy — stand down the engine's own blocker (Chromium
        /// blocks gesture-less popups before they reach the gate, which made
        /// "Allow" have no effect on Blink). Unset leaves the engine default.
        /// </summary>
        private static void ApplyPopupPolicyOwnership()
        {
            if (PopupWindowsMode != null)
                OrionBackend.SetNativePopupBlocking(0);
        }
    }
}
