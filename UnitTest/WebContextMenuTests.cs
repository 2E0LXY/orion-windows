using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Orion
{
    public class WebContextMenuTests
    {
        private static WebContextMenuContext Context(
            WebContextMenuTypeFlags typeFlags = WebContextMenuTypeFlags.Page,
            WebContextMenuEditFlags editFlags = WebContextMenuEditFlags.None,
            WebContextMenuMediaType mediaType = WebContextMenuMediaType.None,
            bool canGoBack = false,
            bool canGoForward = false,
            bool canPrint = true,
            string linkUrl = "",
            string unfilteredLinkUrl = "",
            string sourceUrl = "",
            string pageUrl = "https://example.com/",
            string selectionText = "") =>
            new(X: 10, Y: 20, typeFlags, editFlags, mediaType,
                HasImageContents: sourceUrl.Length > 0, canGoBack, canGoForward, canPrint,
                linkUrl, unfilteredLinkUrl, sourceUrl, pageUrl, selectionText, LinkText: "");

        private static IReadOnlyList<WebContextMenuItem> Build(WebContextMenuContext context, bool canSavePage = true) =>
            WebContextMenu.Build(context, "Kagi", "https://kagi.com/search?q=%s", canSavePage);

        [Fact]
        public void PageMenu_HasNavigationAndPageItems()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(canGoBack: true));
            Assert.Equal("Back", items[0].Label);
            Assert.True(items[0].IsEnabled);
            Assert.Equal("Forward", items[1].Label);
            Assert.False(items[1].IsEnabled);
            Assert.Contains(items, i => i.Label == "Reload Page");
            Assert.Contains(items, i => i.AppAction == WebContextMenuAppAction.SavePageAs);
            Assert.Contains(items, i => i.Label == "View Page Source" && i.Arg == "view-source:https://example.com/");
        }

        [Fact]
        public void PageMenu_NoSavePage_WhenHostCannot()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(), canSavePage: false);
            Assert.DoesNotContain(items, i => i.AppAction == WebContextMenuAppAction.SavePageAs);
        }

        [Fact]
        public void PageMenu_NoPrint_WhenEngineCannot()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(canPrint: false));
            Assert.DoesNotContain(items, i => i.EngineCommand == WebContextMenuCommand.Print);
            Assert.Contains(items, i => i.Label == "View Page Source");
        }

        [Fact]
        public void InternalPageUrl_SuppressesPageUrlItems()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(pageUrl: "data:text/html;base64,AAAA"));
            Assert.DoesNotContain(items, i => i.Label == "View Page Source");
            Assert.DoesNotContain(items, i => i.AppAction == WebContextMenuAppAction.SavePageAs);
            Assert.Contains(items, i => i.Label == "Reload Page");
        }

        [Fact]
        public void LinkMenu_UsesUnfilteredUrlForCopy()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(
                typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Link,
                linkUrl: "https://a.example/", unfilteredLinkUrl: "https://b.example/"));
            Assert.Equal("Open Link in New Tab", items[0].Label);
            Assert.Equal("https://a.example/", items[0].Arg);
            Assert.Contains(items, i => i.AppAction == WebContextMenuAppAction.CopyText && i.Arg == "https://b.example/");
            Assert.Contains(items, i => i.EngineCommand == WebContextMenuCommand.SaveUrl && i.Arg == "https://a.example/");
            Assert.DoesNotContain(items, i => i.Label == "Back");
        }

        [Fact]
        public void ImageMenu_OffersImageItems()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(
                typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Media,
                mediaType: WebContextMenuMediaType.Image, sourceUrl: "https://img.example/x.png"));
            Assert.Contains(items, i => i.Label == "Open Image in New Tab" && i.Arg == "https://img.example/x.png");
            Assert.Contains(items, i => i.Label == "Download Image" && i.EngineCommand == WebContextMenuCommand.SaveUrl);
        }

        [Fact]
        public void EditableMenu_EnablesPerEditFlags()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(
                typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Editable,
                editFlags: WebContextMenuEditFlags.CanPaste | WebContextMenuEditFlags.CanSelectAll));
            Assert.False(items.Single(i => i.EngineCommand == WebContextMenuCommand.Undo).IsEnabled);
            Assert.False(items.Single(i => i.EngineCommand == WebContextMenuCommand.Cut).IsEnabled);
            Assert.True(items.Single(i => i.EngineCommand == WebContextMenuCommand.Paste).IsEnabled);
            Assert.True(items.Single(i => i.EngineCommand == WebContextMenuCommand.SelectAll).IsEnabled);
        }

        [Fact]
        public void SelectionMenu_ComposesSearchItem()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(
                typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Selection,
                selectionText: "hello world"));
            Assert.Contains(items, i => i.EngineCommand == WebContextMenuCommand.Copy);
            WebContextMenuItem search = items.Single(i => i.Label.StartsWith("Search with", StringComparison.Ordinal));
            Assert.Equal("Search with Kagi for “hello world”", search.Label);
            Assert.Equal("https://kagi.com/search?q=hello%20world", search.Arg);
        }

        [Fact]
        public void SelectionMenu_EllipsizesLongLabel_AndOmitsSearchWithoutTemplate()
        {
            string selection = string.Join(' ', Enumerable.Repeat("word", 30));
            IReadOnlyList<WebContextMenuItem> items = Build(Context(
                typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Selection,
                selectionText: selection));
            WebContextMenuItem search = items.Single(i => i.Label.StartsWith("Search with", StringComparison.Ordinal));
            Assert.Contains("…", search.Label);
            Assert.True(search.Label.Length < 60);

            IReadOnlyList<WebContextMenuItem> noTemplate = WebContextMenu.Build(Context(
                typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Selection,
                selectionText: selection), null, null, canSavePage: true);
            Assert.DoesNotContain(noTemplate, i => i.Label.StartsWith("Search with", StringComparison.Ordinal));
        }

        [Fact]
        public void InspectElement_IsAlwaysLast()
        {
            foreach (WebContextMenuContext context in new[]
            {
                Context(),
                Context(typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Link, linkUrl: "https://a.example/"),
                Context(typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Editable),
            })
            {
                IReadOnlyList<WebContextMenuItem> items = Build(context);
                Assert.Equal(WebContextMenuCommand.InspectElement, items[^1].EngineCommand);
                Assert.True(items[^2].IsSeparator);
            }
        }

        [Fact]
        public void SeparatorsNeverDoubleOrLead()
        {
            IReadOnlyList<WebContextMenuItem> items = Build(Context(
                typeFlags: WebContextMenuTypeFlags.Page | WebContextMenuTypeFlags.Link
                    | WebContextMenuTypeFlags.Media | WebContextMenuTypeFlags.Selection,
                mediaType: WebContextMenuMediaType.Image,
                linkUrl: "https://a.example/", sourceUrl: "https://img.example/x.png",
                selectionText: "text"));
            Assert.False(items[0].IsSeparator);
            for (int i = 1; i < items.Count; i++)
                Assert.False(items[i].IsSeparator && items[i - 1].IsSeparator);
        }
    }
}
