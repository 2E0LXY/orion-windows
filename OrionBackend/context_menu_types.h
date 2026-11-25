#pragma once

// Context-menu types shared across the P/Invoke boundary. Plain C: this
// header defines the wire ABI directly. OrionCodeGen reflects the enums and
// the ContextMenuRequest struct into C# (Orion/Interop/Generated/
// NativeTypes.cs); the generator strips the enum-name prefix from
// enumerators, so C# reads WebContextMenuCommand.Back.

#include <stdint.h>

// What the context-menu hit test found.
enum WebContextMenuTypeFlags {
    WebContextMenuTypeFlagsNone      = 0,
    WebContextMenuTypeFlagsPage      = 1 << 0,
    WebContextMenuTypeFlagsFrame     = 1 << 1,
    WebContextMenuTypeFlagsLink      = 1 << 2,
    WebContextMenuTypeFlagsMedia     = 1 << 3,
    WebContextMenuTypeFlagsSelection = 1 << 4,
    WebContextMenuTypeFlagsEditable  = 1 << 5,
};

// Allowed edit actions at the context-menu target.
enum WebContextMenuEditFlags {
    WebContextMenuEditFlagsNone         = 0,
    WebContextMenuEditFlagsCanUndo      = 1 << 0,
    WebContextMenuEditFlagsCanRedo      = 1 << 1,
    WebContextMenuEditFlagsCanCut       = 1 << 2,
    WebContextMenuEditFlagsCanCopy      = 1 << 3,
    WebContextMenuEditFlagsCanPaste     = 1 << 4,
    WebContextMenuEditFlagsCanDelete    = 1 << 5,
    WebContextMenuEditFlagsCanSelectAll = 1 << 6,
};

// Media element kind at the context-menu target.
enum WebContextMenuMediaType {
    WebContextMenuMediaTypeNone   = 0,
    WebContextMenuMediaTypeImage  = 1,
    WebContextMenuMediaTypeVideo  = 2,
    WebContextMenuMediaTypeAudio  = 3,
    WebContextMenuMediaTypeCanvas = 4,
    WebContextMenuMediaTypeFile   = 5,
    WebContextMenuMediaTypePlugin = 6,
};

// Menu commands the engine executes (app-level items like "Open Link in
// New Tab" are handled in C# and never cross the boundary).
enum WebContextMenuCommand {
    WebContextMenuCommandBack           = 1,
    WebContextMenuCommandForward        = 2,
    WebContextMenuCommandReload         = 3,
    WebContextMenuCommandUndo           = 4,
    WebContextMenuCommandRedo           = 5,
    WebContextMenuCommandCut            = 6,
    WebContextMenuCommandCopy           = 7,
    WebContextMenuCommandPaste          = 8,
    WebContextMenuCommandDelete         = 9,
    WebContextMenuCommandSelectAll      = 10,
    WebContextMenuCommandPrint          = 11,
    WebContextMenuCommandSaveUrl        = 12,  // arg = URL to download
    WebContextMenuCommandInspectElement = 13,  // x/y = the request's coordinates
};

// Context for an engine-requested menu. Strings are borrowed UTF-8, valid
// only for the duration of the callback; C# copies immediately.
typedef struct ContextMenuRequest {
    int32_t x;  // view-relative, engine coordinate units
    int32_t y;
    uint32_t typeFlags;  // WebContextMenuTypeFlags bits
    uint32_t editFlags;  // WebContextMenuEditFlags bits
    int32_t mediaType;   // WebContextMenuMediaType value
    int32_t hasImageContents;
    int32_t canGoBack;
    int32_t canGoForward;
    int32_t canPrint;  // engine can print this view (WK printing is unimplemented)
    const char* linkUrl;
    const char* unfilteredLinkUrl;  // for Copy Link Address (unrewritten)
    const char* sourceUrl;          // image/media src
    const char* pageUrl;
    const char* selectionText;
    const char* linkText;
} ContextMenuRequest;
