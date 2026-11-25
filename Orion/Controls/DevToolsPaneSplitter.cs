using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    /// <summary>
    /// Drag handle of the DevTools pane. A Panel subclass: a Panel paints its
    /// Background over its whole arranged area, which makes the strip
    /// hit-testable, and a subclass is the only place ProtectedCursor can be
    /// set for the resize cursor.
    /// </summary>
    public sealed partial class DevToolsPaneSplitter : Panel
    {
        public DevToolsPaneSplitter()
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        }

        /// <summary>True for a vertical handle (resizes left/right).</summary>
        public bool IsVertical
        {
            get;
            set
            {
                field = value;
                ProtectedCursor = InputSystemCursor.Create(
                    value ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
            }
        }
    }
}
