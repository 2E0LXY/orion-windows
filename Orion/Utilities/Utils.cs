using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;

namespace Orion
{
    /// <summary>
    /// Utility methods for common operations.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Sets the standard Orion app icon on the given window. Drives the
        /// taskbar entry, Alt-Tab thumbnail, and (when present) the title-bar
        /// icon. Safe to call on every window — failures are silently ignored
        /// so a missing asset can't crash the UI.
        /// </summary>
        public static void SetOrionWindowIcon(Window window)
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Assets", "orion.ico");
                window.AppWindow.SetIcon(path);
            }
            catch
            {
                // Non-fatal: window stays usable without the custom icon.
            }
        }


        /// <summary>
        /// Walks up the visual tree to find a parent of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of parent to find.</typeparam>
        /// <param name="obj">The starting element.</param>
        /// <returns>The first parent of the specified type, or default if not found.</returns>
        public static T? GetParent<T>(DependencyObject obj) where T : class => GetParent(obj, typeof(T)) as T;

        /// <summary>
        /// Walks up the visual tree to find a parent of the specified type.
        /// </summary>
        /// <param name="obj">The starting element.</param>
        /// <param name="type">The type of parent to find.</param>
        /// <returns>The first parent of the specified type, or null if not found.</returns>
        public static object? GetParent(DependencyObject? obj, Type type)
        {
            if (obj == null)
                return null;

            DependencyObject? parent = VisualTreeHelper.GetParent(obj);
            return parent is null
                ? null
                : (type.IsAssignableFrom(parent.GetType()) ? parent : GetParent(parent, type));
        }

        /// <summary>
        /// Gets the root element of the visual tree for the given element.
        /// </summary>
        /// <param name="obj">The starting element.</param>
        /// <returns>The root element, or the element itself if it has no parent.</returns>
        public static DependencyObject GetVisualRoot(DependencyObject obj)
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(obj);
            while (parent != null)
            {
                DependencyObject? nextParent = VisualTreeHelper.GetParent(parent);
                if (nextParent == null)
                    return parent;
                parent = nextParent;
            }
            return obj;
        }

        /// <summary>
        /// Gets the window handle (HWND) for the window hosting the given element.
        /// </summary>
        /// <param name="element">A framework element in the visual tree.</param>
        /// <returns>The window handle, or IntPtr.Zero if not found.</returns>
        public static IntPtr GetWindowHandle(FrameworkElement element)
        {
            if (element?.XamlRoot == null)
                return IntPtr.Zero;

            try
            {
                // Get the window handle via XamlRoot's content
                // In WinUI 3, XamlRoot.Content is the root of the visual tree
                if (element.XamlRoot.Content is FrameworkElement root)
                {
                    // The XamlRoot is associated with a window - get its handle
                    // We need to get to the Window, which isn't in the visual tree
                    // Use the App's window as fallback
                    return GetAppWindowHandle();
                }
            }
            catch
            {
                // Fallback to App window
            }

            return GetAppWindowHandle();
        }

        /// <summary>
        /// Gets the window handle from the App's main window.
        /// </summary>
        /// <returns>The window handle, or IntPtr.Zero if not found.</returns>
        public static IntPtr GetAppWindowHandle()
        {
            try
            {
                if (Application.Current is App app)
                {
                    // Access the private _window field
                    System.Reflection.FieldInfo? windowField = typeof(App).GetField("_window",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (windowField != null)
                    {
                        var window = windowField.GetValue(app) as Window;
                        if (window != null)
                            return WinRT.Interop.WindowNative.GetWindowHandle(window);
                    }
                }
            }
            catch
            {
                // Reflection failed
            }

            return IntPtr.Zero;
        }
    }
}
