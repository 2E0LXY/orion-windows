using System;
using Windows.System;
using Xunit;

namespace Orion
{
    public class KeyComboTests
    {
        private const VirtualKey OemComma = (VirtualKey)0xBC;
        private const VirtualKey OemBracketLeft = (VirtualKey)0xDB;

        [Theory]
        [InlineData("<Control>t", VirtualKey.T, VirtualKeyModifiers.Control)]
        [InlineData("<Shift><Control>t", VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
        [InlineData("<Control><Shift>g", VirtualKey.G, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
        [InlineData("<Alt>F5", VirtualKey.F5, VirtualKeyModifiers.Menu)]
        [InlineData("<Shift>F3", VirtualKey.F3, VirtualKeyModifiers.Shift)]
        [InlineData("F11", VirtualKey.F11, VirtualKeyModifiers.None)]
        [InlineData("<Control>comma", (VirtualKey)0xBC, VirtualKeyModifiers.Control)]
        [InlineData("<Control>bracketleft", (VirtualKey)0xDB, VirtualKeyModifiers.Control)]
        [InlineData("<Control><Alt>1", VirtualKey.Number1, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu)]
        [InlineData("<Alt>Left", VirtualKey.Left, VirtualKeyModifiers.Menu)]
        [InlineData("<Control>Page_Down", VirtualKey.PageDown, VirtualKeyModifiers.Control)]
        [InlineData("<Control>space", VirtualKey.Space, VirtualKeyModifiers.Control)]
        public void TryParseGtkAccel_ParsesValidStrings(string input, VirtualKey key, VirtualKeyModifiers modifiers)
        {
            Assert.True(KeyCombo.TryParseGtkAccel(input, out KeyCombo combo));
            Assert.Equal(new KeyCombo(key, modifiers), combo);
        }

        [Theory]
        [InlineData("<Shift><Control>T", VirtualKey.T)] // gtk stores shifted keyvals uppercase
        [InlineData("<shift><control>t", VirtualKey.T)] // modifier names case-insensitive
        [InlineData("<Ctrl>t", VirtualKey.T)]
        [InlineData("<Primary>t", VirtualKey.T)]
        public void TryParseGtkAccel_IsTolerant(string input, VirtualKey key)
        {
            Assert.True(KeyCombo.TryParseGtkAccel(input, out KeyCombo combo));
            Assert.Equal(key, combo.Key);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("<Super>t")]
        [InlineData("<Meta>t")]
        [InlineData("<Control>")]
        [InlineData("<Control")]
        [InlineData("<Control>bogus")]
        [InlineData("<Control>exclam")] // shifted keyval names are not mapped
        [InlineData("<Control>F25")]
        [InlineData("Ctrl+Shift+1")] // legacy free-text from the old profile textbox
        public void TryParseGtkAccel_RejectsInvalidStrings(string? input)
        {
            Assert.False(KeyCombo.TryParseGtkAccel(input, out _));
        }

        [Theory]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Control, "<Control>t")]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, "<Shift><Control>t")]
        [InlineData(VirtualKey.F5, VirtualKeyModifiers.Menu, "<Alt>F5")]
        [InlineData(VirtualKey.F3, VirtualKeyModifiers.Shift, "<Shift>F3")]
        [InlineData((VirtualKey)0xBC, VirtualKeyModifiers.Control, "<Control>comma")]
        [InlineData(VirtualKey.Number1, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu, "<Control><Alt>1")]
        [InlineData(VirtualKey.Left, VirtualKeyModifiers.Menu, "<Alt>Left")]
        public void ToGtkAccelString_EmitsCanonicalForm(VirtualKey key, VirtualKeyModifiers modifiers, string expected)
        {
            Assert.Equal(expected, new KeyCombo(key, modifiers).ToGtkAccelString());
        }

        [Fact]
        public void ToGtkAccelString_RoundTripsThroughParse()
        {
            VirtualKey[] keys =
            [
                VirtualKey.A, VirtualKey.Z, VirtualKey.Number0, VirtualKey.Number9,
                VirtualKey.F1, VirtualKey.F24, OemComma, OemBracketLeft,
                VirtualKey.Left, VirtualKey.Down, VirtualKey.Home, VirtualKey.End,
                VirtualKey.PageUp, VirtualKey.PageDown, VirtualKey.Tab, VirtualKey.Space,
                VirtualKey.Enter, VirtualKey.Escape, VirtualKey.Back, VirtualKey.Delete,
                VirtualKey.Insert,
            ];
            foreach (VirtualKey key in keys)
            {
                var combo = new KeyCombo(key, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                Assert.True(KeyCombo.TryParseGtkAccel(combo.ToGtkAccelString(), out KeyCombo parsed));
                Assert.Equal(combo, parsed);
            }
        }

        [Fact]
        public void ToGtkAccelString_ThrowsOnUnmappableKey()
        {
            var combo = new KeyCombo(VirtualKey.CapitalLock, VirtualKeyModifiers.Control);
            Assert.False(combo.IsMappable);
            _ = Assert.Throws<InvalidOperationException>(combo.ToGtkAccelString);
        }

        [Theory]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Control, true)]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Menu, true)]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, true)]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Shift, false)]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.None, false)]
        [InlineData(VirtualKey.F5, VirtualKeyModifiers.None, true)]
        [InlineData(VirtualKey.F5, VirtualKeyModifiers.Shift, true)]
        [InlineData(VirtualKey.Left, VirtualKeyModifiers.Menu, true)]
        [InlineData(VirtualKey.Delete, VirtualKeyModifiers.Shift, false)]
        public void IsValidBinding_RequiresCtrlOrAltOrFunctionKey(VirtualKey key, VirtualKeyModifiers modifiers, bool valid)
        {
            Assert.Equal(valid, new KeyCombo(key, modifiers).IsValidBinding);
        }

        [Theory]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, "Ctrl+Shift+T")]
        [InlineData(VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift, "Ctrl+Alt+Shift+T")]
        [InlineData((VirtualKey)0xBC, VirtualKeyModifiers.Control, "Ctrl+,")]
        [InlineData(VirtualKey.Number1, VirtualKeyModifiers.Control, "Ctrl+1")]
        [InlineData(VirtualKey.F3, VirtualKeyModifiers.Shift, "Shift+F3")]
        [InlineData(VirtualKey.Left, VirtualKeyModifiers.Menu, "Alt+Left")]
        public void ToDisplayString_UsesWindowsConventions(VirtualKey key, VirtualKeyModifiers modifiers, string expected)
        {
            Assert.Equal(expected, new KeyCombo(key, modifiers).ToDisplayString());
        }
    }
}
