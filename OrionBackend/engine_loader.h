#pragma once

#include <string>

class BrowserEngine;

// Loads the named engine plugin ("webkit" or "blink") from the engines\
// subdirectory next to the facade DLL. Idempotent for the same name; a
// second, different engine is refused — one engine per process. Returns
// nullptr and sets error on failure; the rejected DLL is unloaded so the
// failed session doesn't keep its runtime resident or its files locked
// against replacement. A loaded engine is never unloaded.
BrowserEngine* orionLoadEngine(const char* name, std::string& error);

// The loaded engine, or nullptr before orionLoadEngine succeeds.
BrowserEngine* orionLoadedEngine();
