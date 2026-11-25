namespace Orion
{
    /// <summary>
    /// Keeps the display awake while web content is in element fullscreen
    /// (fullscreen video would otherwise let the screen blank, since WebKit's
    /// Windows port has no sleep inhibitor of its own — WebKitGTK creates a
    /// PAL::SleepDisabler at the same point).
    ///
    /// Reference-counted so several fullscreen windows can hold it at once.
    /// UI-thread only: the execution state applies to the calling thread, and
    /// every caller runs on the single UI thread.
    /// </summary>
    internal static class DisplaySleepBlocker
    {
        private static int _holders;

        public static void Acquire()
        {
            if (++_holders == 1)
                Apply(displayRequired: true);
        }

        public static void Release()
        {
            if (_holders == 0)
                return;
            if (--_holders == 0)
                Apply(displayRequired: false);
        }

        private static void Apply(bool displayRequired)
        {
            uint flags = Win32.ES_CONTINUOUS | (displayRequired ? Win32.ES_DISPLAY_REQUIRED : 0);
            if (Win32.SetThreadExecutionState(flags) == 0)
                LogWarning($"SetThreadExecutionState(0x{flags:X}) failed");
        }
    }
}
