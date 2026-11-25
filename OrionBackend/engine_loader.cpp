#include "pch.h"
#include "engine_loader.h"

#include "browser_engine.h"
#include "download_bridge.h"
#include "generated/orion_version.h"
#include "host_api.h"
#include "host_services.h"
#include "utils.h"

#include <src/file_system.h>
#include <src/log.h>

#include <cstdarg>
#include <filesystem>
#include <mutex>

static std::mutex g_load_mutex;
static BrowserEngine* g_engine = nullptr;
static std::string g_engine_name;

static void hostLogVa(char level, const char* format, va_list args)
{
    orion::log(std::string(1, level), format, args);
}

static std::filesystem::path facadeDir()
{
    return moduleDirFromAddress(reinterpret_cast<const void*>(&facadeDir));
}

BrowserEngine* orionLoadedEngine()
{
    return g_engine;
}

BrowserEngine* orionLoadEngine(const char* name, std::string& error)
{
    std::lock_guard lock(g_load_mutex);

    std::string engine = name ? name : "";
    if (engine != "webkit" && engine != "blink") {
        error = "unknown engine '" + engine + "'";
        return nullptr;
    }
    if (g_engine) {
        if (engine == g_engine_name)
            return g_engine;
        error = "engine '" + g_engine_name + "' is already loaded; cannot load '" + engine + "'";
        return nullptr;
    }

    const wchar_t* dllName = engine == "webkit" ? L"OrionEngineWK.dll" : L"OrionEngineB.dll";
    std::filesystem::path dir = facadeDir() / L"engines" / widen(engine);
    std::filesystem::path dllPath = dir / dllName;

    // LOAD_WITH_ALTERED_SEARCH_PATH resolves the plugin's static imports
    // (WebKit2.dll / libcef.dll and their dependencies) from its own
    // directory instead of the exe directory.
    HMODULE plugin = LoadLibraryExW(dllPath.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (!plugin) {
        // Capture before building the message: narrow() makes Win32 calls
        // that would clobber the thread's last error.
        const DWORD load_error = GetLastError();
        error = "LoadLibrary failed for " + narrow(dllPath.wstring())
            + " (error " + std::to_string(load_error) + ")";
        return nullptr;
    }

    auto createEngine = reinterpret_cast<OrionCreateEngineFn>(
        GetProcAddress(plugin, "OrionCreateEngine"));
    if (!createEngine) {
        error = "OrionCreateEngine export missing in " + narrow(dllPath.wstring());
        FreeLibrary(plugin);
        return nullptr;
    }

    static HostServices services = {};
    services.struct_size = sizeof(HostServices);
    services.abi_version = kOrionEngineAbiVersion;
    services.buildId = ORION_VERSION;
    services.toolset = _MSC_FULL_VER;
    services.logVa = &hostLogVa;
    services.bridgeCreateDownload = &bridgeCreateDownload;
    services.bridgeUpdateDownloadProgress = &bridgeUpdateDownloadProgress;
    services.bridgeFinishDownload = &bridgeFinishDownload;
    services.bridgeFailDownload = &bridgeFailDownload;
    services.bridgeRegisterCancelHandler = &bridgeRegisterCancelHandler;
    services.bridgeUnregisterCancelHandler = &bridgeUnregisterCancelHandler;
    services.bridgeCancelDownload = &bridgeCancelDownload;
    services.bridgeGetDownloadFolder = &bridgeGetDownloadFolder;
    services.bridgeShouldAskDownloadLocation = &bridgeShouldAskDownloadLocation;
    services.bridgeUpdateDownloadPath = &bridgeUpdateDownloadPath;
    services.bridgeHasPendingRestart = &bridgeHasPendingRestart;
    services.bridgeConsumePendingRestart = &bridgeConsumePendingRestart;
    services.getProfileDataPath = &getProfileDataPath;
    services.dataDirectoryPath = &orion::data_directory_path;
    services.getCreatePopupCallback = &getCreatePopupCallback;
    services.getPopupReadyCallback = &getPopupReadyCallback;
    services.getOpenURLCallback = &getOpenURLCallback;

    BrowserEngine* result = createEngine(&services);
    if (!result) {
        error = "engine plugin " + narrow(dllPath.wstring()) + " rejected the host (ABI or build mismatch)";
        FreeLibrary(plugin);
        return nullptr;
    }

    g_engine = result;
    g_engine_name = engine;
    orion::log_info("Loaded browser engine '%s' from %s",
        engine.c_str(), narrow(dllPath.wstring()).c_str());
    return g_engine;
}
