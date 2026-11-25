#include "pch.h"
#include "host_shim.h"

#include "download_bridge.h"
#include "generated/orion_version.h"
#include "host_api.h"
#include "utils.h"

#include <src/file_system.h>
#include <src/log.h>

#include <cstdarg>
#include <cstdio>
#include <cstring>

// Compiled into the engine plugins only, never into the facade: it defines
// the facade-provided symbols (declared in log.h, file_system.h,
// download_bridge.h, utils.h, host_api.h) as forwarders into the
// HostServices table, so engine code links without orion_core.lib. Any use
// of a core symbol without a forwarder here fails the plugin link.

// Pre-init fallback: plugin code can log during DLL static init (e.g. the
// CEF early-logging hook installs at load time), before OrionCreateEngine
// delivers the HostServices table. Route those lines to OutputDebugString
// rather than calling through a null pointer inside LoadLibrary.
static void fallbackLogVa(char level, const char* format, va_list args)
{
    char line[1024];
    int prefix = snprintf(line, sizeof(line), "[%c] ", level);
    vsnprintf(line + prefix, sizeof(line) - prefix, format, args);
    OutputDebugStringA(line);
}

static HostServices g_host = { .logVa = &fallbackLogVa };

bool orionHostShimInit(const HostServices* host)
{
    if (!host || host->abi_version != kOrionEngineAbiVersion
        || host->struct_size < sizeof(HostServices))
        return false;
    // C++ types cross the table: refuse a facade from a different release
    // or toolset rather than risk silent ABI drift (see host_services.h).
    if (!host->buildId || strcmp(host->buildId, ORION_VERSION) != 0
        || host->toolset != _MSC_FULL_VER)
        return false;
    g_host = *host;
    return true;
}

namespace orion {

void log_info(const char* format, ...)
{
    va_list args{};
    va_start(args, format);
    g_host.logVa('I', format, args);
    va_end(args);
}

void log_error(const char* format, ...)
{
    va_list args{};
    va_start(args, format);
    g_host.logVa('E', format, args);
    va_end(args);
}

void log_error(const std::string& message)
{
    log_error("%s", message.c_str());
}

void log_warning(const char* format, ...)
{
    va_list args{};
    va_start(args, format);
    g_host.logVa('W', format, args);
    va_end(args);
}

// Matches the core definition: debug logging compiles out in release.
void log_debug(const char* format, ...)
{
#ifndef NDEBUG
    va_list args{};
    va_start(args, format);
    g_host.logVa('D', format, args);
    va_end(args);
#endif
}

std::string data_directory_path(const char* app_id, const std::vector<std::string>& nodes)
{
    return g_host.dataDirectoryPath(app_id, nodes);
}

} // namespace orion

std::filesystem::path getProfileDataPath(int64_t profileId, EngineId engineId)
{
    return g_host.getProfileDataPath(profileId, engineId);
}

int64_t bridgeCreateDownload(int64_t profileId, const std::string& name,
    const std::string& path, const std::string& originUri, int64_t totalSize)
{
    return g_host.bridgeCreateDownload(profileId, name, path, originUri, totalSize);
}

void bridgeUpdateDownloadProgress(int64_t downloadId, int64_t currentSize, int64_t totalSize)
{
    g_host.bridgeUpdateDownloadProgress(downloadId, currentSize, totalSize);
}

void bridgeFinishDownload(int64_t downloadId, int64_t finalSize)
{
    g_host.bridgeFinishDownload(downloadId, finalSize);
}

void bridgeFailDownload(int64_t downloadId)
{
    g_host.bridgeFailDownload(downloadId);
}

void bridgeRegisterCancelHandler(int64_t downloadId, std::function<void()> handler)
{
    g_host.bridgeRegisterCancelHandler(downloadId, std::move(handler));
}

bool bridgeUnregisterCancelHandler(int64_t downloadId)
{
    return g_host.bridgeUnregisterCancelHandler(downloadId);
}

bool bridgeCancelDownload(int64_t downloadId)
{
    return g_host.bridgeCancelDownload(downloadId);
}

bool bridgeGetDownloadFolder(int64_t profileId, std::wstring& folderPath)
{
    return g_host.bridgeGetDownloadFolder(profileId, folderPath);
}

bool bridgeShouldAskDownloadLocation(int64_t profileId)
{
    return g_host.bridgeShouldAskDownloadLocation(profileId);
}

void bridgeUpdateDownloadPath(int64_t downloadId, const std::string& name, const std::string& path)
{
    g_host.bridgeUpdateDownloadPath(downloadId, name, path);
}

bool bridgeHasPendingRestart(const std::string& url)
{
    return g_host.bridgeHasPendingRestart(url);
}

PendingRestart bridgeConsumePendingRestart(const std::string& url)
{
    return g_host.bridgeConsumePendingRestart(url);
}

CreatePopupCallback getCreatePopupCallback() { return g_host.getCreatePopupCallback(); }
PopupReadyCallback getPopupReadyCallback() { return g_host.getPopupReadyCallback(); }
OpenURLCallback getOpenURLCallback() { return g_host.getOpenURLCallback(); }
