#pragma once

#include <core/webextensions/web_view.h>
#include <core/webkit_bridge/API/bridge.h>

#include <Windows.h>
#include <cstdint>
#include <string>
#include <vector>

namespace orion {

// Engine-side half of the core module's host/bridge contract, owned by the
// Windows port (the core no longer defines a ScriptBridge type).
//
// The facade implements the core's orion::bridge::WebViewHost (one per
// browsing view) and orion::bridge::WebViewFactoryHost (one per profile
// world) in bridge_hosts.cpp and forwards their native operations here, with
// the view's HWND (or the profile id) made explicit so the engine plugin keeps
// no per-host state. One instance per engine, returned by
// BrowserEngine::createScriptBridge(); CEF returns nullptr (no extension
// support yet), which the facade treats as "extensions unsupported".
class ScriptBridge {
public:
    using WebViewMessageHandler = bridge::WebViewHost::WebViewMessageHandler;

    virtual ~ScriptBridge() = default;

    // ---- per browsing view (bridge::WebViewHost) ----

    // Runs `body` as an async JavaScript function with argument0_json bound
    // to "arg0", in the named script world (empty: page world).
    virtual void web_view_call(
        HWND hwnd,
        const std::string& body,
        const std::string& argument0_json,
        const std::string& script_world) = 0;

    // Injects a user script restricted to documents matching `matches` and
    // not `exclude_matches` (WebCore match patterns; empty matches = inject
    // nowhere), in the named script world (empty: page world).
    virtual void web_view_add_script(
        HWND hwnd,
        const std::vector<std::string>& matches,
        const std::vector<std::string>& exclude_matches,
        const std::string& script,
        const std::string& script_world,
        WebViewScriptInjectionTime injection_time,
        WebViewScriptFrames frames) = 0;

    // Registers window.webkit.messageHandlers.<name>. `context` is opaque;
    // the engine must call destroy_context exactly once when the handler is
    // torn down or registration fails.
    virtual bool web_view_register_script_message_handler(
        HWND hwnd,
        const std::string& name,
        WebViewMessageHandler handler,
        void* context,
        void(*destroy_context)(void* context),
        const std::string& script_world) = 0;

    virtual std::string web_view_security_origin_get_host(HWND hwnd) = 0;
    virtual std::string web_view_uri(HWND hwnd) = 0;

    // ---- per profile world (bridge::WebViewFactoryHost) ----

    // The core asks the world's browser context to route `registration.scheme`
    // (chrome-extension, moz-extension, extension-proxy) through `handler`.
    // Windows runs one world per profile process, so the engine may keep a
    // single scheme -> handler table.
    virtual void register_scheme(
        int64_t profile_id,
        const WebExtensions::WebExtensionSchemeRegistration& registration,
        WebExtensions::WebExtensionSchemeRequestHandler handler) = 0;

    // Extension background pages requested/removed by the core. Not
    // implemented on Windows yet (no hidden extension web views).
    virtual void update_background_pages(
        int64_t profile_id,
        const WebExtensions::WebExtensionBackgroundPagesChange& change) = 0;
};

// Defined in script_bridge_webkit.cpp (OrionEngineWK).
ScriptBridge* create_webkit_script_bridge();

} // namespace orion
