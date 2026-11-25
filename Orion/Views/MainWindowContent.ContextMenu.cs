namespace Orion
{
    /// <summary>
    /// App-drawn web context menu (engine RunContextMenu → WebContextMenu
    /// builder → WebContextMenuPresenter over the browser HWND).
    /// </summary>
    public sealed partial class MainWindowContent
    {
        private WebContextMenuPresenter? _webContextMenu;

        private WebContextMenuPresenter EnsureWebContextMenu() =>
            _webContextMenu ??= new WebContextMenuPresenter(
                WebViewHost,
                (url, disposition) => OpenLink(url, disposition),
                savePageAs: () => _ = ShowSavePageDialog(),
                searchEngine: () => ViewModel.GetSearchEngine());

        private void CloseWebContextMenu() => _webContextMenu?.Close();
    }
}
