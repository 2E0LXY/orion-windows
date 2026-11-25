#include "pch.h"
#include "notification_bridge.h"

#include "common/src/log.h"

#include <rfl/json.hpp>

namespace orion {

namespace {
ShowNotificationCallback g_show_cb = nullptr;
ClearNotificationCallback g_clear_cb = nullptr;
} // namespace

void
HostNotificationProvider::set_callbacks(ShowNotificationCallback show, ClearNotificationCallback clear)
{
    g_show_cb = show;
    g_clear_cb = clear;
}

Error
HostNotificationProvider::show(const NotificationRequest& request)
{
    if (!g_show_cb) {
        log_warning("notifications: no C# presenter registered; dropping '%s'",
            request.notification_id.c_str());
        SUCCEED();
    }
    const std::string json = rfl::json::write(request);
    g_show_cb(json.c_str());
    SUCCEED();
}

Error
HostNotificationProvider::clear(
    NotificationSource source, const std::string& source_id, const std::string& notification_id)
{
    if (g_clear_cb)
        g_clear_cb(static_cast<int32_t>(source), source_id.c_str(), notification_id.c_str());
    SUCCEED();
}

} // namespace orion
