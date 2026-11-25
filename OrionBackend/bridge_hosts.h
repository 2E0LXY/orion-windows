#pragma once

// Facade-side implementations of the core module's host contracts
// (core/webkit_bridge/API/bridge.h; embedding guide: the core module's
// BUILDING.md, "Embedding: hosts and bridges"). The core owns the *Bridge
// objects; these hosts attach to them and forward native operations to the
// loaded engine plugin through orion::ScriptBridge (script_bridge.h).
//
// Ownership (exports.cpp):
//   one ProfileHost per profile        -> CoreApi::attach_profile_host
//   one WebViewFactoryHost per world   -> ProfileWorld::web_view_factory_bridge()
//   one WebViewHost per browsing HWND  -> WebViewFactoryBridge::create_web_view_bridge()
// Windows runs one profile per process and no private mode yet, so exactly
// one world (PrivateMode::no) is ever activated.

#include "script_bridge.h"

#include <core/core_api.h>
#include <core/webkit_bridge/API/bridge.h>

#include <Windows.h>
#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace orion::win {

// Profile lifetime anchor. Attaching creates the profile bridge and both
// worlds in the core without starting anything.
class ProfileHost final : public bridge::ProfileHost {
public:
    ProfileHost(CoreApi& core_api, ProfileId profile_id);
    ~ProfileHost() override;

private:
    bridge::HostAttachment m_attachment;
};

// Per-world native factory: scheme registration and background pages go to
// the engine; there is no Windows-side platform state to initialize.
class WebViewFactoryHost final : public bridge::WebViewFactoryHost {
public:
    WebViewFactoryHost(
        bridge::WebViewFactoryBridge& bridge, ScriptBridge& engine, ProfileId profile_id);
    ~WebViewFactoryHost() override;

    std::weak_ptr<bridge::WebViewFactoryBridge> bridge() const { return m_bridge; }

private:
    void initialize_platform() override;
    void register_scheme(
        const WebExtensions::WebExtensionSchemeRegistration& registration,
        WebExtensions::WebExtensionSchemeRequestHandler handler) override;
    void update_background_pages(
        const WebExtensions::WebExtensionBackgroundPagesChange& change) override;

    std::weak_ptr<bridge::WebViewFactoryBridge> m_bridge;
    ScriptBridge& m_engine;
    ProfileId m_profile_id;
    bridge::HostAttachment m_attachment;
};

// Per browsing view. Forwards every operation to the engine with the HWND.
class WebViewHost final : public bridge::WebViewHost {
public:
    WebViewHost(bridge::WebViewBridge& bridge, ScriptBridge& engine, HWND hwnd);
    ~WebViewHost() override;

    HWND hwnd() const { return m_hwnd; }
    std::weak_ptr<bridge::WebViewBridge> bridge() const { return m_bridge; }

    void web_view_call(
        const std::string& body, const std::string& argument0_json,
        const std::string& script_world) override;
    void web_view_add_script(
        const std::vector<std::string>& matches,
        const std::vector<std::string>& exclude_matches,
        const std::string& script, const std::string& script_world,
        WebViewScriptInjectionTime injection_time,
        WebViewScriptFrames frames) override;
    bool web_view_register_script_message_handler(
        const std::string& name, WebViewMessageHandler handler,
        void* context, void(*destroy_context)(void* context),
        const std::string& script_world) override;
    std::string web_view_security_origin_get_host() override;
    std::string web_view_uri() override;
    // Tab / window identity lives in the C# tab model; the engine layer has no
    // HWND -> tab mapping yet, so these answer "unknown", which the core treats
    // like an extension-only view (popup / background page).
    std::optional<int64_t> web_view_tab_id() override { return std::nullopt; }
    std::optional<int64_t> web_view_window_id() override { return std::nullopt; }
    std::optional<int64_t> web_view_active_tab_id() override { return std::nullopt; }

private:
    std::weak_ptr<bridge::WebViewBridge> m_bridge;
    ScriptBridge& m_engine;
    HWND m_hwnd;
    bridge::HostAttachment m_attachment;
};

} // namespace orion::win
