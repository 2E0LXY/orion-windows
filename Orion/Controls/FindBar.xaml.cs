using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Windows.System;
using Windows.UI.Core;

namespace Orion
{
    /// <summary>
    /// Find-in-page card, floating over the top-right corner of the web
    /// content (Chrome/Edge convention). A dumb view: raises events for query
    /// changes and navigation; the host mediates to the active tab's
    /// NativeWebView and feeds results back via SetResult.
    /// Place stretched over the content area; the card anchors to this
    /// control's top-right corner.
    /// </summary>
    public sealed partial class FindBar : UserControl
    {
        public event Action<string>? QueryChanged;
        public event Action? NextRequested;
        public event Action? PreviousRequested;
        public event Action? CloseRequested;

        private const double CardMargin = 16;

        // Guards TextChanged while the host seeds the box programmatically.
        private bool _suppressQueryChanged;

        public FindBar()
        {
            InitializeComponent();
        }

        public string Query => QueryBox.Text;

        public bool IsOpen => CardPopup.IsOpen;

        /// <summary>
        /// Shows the card seeded with prefill (without raising QueryChanged).
        /// focus: also focus the query box and select its text.
        /// </summary>
        public void Open(string prefill, bool focus = true)
        {
            _suppressQueryChanged = true;
            QueryBox.Text = prefill;
            _suppressQueryChanged = false;
            ClearResult();
            UpdateCardPosition();
            CardPopup.IsOpen = true;
            if (focus)
            {
                _ = QueryBox.Focus(FocusState.Programmatic);
                QueryBox.SelectAll();
            }
        }

        public void Close()
        {
            CardPopup.IsOpen = false;
            ClearResult();
        }

        /// <summary>
        /// matchCount: 0 = no matches; negative = more than |matchCount|
        /// matches. activeMatchOrdinal: 1-based, 0 = unknown.
        /// </summary>
        public void SetResult(int matchCount, int activeMatchOrdinal)
        {
            ResultText.Text = matchCount switch
            {
                0 => "Not found",
                < 0 => $"{-matchCount}+ matches",
                _ when activeMatchOrdinal > 0 => $"{activeMatchOrdinal} of {matchCount}",
                _ => $"{matchCount} matches",
            };
        }

        public void ClearResult() => ResultText.Text = "";

        private void UpdateCardPosition()
        {
            CardPopup.HorizontalOffset = Math.Max(0, ActualWidth - Card.ActualWidth - CardMargin);
            CardPopup.VerticalOffset = 8;
        }

        private void Host_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCardPosition();

        // The card resizes as the result text changes; keep it right-anchored.
        private void Card_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCardPosition();

        private void QueryBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_suppressQueryChanged)
                QueryChanged?.Invoke(QueryBox.Text);
        }

        private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                bool shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                    .HasFlag(CoreVirtualKeyStates.Down);
                if (shift)
                    PreviousRequested?.Invoke();
                else
                    NextRequested?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                CloseRequested?.Invoke();
                e.Handled = true;
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => PreviousRequested?.Invoke();

        private void NextButton_Click(object sender, RoutedEventArgs e) => NextRequested?.Invoke();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
    }
}
