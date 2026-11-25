using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        // Both ComboBoxes list SearchEngines.All in table order; the
        // private-browsing box has an extra first row "Default (same as
        // above)" = SearchEngine.none, so its indexes are offset by one.
        private void PopulateSearchEngineComboBoxes()
        {
            if (SearchEngineComboBox.Items.Count > 0)
                return;
            PrivateBrowsingSearchEngineComboBox.Items.Add(new ComboBoxItem { Content = "Default (same as above)" });
            foreach (SearchEngine engine in SearchEngines.All)
            {
                SearchEngineComboBox.Items.Add(new ComboBoxItem { Content = SearchEngines.Get(engine).Name });
                PrivateBrowsingSearchEngineComboBox.Items.Add(new ComboBoxItem { Content = SearchEngines.Get(engine).Name });
            }
        }

        private void LoadSearchSettings()
        {
            _isLoadingSettings = true;

            PopulateSearchEngineComboBoxes();

            // settings::search::search_engine — enum, default: kagi
            int searchEngine = OrionBackend.GetIntSetting(ProfileId,
                "settings::search::search_engine", "settings::search", "search_engine", (int)SearchEngine.kagi);
            int seIdx = Array.IndexOf(SearchEngines.All, (SearchEngine)searchEngine);
            SearchEngineComboBox.SelectedIndex = seIdx >= 0 ? seIdx : 0;

            // settings::search::also_use_search_engine_in_private_browsing — bool, default: false
            AlsoUseInPrivateBrowsingCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::search::also_use_search_engine_in_private_browsing", "settings::search",
                "also_use_search_engine_in_private_browsing", false);

            // settings::search::private_browsing_search_engine — enum, default: none
            int privateEngine = OrionBackend.GetIntSetting(ProfileId,
                "settings::search::private_browsing_search_engine", "settings::search",
                "private_browsing_search_engine", (int)SearchEngine.none);
            int peIdx = Array.IndexOf(SearchEngines.All, (SearchEngine)privateEngine);
            PrivateBrowsingSearchEngineComboBox.SelectedIndex = peIdx >= 0 ? peIdx + 1 : 0;

            // settings::search::autocomplete_top_hits — bool, default: true
            AutocompleteTopHitsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::search::autocomplete_top_hits", "settings::search", "autocomplete_top_hits", true);

            // settings::search::autocomplete_search_engines — bool, default: true
            AutocompleteSearchEnginesCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::search::autocomplete_search_engines", "settings::search", "autocomplete_search_engines", true);

            // settings::search::autocomplete_ask_fastgpt — bool, default: true
            AutocompleteAskFastgptCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::search::autocomplete_ask_fastgpt", "settings::search", "autocomplete_ask_fastgpt", true);

            // settings::search::autocomplete_history — bool, default: true
            AutocompleteHistoryCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::search::autocomplete_history", "settings::search", "autocomplete_history", true);

            // settings::search::autocomplete_bookmarks — bool, default: true
            AutocompleteBookmarksCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::search::autocomplete_bookmarks", "settings::search", "autocomplete_bookmarks", true);

            // settings::search::autocomplete_open_tabs — bool, default: true
            AutocompleteOpenTabsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::search::autocomplete_open_tabs", "settings::search", "autocomplete_open_tabs", true);

            _isLoadingSettings = false;
        }

        private void SearchEngine_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            int idx = SearchEngineComboBox?.SelectedIndex ?? 0;
            int value = (int)(idx >= 0 && idx < SearchEngines.All.Length ? SearchEngines.All[idx] : SearchEngine.kagi);
            SaveInt("settings::search::search_engine", "settings::search", "search_engine", value, (int)SearchEngine.kagi);
        }

        private void AlsoUseInPrivateBrowsing_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::search::also_use_search_engine_in_private_browsing", "settings::search",
                "also_use_search_engine_in_private_browsing",
                AlsoUseInPrivateBrowsingCheckBox.IsChecked == true, false);
        }

        private void PrivateBrowsingSearchEngine_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // Row 0 is "Default (same as above)" = none; engine rows are offset by one.
            int idx = PrivateBrowsingSearchEngineComboBox?.SelectedIndex ?? 0;
            int value = (int)(idx >= 1 && idx <= SearchEngines.All.Length ? SearchEngines.All[idx - 1] : SearchEngine.none);
            SaveInt("settings::search::private_browsing_search_engine", "settings::search",
                "private_browsing_search_engine", value, (int)SearchEngine.none);
        }

        private void AutocompleteTopHits_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::search::autocomplete_top_hits", "settings::search",
                "autocomplete_top_hits", AutocompleteTopHitsCheckBox.IsChecked == true, true);
        }

        private void AutocompleteSearchEngines_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::search::autocomplete_search_engines", "settings::search",
                "autocomplete_search_engines", AutocompleteSearchEnginesCheckBox.IsChecked == true, true);
        }

        private void AutocompleteAskFastgpt_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::search::autocomplete_ask_fastgpt", "settings::search",
                "autocomplete_ask_fastgpt", AutocompleteAskFastgptCheckBox.IsChecked == true, true);
        }

        private void AutocompleteHistory_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::search::autocomplete_history", "settings::search",
                "autocomplete_history", AutocompleteHistoryCheckBox.IsChecked == true, true);
        }

        private void AutocompleteBookmarks_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::search::autocomplete_bookmarks", "settings::search",
                "autocomplete_bookmarks", AutocompleteBookmarksCheckBox.IsChecked == true, true);
        }

        private void AutocompleteOpenTabs_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::search::autocomplete_open_tabs", "settings::search",
                "autocomplete_open_tabs", AutocompleteOpenTabsCheckBox.IsChecked == true, true);
        }
    }
}
