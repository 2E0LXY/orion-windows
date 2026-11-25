namespace Orion
{
    public static class Log
    {
        public static void LogInfo(string message) => OrionBackend.LogInfo(message);

        public static void LogError(string message) => OrionBackend.LogError(message);

        public static void LogWarning(string message) => OrionBackend.LogWarning(message);

        public static void LogDebug(string message) => OrionBackend.LogDebug(message);
    }
}
