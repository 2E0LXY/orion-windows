using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace Orion
{
    public class ToolbarButtonItem
    {
        public string Glyph { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public sealed partial class CustomizeToolbarControl : UserControl
    {
        public event EventHandler? DoneRequested;

        public List<ToolbarButtonItem> AvailableItems { get; } =
        [
            new() { Glyph = "\xE90C", Label = "Sidebar"        },
            new() { Glyph = "\xE72B", Label = "Back/Forward"   },
            new() { Glyph = "\xE8B7", Label = "Tab Groups"     },
            new() { Glyph = "\xE72E", Label = "Privacy"        },
            new() { Glyph = "\xE734", Label = "Bookmark"       },
            new() { Glyph = "\xE81C", Label = "History"        },
            new() { Glyph = "\xE80F", Label = "Home"           },
            new() { Glyph = "\xE896", Label = "Downloads"      },
            new() { Glyph = "\xE74C", Label = "Extensions"     },
            new() { Glyph = "\xF0E2", Label = "Tab Overview"   },
            new() { Glyph = "\xE710", Label = "New Tab"        },
            new() { Glyph = "\xE8A7", Label = "New Window"     },
            new() { Glyph = "\xE774", Label = "Web Settings"   },
            new() { Glyph = "\xE736", Label = "Reading List"   },
            new() { Glyph = "\xE71E", Label = "Zoom"           },
            new() { Glyph = "\xE749", Label = "Print"          },
            new() { Glyph = "\xE721", Label = "Find"           },
            new() { Glyph = "\xE946", Label = "Page Info"      },
            new() { Glyph = "\xE72D", Label = "Share"          },
            new() { Glyph = "\xE8FD", Label = "Reader Mode"    },
            new() { Glyph = "\xE943", Label = "Dev Tools"      },
        ];

        public List<ToolbarButtonItem> ToolbarPreviewItems { get; } =
        [
            new() { Glyph = "\xE90C", Label = "Sidebar"       },
            new() { Glyph = "\xE72B", Label = "Back/Forward"  },
            new() { Glyph = "\xE72E", Label = "Privacy"       },
            new() { Glyph = "\xE774", Label = "Web Settings"  },
            new() { Glyph = "\xE734", Label = "Bookmark"      },
            new() { Glyph = "\xE710", Label = "New Tab"       },
            new() { Glyph = "\xE896", Label = "Downloads"     },
            new() { Glyph = "\xF0E2", Label = "Tab Overview"  },
        ];

        public CustomizeToolbarControl() => InitializeComponent();

        private void DoneButton_Click(object sender, RoutedEventArgs e) => DoneRequested?.Invoke(this, EventArgs.Empty);
    }
}
