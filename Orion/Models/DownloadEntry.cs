using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;

namespace Orion
{
    public enum DownloadStatus
    {
        Downloading = 0,
        Finished = 1,
        Paused = 2,
        Failed = 3,
        Cancelled = 4,
        Cancelling = 5,
    }

    public partial class DownloadEntry : ObservableObject
    {
        [ObservableProperty]
        public partial long Id { get; set; }

        [ObservableProperty]
        public partial long ProfileId { get; set; }

        [ObservableProperty]
        public partial string FileName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Url { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string FilePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial long TotalBytes { get; set; }

        [ObservableProperty]
        public partial long ReceivedBytes { get; set; }

        [ObservableProperty]
        public partial DownloadStatus Status { get; set; }

        [ObservableProperty]
        public partial DateTime StartTime { get; set; }

        [ObservableProperty]
        public partial double BytesPerSecond { get; set; }

        public double Progress => TotalBytes > 0 ? (double)ReceivedBytes / TotalBytes * 100.0 : 0;

        private long _prevReceivedBytes;
        private long _prevTimestamp;

        partial void OnReceivedBytesChanged(long value)
        {
            OnPropertyChanged(nameof(Progress));

            long now = Stopwatch.GetTimestamp();
            if (_prevTimestamp > 0 && value > _prevReceivedBytes)
            {
                double seconds = (double)(now - _prevTimestamp) / Stopwatch.Frequency;
                if (seconds > 0.1)
                {
                    double speed = (value - _prevReceivedBytes) / seconds;
                    // Smooth with exponential moving average
                    BytesPerSecond = BytesPerSecond > 0
                        ? BytesPerSecond * 0.7 + speed * 0.3
                        : speed;
                    _prevReceivedBytes = value;
                    _prevTimestamp = now;
                }
            }
            else
            {
                _prevReceivedBytes = value;
                _prevTimestamp = now;
            }
        }

        partial void OnTotalBytesChanged(long value) => OnPropertyChanged(nameof(Progress));

        partial void OnStatusChanged(DownloadStatus value)
        {
            if (value is not DownloadStatus.Downloading)
            {
                BytesPerSecond = 0;
                _prevTimestamp = 0;
            }
        }
    }
}
