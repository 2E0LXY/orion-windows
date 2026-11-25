using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private void LoadPrivacySettings()
        {
            _isLoadingSettings = true;

            // settings::privacy::remove_trackers_from_urls_mode — enum, default: for_private_browsing_only (1)
            // ComboBox: index 0=Always(2), index 1=In private browsing(1), index 2=Never(0/not_set)
            int removeTrackers = OrionBackend.GetIntSetting(ProfileId,
                "settings::privacy::remove_trackers_from_urls_mode", "settings::privacy",
                "remove_trackers_from_urls_mode", 1);
            RemoveTrackersComboBox.SelectedIndex = removeTrackers switch { 2 => 0, 1 => 1, _ => 2 };

            // settings::privacy::remove_history_items_mode — enum, default: manually (9)
            // ComboBox: index 0=after_1_day(1), 1=after_1_week(2), 2=after_1_month(4), 3=after_1_year(7), 4=manually(9)
            int removeHistory = OrionBackend.GetIntSetting(ProfileId,
                "settings::privacy::remove_history_items_mode", "settings::privacy",
                "remove_history_items_mode", 9);
            RemoveHistoryItemsComboBox.SelectedIndex = removeHistory switch
            {
                1 => 0,
                2 => 1,
                4 => 2,
                7 => 3,
                _ => 4,
            };

            // settings::privacy::delete_cookies_mode — enum, default: manually (9)
            // ComboBox: index 0=Never/manually(9), 1=On quit/after_app_exits(8), 2=after_1_day(1), 3=after_1_week(2), 4=after_1_month(4)
            int deleteCookies = OrionBackend.GetIntSetting(ProfileId,
                "settings::privacy::delete_cookies_mode", "settings::privacy",
                "delete_cookies_mode", 9);
            AutoDeleteCookiesComboBox.SelectedIndex = deleteCookies switch
            {
                8 => 1,
                1 => 2,
                2 => 3,
                4 => 4,
                _ => 0,
            };

            // settings::privacy::disable_history — bool, default: false
            DisableHistoryCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::privacy::disable_history", "settings::privacy", "disable_history", false);

            // settings::privacy::share_crash_reports — enum, default: after_asking_for_approval (1)
            // after_asking_for_approval=1, always_without_asking=2, never=3
            int shareCrash = OrionBackend.GetIntSetting(ProfileId,
                "settings::privacy::share_crash_reports", "settings::privacy", "share_crash_reports", 1);
            CrashReportsAskRadio.IsChecked = shareCrash == 1;
            CrashReportsAlwaysRadio.IsChecked = shareCrash == 2;
            CrashReportsNeverRadio.IsChecked = shareCrash == 3;

            // settings::privacy::custom_user_agent — string, default: "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"
            CustomUserAgentTextBox.Text = OrionBackend.GetStringSetting(ProfileId,
                "settings::privacy::custom_user_agent", "settings::privacy", "custom_user_agent",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)");

            _isLoadingSettings = false;
        }

        private void RemoveTrackers_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // index 0=Always(for_all_browsing=2), index 1=In private browsing(1), index 2=Never(not_set=0)
            SaveInt("settings::privacy::remove_trackers_from_urls_mode", "settings::privacy",
                "remove_trackers_from_urls_mode", RemoveTrackersComboBox.SelectedIndex switch { 0 => 2, 1 => 1, _ => 0 }, 1);
        }

        private void RemoveHistoryItems_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            int value = RemoveHistoryItemsComboBox.SelectedIndex switch
            {
                0 => 1,
                1 => 2,
                2 => 4,
                3 => 7,
                _ => 9,
            };
            SaveInt("settings::privacy::remove_history_items_mode", "settings::privacy",
                "remove_history_items_mode", value, 9);
        }

        private void AutoDeleteCookies_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            int value = AutoDeleteCookiesComboBox.SelectedIndex switch
            {
                1 => 8,
                2 => 1,
                3 => 2,
                4 => 4,
                _ => 9,
            };
            SaveInt("settings::privacy::delete_cookies_mode", "settings::privacy",
                "delete_cookies_mode", value, 9);
        }

        private void DisableHistory_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::privacy::disable_history", "settings::privacy",
                "disable_history", DisableHistoryCheckBox.IsChecked == true, false);
        }

        private void ShareCrashReports_Changed(object _sender, RoutedEventArgs _e)
        {
            // after_asking_for_approval=1, always_without_asking=2, never=3
            int value;
            if (CrashReportsAlwaysRadio?.IsChecked == true)
                value = 2;
            else if (CrashReportsNeverRadio?.IsChecked == true)
                value = 3;
            else
                value = 1;
            SaveInt("settings::privacy::share_crash_reports", "settings::privacy",
                "share_crash_reports", value, 1);
        }

        private void CustomUserAgent_TextChanged(object _sender, TextChangedEventArgs _e)
        {
            SaveString("settings::privacy::custom_user_agent", "settings::privacy", "custom_user_agent",
                CustomUserAgentTextBox.Text, "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)");
        }
    }
}
