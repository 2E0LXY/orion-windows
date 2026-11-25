using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Windows.Foundation;

namespace Orion
{
    public sealed partial class PopularExtensionsWindowContent : UserControl
    {
        internal UIElement TitleBarElement => AppTitleBar;
        internal Window? ParentWindow { get; set; }
        internal long ProfileId { get; set; }

        private bool _isUpdatingSelection;
        private bool _isScrollingToSection;

        private List<PopularExtensionItem> _passwordManagers = [];
        private List<PopularExtensionItem> _adBlockers = [];
        private List<PopularExtensionItem> _browseBetter = [];

        public PopularExtensionsWindowContent()
        {
            InitializeComponent();
            LoadExtensionData();
            ThemeService.Register(this);
        }

        internal void RefreshAndSelect(string? category)
        {
            RefreshInstalledState();
            SelectCategory(category ?? "PasswordManagers");
        }

        private void LoadExtensionData()
        {
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "extension_list.json");
            if (!File.Exists(jsonPath))
                return;

            string json = File.ReadAllText(jsonPath);
            ExtensionListData? data = JsonSerializer.Deserialize<ExtensionListData>(json);
            if (data == null)
                return;

            _passwordManagers = CreateItems(data.PasswordManagers, "\uE8D7");
            _adBlockers = CreateItems(data.AdBlockers, "\uE8F7");
            _browseBetter = CreateItems(data.BrowserBetter, "\uE734");

            PasswordManagersRepeater.ItemsSource = _passwordManagers;
            AdsTrackingRepeater.ItemsSource = _adBlockers;
            BrowseBetterRepeater.ItemsSource = _browseBetter;
        }

        private List<PopularExtensionItem> CreateItems(ExtensionInfoData[]? extensions, string glyph)
        {
            if (extensions == null)
                return [];

            var items = new List<PopularExtensionItem>(extensions.Length);
            foreach (ExtensionInfoData ext in extensions)
                items.Add(new PopularExtensionItem(ext) { CategoryGlyph = glyph, InstallAction = DoInstall });
            return items;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => RefreshInstalledState();

        private void RefreshInstalledState()
        {
            var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<ExtensionEntry> installed = OrionBackend.GetInstalledExtensions(ProfileId);
            foreach (ExtensionEntry ext in installed)
                _ = installedNames.Add(ext.Name);

            foreach (PopularExtensionItem item in _passwordManagers.Concat(_adBlockers).Concat(_browseBetter))
                item.IsInstalled = installedNames.Contains(item.Label);
        }

        private static readonly HttpClient s_httpClient = new();

        private async void DoInstall(PopularExtensionItem item)
        {
            item.IsInstalling = true;
            item.DownloadProgress = 0;

            ExtensionInfoData ext = item.Data;
            string url = ext.InstallURL ?? "";
            if (string.IsNullOrEmpty(url))
            {
                item.IsInstalling = false;
                return;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), $"orion_ext_{Guid.NewGuid()}.xpi");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                string? ua = MainWindowViewModel.GetUserAgentString(ProfileId);
                if (ua != null)
                    _ = request.Headers.TryAddWithoutValidation("User-Agent", ua);
                using HttpResponseMessage response = await s_httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead);
                _ = response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                await using Stream contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    if (totalBytes > 0)
                        item.DownloadProgress = (double)totalRead / totalBytes.Value * 100;
                }

                item.DownloadProgress = 100;
            }
            catch
            {
                item.IsInstalling = false;
                try { File.Delete(tempPath); } catch { }
                return;
            }

            // Install on UI thread (needs pump_wait for orion run loop)
            string resultId = OrionBackend.InstallExtension(
                ProfileId,
                ext.Name ?? "", ext.DisplayName ?? "", ext.Info ?? "",
                url, ext.WebExtType ?? "", ext.WebExtID ?? "",
                ext.GeckoId ?? "", ext.IconURL ?? "", ext.Version ?? "",
                ext.OrionHosted == true, tempPath);

            item.IsInstalling = false;

            if (!string.IsNullOrEmpty(resultId))
                item.IsInstalled = true;

            try { File.Delete(tempPath); } catch { }
        }

        public void SelectCategory(string tag)
        {
            foreach (object? item in CategoryNavigationView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag is string itemTag && itemTag == tag)
                {
                    CategoryNavigationView.SelectedItem = navItem;
                    break;
                }
            }
        }

        private void CategoryNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                // Footer items open external URLs
                switch (tag)
                {
                    case "ChromeStore":
                        NavigateMainWindow("https://chromewebstore.google.com/");
                        return;
                    case "FirefoxStore":
                        NavigateMainWindow("https://addons.mozilla.org/");
                        return;
                    default:
                        break;
                }

                // Scroll to the section header (only if user clicked, not from scroll sync)
                if (!_isUpdatingSelection)
                    ScrollToSection(tag);
            }
        }

        private void ScrollToSection(string tag)
        {
            FrameworkElement? header = tag switch
            {
                "PasswordManagers" => PasswordManagersHeader,
                "AdsTracking" => AdsTrackingHeader,
                "BrowseBetter" => BrowseBetterHeader,
                _ => null,
            };

            if (header == null)
                return;

            GeneralTransform transform = header.TransformToVisual(ContentScrollViewer);
            Point position = transform.TransformPoint(new Point(0, 0));

            _isScrollingToSection = true;
            _ = ContentScrollViewer.ChangeView(null, ContentScrollViewer.VerticalOffset + position.Y - 24, null, false);
        }

        private void ContentScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_isScrollingToSection)
            {
                if (!e.IsIntermediate)
                    _isScrollingToSection = false;
                return;
            }

            // Update selected nav item based on scroll position
            // Trigger when section header is in the upper 2/3 of the viewport
            double offset = ContentScrollViewer.VerticalOffset + (ContentScrollViewer.ViewportHeight * 2 / 3);

            double browsePos = BrowseBetterHeader.TransformToVisual(ContentScrollViewer)
                .TransformPoint(new Point(0, 0)).Y + ContentScrollViewer.VerticalOffset;
            double adsPos = AdsTrackingHeader.TransformToVisual(ContentScrollViewer)
                .TransformPoint(new Point(0, 0)).Y + ContentScrollViewer.VerticalOffset;

            string activeTag;
            if (offset >= browsePos)
                activeTag = "BrowseBetter";
            else if (offset >= adsPos)
                activeTag = "AdsTracking";
            else
                activeTag = "PasswordManagers";

            // Update selection without triggering scroll
            foreach (object? item in CategoryNavigationView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag is string itemTag && itemTag == activeTag)
                {
                    var selected = (NavigationViewItem)CategoryNavigationView.SelectedItem;
                    if (selected != navItem)
                    {
                        _isUpdatingSelection = true;
                        CategoryNavigationView.SelectedItem = navItem;
                        _isUpdatingSelection = false;
                    }
                    break;
                }
            }
        }

        private static void NavigateMainWindow(string url)
        {
            if (Application.Current is App app)
            {
                FieldInfo? windowField = typeof(App).GetField("_window",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (windowField?.GetValue(app) is MainWindow mainWindow)
                {
                    _ = mainWindow.MainWindowContent.ViewModel.CreateNewTab(url);
                    mainWindow.Activate();
                }
            }
        }

        private void ManageExtensions_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                FieldInfo? windowField = typeof(App).GetField("_window",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (windowField?.GetValue(app) is MainWindow mainWindow)
                {
                    mainWindow.MainWindowContent.OpenSettingsTab("Extensions");
                    mainWindow.Activate();
                }
            }
        }
    }
}
