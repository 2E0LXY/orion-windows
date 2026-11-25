using System;

namespace Orion
{
    /// <summary>
    /// No-op stub replacing the real NativeWebView for unit tests.
    /// The real class calls OrionBackend P/Invoke; this one does nothing.
    /// </summary>
    public class NativeWebView : IDisposable
    {

        public void GoBack() { }
        public void GoForward() { }
        public void Reload() { }
        public void ReloadBypassingCache() { }
        public void ToggleDevTools() { }
#pragma warning disable IDE0060 // Remove unused parameter
        public void LoadUrl(string url) { }
        public void Resize(int width, int height) { }
        public void SetUserAgent(string? userAgent) { }
        public void Dispose() { }
    }
}
