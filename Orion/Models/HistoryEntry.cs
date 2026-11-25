using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Orion
{
    public partial class HistoryEntry : ObservableObject
    {
        [ObservableProperty]
        public partial HistoryEntryId Id { get; set; }

        [ObservableProperty]
        public partial ProfileId ProfileId { get; set; }

        [ObservableProperty]
        public partial DateTime VisitedAt { get; set; }

        [ObservableProperty]
        public partial string Website { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Address { get; set; } = string.Empty;
    }
}
