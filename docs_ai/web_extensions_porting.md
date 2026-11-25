# WebExtensions: Platform Split, Windows Porting, and Remaining Core Work

Companion to [web_extensions.md](../docs/web_extensions.md), which covers the core
module internals (IDL, codegen, serialization, bridge injection). This
document focuses on what is platform-specific, what the Windows port has and
still needs, and what gaps remain in the shared core.

State as of 2026-07. When updating, re-verify the "current state" claims
against the code -- especially section 5, which describes core-module
implementation status that changes independently of this repo.

## 1. Architecture in One Page

Extension state lives in the profile database (`extensions` table). Per
profile world, a `WebExtensionsRuntime` (owned by the core's
`bridge::ProfileWorld`) loads the installed extensions and their resources
(unzipped in memory). For each web view that should see the `browser.*` APIs,
`WebViewFactoryBridge::configure_web_view(view_bridge)`:

1. injects the per-extension bridge scripts (generated JS + per-service JS
   glue) via the view's `bridge::WebViewHost::web_view_add_script`, and
2. registers two script message handlers, `nativeCallBridge` and
   `nativeEventBridge`, whose callbacks land in a per-webview
   `NativeBridgeContext` (`core/webextensions/runtime.cpp`).

A `browser.*` call in JS posts `{class, function, arguments, promise: id}`
to `nativeCallBridge`. `NativeBridgeContext` first checks that the web
view's security-origin host matches an installed extension id (extension
pages live at `moz-extension://<id>/` / `chrome-extension://<id>/`, so the
host *is* the id) -- otherwise the call is dropped as "inaccessible
context". It then routes through the generated dispatcher
(`native_call_bridge_received`) into the service implementation, which
settles the JS promise by executing `__bridge__.handlePromise(...)` in the
page via `WebViewHost::web_view_call`. Events flow the same way through
`nativeEventBridge` / `__bridge__.handleNotify(...)`. Details in
web_extensions.md sections 4-5.

Everything above is cross-platform. A platform port supplies:

- the **bridge hosts** of `core/webkit_bridge/API/bridge.h`: a `ProfileHost`
  per profile, a `WebViewFactoryHost` per activated world (scheme
  registration, background pages) and a `WebViewHost` per native view
  (script injection, message handlers, origin lookup; see 3.1),
- **URL scheme handlers** that serve extension resources for
  `moz-extension://` / `chrome-extension://` URLs (see 3.3),
- **web views** for extension pages (popup, background) and the toolbar UI
  around them (see 3.4/3.7),
- host-app data for APIs the core cannot know about (`browser.tabs`,
  `browser.windows`, see 3.6).

## 2. Platform Split: What Lives Where

### 2.1 Core module (fully cross-platform)

All paths relative to `external/orion-gtk/app/src/core_module/`.

**Extension lifecycle and storage:**

| Path | Purpose |
| --- | --- |
| `core/dto/extension_dto.h` | Extension DTOs. `ExtensionDto.id` is a random UUID minted by the DB on insert; `sku` is the deterministic identity (UUIDv4 derived from `webExtID` / `geckoId` / `installURL`); `(profile_id, sku, type)` is unique. Types: `firefox=1`, `chrome=2`, `gresources=3`. |
| `core/dto/extension_storage_area_dto.h` | DTOs for `browser.storage` key-value operations |
| `core/profiles_db/extension.cpp` | SQLite CRUD for the `extensions` table (install, update, delete, query, seq_num ordering) |
| `core/profiles_db/extension_storage_area.cpp` | SQLite CRUD for per-extension storage areas (keyed by extension id + storage id) |
| `core/webextensions/extension.h` / `.cpp` | `WebExtension`: extension_id, `resources()`, `manifest_uri()`, `default_popup_uri()`, default title/icon/locale |
| `core/webextensions/runtime.h` / `.cpp` | `WebExtensionsRuntime` + `WebExtensionsRuntimeImpl`: per-profile extension list (reloaded on `extensions_change()` events), `configure_web_view()`, `on_extensions_update` observable. Also defines `NativeBridgeContext` (the message-handler endpoint). |
| `core/webextensions/runtime_impl.h` | `Implements<...>` mixin: `WebExtensionsRuntimeImpl` inherits every generated service base; impl classes with `set_api` / `set_runtime` / `script_source_for_extension` are auto-wired |
| `core/webextensions/install_source.h` / `.cpp` | `WebExtensionInstallSource`: installs an extension package to a profile |
| `core/webextensions/internal_store.h` / `.cpp` | `WebExtensionsInternalStore`: popular-extension catalog, SKU/type detection, fetch paths |
| `core/webextensions/repository.h` / `.cpp` | `WebExtensionsRepository` / `WebExtensionResources`: unzips a package into memory, parses the manifest, exposes `data_for_path("/popup.html")` and `scheme()` (`moz-extension` for firefox, `chrome-extension` for chrome) |

