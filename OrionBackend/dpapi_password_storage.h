#pragma once

#include "core/password_storage.h"

namespace orion {

class DpapiPasswordStorage final : public PasswordStorage {
public:
    Error upsert_password_items(const char* app_id, const std::vector<PasswordItemDto>& items) override;
    Error get_password_items(const char* app_id, std::vector<PasswordItemDto>* out_items) override;
};

} // namespace orion
