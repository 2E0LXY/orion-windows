using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Text.Json.Serialization;

namespace Orion
{
    public class ExtensionInfoData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("info")]
        public string? Info { get; set; }

        [JsonPropertyName("iconURL")]
        public string? IconURL { get; set; }

        [JsonPropertyName("installURL")]
        public string? InstallURL { get; set; }

        [JsonPropertyName("webExtID")]
        public string? WebExtID { get; set; }

        [JsonPropertyName("webExtType")]
        public string? WebExtType { get; set; }

        [JsonPropertyName("geckoId")]
        public string? GeckoId { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("orionHosted")]
        public bool? OrionHosted { get; set; }
    }

    public class ExtensionListData
    {
        [JsonPropertyName("passwordManagers")]
        public ExtensionInfoData[]? PasswordManagers { get; set; }

        [JsonPropertyName("adBlockers")]
        public ExtensionInfoData[]? AdBlockers { get; set; }

        [JsonPropertyName("browserBetter")]
        public ExtensionInfoData[]? BrowserBetter { get; set; }
    }

    public partial class PopularExtensionItem(ExtensionInfoData data) : ObservableObject
    {
        public ExtensionInfoData Data { get; } = data;

        public string CategoryGlyph { get; init; } = "\uE734";

        public Action<PopularExtensionItem>? InstallAction { get; init; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ButtonText))]
        [NotifyPropertyChangedFor(nameof(ButtonEnabled))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        public partial bool IsInstalled { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ButtonText))]
        [NotifyPropertyChangedFor(nameof(ButtonEnabled))]
        [NotifyPropertyChangedFor(nameof(InstallingVisibility))]
        [NotifyPropertyChangedFor(nameof(NotInstallingVisibility))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        public partial bool IsInstalling { get; set; }

        [ObservableProperty]
        public partial double DownloadProgress { get; set; }

        public string Label => Data.DisplayName ?? Data.Name ?? "";
        public string Description => Data.Info ?? "";
        public string TypeVersion
        {
            get
            {
                string type = (Data.WebExtType ?? "") switch
                {
                    "chrome" => "Chrome",
                    "firefox" => "Firefox",
                    _ => "",
                };
                return string.IsNullOrEmpty(Data.Version) ? type : $"{type} - {Data.Version}";
            }
        }

        public bool CanInstall => !string.IsNullOrEmpty(Data.InstallURL);
        public string ButtonText
        {
            get
            {
                if (IsInstalling) return "Installing...";
                if (IsInstalled) return "Installed";
                return "Get";
            }
        }
        public bool ButtonEnabled => !IsInstalled && !IsInstalling && CanInstall;
        public Visibility InstallingVisibility => IsInstalling ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NotInstallingVisibility => IsInstalling ? Visibility.Collapsed : Visibility.Visible;

        [RelayCommand(CanExecute = nameof(ButtonEnabled))]
        private void Install() => InstallAction?.Invoke(this);
    }
}
