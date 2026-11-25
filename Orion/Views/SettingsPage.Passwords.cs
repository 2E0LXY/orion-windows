using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private void LoadPasswordsSettings()
        {
            _isLoadingSettings = true;

            // settings::passwords::password_provider — enum, default: orion_keychain (1)
            // ComboBox has only one item: index 0 = orion_keychain (1)
            int passwordProvider = OrionBackend.GetIntSetting(ProfileId,
                "settings::passwords::password_provider", "settings::passwords", "password_provider", 1);
            PasswordProviderComboBox.SelectedIndex = passwordProvider == 1 ? 0 : 0;

            // settings::passwords::offer_to_autofill_passwords_from_orion_keychain — bool, default: true
            OfferAutofillCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::passwords::offer_to_autofill_passwords_from_orion_keychain", "settings::passwords",
                "offer_to_autofill_passwords_from_orion_keychain", true);

            // settings::passwords::offer_to_save_passwords_to_orion_keychain — bool, default: true
            OfferSavePasswordsCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::passwords::offer_to_save_passwords_to_orion_keychain", "settings::passwords",
                "offer_to_save_passwords_to_orion_keychain", true);

            // settings::passwords::submit_form_automatically — bool, default: true
            SubmitFormAutomaticallyCheckBox.IsChecked = OrionBackend.GetBoolSetting(ProfileId,
                "settings::passwords::submit_form_automatically", "settings::passwords",
                "submit_form_automatically", true);

            // settings::passwords::use_touch_id — Apple-specific, no control on Windows

            _isLoadingSettings = false;
        }

        private void PasswordProvider_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            // Only one item currently: index 0 = orion_keychain (1)
            SaveInt("settings::passwords::password_provider", "settings::passwords", "password_provider", 1, 1);
        }

        private void OfferAutofill_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::passwords::offer_to_autofill_passwords_from_orion_keychain", "settings::passwords",
                "offer_to_autofill_passwords_from_orion_keychain", OfferAutofillCheckBox.IsChecked == true, true);
        }

        private void OfferSavePasswords_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::passwords::offer_to_save_passwords_to_orion_keychain", "settings::passwords",
                "offer_to_save_passwords_to_orion_keychain", OfferSavePasswordsCheckBox.IsChecked == true, true);
        }

        private void SubmitFormAutomatically_Changed(object _sender, RoutedEventArgs _e)
        {
            SaveBool("settings::passwords::submit_form_automatically", "settings::passwords",
                "submit_form_automatically", SubmitFormAutomaticallyCheckBox.IsChecked == true, true);
        }

        private void ManagePasswords_Click(object _sender, RoutedEventArgs _e)
        {
            LibraryWindow.ShowHistory("Passwords", ProfileId);
        }
    }
}
