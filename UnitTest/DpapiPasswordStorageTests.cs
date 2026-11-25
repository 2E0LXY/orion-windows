using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Orion
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PasswordItemNative
    {
        public long Id;
        public long ProfileId;
        public IntPtr Url;
        public IntPtr Username;
        public IntPtr Password;
        public IntPtr VerificationKey;
        public IntPtr VerificationCode;
    }

    public class PasswordStorageTests : IDisposable
    {
        private const string OrionDll = "OrionBackend.dll";

        [DllImport(OrionDll)]
        private static extern IntPtr GetPasswordItems(
            long profileId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string searchText,
            out int count);

        [DllImport(OrionDll)]
        private static extern long CreatePasswordItem(
            long profileId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string verificationKey,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string verificationCode,
            [MarshalAs(UnmanagedType.Bool)] bool updateExisting);

        [DllImport(OrionDll)]
        private static extern void UpdatePasswordItem(
            long id,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string verificationKey,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string verificationCode);

        [DllImport(OrionDll)]
        private static extern void DeletePasswordItems(
            [In] long[] ids,
            int count);

        [DllImport(OrionDll)]
        private static extern void FreePasswordItems(IntPtr items, int count);

        [DllImport(OrionDll)]
        [return: MarshalAs(UnmanagedType.LPUTF8Str)]
        private static extern string InitOrionCore(long profileId);

        [DllImport(OrionDll)]
        private static extern void ShutdownOrionCore();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static readonly bool _dllAvailable = SetupDll();
        private static bool _coreInitialized;

        private static bool SetupDll()
        {
            string? dir = Path.GetDirectoryName(typeof(PasswordStorageTests).Assembly.Location);
            if (dir == null) return false;

            var d = new DirectoryInfo(dir);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "Orion.slnx")))
                d = d.Parent;
            if (d == null) return false;

            // Probe the configuration matching this test build first: a Release
            // test run (publish.bat's pre-publish gate, ci\unittest.cmd) must not
            // silently P/Invoke a stale Debug DLL left over from earlier work.
#if DEBUG
            string[] configs = ["Debug", "Release"];
#else
            string[] configs = ["Release", "Debug"];
