namespace Orion
{
    public sealed partial class MainWindowContent
    {
        // Last find query, shared across tabs and windows (the macOS find
        // pasteboard analogue).
        private static string s_lastFindQuery = "";

        private void InitializeFindBar()
        {
            FindBar.QueryChanged += OnFindQueryChanged;
            FindBar.NextRequested += () => FindNext(forward: true);
            FindBar.PreviousRequested += () => FindNext(forward: false);
            FindBar.CloseRequested += CloseFindBar;
        }

        /// <summary>
        /// Opens the find bar for the active tab, prefilled with the last
        /// query (searching it immediately if non-empty). No-op on tabs
        /// without a web view (internal pages).
        /// </summary>
        private void OpenFindBar()
        {
            BrowserTabViewModel? tabVM = ViewModel.ActiveTabViewModel;
            if (tabVM?.NativeWebView == null)
                return;
            tabVM.FindBarVisible = true;
            FindBar.Open(s_lastFindQuery);
            if (FindBar.Query.Length > 0)
                tabVM.NativeWebView.FindInPage(FindBar.Query, forward: true, findNext: false);
        }

        private void CloseFindBar()
        {
            BrowserTabViewModel? tabVM = ViewModel.ActiveTabViewModel;
            tabVM?.FindBarVisible = false;
            FindBar.Close();

            NativeWebView? webView = tabVM?.NativeWebView;
            if (webView == null)
                return;
            webView.StopFindInPage(clearSelection: false);
            // Return focus to the page; otherwise Win32 focus stays in the
            // XAML island and typing goes nowhere until the user clicks.
            if (Win32.IsWindow(webView.Hwnd))
                _ = Win32.SetFocus(webView.Hwnd);
        }

        private void OnFindQueryChanged(string query)
        {
            s_lastFindQuery = query;
            NativeWebView? webView = ViewModel.ActiveTabViewModel?.NativeWebView;
            if (webView == null)
                return;
            if (query.Length == 0)
            {
                webView.StopFindInPage(clearSelection: false);
                FindBar.ClearResult();
                return;
            }
            webView.FindInPage(query, forward: true, findNext: false);
        }

        private void FindNext(bool forward)
        {
            if (!FindBar.IsOpen)
            {
                // Opening starts a search for the last query, which lands on
                // the nearest match — the "find next" a closed bar can offer.
                OpenFindBar();
                return;
            }
            if (FindBar.Query.Length == 0)
                return;
            ViewModel.ActiveTabViewModel?.NativeWebView?.FindInPage(FindBar.Query, forward, findNext: true);
        }

        /// <summary>
        /// Restores the incoming tab's find bar visibility on tab switch.
        /// Background tabs keep their highlights (per macOS); no re-search.
        /// </summary>
        private void SyncFindBarToActiveTab(BrowserTab tab)
        {
            BrowserTabViewModel tabVM = ViewModel.GetOrCreateTabViewModel(tab);
            if (tabVM.FindBarVisible && tabVM.NativeWebView != null)
                FindBar.Open(s_lastFindQuery, focus: false);
            else if (FindBar.IsOpen)
                FindBar.Close();
        }
    }
}
