using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Orion
{
    public sealed partial class MainWindowContent
    {
        /// <summary>
        /// Widest consumer is an overview tile (~400 logical px) at 200% DPI.
        /// </summary>
        private const int ThumbnailMaxPixelWidth = 1024;

        /// <summary>
        /// Requests a thumbnail capture for a tab whose content is currently
        /// visible: web tabs go through the engine snapshot seam (safe on
        /// switch-away — the capture is queued ahead of the hide), internal
        /// orion:// tabs render their XAML control (must still be visible
        /// when the async render runs, so call this at activation, not
        /// switch-away). Results land in BrowserTab.ThumbnailJpeg.
        /// </summary>
        private void RequestTabThumbnail(BrowserTab tab)
        {
            if (tab.IsInternalPage)
            {
                _ = CaptureInternalPageThumbnailAsync(tab);
            }
            else
            {
                ViewModel.FindTabViewModel(tab)?.NativeWebView?
                    .RequestSnapshot(ThumbnailMaxPixelWidth);
            }
        }

        /// <summary>
        /// Schedules an internal page capture once the control has rendered.
        /// Called when an internal tab becomes active — the XAML equivalent
        /// of the engines' switch-away policy, inverted because a collapsed
        /// control renders 0x0 while a visible one can capture any time.
        /// </summary>
        private void ScheduleInternalPageThumbnail(BrowserTab tab)
        {
            _ = DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () =>
                {
                    if (tab == ViewModel.ActiveTab && tab.IsInternalPage)
                        _ = CaptureInternalPageThumbnailAsync(tab);
                });
        }

        /// <summary>The XAML control displayed for an internal page URL.</summary>
        private FrameworkElement? InternalPageElementFor(string url)
        {
            if (url.StartsWith("orion://settings"))
                return SettingsPageContent;
            if (url.StartsWith("orion://downloads"))
                return DownloadsPageContent;
            if (BrowserTab.IsStartPageUrl(url))
                return StartPageContent;
            return null;
        }

        /// <summary>
        /// Renders an internal page's XAML control to a JPEG thumbnail.
        /// Only works while the control is loaded and visible — same
        /// constraint the engines impose, so the same capture policy applies.
        /// </summary>
        private async Task CaptureInternalPageThumbnailAsync(BrowserTab tab)
        {
            try
            {
                FrameworkElement? page = InternalPageElementFor(tab.Url);
                if (page is not { Visibility: Visibility.Visible })
                    return;

                // Render the content area grid, not the bare page control:
                // internal pages can have (partially) transparent backgrounds
                // that composite against the grid's theme background — a bare
                // control render leaves those pixels transparent, which JPEG
                // flattens to black.
                FrameworkElement element = ContentAreaGrid;
                if (element.ActualWidth < 1 || element.ActualHeight < 1)
                    return;

                double scale = Math.Min(
                    XamlRoot?.RasterizationScale ?? 1.0,
                    ThumbnailMaxPixelWidth / element.ActualWidth);
                int width = Math.Max(1, (int)(element.ActualWidth * scale));
                int height = Math.Max(1, (int)(element.ActualHeight * scale));

                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(element, width, height);
                if (bitmap.PixelWidth < 1 || bitmap.PixelHeight < 1)
                    return; // control was hidden before the render ran
                IBuffer pixels = await bitmap.GetPixelsAsync();

                using var stream = new InMemoryRandomAccessStream();
                var quality = new BitmapPropertySet
                {
                    ["ImageQuality"] = new BitmapTypedValue(0.6f, PropertyType.Single),
                };
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(
                    BitmapEncoder.JpegEncoderId, stream, quality);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight,
                    96, 96, pixels.ToArray());
                await encoder.FlushAsync();

                byte[] bytes = new byte[stream.Size];
                var reader = new DataReader(stream.GetInputStreamAt(0));
                _ = await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);
                tab.ThumbnailJpeg = bytes;
            }
            catch (Exception ex)
            {
                LogError($"Internal page thumbnail capture failed: {ex.Message}");
            }
        }
    }
}
