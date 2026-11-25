using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.System;
using Windows.UI.Core;

namespace Orion
{
    /// <summary>
    /// Captures a keyboard chord (macOS Orion recorder contract): focus starts
    /// capture; a valid chord raises ChordCaptured; unmodified Esc cancels,
    /// unmodified Backspace/Delete raises ClearRequested, unmodified Tab moves
    /// focus on. Invalid chords are swallowed and capture continues. The host
    /// owns validation against other bindings and persistence — it calls
    /// ShowCombo with the outcome.
    /// </summary>
    public sealed partial class ShortcutRecorderBox : UserControl
    {
        /// <summary>
        /// True while any recorder in the process is capturing. App shortcut
        /// dispatch (accelerators + DispatchShortcut) pauses on this so the
        /// chord being recorded never triggers the action it collides with.
        /// </summary>
        public static bool AnyCapturing { get; private set; }

        public event Action<ShortcutRecorderBox, KeyCombo>? ChordCaptured;
        public event Action<ShortcutRecorderBox>? ClearRequested;

        private KeyCombo? _shown;
        private bool _capturing;

        public ShortcutRecorderBox()
        {
            InitializeComponent();
            ShowIdleText();
            GotFocus += (s, e) => StartCapture();
            LostFocus += (s, e) => EndCapture();
            // A UserControl is not focused by pointer automatically.
            Tapped += (s, e) => { _ = Focus(FocusState.Pointer); StartCapture(); };
            // A closing dialog may skip LostFocus; AnyCapturing must not leak.
            Unloaded += (s, e) => EndCapture();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>Sets the combo the idle state displays (null = "None").</summary>
        public void ShowCombo(KeyCombo? combo)
        {
            _shown = combo;
            if (!_capturing)
                ShowIdleText();
        }

        private void StartCapture()
        {
            if (_capturing)
                return;
            _capturing = true;
            AnyCapturing = true;
            ComboText.Text = "Type shortcut…";
            ComboText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            Box.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        }

        private void EndCapture()
        {
            if (!_capturing)
                return;
            _capturing = false;
            AnyCapturing = false;
            Box.BorderBrush = (Brush)Application.Current.Resources["TextControlBorderBrush"];
            ShowIdleText();
        }

        private void ShowIdleText()
        {
            ComboText.Text = _shown?.ToDisplayString() ?? "None";
            ComboText.Foreground = (Brush)Application.Current.Resources[
                _shown != null ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"];
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_capturing)
            {
                // Idle but focused (after an Esc cancel): Enter/Space re-arms.
                if (e.Key is VirtualKey.Enter or VirtualKey.Space)
                {
                    StartCapture();
                    e.Handled = true;
                }
                return;
            }

            if (IsModifierKey(e.Key))
            {
                e.Handled = true;
                return;
            }

            VirtualKeyModifiers modifiers = CurrentModifiers();
            if (modifiers == VirtualKeyModifiers.None)
            {
                if (e.Key == VirtualKey.Escape)
                {
                    EndCapture();
                    e.Handled = true;
                    return;
                }
                if (e.Key is VirtualKey.Back or VirtualKey.Delete)
                {
                    EndCapture();
                    ClearRequested?.Invoke(this);
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Tab)
                    return; // unhandled: focus advances, LostFocus ends capture
            }

            var combo = new KeyCombo(e.Key, modifiers);
            e.Handled = true;
            if (!combo.IsValidBinding || !combo.IsMappable)
                return; // swallowed; keep capturing
            EndCapture();
            ChordCaptured?.Invoke(this, combo);
        }

        private static bool IsModifierKey(VirtualKey key) => key is
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
            or VirtualKey.LeftWindows or VirtualKey.RightWindows;

        private static VirtualKeyModifiers CurrentModifiers()
        {
            VirtualKeyModifiers modifiers = VirtualKeyModifiers.None;
            if (IsDown(VirtualKey.Control))
                modifiers |= VirtualKeyModifiers.Control;
            if (IsDown(VirtualKey.Menu))
                modifiers |= VirtualKeyModifiers.Menu;
            if (IsDown(VirtualKey.Shift))
                modifiers |= VirtualKeyModifiers.Shift;
            return modifiers;

            static bool IsDown(VirtualKey key) =>
                Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
                    .HasFlag(CoreVirtualKeyStates.Down);
        }
    }
}
