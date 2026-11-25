using System;
using System.Collections.Generic;
using System.Text;

namespace Orion
{
    /// <summary>
    /// Suppresses the system beep played for unhandled Alt+key chords, so
    /// Alt-based shortcuts (and stray Alt presses) are silent like in other
    /// browsers. Two producers, both fed by WM_SYSCHAR:
    /// - XAML focus: WinUI's InputSiteWindowClass windows process the chord
    ///   and beep (verified by message tracing; no SC_KEYMENU is involved).
    ///   Swallowing WM_SYSCHAR there is safe — XAML access keys ride keydown.
    ///   Input sites exist under every XAML surface, including the windowed
    ///   popups WinUI creates for flyouts, dialogs and the find bar, so they
    ///   are caught at creation via a UI-thread WinEvent hook (Initialize)
    ///   rather than by enumerating any one window's children.
    /// - Other children (the WebKit view) must keep WM_SYSCHAR for page
    ///   access keys; their unhandled chords fall to DefWindowProc, which
    ///   raises SC_KEYMENU on the top level — swallowed there (Attach).
    /// Alt+Space is exempt everywhere and keeps opening the system menu.
    /// </summary>
    internal static class AltMenuBeepSuppressor
    {
        private const uint WM_SYSCHAR = 0x0106;
        private const uint WM_NCDESTROY = 0x0082;
        private const string InputSiteWindowClass = "InputSiteWindowClass";

        // Rooted for the process lifetime; shared by all subclassed windows.
        private static readonly Win32.SUBCLASSPROC _proc = SubclassProc;
        private static readonly Win32.WinEventDelegate _createHook = OnWindowCreated;
        private static readonly HashSet<IntPtr> _subclassed = [];

        /// <summary>
        /// Installs the input-site creation hook. Call once on the UI thread
        /// before the first window — every window is created after it.
        /// </summary>
        public static void Initialize()
        {
            if (Win32.SetWinEventHook(Win32.EVENT_OBJECT_CREATE, Win32.EVENT_OBJECT_CREATE,
                IntPtr.Zero, _createHook, Win32.GetCurrentProcessId(), Win32.GetCurrentThreadId(),
                Win32.WINEVENT_OUTOFCONTEXT) == IntPtr.Zero)
            {
                OrionBackend.LogError("AltMenuBeepSuppressor: SetWinEventHook failed");
            }
        }

        /// <summary>Subclasses a top-level window for the SC_KEYMENU swallow.</summary>
        public static void Attach(Microsoft.UI.Xaml.Window window) =>
            Subclass(WinRT.Interop.WindowNative.GetWindowHandle(window));

        private static void OnWindowCreated(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject == Win32.OBJID_WINDOW && hwnd != IntPtr.Zero
                && ClassNameOf(hwnd) == InputSiteWindowClass)
            {
                Subclass(hwnd);
            }
        }

        private static void Subclass(IntPtr hwnd)
        {
            if (!_subclassed.Add(hwnd))
                return;
            if (!Win32.SetWindowSubclass(hwnd, _proc, 1, 0))
            {
                _ = _subclassed.Remove(hwnd);
                OrionBackend.LogError($"AltMenuBeepSuppressor: SetWindowSubclass failed for {ClassNameOf(hwnd)}");
            }
        }

        private static int SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam,
            IntPtr uIdSubclass, uint dwRefData)
        {
            if (msg == WM_NCDESTROY)
            {
                // Popups churn and Windows recycles HWND values; a stale set
                // entry would skip subclassing a new window on a reused handle.
                _ = Win32.RemoveWindowSubclass(hwnd, _proc, 1);
                _ = _subclassed.Remove(hwnd);
            }
            if (msg == WM_SYSCHAR && (long)wParam != ' ')
                return 0;
            if (msg == Win32.WM_SYSCOMMAND
                && ((long)wParam & 0xFFF0) == Win32.SC_KEYMENU
                && (long)lParam != ' ')
            {
                return 0;
            }
            return Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
        }

        private static string ClassNameOf(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            _ = Win32.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
