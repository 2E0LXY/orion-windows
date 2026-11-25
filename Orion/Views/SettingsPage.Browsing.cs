using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        // Single source for the minimum-font-size dropdown; the ComboBox is
        // populated from this (72 is testing-only — remove before release).
        private static readonly int[] MinimumFontSizeValues = [9, 10, 12, 14, 16, 18, 24, 72];

        private void LoadBrowsingSettings()
        {
            _isLoadingSettings = true;

            // settings::browsing::automatic_https_upgrade — bool, default: true
            AutomaticHttpsUpgradeCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::automatic_https_upgrade", "settings::browsing", "automatic_https_upgrade", true);

            // settings::browsing::show_full_website_address — bool, default: true
            ShowFullWebsiteAddressCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::show_full_website_address", "settings::browsing", "show_full_website_address", true);

            // settings::browsing::open_bookmarks_in_new_tabs — bool, default: false
            OpenBookmarksInNewTabsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::open_bookmarks_in_new_tabs", "settings::browsing", "open_bookmarks_in_new_tabs", false);

            // settings::browsing::switch_saved_windows_in_place — bool, default: true
            SwitchSavedWindowsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::switch_saved_windows_in_place", "settings::browsing", "switch_saved_windows_in_place", true);

            // settings::browsing::open_external_links_in_preview — bool, default: true
            OpenExternalLinksInPreviewCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::open_external_links_in_preview", "settings::browsing", "open_external_links_in_preview", true);

            // settings::browsing::show_standard_window_buttons_in_focus_mode — bool, default: true
            ShowStandardWindowButtonsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::show_standard_window_buttons_in_focus_mode", "settings::browsing", "show_standard_window_buttons_in_focus_mode", true);

            // settings::browsing::confirm_closing_tabs_when_picture_in_picture_is_video_is_playing — bool, default: true
            ConfirmPipClosingCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::confirm_closing_tabs_when_picture_in_picture_is_video_is_playing", "settings::browsing",
                "confirm_closing_tabs_when_picture_in_picture_is_video_is_playing", true);

            // settings::browsing::check_spelling_while_typing — bool, default: true
            CheckSpellingCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::check_spelling_while_typing", "settings::browsing", "check_spelling_while_typing", true);

            // settings::browsing::correct_spelling_automatically — bool, default: false
            CorrectSpellingCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::correct_spelling_automatically", "settings::browsing", "correct_spelling_automatically", false);

            // settings::browsing::enable_minimum_font_size — bool, default: true
            bool enableMinFont = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::enable_minimum_font_size", "settings::browsing", "enable_minimum_font_size", true);
            MinimumFontSizeCheckBox.IsChecked = enableMinFont;
            MinimumFontSizeComboBox.IsEnabled = enableMinFont;

            // settings::browsing::minimum_font_size — FontSize/int, default: 9
            int minFontSize = OrionBackend.GetIntSetting(ProfileId,
                "settings::browsing::minimum_font_size", "settings::browsing", "minimum_font_size", 9);
            MinimumFontSizeComboBox.ItemsSource = MinimumFontSizeValues;
            int fontSizeIndex = Array.IndexOf(MinimumFontSizeValues, minFontSize);
            MinimumFontSizeComboBox.SelectedIndex = fontSizeIndex >= 0 ? fontSizeIndex : 0;

            // settings::browsing::press_tab_to_highlight_each_item_on_web_page — bool, default: false
            PressTabToHighlightCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::press_tab_to_highlight_each_item_on_web_page", "settings::browsing",
                "press_tab_to_highlight_each_item_on_web_page", false);

            // settings::browsing::prevent_escape_from_exiting_fullscreen — bool, default: false
            PreventEscapeFullscreenCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::browsing::prevent_escape_from_exiting_fullscreen", "settings::browsing",
                "prevent_escape_from_exiting_fullscreen", false);

            _isLoadingSettings = false;
        }

        private void AutomaticHttpsUpgrade_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::automatic_https_upgrade", "settings::browsing",
                "automatic_https_upgrade", AutomaticHttpsUpgradeCheckBox.IsChecked == true, true);
        }

        private void ShowFullWebsiteAddress_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::show_full_website_address", "settings::browsing",
                "show_full_website_address", ShowFullWebsiteAddressCheckBox.IsChecked == true, true);
        }

        private void OpenBookmarksInNewTabs_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::open_bookmarks_in_new_tabs", "settings::browsing",
                "open_bookmarks_in_new_tabs", OpenBookmarksInNewTabsCheckBox.IsChecked == true, false);
        }

        private void SwitchSavedWindows_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::switch_saved_windows_in_place", "settings::browsing",
                "switch_saved_windows_in_place", SwitchSavedWindowsCheckBox.IsChecked == true, true);
        }

        private void OpenExternalLinksInPreview_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::open_external_links_in_preview", "settings::browsing",
                "open_external_links_in_preview", OpenExternalLinksInPreviewCheckBox.IsChecked == true, true);
        }

        private void ShowStandardWindowButtons_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::show_standard_window_buttons_in_focus_mode", "settings::browsing",
                "show_standard_window_buttons_in_focus_mode", ShowStandardWindowButtonsCheckBox.IsChecked == true, true);
        }

        private void ConfirmPipClosing_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::confirm_closing_tabs_when_picture_in_picture_is_video_is_playing", "settings::browsing",
                "confirm_closing_tabs_when_picture_in_picture_is_video_is_playing", ConfirmPipClosingCheckBox.IsChecked == true, true);
        }

        private void CheckSpelling_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::check_spelling_while_typing", "settings::browsing",
                "check_spelling_while_typing", CheckSpellingCheckBox.IsChecked == true, true);
        }

        private void CorrectSpelling_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::correct_spelling_automatically", "settings::browsing",
                "correct_spelling_automatically", CorrectSpellingCheckBox.IsChecked == true, false);
        }

        private void MinimumFontSize_Changed(object _sender, RoutedEventArgs _e)
        {
            if (MinimumFontSizeComboBox is { } fontSizeComboBox)
                fontSizeComboBox.IsEnabled = MinimumFontSizeCheckBox.IsChecked == true;

            SaveBool("settings::browsing::enable_minimum_font_size", "settings::browsing",
                "enable_minimum_font_size", MinimumFontSizeCheckBox.IsChecked == true, true);
        }

        private void MinimumFontSize_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            int value = MinimumFontSizeComboBox?.SelectedItem is int size ? size : 9;
            SaveInt("settings::browsing::minimum_font_size", "settings::browsing", "minimum_font_size", value, 9);
        }

        private void PressTabToHighlight_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::press_tab_to_highlight_each_item_on_web_page", "settings::browsing",
                "press_tab_to_highlight_each_item_on_web_page", PressTabToHighlightCheckBox.IsChecked == true, false);
        }

        private void PreventEscapeFullscreen_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::browsing::prevent_escape_from_exiting_fullscreen", "settings::browsing",
                "prevent_escape_from_exiting_fullscreen", PreventEscapeFullscreenCheckBox.IsChecked == true, false);
        }
    }
}
