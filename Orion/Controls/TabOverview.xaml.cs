using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.System;

namespace Orion
{
    /// <summary>
    /// Tab overview: a popup grid of thumbnail tiles for the active group's
    /// tabs, rendered above the native web HWND (unconstrained Popup, same
    /// pattern as FindBar/PopupDialog) so the page stays visible behind a
    /// dim scrim. Closes itself on Esc or a scrim click; tile activation is
    /// routed to the host via TabActivated.
    /// </summary>
    public sealed partial class TabOverview : UserControl
    {
        /// <summary>Raised when a tile is clicked (the popup closes itself first); host activates the tab.</summary>
        public event Action<BrowserTab>? TabActivated;

        /// <summary>Raised by a tile's close button; host closes the tab (overview stays open).</summary>
        public event Action<BrowserTab>? TabCloseRequested;

        public TabOverview()
        {
            InitializeComponent();
        }

        public bool IsOpen => OverviewPopup.IsOpen;

        /// <summary>The unfiltered groups; Tabs holds each group's live collection.</summary>
        private System.Collections.Generic.IReadOnlyList<TabOverviewGroup> _allGroups = [];

        private readonly CollectionViewSource _groupedSource = new()
        {
            IsSourceGrouped = true,
            ItemsPath = new PropertyPath("Tabs"),
        };

        /// <summary>Opens the overview showing all given tab groups.</summary>
        public void Open(System.Collections.Generic.IReadOnlyList<TabOverviewGroup> groups)
        {
            _allGroups = groups;
            SearchBox.Text = string.Empty;
            ApplyFilter();
            UpdateSize();
            OverviewPopup.IsOpen = true;
            // Focus lands after the popup's content is realized. The search
            // box gets it (type-to-filter); Down arrow moves into the grid.
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (IsOpen)
                    _ = SearchBox.Focus(FocusState.Programmatic);
            });
        }

        /// <summary>
        /// Rebinds the grid from the live group collections, applying the
        /// query (title/URL substring, case-insensitive) and dropping groups
        /// with no matches. Always materializes, so re-running after a tab
        /// closes also refreshes the view.
        /// </summary>
        private void ApplyFilter()
        {
            string query = SearchBox.Text.Trim();
            var display = new System.Collections.Generic.List<TabOverviewGroup>();
            foreach (TabOverviewGroup group in _allGroups)
            {
                var tabs = new System.Collections.Generic.List<BrowserTab>();
                foreach (object item in group.Tabs)
                {
                    if (item is BrowserTab tab
                        && (query.Length == 0
                            || tab.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || tab.Url.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    {
                        tabs.Add(tab);
                    }
                }
                if (tabs.Count > 0)
                    display.Add(group with { Tabs = tabs });
            }
            _groupedSource.Source = display;
            TabGrid.ItemsSource = _groupedSource.View;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                // Enter activates the first (best) match.
                if (TabGrid.Items.Count > 0 && TabGrid.Items[0] is BrowserTab tab)
                {
                    Close();
                    TabActivated?.Invoke(tab);
                }
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down)
            {
                FocusGrid();
                e.Handled = true;
            }
        }

        public void Close() => OverviewPopup.IsOpen = false;

        /// <summary>Moves keyboard focus into the grid (arrow keys + Enter work).</summary>
        private void FocusGrid()
        {
            if (TabGrid.Items.Count > 0)
                (TabGrid.ContainerFromIndex(0) as GridViewItem)?.Focus(FocusState.Programmatic);
            else
                _ = Focus(FocusState.Programmatic);
        }

        public static Visibility VisibleIfNull(ImageSource? source) =>
            source == null ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility VisibleIfNotNull(ImageSource? source) =>
            source != null ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>The scrim fills this control (which fills the content area).</summary>
        private void UpdateSize()
        {
            Scrim.Width = ActualWidth;
            Scrim.Height = ActualHeight;
        }

        private void Host_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsOpen)
                UpdateSize();
        }

        private void TabGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is BrowserTab tab)
            {
                Close();
                TabActivated?.Invoke(tab);
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: BrowserTab tab })
            {
                TabCloseRequested?.Invoke(tab);
                // The grid shows materialized snapshots of the live group
                // collections; rebuild once the host has processed the close.
                _ = DispatcherQueue.TryEnqueue(ApplyFilter);
            }
        }

        private void Scrim_Tapped(object sender, TappedRoutedEventArgs e) => Close();

        // Stops taps inside the card from reaching the scrim's close handler.
        private void Card_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

        private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                // Two-stage Esc: clear an active query first, close second.
                if (SearchBox.Text.Length > 0)
                {
                    SearchBox.Text = string.Empty;
                    _ = SearchBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    Close();
                }
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// One tab group shown in the overview. Tabs is the group's tab
    /// enumerable (live collection in, materialized snapshot for display).
    /// </summary>
    public sealed record TabOverviewGroup(string Name, TabGroup Group, System.Collections.IEnumerable Tabs);
}
