namespace Orion
{
    public class ExtensionToolbarItem
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string PopupUri { get; init; } = string.Empty;
        public byte[]? IconData { get; init; }
    }
}
