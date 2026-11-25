using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Orion
{
    /// <summary>
    /// Managed copy of a native context-menu request (ContextMenuRequestNative).
    /// </summary>
    public sealed record WebContextMenuContext(
        int X,
        int Y,
        WebContextMenuTypeFlags TypeFlags,
        WebContextMenuEditFlags EditFlags,
        WebContextMenuMediaType MediaType,
        bool HasImageContents,
        bool CanGoBack,
        bool CanGoForward,
        bool CanPrint,
        string LinkUrl,
        string UnfilteredLinkUrl,
        string SourceUrl,
        string PageUrl,
        string SelectionText,
        string LinkText)
    {
        /// <summary>
        /// Copies a borrowed ContextMenuRequestNative* into a managed record.
        /// Must be called before the native callback returns.
        /// </summary>
        internal static WebContextMenuContext FromNative(IntPtr request)
        {
            ContextMenuRequestNative native = Marshal.PtrToStructure<ContextMenuRequestNative>(request);
            return new WebContextMenuContext(
                native.x,
                native.y,
                (WebContextMenuTypeFlags)native.typeFlags,
                (WebContextMenuEditFlags)native.editFlags,
                (WebContextMenuMediaType)native.mediaType,
                native.hasImageContents != 0,
                native.canGoBack != 0,
                native.canGoForward != 0,
                native.canPrint != 0,
                Marshal.PtrToStringUTF8(native.linkUrl) ?? "",
                Marshal.PtrToStringUTF8(native.unfilteredLinkUrl) ?? "",
                Marshal.PtrToStringUTF8(native.sourceUrl) ?? "",
                Marshal.PtrToStringUTF8(native.pageUrl) ?? "",
                Marshal.PtrToStringUTF8(native.selectionText) ?? "",
                Marshal.PtrToStringUTF8(native.linkText) ?? "");
        }
    }

    /// <summary>Menu actions handled in C# rather than by the engine.</summary>
    public enum WebContextMenuAppAction
    {
        None,
        OpenInNewTab,    // Arg = URL
        OpenInNewWindow, // Arg = URL
        CopyText,        // Arg = text for the clipboard
        SavePageAs,
    }

    /// <summary>
    /// One entry of an app-drawn web context menu: either an engine command
    /// (dispatched via ExecuteContextMenuCommand) or an app action.
    /// </summary>
    public sealed record WebContextMenuItem(
        string Label,
        WebContextMenuCommand EngineCommand = 0,
        WebContextMenuAppAction AppAction = WebContextMenuAppAction.None,
        string Arg = "",
        bool IsEnabled = true,
        bool IsSeparator = false)
    {
        public static WebContextMenuItem Separator { get; } = new("", IsSeparator: true);
    }

    /// <summary>
    /// Composes the app-drawn context menu from an engine request.
    /// Pure data in/out — display and dispatch live in WebContextMenuPresenter.
    /// </summary>
    public static class WebContextMenu
    {
        // Search queries and label previews from arbitrarily large selections.
        private const int MaxSearchQueryLength = 500;
        private const int MaxLabelPreviewLength = 30;

        /// <summary>
        /// searchEngineName/searchUrlTemplate: default engine for the
        /// "Search with …" item; pass null to omit it (%s = query placeholder).
        /// canSavePage: host offers a save-page dialog.
        /// </summary>
        public static IReadOnlyList<WebContextMenuItem> Build(
            WebContextMenuContext context,
            string? searchEngineName,
            string? searchUrlTemplate,
            bool canSavePage)
        {
            var items = new List<WebContextMenuItem>();
            void StartGroup()
            {
                if (items.Count > 0 && !items[^1].IsSeparator)
                    items.Add(WebContextMenuItem.Separator);
            }

            bool hasLink = context.TypeFlags.HasFlag(WebContextMenuTypeFlags.Link) && context.LinkUrl.Length > 0;
            bool hasImage = context.MediaType == WebContextMenuMediaType.Image && context.SourceUrl.Length > 0;
            bool hasSelection = context.TypeFlags.HasFlag(WebContextMenuTypeFlags.Selection) && context.SelectionText.Length > 0;
            bool isEditable = context.TypeFlags.HasFlag(WebContextMenuTypeFlags.Editable);

            if (hasLink)
            {
                items.Add(new("Open Link in New Tab", AppAction: WebContextMenuAppAction.OpenInNewTab, Arg: context.LinkUrl));
                items.Add(new("Open Link in New Window", AppAction: WebContextMenuAppAction.OpenInNewWindow, Arg: context.LinkUrl));
                items.Add(new("Copy Link", AppAction: WebContextMenuAppAction.CopyText,
                    Arg: context.UnfilteredLinkUrl.Length > 0 ? context.UnfilteredLinkUrl : context.LinkUrl));
                items.Add(new("Download Linked File", EngineCommand: WebContextMenuCommand.SaveUrl, Arg: context.LinkUrl));
            }

            if (hasImage)
            {
                StartGroup();
                items.Add(new("Open Image in New Tab", AppAction: WebContextMenuAppAction.OpenInNewTab, Arg: context.SourceUrl));
                items.Add(new("Copy Image Address", AppAction: WebContextMenuAppAction.CopyText, Arg: context.SourceUrl));
                items.Add(new("Download Image", EngineCommand: WebContextMenuCommand.SaveUrl, Arg: context.SourceUrl));
            }

            if (isEditable)
            {
                StartGroup();
                items.Add(new("Undo", WebContextMenuCommand.Undo, IsEnabled: context.EditFlags.HasFlag(WebContextMenuEditFlags.CanUndo)));
                items.Add(new("Redo", WebContextMenuCommand.Redo, IsEnabled: context.EditFlags.HasFlag(WebContextMenuEditFlags.CanRedo)));
                items.Add(WebContextMenuItem.Separator);
                items.Add(new("Cut", WebContextMenuCommand.Cut, IsEnabled: context.EditFlags.HasFlag(WebContextMenuEditFlags.CanCut)));
                items.Add(new("Copy", WebContextMenuCommand.Copy, IsEnabled: context.EditFlags.HasFlag(WebContextMenuEditFlags.CanCopy)));
                items.Add(new("Paste", WebContextMenuCommand.Paste, IsEnabled: context.EditFlags.HasFlag(WebContextMenuEditFlags.CanPaste)));
                items.Add(new("Select All", WebContextMenuCommand.SelectAll, IsEnabled: context.EditFlags.HasFlag(WebContextMenuEditFlags.CanSelectAll)));
            }
            else if (hasSelection)
            {
                StartGroup();
                items.Add(new("Copy", WebContextMenuCommand.Copy));
            }

            if (hasSelection && searchUrlTemplate != null)
            {
                if (isEditable)
                    StartGroup();
                string query = context.SelectionText.Length > MaxSearchQueryLength
                    ? context.SelectionText[..MaxSearchQueryLength]
                    : context.SelectionText;
                items.Add(new($"Search with {searchEngineName} for “{Ellipsize(context.SelectionText)}”",
                    AppAction: WebContextMenuAppAction.OpenInNewTab,
                    Arg: searchUrlTemplate.Replace("%s", Uri.EscapeDataString(query))));
            }

            if (items.Count == 0)
            {
                // Plain page. Skip page-URL items on engine-internal pages
                // (CEF error pages load data:/chrome-error: URIs).
                bool pageUrlUsable = context.PageUrl.Length > 0
                    && !context.PageUrl.StartsWith("data:", StringComparison.Ordinal)
                    && !context.PageUrl.StartsWith("chrome-error:", StringComparison.Ordinal);
                items.Add(new("Back", WebContextMenuCommand.Back, IsEnabled: context.CanGoBack));
                items.Add(new("Forward", WebContextMenuCommand.Forward, IsEnabled: context.CanGoForward));
                items.Add(new("Reload Page", WebContextMenuCommand.Reload));
                if (pageUrlUsable)
                {
                    items.Add(WebContextMenuItem.Separator);
                    if (canSavePage)
                        items.Add(new("Save Page...", AppAction: WebContextMenuAppAction.SavePageAs));
                    if (context.CanPrint)
                        items.Add(new("Print Page...", WebContextMenuCommand.Print));
                    items.Add(new("View Page Source", AppAction: WebContextMenuAppAction.OpenInNewTab,
                        Arg: "view-source:" + context.PageUrl));
                }
            }

            StartGroup();
            items.Add(new("Inspect Element", WebContextMenuCommand.InspectElement));
            return items;
        }

        private static string Ellipsize(string text)
        {
            text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= MaxLabelPreviewLength ? text : text[..MaxLabelPreviewLength].TrimEnd() + "…";
        }
    }
}
