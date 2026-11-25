#pragma once

#include "browser_engine_types.h"

#include <memory>
#include <vector>


// Facade-provided services consumed by engine code. Defined in exports.cpp.

// Popup/OpenURL callbacks registered by C# via the Set*Callback exports.
CreatePopupCallback getCreatePopupCallback();
PopupReadyCallback getPopupReadyCallback();
OpenURLCallback getOpenURLCallback();