**Service implementations** (`core/webextensions/private/`): `alarms`,
`runtime`, `storage`, `i18n`. Services *without* an impl class (Tabs,
Windows, WebNavigation, ContextMenus, WebRequest, BrowserAction, PageAction)
fall through to the generated base whose rpc bodies log
`WebExtensions> <Service>::<fn>> NOT IMPLEMENTED` and never settle the JS
promise. Implementation status is in section 5.

**IDL and code generation** (`core/webkit_bridge/codegen/`): `generate.go`
plus one `.idl` per service: `runtime` (also `RuntimePort`), `tabs`,
`storage` (`Storage`, `StorageAreaLocal`, `StorageAreaSession`), `alarms`,
`i18n`, `browseraction`, `pageaction`, `contextmenus`, `webnavigation`,
`webrequest`, `windows`, plus `manifest.json.idl` and
`extension_list.json.idl` (JSON schemas, not services). IDL member kinds:

- `rpc` -- dispatched to the native service implementation
- `extern` -- implemented in injected JS; the impl class provides a
  `script_source_for_extension()` that installs a
  `window.__extern__.<Service>` hook (e.g. `runtime.getURL`,
  `i18n.getMessage`)
- `listener` -- event; JS add/removeListener round-trips through
  `nativeEventBridge`

**Host/bridge contracts (implemented per platform):**
`core/webkit_bridge/API/bridge.h` -- the core owns `ProfileBridge` /
`ProfileWorld` / `WebViewFactoryBridge` / `WebViewBridge` (plus context-menu
and clipboard bridges); the app implements the matching `*Host` classes and
attaches them, keeping the returned `HostAttachment` token. Embedding guide:
the core module's `BUILDING.md`, "Embedding: hosts and bridges".

**CoreApi extension entry points:** `install_extension()`,
`get_installed_extension_ids()` / `get_installed_extensions()` /
`get_installed_extensions_by_query()`, `uninstall_extension()`,
`update_extension()`, `extensions_change()` observable,
`attach_profile_host(profile_id, host)` (creates the profile bridge and both
worlds; the world owns the runtime, reachable as `world->runtime()` after
`world->initialize()`), `get_profile_world(profile_id, private_mode)`,
`internal_store()`, `repository()`,
`extension_storage_areas_*()`, `shutdown_extensions()`.

### 2.2 Linux/GTK-specific code

All paths relative to `external/orion-gtk/app/src/`.

**Hosts:** `webextensions/profile_host.cpp` (`GtkProfileHost`, attaches the
per-world menu/clipboard hosts), `webextensions/webview_factory_host.cpp`
(`WebViewFactoryHost`: web context, scheme registration, background views)
and `webextensions/webview_host.cpp` (`GtkWebViewHost`, one per
`WebKitWebView`):

| WebViewHost method | GTK WebKit API |
| --- | --- |
| `web_view_call` | `webkit_web_view_call_async_javascript_function` with a `GVariantDict` for `arg0` |
| `web_view_add_script` | `webkit_user_script_new` + `webkit_user_content_manager_add_script` with URL allow-list |
| `web_view_register_script_message_handler` | `webkit_user_content_manager_register_script_message_handler` + signal `"script-message-received::<name>"` |
| `web_view_security_origin_get_host` | `webkit_web_view_get_uri` -> `webkit_security_origin_new_for_uri` -> `webkit_security_origin_get_host` |

**Extension WebView factory** (`webextensions/web_view_factory.h` / `.cpp`):
creates the WebViews for extension popup and background pages on a
**dedicated `WebKitWebContext`**, and registers the `moz-extension` /
`chrome-extension` scheme handlers on that context (`sync_web_context()`),
serving `WebExtensionResources::data_for_path()`. It also registers an
`extension-proxy` scheme -- a CORS proxy that re-issues
`extension-proxy://https/<host>/<path>` requests over libsoup so extension
pages can fetch cross-origin. Requests are origin-checked
(`handle_security_origin`): only the extension's own pages may fetch its
resources. Note: because the handlers live on the factory's context,
regular GTK browsing tabs cannot load extension URLs (unlike the current
Windows approach, which registers the schemes on every browsing page).

**Extension UI (GTK dialogs, popovers, list views)** (`window/`,
`data_item/`): `extension_popover` (toolbar popup host),
`add_web_extension_dialog`, `popular_extensions_dialog`,
`extensions_keyboard_shortcuts_dialog`, `import_from_extension_dialog`,
`columnview/extensions_listview`, `data_item/extension_item`,
`data_item/popular_extension_item`.

### 2.3 Windows-specific code (current state)

