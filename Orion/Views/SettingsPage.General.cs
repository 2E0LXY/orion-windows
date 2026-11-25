using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private void LoadGeneralSettings()
        {
            _isLoadingSettings = true;

            // settings::general::app_opens_with — app-wide (common orion.db),
            // enum, default: all_non_private_windows_from_last_session
            var appOpensWith = (AppOpensWith)int.Parse(OrionBackend.GetAppSetting(
                "settings::general::app_opens_with",
                ((int)AppOpensWith.AllNonPrivateWindowsFromLastSession).ToString()));
            OrionOpensWithComboBox.SelectedIndex = Math.Clamp((int)appOpensWith - 1, 0, 3);

            // settings::general::new_windows_open_with — enum, default: start_page (1)
            int newWindowsOpenWith = OrionBackend.GetIntSetting(ProfileId,
                "settings::general::new_windows_open_with", "settings::general", "new_windows_open_with", 1);
            NewWindowsOpenWithComboBox.SelectedIndex = Math.Clamp(newWindowsOpenWith - 1, 0, 4);

            // settings::general::new_tabs_open_with — enum, default: start_page (1)
            int newTabsOpenWith = OrionBackend.GetIntSetting(ProfileId,
                "settings::general::new_tabs_open_with", "settings::general", "new_tabs_open_with", 1);
            NewTabsOpenWithComboBox.SelectedIndex = Math.Clamp(newTabsOpenWith - 1, 0, 4);

            // settings::general::homepage — string, default: "https://kagi.com/"
            HomepageTextBox.Text = OrionBackend.GetStringSetting(ProfileId,
                "settings::general::homepage", "settings::general", "homepage", "https://kagi.com/");

            // settings::general::file_download_location — enum: downloads=1, ask_for_each_download=2, custom_path=3
            int fileDownloadLocation = OrionBackend.GetIntSetting(ProfileId,
                "settings::general::file_download_location", "settings::general", "file_download_location", 2);
            FileDownloadLocationComboBox.SelectedIndex = Math.Clamp(fileDownloadLocation - 1, 0, 2);
            UpdateCustomDownloadPathDisplay(fileDownloadLocation);

            // settings::general::remove_download_items_mode — enum, default: manually (4)
            int removeDownloadItems = OrionBackend.GetIntSetting(ProfileId,
                "settings::general::remove_download_items_mode", "settings::general", "remove_download_items_mode", 4);
            RemoveDownloadItemsComboBox.SelectedIndex = Math.Clamp(removeDownloadItems - 1, 0, 3);

            // settings::general::language — enum, default: english (6)
            int language = OrionBackend.GetIntSetting(ProfileId,
                "settings::general::language", "settings::general", "language", 6);
            LanguageComboBox.SelectedIndex = Math.Clamp(language - 1, 0, 21);

            // settings::general::automatically_keep_app_up_to_date — bool, default: false
            AutoUpdateCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::general::automatically_keep_app_up_to_date", "settings::general", "automatically_keep_app_up_to_date", false);

            // settings::general::show_warning_before_quitting — bool, default: true
            ShowWarningBeforeQuittingCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::general::show_warning_before_quitting", "settings::general", "show_warning_before_quitting", true);

            // settings::general::open_external_links_in — enum, default: last_active_profile (1)
            int openExternalLinksIn = OrionBackend.GetIntSetting(ProfileId,
                "settings::general::open_external_links_in", "settings::general", "open_external_links_in", 1);
            OpenExternalLinksComboBox.SelectedIndex = Math.Clamp(openExternalLinksIn - 1, 0, 1);

            _isLoadingSettings = false;
        }

        private void OrionOpensWith_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (_isLoadingSettings)
                return;
            OrionBackend.SetAppSetting("settings::general::app_opens_with",
                (OrionOpensWithComboBox.SelectedIndex + 1).ToString(), "integer");
        }

        private void NewWindowsOpenWith_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // enum value = index + 1 (start_page=1 .. notes=5)
            SaveInt("settings::general::new_windows_open_with", "settings::general", "new_windows_open_with",
                NewWindowsOpenWithComboBox.SelectedIndex + 1, 1);
        }

        private void NewTabsOpenWith_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // enum value = index + 1 (start_page=1 .. notes=5)
            SaveInt("settings::general::new_tabs_open_with", "settings::general", "new_tabs_open_with",
                NewTabsOpenWithComboBox.SelectedIndex + 1, 1);
        }

        private void AutoUpdate_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::general::automatically_keep_app_up_to_date", "settings::general",
                "automatically_keep_app_up_to_date", AutoUpdateCheckBox.IsChecked == true, false);
        }

        private void ShowWarningBeforeQuitting_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::general::show_warning_before_quitting", "settings::general",
                "show_warning_before_quitting", ShowWarningBeforeQuittingCheckBox.IsChecked == true, true);
        }

        private void OpenExternalLinksIn_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // enum value = index + 1 (last_active_profile=1, default_profile=2)
            SaveInt("settings::general::open_external_links_in", "settings::general", "open_external_links_in",
                OpenExternalLinksComboBox.SelectedIndex + 1, 1);
        }

        private void Language_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // enum value = index + 1 (german=1 .. polish_poland=22)
            SaveInt("settings::general::language", "settings::general", "language",
                LanguageComboBox.SelectedIndex + 1, 6);
        }

        private void RemoveDownloadItems_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // enum value = index + 1 (after_1_day=1 .. manually=4)
            SaveInt("settings::general::remove_download_items_mode", "settings::general", "remove_download_items_mode",
                RemoveDownloadItemsComboBox.SelectedIndex + 1, 4);
        }

        private async void FileDownloadLocation_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (_isLoadingSettings) return;

            // Dropdown index → enum: Downloads=1, Ask=2, Other...=3
            int value = FileDownloadLocationComboBox.SelectedIndex + 1;

            if (value == 3)
            {
                var picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.Downloads
                };
                picker.FileTypeFilter.Add("*");

                MainWindow? window = MainWindow.AllWindows.FirstOrDefault();
                if (window == null) return;
                nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                StorageFolder? folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    SaveString("settings::general::file_download_custom_path", "settings::general",
                        "file_download_custom_path", folder.Path, "");
                    SaveInt("settings::general::file_download_location", "settings::general",
                        "file_download_location", 3, 2);
                    UpdateCustomDownloadPathDisplay(3);
                }
                else
                {
                    // User cancelled — revert to previous selection
                    _isLoadingSettings = true;
                    int current = OrionBackend.GetIntSetting(ProfileId,
                        "settings::general::file_download_location", "settings::general", "file_download_location", 2);
                    FileDownloadLocationComboBox.SelectedIndex = Math.Clamp(current - 1, 0, 2);
                    _isLoadingSettings = false;
                }
                return;
            }

            SaveInt("settings::general::file_download_location", "settings::general",
                "file_download_location", value, 2);
            UpdateCustomDownloadPathDisplay(value);
        }

        private void UpdateCustomDownloadPathDisplay(int fileDownloadLocation)
        {
            if (fileDownloadLocation == 3)
            {
                string customPath = OrionBackend.GetStringSetting(ProfileId,
                    "settings::general::file_download_custom_path", "settings::general", "file_download_custom_path", "");
                if (!string.IsNullOrEmpty(customPath))
                {
                    CustomDownloadPathText.Text = customPath;
                    CustomDownloadPathText.Visibility = Visibility.Visible;
                    return;
                }
            }
            CustomDownloadPathText.Visibility = Visibility.Collapsed;
        }

        private void Homepage_TextChanged(object _sender, TextChangedEventArgs _e)
        {
            SaveString("settings::general::homepage", "settings::general", "homepage",
                HomepageTextBox.Text, "https://kagi.com/");
        }

        private void SetDefaultBrowser_Click(object _sender, RoutedEventArgs _e)
        {
            // TODO: Set Orion as default browser via Windows settings
        }
    }
}
