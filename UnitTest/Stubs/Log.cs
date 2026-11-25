using System;

namespace Orion
{
    /// <summary>
    /// Console-backed stub replacing the real Log for unit tests.
    /// The real class calls OrionBackend P/Invoke; this one writes to stdout.
    /// </summary>
    public static class Log
    {
        public static void LogInfo(string message) => Console.WriteLine($"[INFO]  {message}");
        public static void LogError(string message) => Console.WriteLine($"[ERROR] {message}");
        public static void LogWarning(string message) => Console.WriteLine($"[WARN]  {message}");
        public static void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
    }
}
