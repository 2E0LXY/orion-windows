using CommunityToolkit.Mvvm.ComponentModel;

namespace Orion
{
    public partial class BookmarkEntry : ObservableObject
    {
        // BookmarkId for bookmarks, BookmarkFolderId for folders (both are int64 underneath)
        [ObservableProperty]
        public partial long Id { get; set; }

        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Url { get; set; } = string.Empty;

        [ObservableProperty]
        public partial long ParentFolderId { get; set; }  // 0 = root level

        [ObservableProperty]
        public partial bool IsFolder { get; set; }

        // Folders only
        [ObservableProperty]
        public partial bool IsInternal { get; set; }  // Bookmarks Bar / Favorites — cannot be deleted

        [ObservableProperty]
        public partial int FolderType { get; set; }  // 0=normal, 1=bookmarks_bar, 2=favorites

        public string IconGlyph => IsFolder
            ? (FolderType == 2 ? "\uE734" : "\uE8B7")  // 2=favorites → star, else folder
            : "\uE734";
    }
}
