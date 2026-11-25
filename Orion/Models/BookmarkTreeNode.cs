using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Orion
{
    public partial class BookmarkTreeNode(BookmarkEntry entry) : ObservableObject
    {
        public BookmarkEntry Entry { get; } = entry;
        public ObservableCollection<BookmarkTreeNode> Children { get; } = [];

        public bool IsFolder => Entry.IsFolder;
        public bool IsDeleteEnabled => !Entry.IsInternal;
        public string Title => Entry.Title;
        public string Url => Entry.IsFolder ? "" : Entry.Url;
        public string IconGlyph => Entry.IconGlyph;
        public long EntryId => Entry.Id;

        [ObservableProperty]
        public partial ImageSource? FaviconImage { get; set; }

        public async Task LoadFaviconAsync()
        {
            if (IsFolder || string.IsNullOrEmpty(Entry.Url))
                return;
            OrionBackend.FaviconResult? result = OrionBackend.GetFavicon(Entry.Url);
            if (result != null)
                FaviconImage = await BrowserTab.CreateFaviconImageAsync(result.Data, result.IconUrl);
        }
        // Null for bookmark nodes so the TreeView won't accept drops into them
        public ObservableCollection<BookmarkTreeNode>? FolderChildren => IsFolder ? Children : null;

        public static Windows.UI.Text.FontWeight FolderFontWeight(bool isFolder) => isFolder
                ? new Windows.UI.Text.FontWeight(600)  // SemiBold
                : new Windows.UI.Text.FontWeight(400);  // Normal

        // Recursively builds a bookmark tree from a flat list of all entries.
        // The core returns entries pre-sorted within each parent.
        public static void BuildTree(
            ICollection<BookmarkTreeNode> target,
            IEnumerable<BookmarkEntry> allEntries,
            long parentFolderId = 0)
        {
            List<BookmarkEntry> list = allEntries as List<BookmarkEntry> ?? [.. allEntries];
            BuildTreeCore(target, list, parentFolderId);
        }

        private static void BuildTreeCore(
            ICollection<BookmarkTreeNode> target,
            List<BookmarkEntry> entries,
            long parentFolderId)
        {
            foreach (BookmarkEntry? entry in entries.Where(e => e.ParentFolderId == parentFolderId))
            {
                var node = new BookmarkTreeNode(entry);
                if (entry.IsFolder)
                    BuildTreeCore(node.Children, entries, entry.Id);
                target.Add(node);
            }
        }

        public static (IList<BookmarkTreeNode> Siblings, int Index, long ParentFolderId)? FindInTree(
            IList<BookmarkTreeNode> nodes, BookmarkTreeNode target, long parentFolderId = 0)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (ReferenceEquals(nodes[i], target))
                    return (nodes, i, parentFolderId);

                if (nodes[i].IsFolder)
                {
                    (IList<BookmarkTreeNode> Siblings, int Index, long ParentFolderId)? found = FindInTree(nodes[i].Children, target, nodes[i].EntryId);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public static void PersistMove(BookmarkTreeNode node, IList<BookmarkTreeNode> siblings, int index, long parentFolderId)
        {
            long nextBookmarkId = 0;
            long nextFolderId = 0;
            if (index < siblings.Count - 1)
            {
                BookmarkTreeNode nextNode = siblings[index + 1];
                if (nextNode.IsFolder)
                    nextFolderId = nextNode.EntryId;
                else
                    nextBookmarkId = nextNode.EntryId;
            }

            if (node.IsFolder)
                OrionBackend.MoveBookmarkFolder(node.EntryId, parentFolderId, nextBookmarkId, nextFolderId);
            else
                OrionBackend.MoveBookmark(node.EntryId, parentFolderId, nextBookmarkId, nextFolderId);
            node.Entry.ParentFolderId = parentFolderId;
        }
    }
}
