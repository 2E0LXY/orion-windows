using System;
using System.Collections.Generic;
using System.Text;
using Windows.System;

namespace Orion
{
    /// <summary>
    /// A keyboard chord: one VirtualKey plus Ctrl/Alt/Shift. Persisted in the
    /// core entity columns (profiles.keyboard_shortcut, windows.keyboard_shortcut)
    /// as GTK accelerator names — the format orion-linux writes into the same
    /// columns — e.g. "&lt;Shift&gt;&lt;Control&gt;t"; "" means unbound.
    /// The Windows key cannot be a modifier (the engines never transmit it).
    /// </summary>
    public readonly record struct KeyCombo(VirtualKey Key, VirtualKeyModifiers Modifiers)
    {
        /// <summary>
        /// Recorder rule (macOS parity): requires Ctrl or Alt, or an F1–F24 key
        /// (bare and Shift+F allowed). Shift alone is insufficient.
        /// </summary>
        public bool IsValidBinding =>
            Modifiers.HasFlag(VirtualKeyModifiers.Control)
            || Modifiers.HasFlag(VirtualKeyModifiers.Menu)
            || Key is >= VirtualKey.F1 and <= VirtualKey.F24;

        /// <summary>Whether the key has a GDK keyval name and can be persisted.</summary>
        public bool IsMappable => TryGetGdkKeyName(Key, out _);

        /// <summary>
        /// Parses a GTK accelerator name. Tolerant of modifier order and letter
        /// case; fails on modifiers Windows cannot represent (Super/Meta/Hyper)
        /// and on unknown keyval names, so callers treat the value as unbound.
        /// </summary>
        public static bool TryParseGtkAccel(string? s, out KeyCombo combo)
        {
            combo = default;
            if (string.IsNullOrEmpty(s))
                return false;

            VirtualKeyModifiers modifiers = VirtualKeyModifiers.None;
            int i = 0;
            while (i < s.Length && s[i] == '<')
            {
                int end = s.IndexOf('>', i + 1);
                if (end < 0)
                    return false;
                string name = s[(i + 1)..end];
                if (Is(name, "Control") || Is(name, "Ctrl") || Is(name, "Ctl") || Is(name, "Primary"))
                    modifiers |= VirtualKeyModifiers.Control;
                else if (Is(name, "Shift"))
                    modifiers |= VirtualKeyModifiers.Shift;
                else if (Is(name, "Alt"))
                    modifiers |= VirtualKeyModifiers.Menu;
                else
                    return false;
                i = end + 1;
            }

            if (!TryParseGdkKeyName(s[i..], out VirtualKey key))
                return false;

            combo = new KeyCombo(key, modifiers);
            return true;

            static bool Is(string name, string candidate) =>
                name.Equals(candidate, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Canonical storage form. Modifier order matches gtk_accelerator_name
        /// (GdkModifierType bit order: Shift, Control, Alt) so identical combos
        /// serialize byte-identically across ports. Throws on unmappable keys —
        /// the recorder never lets one through.
        /// </summary>
        public string ToGtkAccelString()
        {
            if (!TryGetGdkKeyName(Key, out string? keyName))
                throw new InvalidOperationException($"VirtualKey {Key} has no GDK keyval name");
            var sb = new StringBuilder();
            if (Modifiers.HasFlag(VirtualKeyModifiers.Shift))
                _ = sb.Append("<Shift>");
            if (Modifiers.HasFlag(VirtualKeyModifiers.Control))
                _ = sb.Append("<Control>");
            if (Modifiers.HasFlag(VirtualKeyModifiers.Menu))
                _ = sb.Append("<Alt>");
            return sb.Append(keyName).ToString();
        }

        public string ToDisplayString()
        {
            var sb = new StringBuilder();
            if (Modifiers.HasFlag(VirtualKeyModifiers.Control))
                _ = sb.Append("Ctrl+");
            if (Modifiers.HasFlag(VirtualKeyModifiers.Menu))
                _ = sb.Append("Alt+");
            if (Modifiers.HasFlag(VirtualKeyModifiers.Shift))
                _ = sb.Append("Shift+");
            return sb.Append(DisplayKeyName(Key)).ToString();
        }

        // OEM virtual keys (WinUser.h names); VirtualKey defines no members for them.
        private const VirtualKey VK_OEM_1 = (VirtualKey)0xBA;      // ;
        private const VirtualKey VK_OEM_PLUS = (VirtualKey)0xBB;   // =
        private const VirtualKey VK_OEM_COMMA = (VirtualKey)0xBC;  // ,
        private const VirtualKey VK_OEM_MINUS = (VirtualKey)0xBD;  // -
        private const VirtualKey VK_OEM_PERIOD = (VirtualKey)0xBE; // .
        private const VirtualKey VK_OEM_2 = (VirtualKey)0xBF;      // /
        private const VirtualKey VK_OEM_3 = (VirtualKey)0xC0;      // `
        private const VirtualKey VK_OEM_4 = (VirtualKey)0xDB;      // [
        private const VirtualKey VK_OEM_5 = (VirtualKey)0xDC;      // \
        private const VirtualKey VK_OEM_6 = (VirtualKey)0xDD;      // ]
        private const VirtualKey VK_OEM_7 = (VirtualKey)0xDE;      // '

        // GDK keyval names (gdkkeysyms.h) for keys without algorithmic names.
        // Letters, digits and F-keys are handled in code.
        private static readonly Dictionary<VirtualKey, string> KeyToGdkName = new()
        {
            [VK_OEM_1] = "semicolon",
            [VK_OEM_PLUS] = "equal",
            [VK_OEM_COMMA] = "comma",
            [VK_OEM_MINUS] = "minus",
            [VK_OEM_PERIOD] = "period",
            [VK_OEM_2] = "slash",
            [VK_OEM_3] = "grave",
            [VK_OEM_4] = "bracketleft",
            [VK_OEM_5] = "backslash",
            [VK_OEM_6] = "bracketright",
            [VK_OEM_7] = "apostrophe",
            [VirtualKey.Left] = "Left",
            [VirtualKey.Up] = "Up",
            [VirtualKey.Right] = "Right",
            [VirtualKey.Down] = "Down",
            [VirtualKey.Home] = "Home",
            [VirtualKey.End] = "End",
            [VirtualKey.PageUp] = "Page_Up",
            [VirtualKey.PageDown] = "Page_Down",
            [VirtualKey.Tab] = "Tab",
            [VirtualKey.Space] = "space",
            [VirtualKey.Enter] = "Return",
            [VirtualKey.Escape] = "Escape",
            [VirtualKey.Back] = "BackSpace",
            [VirtualKey.Delete] = "Delete",
            [VirtualKey.Insert] = "Insert",
        };

        private static readonly Dictionary<string, VirtualKey> GdkNameToKey = BuildGdkNameToKey();

        private static Dictionary<string, VirtualKey> BuildGdkNameToKey()
        {
            var map = new Dictionary<string, VirtualKey>(StringComparer.Ordinal);
            foreach ((VirtualKey key, string name) in KeyToGdkName)
                map[name] = key;
            return map;
        }

        private static bool TryGetGdkKeyName(VirtualKey key, out string? name)
        {
            if (key is >= VirtualKey.A and <= VirtualKey.Z)
            {
                name = ((char)('a' + (key - VirtualKey.A))).ToString();
                return true;
            }
            if (key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
            {
                name = ((char)('0' + (key - VirtualKey.Number0))).ToString();
                return true;
            }
            if (key is >= VirtualKey.F1 and <= VirtualKey.F24)
            {
                name = $"F{key - VirtualKey.F1 + 1}";
                return true;
            }
            bool found = KeyToGdkName.TryGetValue(key, out string? mapped);
            name = mapped;
            return found;
        }

        private static bool TryParseGdkKeyName(string name, out VirtualKey key)
        {
            key = default;
            if (name.Length == 1)
            {
                char c = name[0];
                if (c is >= 'a' and <= 'z')
                {
                    key = VirtualKey.A + (c - 'a');
                    return true;
                }
                if (c is >= 'A' and <= 'Z')
                {
                    key = VirtualKey.A + (c - 'A');
                    return true;
                }
                if (c is >= '0' and <= '9')
                {
                    key = VirtualKey.Number0 + (c - '0');
                    return true;
                }
                return false;
            }
            if (name.Length is 2 or 3 && name[0] == 'F'
                && int.TryParse(name.AsSpan(1), out int f) && f is >= 1 and <= 24)
            {
                key = VirtualKey.F1 + (f - 1);
                return true;
            }
            return GdkNameToKey.TryGetValue(name, out key);
        }

        private static readonly Dictionary<VirtualKey, string> OemDisplayNames = new()
        {
            [VK_OEM_1] = ";",
            [VK_OEM_PLUS] = "=",
            [VK_OEM_COMMA] = ",",
            [VK_OEM_MINUS] = "-",
            [VK_OEM_PERIOD] = ".",
            [VK_OEM_2] = "/",
            [VK_OEM_3] = "`",
            [VK_OEM_4] = "[",
            [VK_OEM_5] = "\\",
            [VK_OEM_6] = "]",
            [VK_OEM_7] = "'",
        };

        private static string DisplayKeyName(VirtualKey key)
        {
            if (key is >= VirtualKey.A and <= VirtualKey.Z)
                return ((char)('A' + (key - VirtualKey.A))).ToString();
            if (key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
                return ((char)('0' + (key - VirtualKey.Number0))).ToString();
            return OemDisplayNames.TryGetValue(key, out string? name) ? name : key.ToString();
        }
    }
}
