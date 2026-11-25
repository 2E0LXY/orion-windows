using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;

namespace Orion
{
    public partial class ReadingListEntry : ObservableObject
    {
        [ObservableProperty]
        public partial ImageSource? FaviconImage { get; set; }

        public async Task LoadFaviconAsync()
        {
            if (string.IsNullOrEmpty(Address))
                return;
            OrionBackend.FaviconResult? result = OrionBackend.GetFavicon(Address);
            if (result != null)
                FaviconImage = await BrowserTab.CreateFaviconImageAsync(result.Data, result.IconUrl);
        }

        [ObservableProperty]
        public partial ReadingListEntryId Id { get; set; }

        [ObservableProperty]
        public partial ProfileId ProfileId { get; set; }

        [ObservableProperty]
        public partial DateTime AddedOn { get; set; }

        [ObservableProperty]
        public partial string IconName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Address { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Referrer { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsUnread { get; set; } = true;

        [ObservableProperty]
        public partial bool IsSavedOffline { get; set; }

        [ObservableProperty]
        public partial string Description { get; set; } = string.Empty;
    }
}
