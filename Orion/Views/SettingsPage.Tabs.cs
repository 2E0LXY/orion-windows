using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private void LoadTabsSettings()
        {
            _isLoadingSettings = true;

            // settings::tabs::use_mini_toolbar — bool, default: false
            UseMiniToolbarCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::use_mini_toolbar", "settings::tabs", "use_mini_toolbar", false);

            // settings::tabs::show_tab_preview_on_hover — bool, default: true
            ShowTabPreviewCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::show_tab_preview_on_hover", "settings::tabs", "show_tab_preview_on_hover", true);

            // settings::tabs::always_show_website_titles_in_tabs — bool, default: true
            AlwaysShowWebsiteTitlesCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::always_show_website_titles_in_tabs", "settings::tabs", "always_show_website_titles_in_tabs", true);

            // settings::tabs::show_favicons_inside_tabs — bool, default: true
            ShowFaviconsInTabsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::show_favicons_inside_tabs", "settings::tabs", "show_favicons_inside_tabs", true);

            // settings::tabs::close_window_with_last_tab — bool, default: true
            CloseWindowWithLastTabCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::close_window_with_last_tab", "settings::tabs", "close_window_with_last_tab", true);

            // settings::tabs::close_window_with_last_tab_when_pinned_tabs_present — bool, default: false
            WhenPinnedTabsPresentCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::close_window_with_last_tab_when_pinned_tabs_present", "settings::tabs", "close_window_with_last_tab_when_pinned_tabs_present", false);
            WhenPinnedTabsPresentCheckBox.IsEnabled = CloseWindowWithLastTabCheckBox.IsChecked == true;

            // settings::tabs::open_new_tab_next_to_current_tab — bool, default: false
            OpenNewTabNextToCurrentCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::open_new_tab_next_to_current_tab", "settings::tabs", "open_new_tab_next_to_current_tab", false);

            // settings::tabs::when_opening_link_in_new_tab_make_it_active — bool, default: false
            MakeNewTabActiveCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::when_opening_link_in_new_tab_make_it_active", "settings::tabs", "when_opening_link_in_new_tab_make_it_active", false);

            // settings::tabs::prevent_closing_pinned_tabs — bool, default: true
            PreventClosingPinnedTabsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::prevent_closing_pinned_tabs", "settings::tabs", "prevent_closing_pinned_tabs", true);

            // settings::tabs::auto_activate_pinned_tabs_on_loading — bool, default: false
            AutoActivatePinnedTabsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::auto_activate_pinned_tabs_on_loading", "settings::tabs", "auto_activate_pinned_tabs_on_loading", false);

            // settings::tabs::return_to_last_active_tab_when_closing_current_tab — bool, default: true
            ReturnToLastActiveTabCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::return_to_last_active_tab_when_closing_current_tab", "settings::tabs", "return_to_last_active_tab_when_closing_current_tab", true);

            // settings::tabs::use_ctrl_z_to_undo_close_tab — bool, default: true
            UndoCloseTabCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::use_ctrl_z_to_undo_close_tab", "settings::tabs", "use_ctrl_z_to_undo_close_tab", true);

            // settings::tabs::use_ctrl_1_to_9_to_switch_tabs — bool, default: true
            CtrlNumberSwitchTabsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::use_ctrl_1_to_9_to_switch_tabs", "settings::tabs", "use_ctrl_1_to_9_to_switch_tabs", true);

            // settings::tabs::show_nested_vertical_tabs — bool, default: true
            ShowNestedVerticalTabsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::show_nested_vertical_tabs", "settings::tabs", "show_nested_vertical_tabs", true);

            // settings::tabs::closing_expanded_tab_behavior — int, default: 0 (promote=0, convert_first_child=1, preserve_tree=2)
            ClosingExpandedTabComboBox.SelectedIndex = OrionBackend.GetIntSetting(ProfileId,
                "settings::tabs::closing_expanded_tab_behavior", "settings::tabs", "closing_expanded_tab_behavior", 0);

            // settings::tabs::use_tab_switcher_ui_for_ctrl_tab — bool, default: true
            UseTabSwitcherCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::tabs::use_tab_switcher_ui_for_ctrl_tab", "settings::tabs", "use_tab_switcher_ui_for_ctrl_tab", true);

            // settings::tabs::tab_layout — enum, default: standard (1); standard=1, compact=2, vertical=3
            int tabLayout = OrionBackend.GetIntSetting(ProfileId,
                "settings::tabs::tab_layout", "settings::tabs", "tab_layout", 1);
            StandardTabLayoutRadio.IsChecked = tabLayout == 1;
            CompactTabLayoutRadio.IsChecked = tabLayout == 2;
            VerticalTabLayoutRadio.IsChecked = tabLayout == 3;

            // settings::tabs::tab_switch_order — enum, default: tab_order_in_window (1)
            // ComboBox index 0 = most_recently_used_order (2), index 1 = tab_order_in_window (1)
            int tabSwitchOrder = OrionBackend.GetIntSetting(ProfileId,
                "settings::tabs::tab_switch_order", "settings::tabs", "tab_switch_order", 1);
            SwitchTabsUsingComboBox.SelectedIndex = tabSwitchOrder == 2 ? 0 : 1;

            _isLoadingSettings = false;
        }

        private void UseMiniToolbar_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::use_mini_toolbar", "settings::tabs",
                "use_mini_toolbar", UseMiniToolbarCheckBox.IsChecked == true, false);
        }

        private void ShowTabPreview_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::show_tab_preview_on_hover", "settings::tabs",
                "show_tab_preview_on_hover", ShowTabPreviewCheckBox.IsChecked == true, true);
        }

        private void CloseWindowWithLastTab_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::close_window_with_last_tab", "settings::tabs",
                "close_window_with_last_tab", CloseWindowWithLastTabCheckBox.IsChecked == true, true);

            if (WhenPinnedTabsPresentCheckBox is { } cb)
                cb.IsEnabled = CloseWindowWithLastTabCheckBox.IsChecked == true;
        }

        private void WhenPinnedTabsPresent_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::close_window_with_last_tab_when_pinned_tabs_present", "settings::tabs",
                "close_window_with_last_tab_when_pinned_tabs_present", WhenPinnedTabsPresentCheckBox.IsChecked == true, true);
        }

        private void OpenNewTabNextToCurrent_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::open_new_tab_next_to_current_tab", "settings::tabs",
                "open_new_tab_next_to_current_tab", OpenNewTabNextToCurrentCheckBox.IsChecked == true, false);
        }

        private void MakeNewTabActive_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::when_opening_link_in_new_tab_make_it_active", "settings::tabs",
                "when_opening_link_in_new_tab_make_it_active", MakeNewTabActiveCheckBox.IsChecked == true, false);
        }

        private void PreventClosingPinnedTabs_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::prevent_closing_pinned_tabs", "settings::tabs",
                "prevent_closing_pinned_tabs", PreventClosingPinnedTabsCheckBox.IsChecked == true, true);
        }

        private void AutoActivatePinnedTabs_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::auto_activate_pinned_tabs_on_loading", "settings::tabs",
                "auto_activate_pinned_tabs_on_loading", AutoActivatePinnedTabsCheckBox.IsChecked == true, false);
        }

        private void ReturnToLastActiveTab_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::return_to_last_active_tab_when_closing_current_tab", "settings::tabs",
                "return_to_last_active_tab_when_closing_current_tab", ReturnToLastActiveTabCheckBox.IsChecked == true, true);
        }

        private void UndoCloseTab_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::use_ctrl_z_to_undo_close_tab", "settings::tabs",
                "use_ctrl_z_to_undo_close_tab", UndoCloseTabCheckBox.IsChecked == true, true);
        }

        private void CtrlNumberSwitchTabs_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::use_ctrl_1_to_9_to_switch_tabs", "settings::tabs",
                "use_ctrl_1_to_9_to_switch_tabs", CtrlNumberSwitchTabsCheckBox.IsChecked == true, true);
        }

        private void ShowNestedVerticalTabs_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::show_nested_vertical_tabs", "settings::tabs",
                "show_nested_vertical_tabs", ShowNestedVerticalTabsCheckBox.IsChecked == true, true);

            if (!_isLoadingSettings && MainViewModel != null)
                MainViewModel.ShowNestedVerticalTabs = ShowNestedVerticalTabsCheckBox.IsChecked == true;
        }

        private void ClosingExpandedTab_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            SaveInt("settings::tabs::closing_expanded_tab_behavior", "settings::tabs", "closing_expanded_tab_behavior",
                ClosingExpandedTabComboBox?.SelectedIndex ?? 0, 0);
        }

        private void UseTabSwitcher_Changed(object _sender, RoutedEventArgs _e)
        {
            if (SwitchTabsUsingComboBox is { } switchTabsComboBox)
                switchTabsComboBox.IsEnabled = UseTabSwitcherCheckBox.IsChecked == true;

            SaveBool("settings::tabs::use_tab_switcher_ui_for_ctrl_tab", "settings::tabs",
                "use_tab_switcher_ui_for_ctrl_tab", UseTabSwitcherCheckBox.IsChecked == true, true);
        }

        private void AlwaysShowWebsiteTitles_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::always_show_website_titles_in_tabs", "settings::tabs",
                "always_show_website_titles_in_tabs", AlwaysShowWebsiteTitlesCheckBox.IsChecked == true, true);

            if (!_isLoadingSettings && MainViewModel != null)
                MainViewModel.ShowWebsiteTitlesInTabs = AlwaysShowWebsiteTitlesCheckBox.IsChecked == true;
        }

        private void ShowFaviconsInTabs_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::tabs::show_favicons_inside_tabs", "settings::tabs",
                "show_favicons_inside_tabs", ShowFaviconsInTabsCheckBox.IsChecked == true, true);

            if (!_isLoadingSettings && MainViewModel != null)
                MainViewModel.ShowFaviconsInTabs = ShowFaviconsInTabsCheckBox.IsChecked == true;
        }

        private void TabLayout_Changed(object _sender, RoutedEventArgs _e)
        {
            if (!_isLoadingSettings && MainViewModel != null)
            {
                if (VerticalTabLayoutRadio?.IsChecked == true)
                    MainViewModel.TabLayout = TabLayout.vertical;
                else if (CompactTabLayoutRadio?.IsChecked == true)
                    MainViewModel.TabLayout = TabLayout.compact;
                else
                    MainViewModel.TabLayout = TabLayout.standard;
            }
        }

        private void SwitchTabsUsing_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // index 0 = most_recently_used_order (2), index 1 = tab_order_in_window (1)
            SaveInt("settings::tabs::tab_switch_order", "settings::tabs", "tab_switch_order",
                SwitchTabsUsingComboBox?.SelectedIndex == 0 ? 2 : 1, 1);
        }
    }
}