**C++ backend (`OrionBackend/`)** -- the facade implements the core's host
contracts (`bridge_hosts.{h,cpp}`) and forwards native operations to the
engine plugin's `orion::ScriptBridge` (Windows-owned interface in
`script_bridge.h`, one instance per engine, every method takes the view's
HWND). The WebKit plugin implements it plus the extension URL scheme handlers
(`OrionEngineWK.dll`); the CEF plugin's `createScriptBridge()` returns null =
extensions disabled.

| Path | Purpose |
| --- | --- |
| `script_bridge.h` | Windows-owned `orion::ScriptBridge`: the engine's half of the host contract -- the `WebViewHost` operations with an explicit HWND, plus `register_scheme` / `update_background_pages` for the factory host. Declares `create_webkit_script_bridge()`. |
| `bridge_hosts.h` / `.cpp` | Facade hosts: `orion::win::ProfileHost` (profile anchor), `WebViewFactoryHost` (forwards scheme registration and background-page changes to the engine), `WebViewHost` (forwards per-view operations with its HWND; tab/window ids answer unknown). |
| `script_bridge_webkit.cpp` | `WebKitScriptBridge` -- the per-HWND operations via the WebKit C API (see 3.1); the HWND is mapped to the `WebKitBrowserWindow`'s `WKPageRef` via `temp_browserWindows`. `register_scheme` installs the core's handler in `extension_scheme_webkit.cpp`. |
| `extension_scheme_webkit.cpp` / `.h` | WK URL scheme handlers for `chrome-extension`, `moz-extension` and `extension-proxy`, dispatching each request to the core-provided handler for its scheme (see 3.3) |
| `exports.cpp` | Extension exports: `InstallExtension`, `GetInstalledExtensions`, `FreeInstalledExtensions`, `UninstallExtension`, `SetExtensionEnabled`, `GetExtensionToolbarItems`, `ConfigureWebViewExtensions(hwnd, profileId)`. Stores the engine's `ScriptBridge` in `LoadBrowserEngine()`; owns the host attachments (`g_profile_hosts`: `ProfileHost` + `WebViewFactoryHost` per profile, `g_web_view_hosts`: `WebViewHost` per HWND), released in `ShutdownOrionCore()` (`shutdownExtensionHosts`) before browser views are destroyed. |
| `webkit/WebKitBrowserWindow.cpp` | Registers the extension scheme handlers in `create()` (all configs) and the `orion-debug://` scheme (debug builds; `orion-debug://info` doubles as a scheme-handler smoke test) |

**C# frontend (`Orion/`):**

| Path | Purpose |
| --- | --- |
| `Interop/OrionBackend.cs` | P/Invoke declarations for the extension exports |
| `notification_bridge.h/.cpp` | `HostNotificationProvider`: the core's `NotificationProvider` for Windows, forwarding JSON requests to the C# presenter (exports `SetNotificationCallbacks` / `NotifyNotificationEvent` are in `exports.cpp`) |
| `Services/NotificationService.cs` | Windows App SDK toast presenter + activation routing for every notification source (extensions today; page/browser notifications can reuse it) |
| `Models/NativeWebView.cs` | Calls `ConfigureWebViewExtensions` right after `CreateOrionWindow`, so every browsing web view gets the bridge scripts + message handlers |
| `App.xaml.cs` | `ReinstallTestExtensions()` (`ORION_TEST_EXTENSIONS` builds, i.e. all WK configurations): reinstalls the `orion-api-test` packages (built by the core module) on every launch and logs the popup URLs |
| `Models/ExtensionEntry.cs` | Installed-extension model (Id, Name, Version, ExtensionType, IsEnabled) |
| `Models/PopularExtensionItem.cs` | Popular-extension catalog model with download/install tracking |
| `Views/SettingsPage.Extensions.cs` | Settings UI: list installed extensions, toggle enable/disable, uninstall |
| `Views/PopularExtensionsWindow.xaml.cs` / `...Content.xaml.cs` | Popular-extensions window: downloads `.xpi` files, calls `InstallExtension` |

**WebKit build configuration (`ports/webkit-win/`):**
`webkit-options.cmake` sets `ENABLE_WK_WEB_EXTENSIONS=ON` and
`ENABLE_CONTENT_EXTENSIONS=ON` unconditionally (these are not vcpkg
features), so the engine-level hooks -- custom URI schemes, user scripts,
script message handlers -- are available via the C API in every build.

**Known engine gap -- custom scheme handlers are dropped on Windows:**

