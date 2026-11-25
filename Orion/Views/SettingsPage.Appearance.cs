using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private void LoadAppearanceSettings()
        {
            _isLoadingSettings = true;

            // settings::appearance::default_font_family — string, default: "Times New Roman"
            string defaultFontFamily = OrionBackend.GetStringSetting(ProfileId,
                "settings::appearance::default_font_family", "settings::appearance", "default_font_family", "Times New Roman");
            DefaultFontComboBox.SelectedItem = DefaultFontComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Content == defaultFontFamily);

            // settings::appearance::prefer_page_rendering_updates_near_120fps — bool, default: false
            Prefer120FpsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::prefer_page_rendering_updates_near_120fps", "settings::appearance",
                "prefer_page_rendering_updates_near_120fps", false);

            // settings::appearance::disable_transparency_for_fullscreen_windows — bool, default: true
            DisableTransparencyCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::disable_transparency_for_fullscreen_windows", "settings::appearance",
                "disable_transparency_for_fullscreen_windows", true);

            // settings::appearance::auto_show_sidebar_in_fullscreen — bool, default: true
            AutoShowSidebarCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::auto_show_sidebar_in_fullscreen", "settings::appearance", "auto_show_sidebar_in_fullscreen", true);

            // settings::appearance::bookmarks_bar_style — enum, default: text_only (1)
            int bookmarksBarStyle = OrionBackend.GetIntSetting(ProfileId,
                "settings::appearance::bookmarks_bar_style", "settings::appearance", "bookmarks_bar_style", 1);
            BookmarksBarComboBox.SelectedIndex = Math.Clamp(bookmarksBarStyle - 1, 0, 2);

            // settings::appearance::always_show_toolbar_in_fullscreen — bool, default: true
            AlwaysShowToolbarCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::always_show_toolbar_in_fullscreen", "settings::appearance", "always_show_toolbar_in_fullscreen", true);

            // settings::appearance::show_bookmarks_bar — bool, default: true
            ShowBookmarksBarCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::show_bookmarks_bar", "settings::appearance", "show_bookmarks_bar", true);

            // settings::appearance::display_rss_feeds_indicator — bool, default: true
            DisplayRssFeedsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::display_rss_feeds_indicator", "settings::appearance", "display_rss_feeds_indicator", true);

            // settings::appearance::display_web_application_indicator — bool, default: true
            DisplayWebAppCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::display_web_application_indicator", "settings::appearance", "display_web_application_indicator", true);

            // settings::appearance::show_website_color_for_compact_and_vertical_tabs — bool, default: false
            ShowWebsiteColorCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::appearance::show_website_color_for_compact_and_vertical_tabs", "settings::appearance",
                "show_website_color_for_compact_and_vertical_tabs", false);

            // settings::appearance::theme — enum, default: system (1)
            int theme = OrionBackend.GetIntSetting(ProfileId,
                "settings::appearance::theme", "settings::appearance", "theme", 1);
            ThemeComboBox.SelectedIndex = Math.Clamp(theme - 1, 0, 2);

            _isLoadingSettings = false;
        }

        private void DefaultFontFamily_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (DefaultFontComboBox.SelectedItem is ComboBoxItem item)
            {
                SaveString("settings::appearance::default_font_family", "settings::appearance", "default_font_family",
                    (string)item.Content, "Times New Roman");
            }
        }

        private void Prefer120Fps_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::appearance::prefer_page_rendering_updates_near_120fps", "settings::appearance",
                "prefer_page_rendering_updates_near_120fps", Prefer120FpsCheckBox.IsChecked == true, false);
        }

        private void DisableTransparency_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::appearance::disable_transparency_for_fullscreen_windows", "settings::appearance",
                "disable_transparency_for_fullscreen_windows", DisableTransparencyCheckBox.IsChecked == true, true);
        }

        private void AutoShowSidebar_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::appearance::auto_show_sidebar_in_fullscreen", "settings::appearance",
                "auto_show_sidebar_in_fullscreen", AutoShowSidebarCheckBox.IsChecked == true, true);
        }

        private void BookmarksBarStyle_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // enum value = index + 1 (text_only=1, icon_and_text=2, icon_only=3)
            SaveInt("settings::appearance::bookmarks_bar_style", "settings::appearance", "bookmarks_bar_style",
                BookmarksBarComboBox.SelectedIndex + 1, 1);
        }

        private void AlwaysShowToolbar_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::appearance::always_show_toolbar_in_fullscreen", "settings::appearance",
                "always_show_toolbar_in_fullscreen", AlwaysShowToolbarCheckBox.IsChecked == true, true);
        }

        private void ShowBookmarksBar_Changed(object _sender, RoutedEventArgs _e)
        {
            if (!_isLoadingSettings && MainViewModel != null)
                MainViewModel.ShowBookmarkBar = ShowBookmarksBarCheckBox.IsChecked == true;
        }

        private void DisplayRssFeeds_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::appearance::display_rss_feeds_indicator", "settings::appearance",
                "display_rss_feeds_indicator", DisplayRssFeedsCheckBox.IsChecked == true, true);
        }

        private void DisplayWebApp_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::appearance::display_web_application_indicator", "settings::appearance",
                "display_web_application_indicator", DisplayWebAppCheckBox.IsChecked == true, true);
        }

        private void ShowWebsiteColor_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::appearance::show_website_color_for_compact_and_vertical_tabs", "settings::appearance",
                "show_website_color_for_compact_and_vertical_tabs", ShowWebsiteColorCheckBox.IsChecked == true, false);
        }

        private void Theme_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // enum value = index + 1 (system=1, light=2, dark=3)
            SaveInt("settings::appearance::theme", "settings::appearance", "theme",
                ThemeComboBox.SelectedIndex + 1, 1);
        }

        private void ManageProfiles_Click(object _sender, RoutedEventArgs _e) => SelectCategory("Profiles");
    }
}
