#pragma once

#include "host_services.h"

// Installs the facade's HostServices table for this plugin. Returns false on
// ABI mismatch; the plugin must then fail OrionCreateEngine with nullptr.
bool orionHostShimInit(const HostServices* host);
