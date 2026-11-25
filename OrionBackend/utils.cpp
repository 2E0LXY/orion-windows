#include "pch.h"

#include "utils.h"
#include <shlobj.h>
#include <src/uuid.h>
#include <src/file_system.h>
#include <src/log.h>

// Helpers: allocate strings with CoTaskMemAlloc for C# to free

char* ReturnString(const std::string& str)
{
    char* buf = static_cast<char*>(CoTaskMemAlloc(str.size() + 1));
    if (buf) memcpy(buf, str.c_str(), str.size() + 1);
    return buf;
}

char* ReturnString(const char* str)
{
    if (!str) return nullptr;
    size_t len = strlen(str) + 1;
    char* buf = static_cast<char*>(CoTaskMemAlloc(len));
    if (buf) memcpy(buf, str, len);
    return buf;
}

wchar_t* ReturnWString(const std::wstring& str)
{
    size_t bytes = (str.size() + 1) * sizeof(wchar_t);
    wchar_t* buf = static_cast<wchar_t*>(CoTaskMemAlloc(bytes));
    if (buf) memcpy(buf, str.c_str(), bytes);
    return buf;
}

wchar_t* ReturnWString(const wchar_t* str)
{
    if (!str) return nullptr;
    size_t len = wcslen(str) + 1;
    wchar_t* buf = static_cast<wchar_t*>(CoTaskMemAlloc(len * sizeof(wchar_t)));
    if (buf) memcpy(buf, str, len * sizeof(wchar_t));
    return buf;
}

bool getKnownFolderPath(const GUID& id, std::wstring& knownFolderPath)
{
    PWSTR path = nullptr;
    if (FAILED(SHGetKnownFolderPath(id, KF_FLAG_CREATE, 0, &path)))
        return false;
    knownFolderPath = std::wstring(path);
    CoTaskMemFree(path);
    return true;
}

static const char* engineDirName(EngineId engineId)
{
    switch (engineId) {
    case EngineId::WebKit: return "webkit";
    case EngineId::Blink:  return "blink";
    default:               return "unknown";
    }
}

static std::filesystem::path engineProfileDataPath(int64_t profileId, EngineId engineId)
{
    std::string seed = std::to_string(profileId) + "_" + std::to_string(static_cast<int32_t>(engineId));
    std::string uuid = orion::generate_uuid_v4(seed);
    // Blink: the per-profile CEF root_cache_path (user data dir); site data
    // lands in <path>\Default. Per-profile roots keep CEF's process-singleton
    // lock (keyed on root_cache_path) compatible with one process per profile.
    std::vector<std::string> nodes = engineId == EngineId::Blink
        ? std::vector<std::string>{engineDirName(engineId), uuid}
        : std::vector<std::string>{engineDirName(engineId), "Profiles", uuid};
    return std::filesystem::path(orion::data_directory_path("com.kagi.orion", nodes));
}

std::filesystem::path getProfileDataPath(int64_t profileId, EngineId engineId)
{
    std::filesystem::path path = engineProfileDataPath(profileId, engineId);
    bool created = std::filesystem::create_directories(path);
    if (created)
        orion::log_info("Created profile data directory: %s", path.string().c_str());
    return path;
}

// Deletes one data dir of a deleted profile, retrying until
// |unlockDeadlineTick| (GetTickCount64 time): the owner's handles release
// only at its termination, and the engines' child processes exit only after
// engine shutdown returns — remove_all fails until they let go (files they
// hold are delete-pending and keep the dir non-empty). The dir is deleted
// under its own name: ids are never reused, so nothing can observe or
// reattach a half-deleted dir, and a failed or interrupted delete (logged)
// is resumed by any later sweep — the permanent registry row keeps naming it.
static void deleteDir(const std::filesystem::path& dir, const char* what,
    int64_t profileId, ULONGLONG unlockDeadlineTick)
{
    for (;;) {
        std::error_code ec;
        std::filesystem::remove_all(dir, ec);
        if (!std::filesystem::exists(dir))
            return;
        if (GetTickCount64() >= unlockDeadlineTick) {
            orion::log_warning("could not delete %s data for profile %lld: %s",
                what, (long long)profileId, ec.message().c_str());
            return;
        }
        Sleep(50);
    }
}

void deleteProfileData(int64_t profileId, uint32_t unlockWaitMs)
{
    const ULONGLONG deadline = GetTickCount64() + unlockWaitMs;
    std::filesystem::path coreDir =
        std::filesystem::path(orion::data_directory_path("com.kagi.orion", {"Profiles"}))
            / std::to_string(profileId);
    deleteDir(coreDir, "core", profileId, deadline);
    for (EngineId engine : {EngineId::WebKit, EngineId::Blink})
        deleteDir(engineProfileDataPath(profileId, engine), engineDirName(engine),
            profileId, deadline);
}