`WKPageConfigurationSetURLSchemeHandlerForURLScheme` (Kagi-added C API,
`Source/WebKit/UIProcess/API/C/WKURLSchemeHandlerRef.cpp`) stores the
handler on `API::PageConfiguration`, but only the Cocoa `WKWebView` copies
configuration handlers into the `WebPageProxy` after page creation
(`WKWebView.mm`). The Windows `WebView`
(`Source/WebKit/UIProcess/win/WebView.cpp`) never reads
`configuration->urlSchemeHandlers()`, so navigations to custom schemes
(`orion-debug://`, `moz-extension://`, ...) are sent to the network process
and fail with `didFailProvisionalNavigation code=1 "Unsupported protocol"`
(curl `CURLE_UNSUPPORTED_PROTOCOL`). Fix: mirror the Cocoa loop in the
`WebView` constructor -- snapshot `pageConfiguration->urlSchemeHandlers()`
before the configuration is moved into `createWebPage`, then
`m_page->setURLSchemeHandlerForScheme(pair.value.get(), pair.key)` for each
entry (`setURLSchemeHandlerForScheme` already handles
`LegacySchemeRegistry` registration and notifying a running web process).
Until that lands in `kagisearch/webkit` and `REF` is bumped in
`ports/webkit-win/portfile.cmake`, no custom scheme URL loads on Windows.

## 3. Windows Port: Done and Remaining

Ordered roughly by dependency.

### 3.1 Bridge hosts and the WebKit `ScriptBridge` -- DONE (CEF: TODO)

`OrionBackend/bridge_hosts.cpp` (facade) forwards each `WebViewHost`
operation to the engine's `ScriptBridge` with the view's HWND;
`OrionBackend/script_bridge_webkit.cpp` implements them:

| WebViewHost operation | WebKit C API (implemented) | CEF equivalent (TODO) |
| --- | --- | --- |
| `web_view_add_script` | fork: `WKUserScriptCreateWithSourceAndMatchPatterns` (allow/block lists + world) + `WKUserContentControllerAddUserScript`; stock API fallback: `WKUserScriptCreateWithSource` | inject via `OnContextCreated` / `CefFrame::ExecuteJavaScript` |
| `web_view_register_script_message_handler` | fork: `WKUserContentControllerAddScriptMessageHandlerInWorld`; stock API fallback: `WKUserContentControllerAddScriptMessageHandler` | `CefMessageRouterBrowserSide` or a custom `CefV8Handler` + `window.postMessage` bridge |
| `web_view_call` | `WKPageCallAsyncJavaScript`, payload as the `arg0` dictionary entry | `CefFrame::ExecuteJavaScript` |
| `web_view_security_origin_get_host` | `WKPageCopyActiveURL` -> `WKURLCopyHostName` | `CefFrame::GetURL` then parse |

Implementation notes:

- Match patterns and script worlds: the stock `WKUserScriptCreateWithSource`
  has **no allow-list or world parameter** (unlike GTK's
  `webkit_user_script_new_for_world`), so with it every extension's runtime
  script lands in every page of the web view. Native dispatch stays safe
  (`NativeBridgeContext::current_extension()` requires the security-origin
  host to match an installed extension id), but with two or more extensions
  installed the last-injected script wins for the JS-side constants
  (`runtime.id`, `getManifest()`, `getURL()`), which the test extension's
  "matches this page's origin" tests catch. Orion's WebKit fork adds
  `WKUserScriptCreateWithSourceAndMatchPatterns` and
  `WKUserContentControllerAddScriptMessageHandlerInWorld`
  (`Source/WebKit/UIProcess/API/C/WKUserScriptRef.*`,
  `WKUserContentControllerRef.*`), which feed the core's `matches` /
  `exclude_matches` / `script_world` straight into `WebCore::UserScript`
  like GTK does. The bridge uses them when the headers define
  `WK_HAVE_USER_SCRIPT_MATCH_PATTERNS_AND_WORLDS` /
  `WK_HAVE_SCRIPT_MESSAGE_HANDLER_IN_WORLD` (LocalDebug against a fork
  checkout that has them; vcpkg builds once `WEBKIT_REF` is bumped) and
  falls back to the stock calls otherwise. `web_view_call`
  (`WKPageCallAsyncJavaScript`) still has no in-world variant.
- Message handler contexts are intentionally leaked: the WebKit C API has
  no remove-by-name, and the core registers handlers once per webview
  lifetime.
- CEF's script injection happens in the renderer process
  (`OnContextCreated`) with `CefProcessMessage` IPC for routing -- more
  complex than WebKit's model; `CefMessageRouterBrowserSide` handles most
  of the boilerplate.

### 3.2 Host attachment and `configure_web_view` -- DONE

Follows the core's embedding guide (`BUILDING.md`, "Embedding: hosts and
bridges"); Windows runs one profile per process and no private mode, so
exactly one world (`PrivateMode::no`) is activated per process.

1. `LoadBrowserEngine()` stores `engine->createScriptBridge()` in
   `g_script_bridge` (null for CEF: extensions disabled, every step below
   no-ops with a log line).
