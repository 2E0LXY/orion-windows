#if DISABLE_XAML_GENERATED_MAIN
#if !WINAPPSDKSELFCONTAINED && !PACKAGED
using Microsoft.Windows.ApplicationModel.DynamicDependency;
#endif

namespace Orion
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            // CEF re-launches the exe for renderer/GPU/utility subprocesses.
            // Detect this early and exit before WinUI startup.
            OrionBackend.Initialize();
            int subprocessExitCode = OrionBackend.CheckBrowserSubprocess();
            if (subprocessExitCode >= 0)
                return;

            // One profile per process: resolve which profile this process
            // hosts (running the first-launch registry migration if needed),
            // then either claim it or forward to the instance that owns it.
            long requestedProfile = 0;
            foreach (string a in args)
            {
                if (a.StartsWith("--profile=", System.StringComparison.Ordinal)
                    && long.TryParse(a["--profile=".Length..], out long parsed))
                {
                    requestedProfile = parsed;
                }
            }
            // A cold start (no --profile) reopens the profiles that were open
            // when the user last quit Orion (one process per profile): the
            // profile focused at quit hosts this process, the rest are spawned
            // by App.OnLaunched. The list is written by the quit path and
            // consumed here exactly once, so profiles closed individually
            // later don't come back.
            if (requestedProfile == 0)
            {
                // Opens/migrates the registry DB so GetAppSetting works.
                _ = OrionBackend.ResolveStartupProfile(0);
                string reopenCsv = OrionBackend.GetAppSetting(
                    "settings::general::reopen_profiles", "");
                if (reopenCsv.Length > 0)
                {
                    OrionBackend.SetAppSetting("settings::general::reopen_profiles", "");
                    foreach (string part in reopenCsv.Split(','))
                    {
                        // Skip ids whose profile no longer exists.
                        if (!long.TryParse(part, out long id)
                            || OrionBackend.ResolveStartupProfile(id) != id)
                        {
                            continue;
                        }
                        if (requestedProfile == 0)
                            requestedProfile = id;
                        else if (id != requestedProfile && !App.ReopenProfileIds.Contains(id))
                            App.ReopenProfileIds.Add(id);
                    }
                }
            }

            long profileId = OrionBackend.ResolveStartupProfile(requestedProfile);
            if (profileId == 0)
                profileId = requestedProfile;  // InitOrionCore will surface the error
            App.StartupProfileId = profileId;
            if (!SingleInstance.TryAcquire(profileId))
                return;

            // [STAThread] can't be used — it inits COM as STA before Main,
            // conflicting with CEF GPU subprocess which needs MTA.
            System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.Unknown);
            System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.STA);

#if !WINAPPSDKSELFCONTAINED && !PACKAGED
            bool bootstrapInitialized = Bootstrap.TryInitialize(0x00010008, out int hresult);

            if (!bootstrapInitialized)
            {
                throw new System.Exception(
                    $"Failed to initialize Windows App SDK. HRESULT: 0x{hresult:X8}. " +
                    "Install the Windows App SDK 1.8 runtime from https://aka.ms/windowsappsdk");
            }
#endif

#if PACKAGED
            try
            {
#endif
            Microsoft.UI.Xaml.Application.Start(p =>
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
#if PACKAGED
            }
            catch (System.Runtime.InteropServices.COMException ex)
                when (ex.HResult == unchecked((int)0x80040154)) // REGDB_E_CLASSNOTREG
            {
                throw new System.InvalidOperationException(
                    "WinRT classes not registered — the MSIX package hasn't been deployed. " +
                    "In Visual Studio, set 'OrionPackage' (not 'Orion') as the startup project.", ex);
            }
#endif

#if !WINAPPSDKSELFCONTAINED && !PACKAGED
            Bootstrap.Shutdown();
#endif
        }
    }
}
#endif
