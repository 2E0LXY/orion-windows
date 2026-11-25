using Microsoft.UI.Xaml;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private void LoadAdvancedSettings()
        {
            _isLoadingSettings = true;

            // settings::advanced::enable_json_formatting — bool, default: true
            EnableJsonFormattingCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::advanced::enable_json_formatting", "settings::advanced",
                "enable_json_formatting", true);

            // settings::advanced::allow_installation_of_3rd_party_chrome_extensions — bool, default: false
            AllowChromeExtensionsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::advanced::allow_installation_of_3rd_party_chrome_extensions", "settings::advanced",
                "allow_installation_of_3rd_party_chrome_extensions", false);

            // settings::advanced::allow_installation_of_3rd_party_firefox_extensions — bool, default: false
            AllowFirefoxExtensionsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::advanced::allow_installation_of_3rd_party_firefox_extensions", "settings::advanced",
                "allow_installation_of_3rd_party_firefox_extensions", false);

            _isLoadingSettings = false;
        }

        private void EnableJsonFormatting_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::advanced::enable_json_formatting", "settings::advanced",
                "enable_json_formatting", EnableJsonFormattingCheckBox.IsChecked == true, true);
        }

        private void AllowChromeExtensions_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::advanced::allow_installation_of_3rd_party_chrome_extensions", "settings::advanced",
                "allow_installation_of_3rd_party_chrome_extensions", AllowChromeExtensionsCheckBox.IsChecked == true, false);
        }

        private void AllowFirefoxExtensions_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::advanced::allow_installation_of_3rd_party_firefox_extensions", "settings::advanced",
                "allow_installation_of_3rd_party_firefox_extensions", AllowFirefoxExtensionsCheckBox.IsChecked == true, false);
        }
    }
}