#endif
            string? dllPath = configs
                .Select(config => Path.Combine(d.FullName, "x64", config, OrionDll))
                .FirstOrDefault(File.Exists);
            if (dllPath == null)
                return false;

            _ = SetDllDirectory(Path.GetDirectoryName(dllPath)!);
            return true;
        }

        private static void EnsureCoreInitialized()
        {
            if (_coreInitialized) return;
            // Initializing the core touches the real %LOCALAPPDATA% data —
            // including the one-time per-profile database migration — so
            // core-backed assertions are opt-in for developer machines.
            if (Environment.GetEnvironmentVariable("ORION_CORE_TESTS") != "1") return;
            _coreInitialized = InitOrionCore(0) == "OK";
        }

        private record PasswordItem(
            long Id, long ProfileId,
            string Url, string Username, string Password,
            string VerificationKey, string VerificationCode);

        private static PasswordItem[] GetItems(long profileId, string searchText = "")
        {
            IntPtr ptr = GetPasswordItems(profileId, searchText, out int count);
            if (ptr == IntPtr.Zero || count == 0)
                return [];

            int size = Marshal.SizeOf<PasswordItemNative>();
            var result = new PasswordItem[count];
            for (int i = 0; i < count; i++)
            {
                PasswordItemNative n = Marshal.PtrToStructure<PasswordItemNative>(ptr + (i * size));
                result[i] = new PasswordItem(
                    n.Id, n.ProfileId,
                    Marshal.PtrToStringUTF8(n.Url) ?? "",
                    Marshal.PtrToStringUTF8(n.Username) ?? "",
                    Marshal.PtrToStringUTF8(n.Password) ?? "",
                    Marshal.PtrToStringUTF8(n.VerificationKey) ?? "",
                    Marshal.PtrToStringUTF8(n.VerificationCode) ?? "");
            }

            FreePasswordItems(ptr, count);
            return result;
        }

        private readonly List<long> _createdIds = [];

        public PasswordStorageTests()
        {
            if (!_dllAvailable) return;
            try { EnsureCoreInitialized(); }
            catch { /* DLL not loadable or core init failed */ }
        }

        public void Dispose()
        {
            if (!_dllAvailable || !_coreInitialized || _createdIds.Count == 0) return;
            try { DeletePasswordItems([.. _createdIds], _createdIds.Count); }
            catch { /* best effort cleanup */ }
        }

        private long Create(long profileId, string url, string username, string password,
            string verificationKey = "", string verificationCode = "", bool updateExisting = false)
        {
            long id = CreatePasswordItem(profileId, url, username, password,
                verificationKey, verificationCode, updateExisting);
            if (id != 0)
                _createdIds.Add(id);
            return id;
        }

        [Fact]
        public void EmptyRead_ReturnsNoItems()
        {
            Assert.SkipUnless(_dllAvailable && _coreInitialized, "OrionBackend not available");

            PasswordItem[] items = GetItems(99999);
            Assert.Empty(items);
        }

        [Fact]
        public void CreateAndReadBack_RoundTrips()
        {
            Assert.SkipUnless(_dllAvailable && _coreInitialized, "OrionBackend not available");

            long id1 = Create(1, "https://example.com/login", "user1", "p@ss\"w0rd!",
                verificationKey: "JBSWY3DPEHPK3PXP");
            long id2 = Create(1, "https://example.org", "user2@example.org", "backslash\\and\nnewline");

            Assert.NotEqual(0, id1);
            Assert.NotEqual(0, id2);

            PasswordItem[] items = GetItems(1);
            PasswordItem? item1 = items.FirstOrDefault(i => i.Id == id1);
            PasswordItem? item2 = items.FirstOrDefault(i => i.Id == id2);

            Assert.NotNull(item1);
            Assert.Equal("https://example.com/login", item1.Url);
            Assert.Equal("user1", item1.Username);
            Assert.Equal("p@ss\"w0rd!", item1.Password);
            Assert.Equal("JBSWY3DPEHPK3PXP", item1.VerificationKey);

            Assert.NotNull(item2);
            Assert.Equal("https://example.org", item2.Url);
            Assert.Equal("backslash\\and\nnewline", item2.Password);
        }

        [Fact]
        public void Update_ModifiesExistingItem()
        {
            Assert.SkipUnless(_dllAvailable && _coreInitialized, "OrionBackend not available");

            long id = Create(1, "https://a.com", "olduser", "oldpass");
            Assert.NotEqual(0, id);

            UpdatePasswordItem(id, "https://a.com", "newuser", "newpass", "", "");

            PasswordItem[] items = GetItems(1);
            PasswordItem? item = items.FirstOrDefault(i => i.Id == id);

            Assert.NotNull(item);
            Assert.Equal("newuser", item.Username);
            Assert.Equal("newpass", item.Password);
        }

        [Fact]
        public void Delete_RemovesItem()
        {
            Assert.SkipUnless(_dllAvailable && _coreInitialized, "OrionBackend not available");

            long id = Create(1, "https://delete-me.com", "user", "pass");
            Assert.NotEqual(0, id);

            DeletePasswordItems([id], 1);
            _ = _createdIds.Remove(id);

            PasswordItem[] items = GetItems(1);
            Assert.DoesNotContain(items, i => i.Id == id);
        }

        [Fact]
        public void UnicodeValues_RoundTrip()
        {
            Assert.SkipUnless(_dllAvailable && _coreInitialized, "OrionBackend not available");

            long id = Create(1, "https://unicode-test.jp", "太郎", "üöäß");
            Assert.NotEqual(0, id);

            PasswordItem[] items = GetItems(1);
            PasswordItem? item = items.FirstOrDefault(i => i.Id == id);

            Assert.NotNull(item);
            Assert.Equal("太郎", item.Username);
            Assert.Equal("üöäß", item.Password);
        }

        [Fact]
        public void SpecialCharactersInPassword_SurviveRoundTrip()
        {
            Assert.SkipUnless(_dllAvailable && _coreInitialized, "OrionBackend not available");

            string injectionAttempt = "]\n},{\"id\":999,\"url\":\"hacked\"},[";
            long id = Create(1, "https://test.com", "user", injectionAttempt);
            Assert.NotEqual(0, id);

            PasswordItem[] items = GetItems(1);
            PasswordItem? item = items.FirstOrDefault(i => i.Id == id);

            Assert.NotNull(item);
            Assert.Equal(injectionAttempt, item.Password);
        }
    }
}
