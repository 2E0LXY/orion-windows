using System.Collections.Generic;
using Xunit;

namespace Orion
{
    public class DownloadEntryTests
    {
        [Fact]
        public void Progress_IsZero_WhenTotalBytesIsZero()
        {
            var entry = new DownloadEntry { TotalBytes = 0, ReceivedBytes = 0 };
            Assert.Equal(0.0, entry.Progress);
        }

        [Fact]
        public void Progress_IsHundred_WhenReceived_Equals_Total()
        {
            var entry = new DownloadEntry { TotalBytes = 1000, ReceivedBytes = 1000 };
            Assert.Equal(100.0, entry.Progress);
        }

        [Fact]
        public void Progress_IsFifty_WhenHalfReceived()
        {
            var entry = new DownloadEntry { TotalBytes = 1000, ReceivedBytes = 500 };
            Assert.Equal(50.0, entry.Progress);
        }

        [Fact]
        public void SetReceivedBytes_FiresPropertyChangedForReceivedBytesAndProgress()
        {
            var entry = new DownloadEntry { TotalBytes = 1000 };
            var raised = new List<string>();
            entry.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            entry.ReceivedBytes = 500;

            Assert.Contains(nameof(DownloadEntry.ReceivedBytes), raised);
            Assert.Contains(nameof(DownloadEntry.Progress), raised);
        }

        [Fact]
        public void SetTotalBytes_FiresPropertyChangedForTotalBytesAndProgress()
        {
            var entry = new DownloadEntry();
            var raised = new List<string>();
            entry.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            entry.TotalBytes = 2000;

            Assert.Contains(nameof(DownloadEntry.TotalBytes), raised);
            Assert.Contains(nameof(DownloadEntry.Progress), raised);
        }
    }
}
