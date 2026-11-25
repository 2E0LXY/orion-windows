using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Orion
{
    public sealed partial class DownloadsPage : UserControl
    {
        private readonly ObservableCollection<DownloadEntry> _allEntries = [];
        private CollectionViewSource? _groupedSource;

        public long ProfileId
        {
            get;
            set
            {
                if (field == value)
                    return;
                field = value;
                LoadDownloads();
            }
        }

        public DownloadsPage()
        {
            InitializeComponent();

            OrionBackend.DownloadCreated += OnDownloadCreated;
            OrionBackend.DownloadUpdated += OnDownloadUpdated;
            OrionBackend.DownloadDeleted += OnDownloadDeleted;

            Unloaded += (_, _) =>
            {
                OrionBackend.DownloadCreated -= OnDownloadCreated;
                OrionBackend.DownloadUpdated -= OnDownloadUpdated;
                OrionBackend.DownloadDeleted -= OnDownloadDeleted;
            };
        }

        private void LoadDownloads()
        {
            _allEntries.Clear();
            if (ProfileId == 0)
                return;

            foreach (DownloadEntry entry in OrionBackend.GetDownloads(ProfileId))
                _allEntries.Add(entry);

            ApplyGrouping();
        }

        private void OnDownloadCreated(DownloadEntry entry)
        {
            if (entry.ProfileId != ProfileId)
                return;

            DownloadEntry? existing = _allEntries.FirstOrDefault(d => d.Id == entry.Id);
            if (existing != null)
            {
                UpdateEntry(existing, entry);
            }
            else
            {
                _allEntries.Add(entry);
            }
            ApplyGrouping(SearchBox.Text);
        }

        private void OnDownloadUpdated(DownloadEntry entry)
        {
            if (entry.ProfileId != ProfileId)
                return;

            DownloadEntry? existing = _allEntries.FirstOrDefault(d => d.Id == entry.Id);
            if (existing != null)
            {
                bool statusChanged = existing.Status != entry.Status;
                UpdateEntry(existing, entry);
                if (statusChanged)
                    ApplyGrouping(SearchBox.Text);
            }
            else
            {
                _allEntries.Add(entry);
                ApplyGrouping(SearchBox.Text);
            }
        }

        private static void UpdateEntry(DownloadEntry target, DownloadEntry source)
        {
            target.FileName = source.FileName;
            target.Url = source.Url;
            target.FilePath = source.FilePath;
            target.TotalBytes = source.TotalBytes;
            target.ReceivedBytes = source.ReceivedBytes;
            target.Status = source.Status;
            target.StartTime = source.StartTime;
        }

        private void OnDownloadDeleted(long downloadId)
        {
            DownloadEntry? entry = _allEntries.FirstOrDefault(d => d.Id == downloadId);
            if (entry != null)
            {
                _ = _allEntries.Remove(entry);
                ApplyGrouping(SearchBox.Text);
            }
        }

        private string GetStatusCategory(DownloadStatus status) => status switch
        {
            DownloadStatus.Downloading or DownloadStatus.Paused or DownloadStatus.Cancelling => "Active",
            DownloadStatus.Finished or DownloadStatus.Failed or DownloadStatus.Cancelled => "Completed",
            _ => "Completed",
        };

        private List<DownloadGroup> GroupEntries(IEnumerable<DownloadEntry> entries)
        {
            string[] categoryOrder = ["Active", "Completed"];

            return [.. entries
                .GroupBy(e => GetStatusCategory(e.Status))
                .OrderBy(g => Array.IndexOf(categoryOrder, g.Key))
                .Select(g => new DownloadGroup(g.Key, [.. g.OrderByDescending(e => e.StartTime)]))];
        }

        private void ApplyGrouping(string? filter = null)
        {
            IEnumerable<DownloadEntry> entries = _allEntries;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                string lower = filter.ToLowerInvariant();
                entries = entries.Where(e =>
                    e.FileName.Contains(lower, StringComparison.InvariantCultureIgnoreCase) ||
                    e.Url.Contains(lower, StringComparison.InvariantCultureIgnoreCase));
            }

            List<DownloadGroup> grouped = GroupEntries(entries);

            _groupedSource = new CollectionViewSource
            {
                IsSourceGrouped = true,
                Source = grouped,
            };

            DownloadsListView.ItemsSource = _groupedSource.View;

            bool hasEntries = grouped.Count > 0;
            DownloadsListView.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                ApplyGrouping(sender.Text);
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            DownloadEntry[] finished = [.. _allEntries.Where(
                d => d.Status is not (DownloadStatus.Downloading or DownloadStatus.Paused))];

            long[] ids = [.. finished.Select(d => d.Id)];
            if (ids.Length > 0)
                OrionBackend.DeleteDownloads(ids);

            foreach (DownloadEntry entry in finished)
                _allEntries.Remove(entry);

            ApplyGrouping();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long id })
            {
                DownloadEntry? entry = _allEntries.FirstOrDefault(d => d.Id == id);
                if (entry != null)
                {
                    if (entry.Status is DownloadStatus.Downloading or DownloadStatus.Paused)
                        OrionBackend.CancelDownload(id);
                    OrionBackend.DeleteDownloads([id]);
                    _ = _allEntries.Remove(entry);
                    ApplyGrouping(SearchBox.Text);
                }
            }
        }

        private void DownloadItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: DownloadEntry entry }
                && entry.Status == DownloadStatus.Finished
                && !string.IsNullOrEmpty(entry.FilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(entry.FilePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    OrionBackend.LogWarning($"Failed to open file: {ex.Message}");
                }
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long id })
            {
                DownloadEntry? entry = _allEntries.FirstOrDefault(d => d.Id == id);
                if (entry != null && !string.IsNullOrEmpty(entry.FilePath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(entry.FilePath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        OrionBackend.LogWarning($"Failed to open file: {ex.Message}");
                    }
                }
            }
        }

        private void ShowInFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long id })
            {
                DownloadEntry? entry = _allEntries.FirstOrDefault(d => d.Id == id);
                if (entry != null && !string.IsNullOrEmpty(entry.FilePath))
                {
                    try
                    {
                        Process.Start("explorer.exe", $"/select,\"{entry.FilePath}\"");
                    }
                    catch (Exception ex)
                    {
                        OrionBackend.LogWarning($"Failed to show in folder: {ex.Message}");
                    }
                }
            }
        }

        private void CopyAddress_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long id })
            {
                DownloadEntry? entry = _allEntries.FirstOrDefault(d => d.Id == id);
                if (entry != null && !string.IsNullOrEmpty(entry.Url))
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(entry.Url);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                }
            }
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long id })
            {
                OrionBackend.RestartDownload(id);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long id })
            {
                OrionBackend.CancelDownload(id);
            }
        }

        // Static helper methods for x:Bind in DataTemplate
        public static Visibility IsActiveDownload(DownloadStatus status)
        {
            return status is DownloadStatus.Downloading or DownloadStatus.Paused
                ? Visibility.Visible : Visibility.Collapsed;
        }

        public static Visibility IsCompleted(DownloadStatus status) => status == DownloadStatus.Finished ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility IsRestartable(DownloadStatus status) =>
            status is DownloadStatus.Failed or DownloadStatus.Cancelled ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility IsRemovable(DownloadStatus status) =>
            status is not (DownloadStatus.Downloading or DownloadStatus.Cancelling) ? Visibility.Visible : Visibility.Collapsed;

        public static bool IsIndeterminate(long totalBytes, DownloadStatus status) =>
            totalBytes <= 0 && status == DownloadStatus.Downloading;

        public static string FormatDownloadSize(long receivedBytes, long totalBytes, DownloadStatus status)
        {
            if (status is DownloadStatus.Downloading or DownloadStatus.Paused)
            {
                if (totalBytes > 0)
                    return $"{FormatFileSize(receivedBytes)} / {FormatFileSize(totalBytes)}";
                return FormatFileSize(receivedBytes);
            }
            return FormatFileSize(totalBytes > 0 ? totalBytes : receivedBytes);
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        public static string FormatStatus(DownloadStatus status, double bytesPerSecond, long receivedBytes, long totalBytes)
        {
            if (status == DownloadStatus.Downloading && bytesPerSecond > 0)
            {
                string speed = $"{FormatFileSize((long)bytesPerSecond)}/s";
                if (totalBytes > receivedBytes)
                {
                    var eta = TimeSpan.FromSeconds((totalBytes - receivedBytes) / bytesPerSecond);
                    return $"{FormatEta(eta)} — {speed}";
                }
                return speed;
            }
            return status switch
            {
                DownloadStatus.Downloading => "Downloading...",
                DownloadStatus.Finished => "Completed",
                DownloadStatus.Failed => "Failed",
                DownloadStatus.Paused => "Paused",
                DownloadStatus.Cancelled => "Canceled",
                DownloadStatus.Cancelling => "Canceling...",
                _ => "",
            };
        }

        private static string FormatEta(TimeSpan t)
        {
            if (t.TotalDays >= 1)
                return $"{(int)t.TotalDays}d {t.Hours}h remaining";
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}h {t.Minutes}m remaining";
            if (t.TotalMinutes >= 1)
                return $"{(int)t.TotalMinutes}m {t.Seconds}s remaining";
            return $"{t.Seconds}s remaining";
        }
    }

    public class DownloadGroup(string key, IEnumerable<DownloadEntry> items) : List<DownloadEntry>(items)
    {
        public string Key { get; } = key;
    }
}