2. `ConfigureWebViewExtensions(hwnd, profileId)` -> `activateProfileWorld`:
   on first use attaches a `win::ProfileHost`
   (`CoreApi::attach_profile_host`, which creates the profile bridge and both
   worlds), locks the main world's `web_view_factory_bridge()`, attaches a
   `win::WebViewFactoryHost`, calls `register_schemes()` (the core hands one
   request handler per scheme to the engine) and `world->initialize()`
   (creates the runtime, starts the async extension load), then registers
   `when_ready()`.
3. It waits for readiness (pumping the run loop, 30 s cap), calls
   `create_web_view_bridge()`, attaches a `win::WebViewHost(bridge, engine,
   hwnd)` and finally `configure_web_view(*view_bridge)`; the host lives in
   `g_web_view_hosts[hwnd]` until `DestroyOrionWindow` (or a re-configure
   of the same HWND).
4. `NativeWebView` (C#) calls it right after `CreateOrionWindow`.
   `GetExtensionToolbarItems` shares the activation + wait
   (`readyRuntimeForProfile`) and reads `world->runtime()->get_extensions()`.
5. **Readiness:** `WebViewFactoryBridge::is_ready()` (and the runtime's
   `is_ready()`) turns true as soon as the extension *list* exists, while the
   packages are still being unpacked -- configuring a view then injects no
   bridge scripts and the toolbar sees no extensions. The facade therefore
   keeps a per-profile flag set by `when_ready()` registered right after
   `world->initialize()`; that reply fires from the runtime's `notify_update`
   after every package loaded.
6. Shutdown (`ShutdownOrionCore` -> `shutdownExtensionHosts`): view hosts,
   then `world->cleanup()`, then factory and profile hosts, all before
   `cleanBrowserEngine()` destroys the native views and before the core stops.
7. Not attached yet: context-menu and clipboard hosts (the bridges no-op
   without a host; Windows draws context menus from C#), and the private
   world.

**Process model -- read before touching this or the core.** On Windows each
profile has exactly one browser process (the `Orion.exe` holding its
single-instance mutex; other launches forward their request over the pipe and
exit before initializing the core, and CEF's `--type=` children never do), and
that process's `CoreApi` is opened on the profile's own database
(`Profiles\<id>\profile.db`, see `docs_ai/profile_storage.md`). A Windows
browser process therefore attaches exactly one `ProfileHost` and activates
one world: `g_profile_hosts` holds one entry
(`activateProfileWorld` refuses a second id with a warning), the WebKit
plugin's scheme -> handler table is per plugin, and the `profile_id`
arguments of the extension exports are consistency checks, not selectors.
On Linux the opposite holds: one process, one `CoreApi`, one shared database,
every profile attached to that core with both of its worlds. The core is
written for the Linux shape and must stay profile-agnostic: when editing the
core, do not assume one profile per process; when editing the Windows glue,
do not assume all profiles share a process. Consequences: a private-browsing
world on Windows would be the same profile's second world in the same
process (needing per-world scheme dispatch and a non-persistent WebKit data
store), and the multi-profile and world-reopen items of the core's
`BUILDING.md` integration checklist do not apply here.

### 3.3 Extension resource scheme handlers -- DONE (blocked by engine gap)

`OrionBackend/extension_scheme_webkit.cpp`. `WebKitBrowserWindow::create()`
registers WK scheme handlers for `chrome-extension`, `moz-extension` and
`extension-proxy` on every page configuration (all marked secure). When the
world is activated the core calls `WebViewFactoryHost::register_scheme` per
scheme; the WebKit `ScriptBridge` stores the handler in a scheme -> handler
table and each request is translated into a `WebExtensionSchemeRequest` and
completed from the core's `WebExtensionSchemeResponse` (the task is retained
for asynchronous completion, which `extension-proxy` needs). The core
resolves the extension by URL host and serves
`WebExtensionResources::data_for_path`; its responses carry no content type,
so the handler derives the MIME type from the path (WebKit would otherwise
download `text/html`). A request before activation is answered 503.

Request and response details depend on the WebKit build. The stock C API
exposes only the request URL and method and takes only status, MIME type and
length for the response, so with it the handler reports the request's own host
as the web view host (the core's `same_security_origin` then always passes,
as in the pre-port Windows handler), `extension-proxy` sees header-less GET
requests, response headers (its CORS headers) are dropped, and there is no
CORS-enabled scheme registration. Orion's WebKit fork adds
`WKURLSchemeTaskGetPage`, `WKURLSchemeTaskCopyRequestHTTPHeaders`,
`WKURLSchemeTaskCopyRequestHTTPBody`,
`WKURLSchemeTaskDidReceiveResponseWithHeaders` and
`WKContextRegisterURLSchemeAsCORSEnabled` (`WKURLSchemeHandlerRef.*`,
`WKContextPrivate.h`); when the headers define
`WK_HAVE_URL_SCHEME_TASK_REQUEST_DETAILS` the handler takes the requesting
origin from the page's active URL (GTK parity: a page on another origin gets
403, a navigation to the extension page itself passes because the active URL
is already the provisional one), forwards request headers and body, returns
response headers and reason phrase, and registers `extension-proxy` as
CORS-enabled. Until the pinned WebKit carries the additions the stock path
above is compiled.

### 3.4 Extension WebView factory (background pages and popups) -- TODO

GTK's `WebExtensionWebViewFactory` creates hidden WebViews for background
pages and transient ones for popups. Windows needs the equivalent:

- **Background pages**: hidden HWND + WebView loading the extension's
  background page, alive for the extension's lifetime (where
  `browser.runtime.onMessage` listeners run).
