#include "pch.h"
#include "dpapi_password_storage.h"

#include "common/src/file_system.h"
#include "common/src/log.h"

#include <rfl/json.hpp>

#include <wincrypt.h>
#pragma comment(lib, "crypt32.lib")

#include <filesystem>
#include <fstream>

namespace orion {

static void secure_zero_string(std::string& s)
{
    if (!s.empty())
        SecureZeroMemory(s.data(), s.size());
    s.clear();
}

static std::filesystem::path
password_file_path(const char* app_id)
{
    std::string dir = data_directory_path(app_id, {"passwords.dpapi"});
    return std::filesystem::path(std::u8string_view(reinterpret_cast<const char8_t*>(dir.data()), dir.size()));
}

struct PasswordItemJson {
    int64_t id = 0;
    int64_t profile_id = 0;
    std::string url;
    std::string username;
    std::string password;
    std::string verification_key;
    std::string verification_code;

    ~PasswordItemJson()
    {
        secure_zero_string(password);
    }
};

static std::string
serialize_items(const std::vector<PasswordItemDto>& items)
{
    std::vector<PasswordItemJson> json_items;
    json_items.reserve(items.size());
    for (const auto& item : items) {
        json_items.push_back({
            .id = item.id.value,
            .profile_id = item.profile_id.value,
            .url = item.url,
            .username = item.username,
            .password = item.password,
            .verification_key = item.verification_key,
            .verification_code = item.verification_code,
        });
    }
    std::string result = rfl::json::write(json_items);
    // json_items destructors zero the password fields via ~PasswordItemJson
    return result;
}

static Error
deserialize_items(const std::string& json, std::vector<PasswordItemDto>* out_items)
{
    RAISE_CHECK(out_items);
    if (json.empty()) SUCCEED();

    auto result = rfl::json::read<std::vector<PasswordItemJson>>(json);
    if (!result) {
        RAISE(ErrorCode::io, "failed to parse password JSON: %s", result.error().what());
    }

    for (const auto& j : result.value()) {
        out_items->push_back({
            .id = PasswordItemId(j.id),
            .profile_id = ProfileId(j.profile_id),
            .url = j.url,
            .username = j.username,
            .password = j.password,
            .verification_key = j.verification_key,
            .verification_code = j.verification_code,
        });
    }

    SUCCEED();
}

static Error
dpapi_encrypt(const std::string& plaintext, std::vector<BYTE>* out)
{
    RAISE_CHECK(out);
    DATA_BLOB in_blob;
    in_blob.pbData = reinterpret_cast<BYTE*>(const_cast<char*>(plaintext.data()));
    in_blob.cbData = static_cast<DWORD>(plaintext.size());

    DATA_BLOB out_blob{};
    if (!CryptProtectData(&in_blob, nullptr, nullptr, nullptr, nullptr, 0, &out_blob)) {
        RAISE(ErrorCode::io, "CryptProtectData failed: %lu", GetLastError());
    }

    out->assign(out_blob.pbData, out_blob.pbData + out_blob.cbData);
    LocalFree(out_blob.pbData);
    SUCCEED();
}

static Error
dpapi_decrypt(const std::vector<BYTE>& ciphertext, std::string* out)
{
    RAISE_CHECK(out);
    if (ciphertext.empty()) {
        *out = "[]";
        SUCCEED();
    }

    DATA_BLOB in_blob;
    in_blob.pbData = const_cast<BYTE*>(ciphertext.data());
    in_blob.cbData = static_cast<DWORD>(ciphertext.size());

    DATA_BLOB out_blob{};
    if (!CryptUnprotectData(&in_blob, nullptr, nullptr, nullptr, nullptr, 0, &out_blob)) {
        RAISE(ErrorCode::io, "CryptUnprotectData failed: %lu", GetLastError());
    }

    out->assign(reinterpret_cast<char*>(out_blob.pbData), out_blob.cbData);
    LocalFree(out_blob.pbData);
    SUCCEED();
}

Error
DpapiPasswordStorage::upsert_password_items(const char* app_id, const std::vector<PasswordItemDto>& items)
{
    auto path = password_file_path(app_id);
    VERIFY(ensure_parent_directories(path));

    std::string json = serialize_items(items);

    std::vector<BYTE> encrypted;
    Error encrypt_err = dpapi_encrypt(json, &encrypted);
    secure_zero_string(json);
    VERIFY(encrypt_err);

    auto tmp_path = path;
    tmp_path += L".tmp";

    {
        std::ofstream file(tmp_path.wstring(), std::ios::binary | std::ios::trunc);
        if (!file) {
            RAISE(ErrorCode::io, "failed to open password temp file for writing: %s", tmp_path.u8string().c_str());
        }
        file.write(reinterpret_cast<const char*>(encrypted.data()), encrypted.size());
        if (!file) {
            RAISE(ErrorCode::io, "failed to write password temp file: %s", tmp_path.u8string().c_str());
        }
    }

    std::error_code ec;
    std::filesystem::rename(tmp_path, path, ec);
    if (ec) {
        std::filesystem::remove(tmp_path, ec);
        RAISE(ErrorCode::io, "failed to rename password temp file: %s", ec.message().c_str());
    }

    SUCCEED();
}

Error
DpapiPasswordStorage::get_password_items(const char* app_id, std::vector<PasswordItemDto>* out_items)
{
    RAISE_CHECK(out_items);

    auto path = password_file_path(app_id);

    std::error_code ec;
    if (!std::filesystem::exists(path, ec)) {
        SUCCEED();
    }

    std::ifstream file(path.wstring(), std::ios::binary);
    if (!file) {
        RAISE(ErrorCode::io, "failed to open password file for reading: %s", path.u8string().c_str());
    }

    std::vector<BYTE> encrypted(
        (std::istreambuf_iterator<char>(file)),
        std::istreambuf_iterator<char>()
    );

    if (encrypted.empty()) {
        SUCCEED();
    }

    std::string json;
    VERIFY(dpapi_decrypt(encrypted, &json));
    Error parse_err = deserialize_items(json, out_items);
    secure_zero_string(json);
    VERIFY(parse_err);
    SUCCEED();
}

} // namespace orion
