using Microsoft.UI.Xaml;
using System;
using Windows.Storage.Pickers;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private bool _isSyncImport;

        private void LoadSyncSettings()
        {
            _isLoadingSettings = true;

            EnableCrossDeviceSyncCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::sync::enable_cross_device_sync_of_tabs_bookmarks_and_reading_list", "settings::sync",
                "enable_cross_device_sync_of_tabs_bookmarks_and_reading_list", true);

            _isLoadingSettings = false;
        }

        private void EnableCrossDeviceSync_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::sync::enable_cross_device_sync_of_tabs_bookmarks_and_reading_list", "settings::sync",
                "enable_cross_device_sync_of_tabs_bookmarks_and_reading_list",
                EnableCrossDeviceSyncCheckBox.IsChecked == true, true);
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Utils.GetWindowHandle(this));

            Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;

            _isSyncImport = false;
            SetSyncInProgress(true);
            OrionBackend.ExportProfileData(ProfileId, folder.Path, OnSyncProgress, OnSyncCompletion);
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Utils.GetWindowHandle(this));

            Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;

            _isSyncImport = true;
            SetSyncInProgress(true);
            OrionBackend.ImportProfileData(ProfileId, folder.Path, OnSyncProgress, OnSyncCompletion);
        }

        private void CancelSyncButton_Click(object sender, RoutedEventArgs e)
        {
            OrionBackend.CancelSync();
        }

        private void OnSyncProgress(int done, int total, string description)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SyncProgressBar.Maximum = total;
                SyncProgressBar.Value = done;
                SyncStatusText.Text = description;
            });
        }

        private void OnSyncCompletion(int success, string? errorMessage)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                SetSyncInProgress(false);

                if (success != 0)
                {
                    string title = _isSyncImport ? "Import Complete" : "Export Complete";
                    string message = _isSyncImport
                        ? "Import successful. Restart Orion to see imported windows and tabs."
                        : "Export successful.";
                    await PopupDialog.ShowMessageAsync(this, title, message);
                }
                else
                {
                    await PopupDialog.ShowMessageAsync(this, "Sync Error", errorMessage ?? "Unknown error");
                }
            });
        }

        private void SetSyncInProgress(bool inProgress)
        {
            ExportButton.IsEnabled = !inProgress;
            ImportButton.IsEnabled = !inProgress;
            CancelSyncButton.Visibility = inProgress ? Visibility.Visible : Visibility.Collapsed;
            SyncProgressPanel.Visibility = inProgress ? Visibility.Visible : Visibility.Collapsed;
            if (inProgress)
            {
                SyncProgressBar.Value = 0;
                SyncStatusText.Text = "";
            }
        }
    }
}
