using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System.Text.RegularExpressions;

namespace Orion
{
    public partial class Profile : ObservableObject
    {
        private static readonly Regex PangoColorRegex = new(@"^<span foreground=""#([0-9A-Fa-f]{6})"">⬤</span>$");

        [ObservableProperty]
        public partial long Id { get; set; }

        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayIcon))]
        [NotifyPropertyChangedFor(nameof(IconFontSize))]
        [NotifyPropertyChangedFor(nameof(IconForeground))]
        public partial string Icon { get; set; } = "<span foreground=\"#2646DE\">\u2B24</span>";

        [ObservableProperty]
        public partial bool ShowInAddressBar { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowBadgeInAppIcon { get; set; }

        [ObservableProperty]
        public partial string KeyboardShortcut { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsInternal { get; set; }

        [ObservableProperty]
        public partial bool IsDefault { get; set; }

        /// <summary>
        /// Browser engine this profile runs with ("webkit" | "blink").
        /// Stored in the cross-profile registry; the engine is fixed per
        /// process, so changes apply the next time the profile's process
        /// starts.
        /// </summary>
        [ObservableProperty]
        public partial string Engine { get; set; } = "webkit";

        /// <summary>
        /// The text to display: ⬤ for colors, the emoji itself for emoji.
        /// </summary>
        public string DisplayIcon => TryParseColor(Icon, out _) ? "\u2B24" : Icon;

        public double IconFontSize => TryParseColor(Icon, out _) ? 28 : 20;

        /// <summary>
        /// Foreground brush: parsed color for Pango markup, black for emoji.
        /// </summary>
        public Brush IconForeground => new SolidColorBrush(
            TryParseColor(Icon, out Windows.UI.Color c)
                ? c
                : Microsoft.UI.Colors.Black);

        public static string ColorToIcon(string hex) => $"<span foreground=\"{hex}\">⬤</span>";

        private static bool TryParseColor(string? icon, out Windows.UI.Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(icon)) return false;
            Match m = PangoColorRegex.Match(icon);
            if (!m.Success) return false;
            string hex = m.Groups[1].Value;
            color = Windows.UI.Color.FromArgb(255,
                byte.Parse(hex[0..2], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
            return true;
        }
    }
}
