#pragma once

#include <cstdint>
#include <filesystem>
#include <string>

#include "browser_engine_types.h"

template <typename C, typename K>
auto* get_if(C& container, const K& key) {
    auto it = container.find(key);
    return it != container.end() ? &it->second : nullptr;
}

// Convert UTF-8 std::string to UTF-16 std::wstring
inline std::wstring widen(const std::string& str) {
    if (str.empty()) return {};
    int size = MultiByteToWideChar(CP_UTF8, 0, str.data(), (int)str.size(), nullptr, 0);
    if (size <= 0) return {};
    std::wstring result(size, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, str.data(), (int)str.size(), result.data(), size);
    return result;
}

// Convert UTF-8 const char* to UTF-16 std::wstring
inline std::wstring widen(const char* str) {
    if (!str || !*str) return {};
    int size = MultiByteToWideChar(CP_UTF8, 0, str, -1, nullptr, 0);
    if (size <= 1) return {};
    std::wstring result(size - 1, L'\0'); // -1 to exclude null terminator
    MultiByteToWideChar(CP_UTF8, 0, str, -1, result.data(), size);
    return result;
}

// Convert UTF-16 std::wstring to UTF-8 std::string
inline std::string narrow(const std::wstring& str) {
    if (str.empty()) return {};
    int size = WideCharToMultiByte(CP_UTF8, 0, str.data(), (int)str.size(), nullptr, 0, nullptr, nullptr);
    if (size <= 0) return {};
    std::string result(size, '\0');
    WideCharToMultiByte(CP_UTF8, 0, str.data(), (int)str.size(), result.data(), size, nullptr, nullptr);
    return result;
}

// Convert UTF-16 const wchar_t* to UTF-8 std::string
inline std::string narrow(const wchar_t* str) {
    if (!str || !*str) return {};
    int size = WideCharToMultiByte(CP_UTF8, 0, str, -1, nullptr, 0, nullptr, nullptr);
    if (size <= 1) return {};
    std::string result(size - 1, '\0'); // -1 to exclude null terminator
    WideCharToMultiByte(CP_UTF8, 0, str, -1, result.data(), size, nullptr, nullptr);
    return result;
}

// Directory of `module` (nullptr = the process exe), empty on failure.
// Grows the buffer until GetModuleFileNameW fits — a fixed MAX_PATH buffer
// silently truncates long install paths and resolves to a garbage directory.
inline std::wstring moduleDir(HMODULE module) {
    std::wstring path;
    for (;;) {
        path.resize(path.size() + MAX_PATH);
        DWORD copied = GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
        if (copied == 0) return {};
        if (copied < path.size()) {
            path.resize(copied);
            break;
        }
    }
    return std::filesystem::path(std::move(path)).parent_path().wstring();
}

// Directory of the module (DLL/exe) containing `address`, empty on failure.
inline std::wstring moduleDirFromAddress(const void* address) {
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            static_cast<LPCWSTR>(address), &module))
        return {};
    return moduleDir(module);
}

char* ReturnString(const std::string& str);
char* ReturnString(const char* str);
wchar_t* ReturnWString(const std::wstring& str);
wchar_t* ReturnWString(const wchar_t* str);

bool getKnownFolderPath(const GUID& id, std::wstring& knownFolderPath);

// The engine-specific profile data directory, created if missing. WebKit:
// data\webkit\Profiles\<uuid>; Blink: data\blink\<uuid> (the per-profile CEF
// user data dir). <uuid> = uuid_v4 seeded with "<profileId>_<engineId>".
std::filesystem::path getProfileDataPath(int64_t profileId, EngineId engineId);

// Deletes a deleted profile's data dirs — the core dir (Profiles\<id>) and
// both engines' site-data dirs — under their own names (ids are never
// reused, so nothing can reattach a half-deleted dir, and a failed delete
// is resumed by any later sweep). Only a process that is not the profile's
// own can succeed: the owner's handles release at its termination and its
// engine children exit after it; unlockWaitMs bounds a retry bridging that
// final teardown.
void deleteProfileData(int64_t profileId, uint32_t unlockWaitMs);
