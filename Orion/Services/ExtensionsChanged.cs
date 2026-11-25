using System;

namespace Orion
{
    public static class ExtensionsChanged
    {
        public static event Action? Changed;
        public static void Notify() => Changed?.Invoke();
    }
}
