#pragma once

#include "browser_engine_types.h"
#include "download_bridge.h"

#include <cstdarg>
#include <filesystem>
#include <memory>
#include <string>
#include <vector>


// ABI version of the HostServices table. Additive changes (new fields at the
// end) only grow struct_size; breaking changes bump kOrionEngineAbiVersion.
inline constexpr uint32_t kOrionEngineAbiVersion = 4;

// Function table the facade (OrionBackend.dll) hands to an engine plugin via
// OrionCreateEngine. The plugin's host_shim.cpp forwards the facade-provided
// symbols (orion::log_*, bridge*, getProfileDataPath, ...) through this
// table, so engine code links without orion_core.lib. C++ types cross this
// boundary; all Orion DLLs build in one solution with the same toolset and
// dynamic CRT.
struct HostServices {
    uint32_t struct_size;   // sizeof(HostServices) as built by the facade
    uint32_t abi_version;   // kOrionEngineAbiVersion as built by the facade

    // Logging; level is 'I', 'E', 'W' or 'D' (matches orion::log levels).
    void (*logVa)(char level, const char* format, va_list args);

    // Download bridge (download_bridge.h).
    int64_t (*bridgeCreateDownload)(int64_t profileId, const std::string& name,
        const std::string& path, const std::string& originUri, int64_t totalSize);
    void (*bridgeUpdateDownloadProgress)(int64_t downloadId, int64_t currentSize, int64_t totalSize);
    void (*bridgeFinishDownload)(int64_t downloadId, int64_t finalSize);
    void (*bridgeFailDownload)(int64_t downloadId);
    void (*bridgeRegisterCancelHandler)(int64_t downloadId, std::function<void()> handler);
    bool (*bridgeUnregisterCancelHandler)(int64_t downloadId);
    bool (*bridgeCancelDownload)(int64_t downloadId);
    bool (*bridgeGetDownloadFolder)(int64_t profileId, std::wstring& folderPath);
    bool (*bridgeShouldAskDownloadLocation)(int64_t profileId);
    void (*bridgeUpdateDownloadPath)(int64_t downloadId, const std::string& name, const std::string& path);
    bool (*bridgeHasPendingRestart)(const std::string& url);
    PendingRestart (*bridgeConsumePendingRestart)(const std::string& url);

    // Paths.
    std::filesystem::path (*getProfileDataPath)(int64_t profileId, EngineId engineId);
    std::string (*dataDirectoryPath)(const char* app_id, const std::vector<std::string>& nodes);

    // Popup/OpenURL callbacks registered by C#; storage lives in the facade.
    // (No engineDir here: plugins derive their own directory from their
    // module handle — see moduleDirFromAddress in utils.h — so the host and
    // plugin can never disagree about where the engine lives.)
    CreatePopupCallback (*getCreatePopupCallback)();
    PopupReadyCallback (*getPopupReadyCallback)();
    OpenURLCallback (*getOpenURLCallback)();

    // Build identity of the facade. C++ types cross this table, so facade
    // and plugin must come from the same build: the version check alone
    // cannot catch a stale plugin from another release or toolset whose
    // table shape still matches (STL/struct internals may have drifted).
    const char* buildId;  // ORION_VERSION
    uint32_t toolset;     // _MSC_FULL_VER
};
