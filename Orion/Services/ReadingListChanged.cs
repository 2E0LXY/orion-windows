using System;

namespace Orion
{
    public static class ReadingListChanged
    {
        public static event Action? Changed;
        public static void Notify() => Changed?.Invoke();
    }
}
