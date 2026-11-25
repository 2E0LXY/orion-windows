#pragma once

#include <WebKit/WKBase.h>
#include <core/webextensions/web_view.h>

namespace orion {

// Installs the core's request handler for one extension URL scheme
// (chrome-extension, moz-extension, extension-proxy). Called by the WebKit
// ScriptBridge when the core registers the profile world's schemes
// (WebViewFactoryBridge::register_schemes -> WebViewFactoryHost::register_scheme).
// Windows runs one profile world per process, so the table is per plugin.
void setExtensionSchemeHandler(
    const WebExtensions::WebExtensionSchemeRegistration& registration,
    WebExtensions::WebExtensionSchemeRequestHandler handler);

// Registers the extension URL schemes on a page configuration so requests
// reach the handlers installed above. Called for every browsing view at
// creation (WebKitBrowserWindow::create); a request whose scheme has no core
// handler yet (world not activated) is answered 503.
void registerExtensionSchemeHandlers(WKContextRef context, WKPageConfigurationRef pageConf);

} // namespace orion
