using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private record WebsiteSettingDef(string Key, string KeyName, int DefaultValue, (string Label, int Value)[] Options);

        private static readonly Dictionary<string, WebsiteSettingDef> _websiteSettings = new()
        {
            ["Reader Mode"] = new("settings::websites::enable_reader_mode_default_setting_for_all_websites",
                "enable_reader_mode_default_setting_for_all_websites", 1,
                [("On", 1), ("Off", 2)]),
            ["Auto-Play"] = new("settings::websites::autoplay_default_setting_for_all_websites",
                "autoplay_default_setting_for_all_websites", 2,
                [("Allow All Auto-Play", 1), ("Stop Media with Sound", 2), ("Never Auto-Play", 3)]),
            ["Page Zoom"] = new("settings::websites::page_zoom_default_setting_for_all_websites",
                "page_zoom_default_setting_for_all_websites", 100,
                [("100%", 100), ("75%", 75), ("85%", 85), ("110%", 110), ("125%", 125), ("150%", 150), ("175%", 175), ("200%", 200)]),
            ["Pop-up Windows"] = new("settings::websites::popup_windows_default_setting_for_all_websites",
                "popup_windows_default_setting_for_all_websites", 2,
                [("Block and Notify", 3), ("Block", 2), ("Allow", 1)]),
            ["Notifications"] = new("settings::websites::notifications_default_setting_for_all_websites",
                "notifications_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 2), ("Don't Allow", 3)]),
            ["Downloads"] = new("settings::websites::downloads_default_setting_for_all_websites",
                "downloads_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 3), ("Deny", 2)]),
            ["Camera"] = new("settings::websites::camera_default_setting_for_all_websites",
                "camera_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 3), ("Deny", 2)]),
            ["Microphone"] = new("settings::websites::microphone_default_setting_for_all_websites",
                "microphone_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 3), ("Deny", 2)]),
            ["Screen Sharing"] = new("settings::websites::screen_sharing_default_setting_for_all_websites",
                "screen_sharing_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 3), ("Deny", 2)]),
            ["Location"] = new("settings::websites::location_default_setting_for_all_websites",
                "location_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 3), ("Deny", 2)]),
            ["Content Blockers"] = new("settings::websites::content_blockers_setting_for_all_websites",
                "content_blockers_setting_for_all_websites", 1,
                [("On", 1), ("Off", 2)]),
            ["Tracking Prevention"] = new("settings::websites::tracking_prevention_setting_for_all_websites",
                "tracking_prevention_setting_for_all_websites", 1,
                [("On", 1), ("Off", 2)]),
            ["Cookies"] = new("settings::websites::cookies_setting_for_all_websites",
                "cookies_setting_for_all_websites", 1,
                [("On", 1), ("Off", 2)]),
            ["JavaScript"] = new("settings::websites::javascript_setting_for_all_websites",
                "javascript_setting_for_all_websites", 1,
                [("On", 1), ("Off", 2)]),
            ["Web Fonts"] = new("settings::websites::web_fonts_setting_for_all_websites",
                "web_fonts_setting_for_all_websites", 1,
                [("On", 1), ("Off", 2)]),
            ["SSL Certificate Check"] = new("settings::websites::ssl_certificate_validation_setting_for_all_websites",
                "ssl_certificate_validation_setting_for_all_websites", 1,
                [("Strict", 1), ("Ignore", 2)]),
            ["User Agent"] = new("settings::websites::user_agent_setting_for_all_websites",
                "user_agent_setting_for_all_websites", 1,
                [("Automatic", 1), ("Custom", 2), ("Chrome", 3), ("Firefox", 4), ("Safari", 5), ("Safari (iPhone)", 6), ("Safari (iPad)", 7), ("Edge", 8)]),
            ["Compatibility Mode"] = new("settings::websites::compatibility_mode_setting_for_all_websites",
                "compatibility_mode_setting_for_all_websites", 2,
                [("On", 1), ("Off", 2)]),
            ["External Apps"] = new("settings::websites::external_apps_default_setting_for_all_websites",
                "external_apps_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 3), ("Deny", 2)]),
            ["Picture in Picture"] = new("settings::websites::picture_in_picture_default_setting_for_all_websites",
                "picture_in_picture_default_setting_for_all_websites", 1,
                [("Automatic", 1), ("Manual", 2)]),
            ["Clipboard"] = new("settings::websites::clipboard_default_setting_for_all_websites",
                "clipboard_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 2), ("Don't Allow", 3)]),
            ["Device Info"] = new("settings::websites::device_info_default_setting_for_all_websites",
                "device_info_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 2), ("Don't Allow", 3)]),
            ["Media Key System"] = new("settings::websites::media_key_system_default_setting_for_all_websites",
                "media_key_system_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 2), ("Don't Allow", 3)]),
            ["Pointer Lock"] = new("settings::websites::pointer_lock_default_setting_for_all_websites",
                "pointer_lock_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 2), ("Don't Allow", 3)]),
            ["Website Data Access"] = new("settings::websites::website_data_access_default_setting_for_all_websites",
                "website_data_access_default_setting_for_all_websites", 1,
                [("Ask", 1), ("Allow", 2), ("Don't Allow", 3)]),
        };

        private void WebsiteSettingsListView_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (WebsiteSettingsListView.SelectedItem is not ListViewItem item)
                return;

            // Hide all panels
            WebsiteSettingPlaceholder.Visibility = Visibility.Collapsed;
            WebsiteSettingDetailContent.Visibility = Visibility.Collapsed;
            WebsiteCustomUserAgentPanel.Visibility = Visibility.Collapsed;
            ConfiguredWebsitesHeader.Visibility = Visibility.Collapsed;
            ConfiguredWebsitesPanel.Visibility = Visibility.Collapsed;

            if (item.Content is not string content || content == "Extension Settings" || !_websiteSettings.TryGetValue(content, out WebsiteSettingDef? def))
            {
                _activeWebsiteSetting = null;
                return;
            }

            _isLoadingSettings = true;
            _activeWebsiteSetting = content;
            WebsiteSettingTitle.Text = content;
            WebsiteDefaultComboBox.Items.Clear();

            foreach ((string label, int _) in def.Options)
            {
                WebsiteDefaultComboBox.Items.Add(new ComboBoxItem { Content = label });
            }

            // Load current value and select matching option
            int currentValue = OrionBackend.GetIntSetting(ProfileId, def.Key, "settings::websites", def.KeyName, def.DefaultValue);
            int selectedIndex = Array.FindIndex(def.Options, o => o.Value == currentValue);
            WebsiteDefaultComboBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

            _isLoadingSettings = false;
            UpdateCustomUserAgentVisibility(currentValue);
            WebsiteSettingDetailContent.Visibility = Visibility.Visible;
            ConfiguredWebsitesHeader.Visibility = Visibility.Visible;
            ConfiguredWebsitesPanel.Visibility = Visibility.Visible;
        }

        private void WebsiteDefaultComboBox_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (_isLoadingSettings || _activeWebsiteSetting == null)
                return;

            if (!_websiteSettings.TryGetValue(_activeWebsiteSetting, out WebsiteSettingDef? def))
                return;

            int idx = WebsiteDefaultComboBox.SelectedIndex;
            if (idx < 0 || idx >= def.Options.Length)
                return;

            int value = def.Options[idx].Value;
            OrionBackend.SetIntSetting(ProfileId, def.Key, "settings::websites", def.KeyName, value, def.DefaultValue);

            if (_activeWebsiteSetting == "User Agent")
            {
                UpdateCustomUserAgentVisibility(value);
                MainViewModel?.ApplyUserAgentToAllViews();
            }
        }

        private void UpdateCustomUserAgentVisibility(int settingValue)
        {
            bool isCustom = _activeWebsiteSetting == "User Agent" && settingValue == 2;
            WebsiteCustomUserAgentPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (isCustom)
            {
                WebsiteCustomUserAgentTextBox.Text = OrionBackend.GetStringSetting(ProfileId,
                    "settings::privacy::custom_user_agent",
                    "settings::privacy", "custom_user_agent", "");
            }
        }

        private void WebsiteCustomUserAgent_TextChanged(object _sender, TextChangedEventArgs _e)
        {
            if (_isLoadingSettings) return;
            SaveString("settings::privacy::custom_user_agent", "settings::privacy", "custom_user_agent",
                WebsiteCustomUserAgentTextBox.Text, "");
            MainViewModel?.ApplyUserAgentToAllViews();
        }

        private async void AddConfiguredWebsite_Click(object _sender, RoutedEventArgs _e)
        {
            var textBox = new TextBox
            {
                PlaceholderText = "https://example.com",
            };

            var comboBox = new ComboBox
            {
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            foreach (ComboBoxItem item in WebsiteDefaultComboBox.Items.Cast<ComboBoxItem>())
            {
                comboBox.Items.Add(new ComboBoxItem { Content = item.Content });
            }
            comboBox.SelectedIndex = 0;

            var panel = new StackPanel();
            panel.Children.Add(textBox);
            panel.Children.Add(comboBox);

            var dialog = new ContentDialog
            {
                Title = "Add Website",
                Content = panel,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                ConfiguredWebsitesListView.Items.Add(new ListViewItem { Content = textBox.Text });
            }
        }
    }
}
