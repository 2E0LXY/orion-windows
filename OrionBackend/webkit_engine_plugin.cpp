#include "pch.h"

#include "forwarding_engine.h"
#include "host_shim.h"
#include "script_bridge.h"

namespace {

class WebKitEngine final : public ForwardingEngine {
public:
    EngineId engineId() const override { return EngineId::WebKit; }
    orion::ScriptBridge* createScriptBridge() override { return orion::create_webkit_script_bridge(); }
};

WebKitEngine g_engine;

} // namespace

extern "C" __declspec(dllexport) BrowserEngine* __cdecl OrionCreateEngine(const HostServices* host)
{
    return orionHostShimInit(host) ? &g_engine : nullptr;
}
