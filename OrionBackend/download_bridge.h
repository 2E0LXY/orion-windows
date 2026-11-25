#pragma once

#include <cstdint>
#include <string>
#include <chrono>
#include <functional>

// Internal bridge between engine download callbacks and the core module.
// Defined in exports.cpp; called from WebKitBrowserWindow.cpp and cef.cpp.

struct ActiveDownload {
    int64_t download_id = 0;
    int64_t total_bytes_written = 0;
    std::chrono::steady_clock::time_point last_progress_flush{};
};

int64_t bridgeCreateDownload(int64_t profileId, const std::string& name,
    const std::string& path, const std::string& originUri, int64_t totalSize);

void bridgeUpdateDownloadProgress(int64_t downloadId, int64_t currentSize,
    int64_t totalSize);

void bridgeFinishDownload(int64_t downloadId, int64_t finalSize);

void bridgeFailDownload(int64_t downloadId);

void bridgeRegisterCancelHandler(int64_t downloadId, std::function<void()> handler);
bool bridgeUnregisterCancelHandler(int64_t downloadId);
bool bridgeCancelDownload(int64_t downloadId);

struct PendingRestart {
    int64_t downloadId;
    std::string filePath;
};

bool bridgeGetDownloadFolder(int64_t profileId, std::wstring& folderPath);
bool bridgeShouldAskDownloadLocation(int64_t profileId);

void bridgeUpdateDownloadPath(int64_t downloadId, const std::string& name,
    const std::string& path);

bool bridgeHasPendingRestart(const std::string& url);
PendingRestart bridgeConsumePendingRestart(const std::string& url);
