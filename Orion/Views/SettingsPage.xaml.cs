using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace Orion
{
    public sealed partial class SettingsPage : UserControl
    {
        private readonly ObservableCollection<Profile> _profiles = [];
        private Profile? _selectedProfile;
        private bool _isUpdatingUI;
        private bool _isLoadingSettings = true;
        private string? _activeWebsiteSetting;
        private bool _openEmojiPicker;

        public MainWindowViewModel? MainViewModel
        {
            get;
            set
            {
                field?.PropertyChanged -= MainViewModel_PropertyChanged;
                field = value;
                if (value != null)
                {
                    value.PropertyChanged += MainViewModel_PropertyChanged;
                    SelectCategory("General");
                }
            }
        }

        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.TabLayout))
            {
                _isLoadingSettings = true;
                StandardTabLayoutRadio.IsChecked = MainViewModel!.TabLayout == TabLayout.standard;
                CompactTabLayoutRadio.IsChecked = MainViewModel!.TabLayout == TabLayout.compact;
                VerticalTabLayoutRadio.IsChecked = MainViewModel!.TabLayout == TabLayout.vertical;
                _isLoadingSettings = false;
            }
        }

        public SettingsPage()
        {
            InitializeComponent();
            InitializeProfilesData();
            // Another profile's process can mutate the registry (create,
            // rename, delete, ...); keep this page's profile UI truthful.
            Loaded += (_, _) => App.ProfilesChangedRemotely += RefreshProfiles;
            Unloaded += (_, _) => App.ProfilesChangedRemotely -= RefreshProfiles;
        }

        /// <summary>
        /// Navigates the settings NavigationView to the specified category.
        /// </summary>
        public void SelectCategory(string? tag)
        {
            if (tag == null)
                return;

            foreach (object? item in SettingsNavigationView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag is string itemTag && itemTag == tag)
                {
                    SettingsNavigationView.SelectedItem = navItem;
                    break;
                }
            }
        }

        private void SettingsNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                // Hide all content panels
                GeneralContent.Visibility = Visibility.Collapsed;
                AppearanceContent.Visibility = Visibility.Collapsed;
                TabsContent.Visibility = Visibility.Collapsed;
                BrowsingContent.Visibility = Visibility.Collapsed;
                SyncContent.Visibility = Visibility.Collapsed;
                PasswordsContent.Visibility = Visibility.Collapsed;
                PrivacyContent.Visibility = Visibility.Collapsed;
                SearchContent.Visibility = Visibility.Collapsed;
                WebsitesGrid.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                ExtensionsContent.Visibility = Visibility.Collapsed;
                PlusContent.Visibility = Visibility.Collapsed;
                AboutContent.Visibility = Visibility.Collapsed;
                ProfilesContent.Visibility = Visibility.Collapsed;

                switch (tag)
                {
                    case "General":
                        GeneralContent.Visibility = Visibility.Visible;
                        LoadGeneralSettings();
                        break;
                    case "Appearance":
                        AppearanceContent.Visibility = Visibility.Visible;
                        LoadAppearanceSettings();
                        break;
                    case "Tabs":
                        TabsContent.Visibility = Visibility.Visible;
                        LoadTabsSettings();
                        break;
                    case "Browsing":
                        BrowsingContent.Visibility = Visibility.Visible;
                        LoadBrowsingSettings();
                        break;
                    case "Sync":
                        SyncContent.Visibility = Visibility.Visible;
                        LoadSyncSettings();
                        break;
                    case "Passwords":
                        PasswordsContent.Visibility = Visibility.Visible;
                        LoadPasswordsSettings();
                        break;
                    case "Privacy":
                        PrivacyContent.Visibility = Visibility.Visible;
                        LoadPrivacySettings();
                        break;
                    case "Search":
                        SearchContent.Visibility = Visibility.Visible;
                        LoadSearchSettings();
                        break;
                    case "Websites":
                        WebsitesGrid.Visibility = Visibility.Visible;
                        break;
                    case "Advanced":
                        AdvancedContent.Visibility = Visibility.Visible;
                        LoadAdvancedSettings();
                        break;
                    case "Extensions":
                        ExtensionsContent.Visibility = Visibility.Visible;
                        LoadExtensionsSettings();
                        break;
                    case "Plus":
                        PlusContent.Visibility = Visibility.Visible;
                        break;
                    case "About":
                        AboutVersionText.Text = "Version: " + OrionBackend.GetOrionVersion();
                        AboutContent.Visibility = Visibility.Visible;
                        break;
                    case "Profiles":
                        ProfilesContent.Visibility = Visibility.Visible;
                        if (_profiles.Count > 0 && ProfileListView.SelectedItem == null)
                            ProfileListView.SelectedIndex = 0;
                        break;
                    default:
                        throw new NotImplementedException($"Unhandled settings category tag: {tag}");
                }
            }
        }

        private long ProfileId => MainViewModel!.ActiveProfileId;

        private void SaveBool(string key, string realm, string keyName, bool value, bool defaultValue)
        {
            if (!_isLoadingSettings)
                OrionBackend.SetBoolSetting(ProfileId, key, realm, keyName, value, defaultValue);
        }

        private void SaveInt(string key, string realm, string keyName, int value, int defaultValue)
        {
            if (!_isLoadingSettings)
                OrionBackend.SetIntSetting(ProfileId, key, realm, keyName, value, defaultValue);
        }

        private void SaveString(string key, string realm, string keyName, string value, string defaultValue)
        {
            if (!_isLoadingSettings)
                OrionBackend.SetStringSetting(ProfileId, key, realm, keyName, value, defaultValue);
        }
    }
}
