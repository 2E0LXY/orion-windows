using CommunityToolkit.Mvvm.ComponentModel;

namespace Orion
{
    public partial class ExtensionEntry : ObservableObject
    {
        [ObservableProperty]
        public partial string Id { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Version { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int ExtensionType { get; set; }

        [ObservableProperty]
        public partial bool IsEnabled { get; set; } = true;

        public string TypeLabel => ExtensionType switch
        {
            1 => "Firefox",
            2 => "Chrome",
            _ => "Unknown",
        };
    }
}