- **Popup pages**: transient WebView (flyout or small window) loading
  `WebExtension::default_popup_uri()` when the toolbar icon is clicked.
- Both need `ConfigureWebViewExtensions` on creation.

Until this exists, extension pages can be tested by navigating a normal
tab to the extension page URL (section 4) -- regular tabs already get
`configure_web_view`, and the scheme handlers serve the resources.

### 3.5 Content script injection into browsing pages -- TODO (all platforms)

Manifest `content_scripts` parsing + URL pattern matching belongs in the
core; the actual injection goes through the view's `WebViewHost`. Not implemented on
any platform (section 5).

### 3.6 `browser.tabs` / `browser.windows` host data -- TODO (all platforms)

The IDLs and dispatch exist (`tabs.query` rpc; `onCreated` / `onUpdated` /
`onRemoved` / `onActivated` listeners; `windows.onFocusChanged` /
`onRemoved`), but the data must come from the host app (C# on Windows).
Likely pattern: a callback interface the backend calls to query tab/window
state, analogous to the bridge hosts.

### 3.7 Extension toolbar UI -- TODO

Toolbar buttons for enabled extensions (icon from
`WebExtension::default_icon_data()`), click opens the popup (3.4), badge
support (`browserAction.setBadgeText` etc. -- also unimplemented in core).

### 3.8 `browser.notifications` -- DONE

Core keeps the per-extension records (`private/notifications.cpp`) and hands
display to a host `orion::NotificationProvider` (`core/notification_provider.h`,
injected like `PasswordStorage` via `CoreApi::set_notification_provider`).
Extension-package `iconUrl` / `imageUrl` / button icons and `data:` URLs are
materialised under `<data>/notifications/<extension>/` before the request
reaches the host, so the host only sees file paths (or http(s) URLs).

Windows: `OrionBackend/notification_bridge.{h,cpp}` (`HostNotificationProvider`)
forwards the request as JSON to C# through the `SetNotificationCallbacks`
export; `Orion/Services/NotificationService.cs` builds Windows App SDK toasts
(`AppNotificationManager`, works unpackaged) and reports Shown / Clicked /
ButtonClicked back through `NotifyNotificationEvent` ->
`CoreApi::notification_event()` -> `onShown` / `onClicked` / `onButtonClicked`
/ `onClosed`. Every toast carries the owning profile id; an activation that
lands in another profile's process is forwarded over the `SingleInstance`
pipe (`notify <payload>`), and a launch-by-toast is picked up from
`AppInstance.GetActivatedEventArgs()` after the windows exist.

The service is source-agnostic (`NotificationSource.Extension / Web / Browser`)
so page Notification API and Orion's own messages can reuse it; nothing feeds
those yet. Limitations: Windows does not report dismissals without a click (no
`onClosed(byUser)` for swipe-away), `requireInteraction` maps to a long
duration (Reminder scenario only when buttons exist), `priority` is ignored,
and `getPermissionLevel` always answers `granted`.

## 4. Testing with the Orion API test extension

The test extension is part of the core module (orion-linux), not of this repo:

- `external/orion-gtk/app/src/core_module/extensions/shared/orion-api-test/`
  is the body (popup, runner, built-in `tests/`, the Chromium/Firefox
  `corpus/`, the manual notifications page).
- `extensions/chrome/orion-api-test/` and `extensions/firefox/orion-api-test/`
  hold only the flavor manifest (MV3 / MV2) plus a `.orion-overlay` file
  naming the shared body, which the packager layers underneath.
- The core's CMake target `webextensions`
  (`cmake/PackageWebExtensions.cmake` + `package_webextension.go`) packages
  every `chrome/*` directory as a signed `.crx` and every `firefox/*` as an
  `.xpi` into the CMake binary dir -- here
  `external/orion-gtk/app/src/core_module/build/core_module/`.
- The core embeds `debug_extension_list.json.in`, which lists these packages
  (`file://` install URLs) and is offered in the extensions UI when the core
  runs in debug mode.

Windows wiring:

- **Single switch:** `$(OrionTestExtensions)` in `Directory.Build.props`
  gates the `ORION_TEST_EXTENSIONS` define in C# (`Orion.csproj`) and C++
  (`OrionBackend.vcxproj`), the `webextensions.vcxproj` project reference, and
  the `.xpi`/`.crx` None items. Never condition test-extension code or content
  on anything else.
- **Debug mode:** `InitOrionCore()` calls `CoreApi::set_debug_mode(true)` in
  test-extension builds (like orion-linux's `--debug`); the migrated harness
  requires the `debug` manifest permission, which only debug mode grants.
- **Install:** `App.ReinstallTestExtensions()` uninstalls and reinstalls
  `orion-api-test.xpi` (Firefox flavor) and `orion-api-test.crx` (Chrome
  flavor) from the exe directory into the profile on every launch.
- **Open:** navigate a tab to `moz-extension://<id>/popup.html` or
  `chrome-extension://<id>/popup.html`; the id is a random per-install UUID
  that changes on every launch, so `ReinstallTestExtensions()` logs the
  current URLs at startup -- grep the log for `orion-api-test`.
- **Corpus maintenance:** `scripts/seed_corpus_tests.py` and
  `scripts/fetch_firefox_tests.py` (run by hand) write into the shared body's
  `corpus/`.
- **Results:** summary + per-test table in the page; also
  `window.__testResults` for automation.
- **Flavor-native conventions:** the built-in suites (`tests/*.js`) are
  written once against `api` / `call()` from `lib/api.js`: in the Firefox
  flavor that is `browser.*` with promises, in the Chrome flavor `chrome.*`
  with the callback convention (`call()` resolves with the callback argument
  and rejects when `chrome.runtime.lastError` is set). Synchronous APIs
  (`runtime.id`, `getManifest()`, `i18n.getMessage()`) are used directly on
  `api.*`. The Chrome flavor's MV3 service worker (`background.sw.js`) uses
  `chrome.*`, the MV2 `background.js` uses `browser.*`. The corpora are not
  routed through the helper: each already targets its browser's convention.

**Notifications:** the built-in `notifications` suite (`tests/notifications.js`)
never calls `create()` -- that would put a real toast on the desktop during every
run -- and only checks the API surface and the non-displaying calls. The manual
page `notifications_manual.html` (linked from the popup under "Manual tests")
creates real toasts with configurable options and logs every `onShown` /
`onClicked` / `onButtonClicked` / `onClosed` callback, so clicking the toast or
its buttons can be matched against what the extension receives. The test
extension manifests request the `notifications` permission for this.

## 5. Core Module Implementation Status

Platform-independent state of the `browser.*` implementations in
`core_module` (verified 2026-07). "Generated stub" means the rpc lands in
the generated base class, logs `NOT IMPLEMENTED`, and the JS promise never
settles.

| Area | Current state |
| --- | --- |
| **`browser.storage.local` / `.session`** | `get` / `getKeys` / `set` / `remove` / `clear` are DB-backed via `extension_storage_areas_*` coroutines. `getBytesInUse` resolves `0`. `local` and `session` differ only by `storage_id` -- **session is persisted in the same profile DB** instead of being memory-only. |
| **`browser.storage` `onChanged`** | Fired only from `clear()`, debounced 200 ms, with no change payload (spec: fires for `set`/`remove` too and carries the changes object + area name). |
| **`browser.alarms`** | Implemented: per-extension in-memory alarm map with scheduling and `onAlarm` dispatch. Not persisted across restarts. |
| **`browser.runtime`** | JS side (`extern`s in injected glue): `id`, `lastError` (always null), `getManifest`, `getURL`, `getFrameId` (always 0), `connect` / `connectNative` (create Port objects with `crypto.randomUUID()` sockets), `sendMessage` overload resolution. Native side: `RuntimePort::connect` overloads are **empty bodies**; `__sendMessage`, `postMessage`, `disconnect`, and the port/connect listeners are generated stubs. Messaging does not work yet. |
| **`browser.i18n`** | `getMessage` / `getUILanguage` implemented in injected JS embedding only the **default locale's** `messages.json`; no `$1..$9` substitutions, no UI-language matching. `detectLanguage` / `getAcceptLanguages` / `getPreferredSystemLanguages` / `getSystemUILanguage` are native stubs returning EN-US. |
| **`browser.tabs` / `browser.windows`** | IDL + dispatch only; needs host-app data (3.6). |
| **`browser.webNavigation`** | `onBeforeNavigate` listener only, never fired. |
| **`browser.webRequest`** | `onAuthRequired` listener only, never fired. |
| **`browser.browserAction` / `pageAction`** | `onClicked` listener only, never fired. No `setIcon` / `setBadgeText` / `setPopup`. |
| **`browser.contextMenus`** | `onClicked` listener only; no `create` / `update` / `remove`. |
| **Content script injection** | Not implemented on any platform. Core needs manifest `content_scripts` parsing + URL pattern matching; injection delegated to `ScriptBridge`. |
| **`browser.notifications`** | Implemented end to end on Windows: records + host presenter seam in core, toasts + activation routing in `Services/NotificationService.cs` (3.8). Permission level is always `granted`; no dismissal-without-click events. |
| **Dispatch failure handling** | Errors (unknown class/function, bad arguments, unimplemented rpc) are logged only -- the JS promise hangs. Should auto-reject. |

## 6. Code Map

```
external/orion-gtk/app/src/
  core_module/core/
    dto/
      extension_dto.h                    Extension metadata DTOs (id vs sku!)
      extension_storage_area_dto.h       Storage API DTOs
    profiles_db/
      extension.cpp                      Extension table CRUD
      extension_storage_area.cpp         Storage area table CRUD
    core_api.h                           Top-level API (install, query, runtime)
    webkit_bridge/
      API/bridge.h                       Host/bridge contracts (ProfileHost,
                                         WebViewFactoryHost, WebViewHost, ...)
      codegen/
        generate.go                      IDL -> C++/JS code generator
        *.idl                            One per browser.* service (see 2.1)
    webextensions/
      runtime.h / .cpp                   WebExtensionsRuntime (owned by ProfileWorld),
                                         configure_web_view(WebViewBridge&),
                                         NativeBridgeContext
      runtime_impl.h                     Implements<...> service mixin
      extension.h / .cpp                 WebExtension model (popup URI, icons)
      install_source.h / .cpp            Package installation
      internal_store.h / .cpp            Catalog / fetch helpers
      repository.h / .cpp                In-memory package resources
      private/
        alarms.h / .cpp                  browser.alarms implementation
        runtime.h / .cpp                 browser.runtime JS glue + port stubs
        storage.h / .cpp                 browser.storage implementation
        i18n.h / .cpp                    browser.i18n JS glue

  webextensions/                         LINUX/GTK ONLY
    profile_host.h / .cpp                GtkProfileHost (+ menu/clipboard hosts)
    webview_host.h / .cpp                GtkWebViewHost (WebViewHost impl)
    webview_factory_host.h / .cpp        WebViewFactoryHost: extension WebViews,
                                         scheme handlers, extension-proxy

  window/                                LINUX/GTK ONLY (extension UI)
    extension_popover.*                  Extension toolbar popover
    add_web_extension_dialog.*           Install confirmation dialog
    popular_extensions_dialog.*          Recommended extensions dialog
    extensions_keyboard_shortcuts_dialog.*
    import_from_extension_dialog.*
    columnview/extensions_listview.*

OrionBackend/                            WINDOWS C++ (WebKit engine plugin)
  notification_bridge.h/.cpp             NotificationProvider -> C# toasts (3.8)
  exports.cpp                            Extension exports, host attachments
                                         (g_profile_hosts / g_web_view_hosts),
                                         ConfigureWebViewExtensions
  bridge_hosts.h / .cpp                  win::ProfileHost / WebViewFactoryHost /
                                         WebViewHost (core host contracts)
  script_bridge.h                        orion::ScriptBridge (engine per-HWND ops),
                                         create_webkit_script_bridge()
  script_bridge_webkit.cpp               WebKitScriptBridge (ScriptBridge impl)
  extension_scheme_webkit.cpp / .h       chrome-extension / moz-extension /
                                         extension-proxy WK scheme handlers ->
                                         core request handlers
  webkit/WebKitBrowserWindow.cpp         Scheme handler registration +
                                         orion-debug:// (debug builds)

external/orion-gtk/app/src/core_module/extensions/
  shared/orion-api-test/                 TEST EXTENSION body (see section 4)
  chrome|firefox/orion-api-test/         flavor manifest + .orion-overlay

Orion/                                   WINDOWS C#
  Interop/OrionBackend.cs                P/Invoke for extension exports
  Services/NotificationService.cs        Toast presenter + activation routing (3.8)
  Models/
    NativeWebView.cs                     Calls ConfigureWebViewExtensions
    ExtensionEntry.cs                    Installed extension model
    PopularExtensionItem.cs              Popular extension catalog model
  Views/
    SettingsPage.Extensions.cs           Extension list in settings
    PopularExtensionsWindow*.xaml.cs     Download + install UI
  App.xaml.cs                            ReinstallTestExtensions (debug)

ports/webkit-win/
  webkit-options.cmake                   ENABLE_WK_WEB_EXTENSIONS=ON,
                                         ENABLE_CONTENT_EXTENSIONS=ON
```
