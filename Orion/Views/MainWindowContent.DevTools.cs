namespace Orion
{
    /// <summary>
    /// DevTools pane wiring: each tab's frontend HWND, reported by the engine
    /// through its NativeWebView, is registered in the pane, which shows the
    /// active tab's one.
    /// </summary>
    public sealed partial class MainWindowContent
    {
        private void InitializeDevToolsPane()
        {
            DevToolsPane.CloseRequested += () => ViewModel.ActiveTabViewModel?.NativeWebView?.CloseDevTools();
        }

        private void ToggleDevTools() => ViewModel.ActiveTabViewModel?.ToggleDevToolsCommand.Execute(null);

        private void OnDevToolsViewChanged(BrowserTab tab, Win32.HWND previous, Win32.HWND current)
        {
            if (previous != Win32.HWND.Zero && previous != current)
                DevToolsPane.Remove(previous);
            if (current != Win32.HWND.Zero)
                DevToolsPane.Add(current);
            if (tab == ViewModel.ActiveTab)
                SyncDevToolsPaneToActiveTab(tab);
        }

        /// <summary>
        /// Shows the active tab's DevTools view, or collapses the pane when the
        /// tab has none, shows an internal page, or a view holds element
        /// fullscreen.
        /// </summary>
        private void SyncDevToolsPaneToActiveTab(BrowserTab tab)
        {
            NativeWebView? webView = ViewModel.FindTabViewModel(tab)?.NativeWebView;
            if (webView == null || webView.DevToolsHwnd == Win32.HWND.Zero
                || tab.IsInternalPage || _elementFullscreenWebView != null)
            {
                DevToolsPane.Hide();
            }
            else
            {
                DevToolsPane.Show(webView.DevToolsHwnd);
            }
        }

        private void SyncDevToolsPaneToActiveTab()
        {
            if (ViewModel.ActiveTab is { } tab)
                SyncDevToolsPaneToActiveTab(tab);
        }
    }
}
