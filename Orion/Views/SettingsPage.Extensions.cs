using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Orion
{
    public sealed partial class SettingsPage
    {
        private readonly ObservableCollection<ExtensionEntry> _installedExtensions = [];

        private void LoadExtensionsSettings()
        {
            _isLoadingSettings = true;

            _installedExtensions.Clear();
            List<ExtensionEntry> extensions = OrionBackend.GetInstalledExtensions(MainViewModel!.ActiveProfileId);
            foreach (ExtensionEntry ext in extensions)
                _installedExtensions.Add(ext);

            InstalledExtensionsList.ItemsSource = _installedExtensions;
            NoExtensionsText.Visibility = _installedExtensions.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            _isLoadingSettings = false;
        }

        private void ExtensionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            if (sender is ToggleSwitch toggle && toggle.DataContext is ExtensionEntry ext)
                OrionBackend.SetExtensionEnabled(ext.Id, toggle.IsOn);
        }

        private void UninstallExtension_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string extensionId)
            {
                OrionBackend.UninstallExtension(extensionId);
                LoadExtensionsSettings();
            }
        }

        private void OpenPopularExtensions_Click(object sender, RoutedEventArgs e)
        {
            PopularExtensionsWindow.Show(profileId: MainViewModel!.ActiveProfileId);
        }
    }
}
