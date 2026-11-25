using System;

namespace Orion
{
    public static class BookmarksChanged
    {
        public static event Action? Changed;
        public static void Notify() => Changed?.Invoke();
    }
}
