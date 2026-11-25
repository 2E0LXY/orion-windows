#include "pch.h"
#include "bridge_hosts.h"

#include <src/log.h>

namespace orion::win {

// ---------------------------------------------------------------- ProfileHost

ProfileHost::ProfileHost(CoreApi& core_api, ProfileId profile_id)
    : m_attachment(core_api.attach_profile_host(profile_id, *this))
{
    // Menu and clipboard hosts are not attached on Windows yet: the bridges
    // no-op without a host (context menus are app-drawn from C#).
}

ProfileHost::~ProfileHost() = default;

// -------------------------------------------------------- WebViewFactoryHost

WebViewFactoryHost::WebViewFactoryHost(
    bridge::WebViewFactoryBridge& bridge, ScriptBridge& engine, ProfileId profile_id)
    : m_bridge(bridge.weak_from_this())
    , m_engine(engine)
    , m_profile_id(profile_id)
    , m_attachment(bridge.attach(*this))
{
}

WebViewFactoryHost::~WebViewFactoryHost() = default;

void WebViewFactoryHost::initialize_platform()
{
    // The engine creates its per-profile browser context lazily with the
    // first view; nothing to prepare here.
    orion::log_debug("BridgeHosts> world for profile %lld initialized", (long long)m_profile_id.value);
}

void WebViewFactoryHost::register_scheme(
    const WebExtensions::WebExtensionSchemeRegistration& registration,
    WebExtensions::WebExtensionSchemeRequestHandler handler)
{
    m_engine.register_scheme(m_profile_id.value, registration, std::move(handler));
}

void WebViewFactoryHost::update_background_pages(
    const WebExtensions::WebExtensionBackgroundPagesChange& change)
{
    m_engine.update_background_pages(m_profile_id.value, change);
}

// --------------------------------------------------------------- WebViewHost

WebViewHost::WebViewHost(bridge::WebViewBridge& bridge, ScriptBridge& engine, HWND hwnd)
    : m_bridge(bridge.weak_from_this())
    , m_engine(engine)
    , m_hwnd(hwnd)
    , m_attachment(bridge.attach(*this))
{
}

WebViewHost::~WebViewHost()
{
    // Detach before the members go away so a late core callback finds no host
    // (the bridge then drops the call) instead of a dangling one.
    m_attachment.reset();
}

void WebViewHost::web_view_call(
    const std::string& body, const std::string& argument0_json,
    const std::string& script_world)
{
    m_engine.web_view_call(m_hwnd, body, argument0_json, script_world);
}

void WebViewHost::web_view_add_script(
    const std::vector<std::string>& matches,
    const std::vector<std::string>& exclude_matches,
    const std::string& script, const std::string& script_world,
    WebViewScriptInjectionTime injection_time,
    WebViewScriptFrames frames)
{
    m_engine.web_view_add_script(
        m_hwnd, matches, exclude_matches, script, script_world, injection_time, frames);
}

bool WebViewHost::web_view_register_script_message_handler(
    const std::string& name, WebViewMessageHandler handler,
    void* context, void(*destroy_context)(void* context),
    const std::string& script_world)
{
    return m_engine.web_view_register_script_message_handler(
        m_hwnd, name, handler, context, destroy_context, script_world);
}

std::string WebViewHost::web_view_security_origin_get_host()
{
    return m_engine.web_view_security_origin_get_host(m_hwnd);
}

std::string WebViewHost::web_view_uri()
{
    return m_engine.web_view_uri(m_hwnd);
}

} // namespace orion::win
