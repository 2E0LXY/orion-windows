using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;

namespace Orion
{
    /// <summary>
    /// Owner of the app's effective UI theme: applies the profile's theme
    /// setting (system/light/dark) to every window root and pushes the
    /// resulting effective theme to the browser engine so web content
    /// evaluates prefers-color-scheme correctly. Both engines follow this
    /// push (not the OS directly), keeping their behavior identical.
    /// UI-thread only.
    /// </summary>
    internal static class ThemeService
    {
        private const string ThemeSettingKey = "settings::appearance::theme";

        // Window roots the setting applies to, held weakly so closed windows
        // just fall out of the list.
        private static readonly List<WeakReference<FrameworkElement>> _roots = [];
        private static ThemeSetting _setting = ThemeSetting.System;
        private static long _profileId;
        private static bool? _pushed;

        // Keeps the callback alive; the observer is registered for the
        // process lifetime, so it is never removed.
        private static OrionBackend.SettingChangedCallback? _themeObserver;

        /// <summary>
        /// Reads the theme setting, observes it for live changes, and pushes
        /// the initial effective theme. Call after LoadBrowserEngine and
        /// before the first window.
        /// </summary>
        public static void Initialize(long profileId)
        {
            _profileId = profileId;
            _setting = (ThemeSetting)OrionBackend.GetIntSetting(profileId,
                ThemeSettingKey, "settings::appearance", "theme", (int)ThemeSetting.System);
            _themeObserver = (pid, _, newValue) =>
            {
                if (pid == _profileId)
                    ApplySetting((ThemeSetting)int.Parse(newValue));
            };
            _ = OrionBackend.AddSettingObserver(ThemeSettingKey, _themeObserver);
            Push(_setting switch
            {
                ThemeSetting.Light => false,
                ThemeSetting.Dark => true,
                // Application.RequestedTheme is the launch-time system theme
                // snapshot; live flips arrive via ActualThemeChanged instead.
                ThemeSetting.System => Application.Current.RequestedTheme == ApplicationTheme.Dark,
                _ => throw new ArgumentOutOfRangeException(nameof(_setting)),
            });
        }

        /// <summary>
        /// Tracks a window root: applies the theme setting, pushes the
        /// resulting effective theme, and follows live theme changes (system
        /// flips and setting writes alike surface as ActualThemeChanged).
        /// No unsubscription needed — the element owns the handler and roots
        /// are held weakly.
        /// </summary>
        public static void Register(FrameworkElement root)
        {
            _roots.Add(new WeakReference<FrameworkElement>(root));
            root.RequestedTheme = RequestedElementTheme();
            Push(root.ActualTheme == ElementTheme.Dark);
            root.ActualThemeChanged += (sender, _) =>
                Push(sender.ActualTheme == ElementTheme.Dark);
        }

        private static void ApplySetting(ThemeSetting setting)
        {
            if (setting == _setting)
                return;
            _setting = setting;
            ElementTheme theme = RequestedElementTheme();
            _ = _roots.RemoveAll(weak => !weak.TryGetTarget(out _));
            foreach (WeakReference<FrameworkElement> weak in _roots)
            {
                if (weak.TryGetTarget(out FrameworkElement? root))
                    root.RequestedTheme = theme;
            }
        }

        private static ElementTheme RequestedElementTheme() => _setting switch
        {
            ThemeSetting.System => ElementTheme.Default,
            ThemeSetting.Light => ElementTheme.Light,
            ThemeSetting.Dark => ElementTheme.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(_setting)),
        };

        private static void Push(bool dark)
        {
            if (_pushed == dark)
                return;
            _pushed = dark;
            OrionBackend.SetUseDarkAppearance(dark);
        }
    }

    /// <summary>Stored values of settings::appearance::theme.</summary>
    internal enum ThemeSetting
    {
        System = 1,
        Light = 2,
        Dark = 3,
    }
}
