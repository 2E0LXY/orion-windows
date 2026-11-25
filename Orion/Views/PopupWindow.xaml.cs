using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;

namespace Orion
{
    /// <summary>
    /// Thin popup-window shell.  Created when a page calls window.open(...)
    /// with explicit window features (width/height).  The actual UI lives in
    /// PopupWindowContent (UserControl) so x:Bind works.
    ///
    /// Popups intentionally bypass the tab system: their navigation history
    /// is NOT persisted to the db (no AddHistoryEntry calls), and they are
    /// NOT stored in the tab-groups table (no CreateTab call).  That matches
    /// Chrome/Firefox popup semantics — popups close with their opener and
    /// have no trace after the session.
    /// </summary>
    public sealed partial class PopupWindow : Window
    {
        private static readonly List<PopupWindow> _allPopups = [];

        internal PopupWindowContent Content_ => PopupContent;

        public PopupWindow(int width, int height, bool showLocationBar)
        {
            InitializeComponent();
            PopupContent.ShowLocationBar = showLocationBar;
            PopupContent.ParentWindow = this;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(PopupContent.TitleBarElementRef);
            Utils.SetOrionWindowIcon(this);
            AltMenuBeepSuppressor.Attach(this);

            if (width > 0 && height > 0)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                AppWindow.Resize(PopupContent.ComputeOuterSize(width, height, hwnd));
            }

            _allPopups.Add(this);
            Closed += (_, _) => _allPopups.Remove(this);
        }

        /// <summary>Closes every live popup window (used on global exit).</summary>
        internal static void CloseAll()
        {
            foreach (PopupWindow w in _allPopups.ToArray())
                w.Close();
        }
    }
}
