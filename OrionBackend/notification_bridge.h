#pragma once

#include "core/notification_provider.h"

#include <cstdint>
#include <string>

// C# callbacks registered through the SetNotificationCallbacks export. Strings
// are UTF-8 and valid only for the duration of the call.
typedef void(__cdecl* ShowNotificationCallback)(const char* request_json);
typedef void(__cdecl* ClearNotificationCallback)(
    int32_t source, const char* source_id, const char* notification_id);

namespace orion {

// The core's NotificationProvider for Windows: forwards to the C#
// NotificationService (Orion/Services/NotificationService.cs), which owns the
// Windows App SDK toast plumbing. The request travels as JSON (snake_case
// member names, enum values as their names) so the nested button/item arrays
// need no hand-maintained wire struct. Interaction comes back through the
// NotifyNotificationEvent export.
class HostNotificationProvider final : public NotificationProvider {
public:
    static void set_callbacks(ShowNotificationCallback show, ClearNotificationCallback clear);

    Error show(const NotificationRequest& request) override;
    Error clear(
        NotificationSource source, const std::string& source_id,
        const std::string& notification_id) override;
};

} // namespace orion
