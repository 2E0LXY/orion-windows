using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Orion
{
    public sealed partial class LibraryWindowContent : UserControl
    {
        private readonly ObservableCollection<HistoryEntry> _allEntries = [];
        private readonly ObservableCollection<HistoryTreeNode> _treeNodes = [];
        private readonly ObservableCollection<BookmarkEntry> _allBookmarks = [];
        private readonly ObservableCollection<BookmarkTreeNode> _bookmarkNodes = [];
        private readonly ObservableCollection<ReadingListEntry> _allReadingListEntries = [];
        private readonly ObservableCollection<ReadingListTreeNode> _readingListNodes = [];
        private readonly ObservableCollection<ExtensionEntry> _allExtensions = [];
        private readonly ObservableCollection<ExtensionListItem> _extensionItems = [];
        private readonly ObservableCollection<PasswordEntry> _allPasswordEntries = [];
        private readonly ObservableCollection<PasswordEntry> _passwordItems = [];

        internal UIElement TitleBarElement => AppTitleBar;
        internal Window? ParentWindow { get; set; }
        internal long ProfileId { get; set; }

        private static readonly Dictionary<string, (string Glyph, string Label)> _categoryInfo = new()
        {
            ["Profiles"] = ("\uE77B", "Profiles coming soon..."),
        };

        public LibraryWindowContent()
        {
            InitializeComponent();
            ReadingListChanged.Changed += OnReadingListChanged;
            ThemeService.Register(this);
        }

        /// <summary>
        /// Selects a category and loads its data. Call after ProfileId is set.
        /// </summary>
        internal void LoadData(string? category) => SelectCategory(category ?? "History");

        private void OnReadingListChanged()
        {
            if (ReadingListContent.Visibility == Visibility.Visible)
            {
                InitializeReadingListData();
                RebuildReadingListTree(ReadingListSearchBox.Text);
            }
        }

        public void SelectCategory(string tag)
        {
            foreach (NavigationViewItem item in CategoryNavigationView.MenuItems.Cast<NavigationViewItem>())
            {
                if (item is NavigationViewItem navItem && navItem.Tag is string itemTag && itemTag == tag)
                {
                    CategoryNavigationView.SelectedItem = navItem;
                    break;
                }
            }
        }

        public static Visibility BoolToVisible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility BoolToCollapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

        private void InitializeHistoryData()
        {
            _allEntries.Clear();
            try
            {
                foreach (HistoryEntry entry in OrionBackend.GetRecentHistory(ProfileId))
                {
                    _allEntries.Add(entry);
                }
            }
            catch
            {
                // Fall back to empty list if core module not available
            }
        }

        private static string GetDayLabel(DateTime date)
        {
            DateTime today = DateTime.Now.Date;
            if (date == today)
                return "Today";
            if (date == today.AddDays(-1))
                return "Yesterday";
            return date.ToString("MMM d, yyyy");
        }

        private void RebuildTree(string? filter = null)
        {
            IEnumerable<HistoryEntry> entries = _allEntries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                entries = entries.Where(e =>
                    e.Website.Contains(filter, StringComparison.InvariantCultureIgnoreCase) ||
                    e.Address.Contains(filter, StringComparison.InvariantCultureIgnoreCase));
            }

            IOrderedEnumerable<IGrouping<DateTime, HistoryEntry>> grouped = entries
                .GroupBy(e => (e.Address, e.VisitedAt.Date))
                .Select(g => g.MaxBy(e => e.VisitedAt)!)
                .GroupBy(e => e.VisitedAt.Date)
                .OrderByDescending(g => g.Key);

            _treeNodes.Clear();
            foreach (IGrouping<DateTime, HistoryEntry>? group in grouped)
            {
                var groupNode = new HistoryTreeNode { GroupName = GetDayLabel(group.Key) };
                foreach (HistoryEntry entry in group.OrderByDescending(e => e.VisitedAt))
                {
                    groupNode.Children.Add(new HistoryTreeNode { Entry = entry });
                }
                _treeNodes.Add(groupNode);
            }

            bool hasEntries = _treeNodes.Count > 0;
            HistoryTreeView.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            ColumnHeader.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;

            foreach (HistoryTreeNode groupNode in _treeNodes)
            {
                foreach (HistoryTreeNode node in groupNode.Children)
                    _ = node.LoadFaviconAsync();
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                RebuildTree(sender.Text);
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HistoryEntryId id)
            {
                HistoryEntry? entry = _allEntries.FirstOrDefault(h => h.Id == id);
                if (entry != null)
                {
                    string address = entry.Address;
                    var matching = _allEntries.Where(h => h.Address == address).ToList();
                    OrionBackend.DeleteHistoryEntries([.. matching.Select(h => h.Id.Value)]);
                    foreach (HistoryEntry h in matching)
                        _ = _allEntries.Remove(h);
                    RebuildTree(SearchBox.Text);
                }
            }
        }

        private void CategoryNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                string displayName = item.Content as string ?? tag;
                TitleBarText.Text = displayName;
                if (ParentWindow is { } parentWindow)
                    parentWindow.Title = displayName;

                HistoryContent.Visibility = Visibility.Collapsed;
                BookmarksContent.Visibility = Visibility.Collapsed;
                ReadingListContent.Visibility = Visibility.Collapsed;
                PasswordsContent.Visibility = Visibility.Collapsed;
                ExtensionsContent.Visibility = Visibility.Collapsed;
                PlaceholderContent.Visibility = Visibility.Collapsed;

                switch (tag)
                {
                    case "History":
                        InitializeHistoryData();
                        RebuildTree(SearchBox.Text);
                        HistoryContent.Visibility = Visibility.Visible;
                        break;
                    case "Bookmarks":
                        InitializeBookmarksData();
                        RebuildBookmarksTree(BookmarksSearchBox.Text);
                        BookmarksContent.Visibility = Visibility.Visible;
                        break;
                    case "ReadingList":
                        InitializeReadingListData();
                        RebuildReadingListTree(ReadingListSearchBox.Text);
                        ReadingListContent.Visibility = Visibility.Visible;
                        break;
                    case "Passwords":
                        InitializePasswordData();
                        RebuildPasswordList(PasswordsSearchBox.Text);
                        PasswordsContent.Visibility = Visibility.Visible;
                        break;
                    case "Extensions":
                        ExtensionsContent.Visibility = Visibility.Visible;
                        break;
                    default:
                        (string Glyph, string Label) = _categoryInfo[tag];
                        PlaceholderIcon.Glyph = Glyph;
                        PlaceholderText.Text = Label;
                        PlaceholderContent.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _allEntries.Clear();
            RebuildTree();
        }

        // --- Bookmarks ---

        private void InitializeBookmarksData()
        {
            _allBookmarks.Clear();
            try
            {
                foreach (BookmarkEntry folder in OrionBackend.GetBookmarkFolders(ProfileId))
                {
                    _allBookmarks.Add(folder);
                }

                foreach (BookmarkEntry bookmark in OrionBackend.GetBookmarks(ProfileId))
                {
                    _allBookmarks.Add(bookmark);
                }
            }
            catch
            {
                // Fall back to empty list if core module not available
            }
        }

        private void RebuildBookmarksTree(string? filter = null)
        {
            _bookmarkNodes.Clear();

            IEnumerable<BookmarkEntry> entries = _allBookmarks;
            bool isFiltering = !string.IsNullOrWhiteSpace(filter);

            if (isFiltering)
            {
                // When filtering, show flat list of matching non-folder entries
                IEnumerable<BookmarkEntry> matching = entries.Where(e =>
                    !e.IsFolder &&
                    (e.Title.Contains(filter!, StringComparison.InvariantCultureIgnoreCase) ||
                     e.Url.Contains(filter!, StringComparison.InvariantCultureIgnoreCase)));

                foreach (BookmarkEntry entry in matching.OrderBy(e => e.Title))
                {
                    _bookmarkNodes.Add(new BookmarkTreeNode(entry));
                }
            }
            else
            {
                BookmarkTreeNode.BuildTree(_bookmarkNodes, _allBookmarks);
            }

            bool hasEntries = _bookmarkNodes.Count > 0;
            BookmarksTreeView.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            BookmarksColumnHeader.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            BookmarksEmptyState.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;

            LoadBookmarkFavicons(_bookmarkNodes);
        }

        private static void LoadBookmarkFavicons(IEnumerable<BookmarkTreeNode> nodes)
        {
            foreach (BookmarkTreeNode node in nodes)
            {
                _ = node.LoadFaviconAsync();
                if (node.IsFolder)
                    LoadBookmarkFavicons(node.Children);
            }
        }

        private void BookmarksSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                RebuildBookmarksTree(sender.Text);
        }

        private void DeleteBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is long id)
            {
                BookmarkEntry? entry = _allBookmarks.FirstOrDefault(b => b.Id == id);
                if (entry != null)
                {
                    if (entry.IsFolder)
                    {
                        // Remove children from local collection before removing folder
                        var children = _allBookmarks.Where(b => b.ParentFolderId == entry.Id).ToList();
                        foreach (BookmarkEntry child in children)
                        {
                            _ = _allBookmarks.Remove(child);
                        }
                        OrionBackend.DeleteBookmarkFolder(entry.Id);
                    }
                    else
                    {
                        OrionBackend.DeleteBookmark(entry.Id);
                    }
                    _ = _allBookmarks.Remove(entry);
                    RebuildBookmarksTree(BookmarksSearchBox.Text);
                    BookmarksChanged.Notify();
                }
            }
        }

        private void AddBookmarkFolder_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement folder creation via backend API
        }

        // --- Reading List ---

        private void InitializeReadingListData()
        {
            _allReadingListEntries.Clear();
            try
            {
                foreach (ReadingListEntry entry in OrionBackend.GetReadingList(ProfileId))
                    _allReadingListEntries.Add(entry);
            }
            catch
            {
                // Fall back to empty list if core module not available
            }
        }

        private void RebuildReadingListTree(string? filter = null)
        {
            IEnumerable<ReadingListEntry> entries = _allReadingListEntries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                entries = entries.Where(e =>
                    e.Title.Contains(filter, StringComparison.InvariantCultureIgnoreCase) ||
                    e.Address.Contains(filter, StringComparison.InvariantCultureIgnoreCase));
            }

            IOrderedEnumerable<IGrouping<DateTime, ReadingListEntry>> grouped = entries
                .GroupBy(e => e.AddedOn.Date)
                .OrderByDescending(g => g.Key);

            _readingListNodes.Clear();
            foreach (IGrouping<DateTime, ReadingListEntry>? group in grouped)
            {
                var groupNode = new ReadingListTreeNode { GroupName = GetDayLabel(group.Key) };
                foreach (ReadingListEntry entry in group.OrderByDescending(e => e.AddedOn))
                    groupNode.Children.Add(new ReadingListTreeNode { Entry = entry });
                _readingListNodes.Add(groupNode);
            }

            bool hasEntries = _readingListNodes.Count > 0;
            ReadingListTreeView.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            ReadingListColumnHeader.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            ReadingListEmptyState.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;

            foreach (ReadingListTreeNode groupNode in _readingListNodes)
            {
                foreach (ReadingListTreeNode node in groupNode.Children)
                    _ = node.LoadFaviconAsync();
            }
        }

        private void ReadingListSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                RebuildReadingListTree(sender.Text);
        }

        private void DeleteReadingListEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ReadingListEntryId id)
            {
                ReadingListEntry? entry = _allReadingListEntries.FirstOrDefault(r => r.Id == id);
                if (entry != null)
                {
                    OrionBackend.DeleteReadingListEntries([entry.Id.Value]);
                    _ = _allReadingListEntries.Remove(entry);
                    RebuildReadingListTree(ReadingListSearchBox.Text);
                    ReadingListChanged.Notify();
                }
            }
        }

        private void ReadingListTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs e)
        {
            if (e.InvokedItem is ReadingListTreeNode { IsGroup: false, Url: string url } && !string.IsNullOrEmpty(url))
                NavigateMainWindow(url);
        }

        public static Windows.UI.Text.FontWeight FolderFontWeight(bool isFolder) => isFolder
                ? new Windows.UI.Text.FontWeight(600)  // SemiBold
                : new Windows.UI.Text.FontWeight(400);  // Normal

        // --- Extensions ---

        private void InitializeExtensionsData()
        {
            foreach (ExtensionEntry ext in OrionBackend.GetInstalledExtensions(ProfileId))
                _allExtensions.Add(ext);
            RebuildExtensionsList();
        }

        private void RebuildExtensionsList(string? filter = null)
        {
            _extensionItems.Clear();

            IEnumerable<ExtensionEntry> entries = _allExtensions;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                entries = entries.Where(e =>
                    e.Name.Contains(filter, StringComparison.InvariantCultureIgnoreCase) ||
                    e.TypeLabel.Contains(filter, StringComparison.InvariantCultureIgnoreCase));
            }

            foreach (ExtensionEntry entry in entries)
            {
                _extensionItems.Add(new ExtensionListItem(entry));
            }

            bool hasItems = _extensionItems.Count > 0;
            ExtensionsListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            ExtensionsEmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ExtensionsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                RebuildExtensionsList(sender.Text);
        }

        private void ReloadExtension_Click(object sender, RoutedEventArgs e) { /* no-op */ }

        private void UninstallExtension_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string id)
            {
                ExtensionEntry? entry = _allExtensions.FirstOrDefault(ex => ex.Id == id);
                if (entry != null)
                {
                    OrionBackend.UninstallExtension(id);
                    _ = _allExtensions.Remove(entry);
                    RebuildExtensionsList(ExtensionsSearchBox.Text);
                }
            }
        }

        private void AddExtension_Click(object sender, RoutedEventArgs e) { /* no-op */ }

        private void DisableAll_Click(SplitButton sender, SplitButtonClickEventArgs e)
        {
            foreach (ExtensionListItem item in _extensionItems)
            {
                item.Entry.IsEnabled = false;
            }
        }

        private void UninstallAll_Click(object sender, RoutedEventArgs e)
        {
            _allExtensions.Clear();
            RebuildExtensionsList();
        }

        private void UpdateExtensions_Click(object sender, RoutedEventArgs e) { /* no-op */ }

        // --- Passwords ---

        private void InitializePasswordData()
        {
            _allPasswordEntries.Clear();
            try
            {
                long[] ids = OrionBackend.GetPasswordItemIds(ProfileId);
                foreach (PasswordEntry entry in OrionBackend.GetPasswordEntries(ids))
                    _allPasswordEntries.Add(entry);
            }
            catch
            {
            }
        }

        private void RebuildPasswordList(string? filter = null)
        {
            _passwordItems.Clear();

            IEnumerable<PasswordEntry> entries = _allPasswordEntries;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                entries = entries.Where(e =>
                    e.Url.Contains(filter, StringComparison.InvariantCultureIgnoreCase) ||
                    e.Username.Contains(filter, StringComparison.InvariantCultureIgnoreCase));
            }

            foreach (PasswordEntry entry in entries.OrderBy(e => e.Url))
                _passwordItems.Add(entry);

            bool hasItems = _passwordItems.Count > 0;
            PasswordsListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            PasswordsColumnHeader.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            PasswordsEmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PasswordsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                RebuildPasswordList(sender.Text);
        }

        private void DeletePassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is long id)
            {
                PasswordEntry? entry = _allPasswordEntries.FirstOrDefault(p => p.Id == id);
                if (entry != null)
                {
                    OrionBackend.DeletePasswordEntries(id);
                    _ = _allPasswordEntries.Remove(entry);
                    RebuildPasswordList(PasswordsSearchBox.Text);
                }
            }
        }

        private void CopyUsername_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string username)
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(username);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            }
        }

        private async void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not long id)
                return;

            PasswordEntry? entry = _passwordItems.FirstOrDefault(p => p.Id == id);
            if (entry == null)
                return;

            if (!entry.IsPasswordRevealed && !await VerifyUserIdentityAsync())
                return;

            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(entry.Password);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }

        private async void RevealPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not long id)
                return;

            PasswordEntry? entry = _passwordItems.FirstOrDefault(p => p.Id == id);
            if (entry == null)
                return;

            if (entry.IsPasswordRevealed)
            {
                entry.IsPasswordRevealed = false;
                return;
            }

            if (!await VerifyUserIdentityAsync())
                return;

            entry.IsPasswordRevealed = true;
        }

        private async Task<bool> VerifyUserIdentityAsync()
        {
            try
            {
                Windows.Security.Credentials.UI.UserConsentVerifierAvailability availability =
                    await Windows.Security.Credentials.UI.UserConsentVerifier.CheckAvailabilityAsync();
                if (availability == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available)
                {
                    Windows.Security.Credentials.UI.UserConsentVerificationResult result =
                        await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync(
                            "Orion needs to verify your identity to show this password.");
                    return result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified;
                }
            }
            catch
            {
            }
            return await VerifyWithWindowsPasswordAsync();
        }

        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword,
            int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private async Task<bool> VerifyWithWindowsPasswordAsync()
        {
            var passwordBox = new PasswordBox { PlaceholderText = "Windows password" };
            var errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                FontSize = 12,
                Visibility = Visibility.Collapsed,
            };
            var panel = new StackPanel { Spacing = 8, MinWidth = 300 };
            panel.Children.Add(new TextBlock { Text = $"Enter the password for {Environment.UserName}:" });
            panel.Children.Add(passwordBox);
            panel.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = "Verify your identity",
                Content = panel,
                PrimaryButtonText = "Verify",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            while (true)
            {
                ContentDialogResult dialogResult = await dialog.ShowAsync();
                if (dialogResult != ContentDialogResult.Primary)
                    return false;

                if (LogonUser(Environment.UserName, Environment.UserDomainName,
                    passwordBox.Password, 2, 0, out IntPtr token))
                {
                    CloseHandle(token);
                    return true;
                }

                errorText.Text = "Incorrect password. Please try again.";
                errorText.Visibility = Visibility.Visible;
                passwordBox.Password = "";
            }
        }

        private async void ExportPasswordsCsv_Click(object sender, RoutedEventArgs e)
        {
            if (ParentWindow is not { } window)
                return;

            long[] ids = OrionBackend.GetPasswordItemIds(ProfileId);
            if (ids.Length == 0)
                return;

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = "passwords";
            picker.FileTypeChoices.Add("CSV", [".csv"]);

            Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null)
                return;

            string csv = OrionBackend.ExportPasswordItemsCsv(ids);
            await Windows.Storage.FileIO.WriteTextAsync(file, csv);
        }

        private async void AddPassword_Click(object sender, RoutedEventArgs e)
        {
            (PasswordEntry? result, _) = await ShowPasswordDialog("Add Password", null);
            if (result != null)
            {
                long newId = OrionBackend.CreatePasswordEntry(ProfileId, result);
                if (newId != 0)
                {
                    result.Id = newId;
                    result.ProfileId = ProfileId;
                    _allPasswordEntries.Add(result);
                    RebuildPasswordList(PasswordsSearchBox.Text);
                }
            }
        }

        private async void PasswordsListView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (PasswordsListView.SelectedItem is not PasswordEntry selected)
                return;

            (PasswordEntry? result, bool deleted) = await ShowPasswordDialog("Edit Password", selected);
            if (deleted)
            {
                OrionBackend.DeletePasswordEntries(selected.Id);
                _ = _allPasswordEntries.Remove(selected);
                RebuildPasswordList(PasswordsSearchBox.Text);
            }
            else if (result != null)
            {
                selected.Url = result.Url;
                selected.Username = result.Username;
                selected.Password = result.Password;
                OrionBackend.UpdatePasswordEntry(selected);
                RebuildPasswordList(PasswordsSearchBox.Text);
            }
        }

        private async Task<(PasswordEntry? Entry, bool Deleted)> ShowPasswordDialog(string title, PasswordEntry? existing)
        {
            var urlBox = new TextBox
            {
                PlaceholderText = "https://example.com",
                Text = existing?.Url ?? "",
                IsReadOnly = existing != null,
            };
            var usernameBox = new TextBox
            {
                PlaceholderText = "Username or email",
                Text = existing?.Username ?? "",
            };
            var passwordBox = new PasswordBox
            {
                PlaceholderText = "Password",
                Password = existing?.Password ?? "",
            };

            var form = new StackPanel { Spacing = 12, MinWidth = 350 };
            form.Children.Add(new TextBlock { Text = "Website URL" });
            form.Children.Add(urlBox);
            form.Children.Add(new TextBlock { Text = "Username" });
            form.Children.Add(usernameBox);
            form.Children.Add(new TextBlock { Text = "Password" });
            form.Children.Add(passwordBox);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = form,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                SecondaryButtonText = existing != null ? "Delete" : null,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            ContentDialogResult dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.Secondary)
                return (null, true);
            if (dialogResult != ContentDialogResult.Primary)
                return (null, false);

            return (new PasswordEntry
            {
                Url = urlBox.Text,
                Username = usernameBox.Text,
                Password = passwordBox.Password,
            }, false);
        }

        // --- Navigation ---

        private static void NavigateMainWindow(string url) =>
            WithMainWindowContent(content => content.NavigateToUrl(url));

        private static void OpenBookmarkInMainWindow(string url, string? title) =>
            WithMainWindowContent(content => content.OpenBookmark(url, title));

        /// <summary>Opens a background tab in the main window without stealing
        /// focus from the library window.</summary>
        private static void OpenBackgroundTabInMainWindow(string url, string? title) =>
            WithMainWindowContent(
                content => content.OpenLink(url, OrionBackend.OpenDisposition.NewBackgroundTab, title),
                activate: false);

        private static void WithMainWindowContent(Action<MainWindowContent> action, bool activate = true)
        {
            if (Application.Current is App app)
            {
                System.Reflection.FieldInfo? windowField = typeof(App).GetField("_window",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (windowField?.GetValue(app) is MainWindow mainWindow)
                {
                    action(mainWindow.MainWindowContent);
                    if (activate)
                        mainWindow.Activate();
                }
            }
        }

        private void HistoryTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs e)
        {
            if (e.InvokedItem is HistoryTreeNode { IsGroup: false, Url: string url } && !string.IsNullOrEmpty(url))
                NavigateMainWindow(url);
        }

        private void BookmarksTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs e)
        {
            if (e.InvokedItem is BookmarkTreeNode { IsFolder: false } node && !string.IsNullOrEmpty(node.Url))
                OpenBookmarkInMainWindow(node.Url, node.Title);
        }

        private void BookmarkTreeItem_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed
                && sender is TreeViewItem { DataContext: BookmarkTreeNode { IsFolder: false } node }
                && !string.IsNullOrEmpty(node.Url))
            {
                OpenBackgroundTabInMainWindow(node.Url, node.Title);
                e.Handled = true;
            }
        }

        private void BookmarksTreeView_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs e)
        {
            foreach (object obj in e.Items)
            {
                if (obj is not BookmarkTreeNode moved)
                    continue;

                (IList<BookmarkTreeNode> Siblings, int Index, long ParentFolderId)? location = BookmarkTreeNode.FindInTree(_bookmarkNodes, moved);
                if (location is not null)
                {
                    (IList<BookmarkTreeNode>? siblings, int idx, long parentFolderId) = location.Value;
                    BookmarkTreeNode.PersistMove(moved, siblings, idx, parentFolderId);
                }
            }
            BookmarksChanged.Notify();
        }
    }

    public partial class HistoryTreeNode : ObservableObject
    {
        public string? GroupName { get; init; }
        public HistoryEntry? Entry { get; init; }
        public ObservableCollection<HistoryTreeNode> Children { get; } = [];
        public bool IsGroup => GroupName != null;

        public string Title => Entry?.Website ?? "";
        public string Url => Entry?.Address ?? "";
        public string DateTimeText => Entry?.VisitedAt.ToString("MMM d, yyyy  h:mm tt") ?? "";
        public HistoryEntryId EntryId => Entry?.Id ?? default;

        [ObservableProperty]
        public partial ImageSource? FaviconImage { get; set; }

        public async Task LoadFaviconAsync()
        {
            if (Entry == null || string.IsNullOrEmpty(Entry.Address))
                return;
            OrionBackend.FaviconResult? result = OrionBackend.GetFavicon(Entry.Address);
            if (result != null)
                FaviconImage = await BrowserTab.CreateFaviconImageAsync(result.Data, result.IconUrl);
        }
    }

    public partial class ReadingListTreeNode : ObservableObject
    {
        public string? GroupName { get; init; }
        public ReadingListEntry? Entry { get; init; }
        public ObservableCollection<ReadingListTreeNode> Children { get; } = [];
        public bool IsGroup => GroupName != null;

        public string Title => Entry?.Title ?? "";
        public string Url => Entry?.Address ?? "";
        public string DateTimeText => Entry?.AddedOn.ToString("MMM d, yyyy  h:mm tt") ?? "";
        public ReadingListEntryId EntryId => Entry?.Id ?? default;

        [ObservableProperty]
        public partial ImageSource? FaviconImage { get; set; }

        public async Task LoadFaviconAsync()
        {
            if (Entry == null || string.IsNullOrEmpty(Entry.Address))
                return;
            OrionBackend.FaviconResult? result = OrionBackend.GetFavicon(Entry.Address);
            if (result != null)
                FaviconImage = await BrowserTab.CreateFaviconImageAsync(result.Data, result.IconUrl);
        }
    }

    public class ExtensionListItem(ExtensionEntry entry)
    {
        private static readonly Windows.UI.Color[] IconColors =
        [
            Windows.UI.Color.FromArgb(255, 0,   150, 136),  // Teal
            Windows.UI.Color.FromArgb(255, 63,  81,  181),  // Indigo
            Windows.UI.Color.FromArgb(255, 198, 54,  43),   // Red
            Windows.UI.Color.FromArgb(255, 230, 126, 34),   // Orange
            Windows.UI.Color.FromArgb(255, 39,  174, 96),   // Green
            Windows.UI.Color.FromArgb(255, 142, 68,  173),  // Purple
        ];

        public ExtensionEntry Entry { get; } = entry;
        public SolidColorBrush IconBrush { get; } = new SolidColorBrush(IconColors[Math.Abs(entry.Name.GetHashCode()) % IconColors.Length]);
        public string IconLetter => Entry.Name.Length > 0 ? Entry.Name[0].ToString().ToUpper() : "?";
    }
}
