#include "pch.h"

#include "forwarding_engine.h"
#include "host_shim.h"

namespace {

class CefEngine final : public ForwardingEngine {
public:
    EngineId engineId() const override { return EngineId::Blink; }
    // The CEF port has no extension runtime yet; the host treats a null
    // bridge as "extensions unsupported" (NullScriptBridge).
    orion::ScriptBridge* createScriptBridge() override { return nullptr; }
};

CefEngine g_engine;

} // namespace

extern "C" __declspec(dllexport) BrowserEngine* __cdecl OrionCreateEngine(const HostServices* host)
{
    return orionHostShimInit(host) ? &g_engine : nullptr;
}
