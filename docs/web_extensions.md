# WebExtensions In `core_module`

This document explains how WebExtensions are implemented in `core_module`, what is generated from IDL, how JS/native calls are serialized, and what you must wire in your app to make bridge injection work.

## 1. Big Picture

The WebExtensions API surface is split into 3 layers:

1. IDL definitions (`core/webkit_bridge/codegen/*.idl`) define services, methods, events, and data models.
2. Codegen (`core/webkit_bridge/codegen/generate.go`) generates (from IDL):
   - C++ interfaces + types (`webkit_bridge_api.h`)
   - C++ dispatcher + embedded JS bridge (`webkit_bridge_impl.cpp`)
3. Runtime wiring (`core/webextensions/runtime.cpp`) injects scripts into extension pages and connects JS message handlers to C++ implementations.

At runtime:

1. JS calls `browser.*` / `chrome.*` generated APIs.
2. Generated JS bridge posts message to native handler (`nativeCallBridge`).
3. Native dispatches to generated C++ router (`native_call_bridge_received`).
4. Your implementation method runs and resolves/rejects a generated `Promise<T>`.
5. Native sends result back to JS via `__bridge__.handlePromise(...)`.

Events are symmetric via `nativeEventBridge` and `__bridge__.handleNotify(...)`.

## 2. Files And Generation Pipeline

Source:

- `core/webkit_bridge/codegen/*.idl`
- `core/webkit_bridge/codegen/generate.go`

Generation is configured in `CMakeLists.txt` via `add_interface(PREFIX webkit_bridge ...)`.

Generated files (in build directory):

- `<build>/webkit_bridge_api.h`
- `<build>/webkit_bridge_impl.cpp`
- `<build>/core/webkit_bridge_api.h`

`orion_core` compiles `<build>/webkit_bridge_impl.cpp` directly.  
Do not manually edit generated files. Edit IDL / implementations / generator.

## 3. What IDL Supports

Supported blocks:

- `message Name { ... }`
- `service Name [global=browser.path] { ... }`
- `rpc method(params) returns(type) {}`
- `listener onEvent(params);`
- `extern symbol;` (provided by JS extern hook, not native RPC)
- `enum Name { ... }`
- `using EnumName;` or `using Alias = Other.Enum;`
- field modifier `repeatable` (array/vector)

Supported types:

- primitives: `bool`, `int32`, `double`, `string`, `any`
- maps: `map<K, V>`
- custom messages and enums

Type aliases in codegen:

- `object` -> `any`
- `json` -> `string`

Practical effect:

- `any` in C++ is transported as JSON string.
- `map<string, object>` and `map<string, json>` end up as `std::map<std::string, std::string>` on C++ side (values are serialized JSON fragments).

## 4. Calling Convention And Serialization

### 4.1 JS -> Native call payload

Generated JS (`nativeCallBridge`) sends:

```json
{
  "class": "Runtime",
  "function": "__sendMessage#1",
  "arguments": "[\"...json array of args...\"]",
  "promise": 42
}
```

Notes:

- `arguments` is a JSON string containing a JSON array.
- Overloads are disambiguated by generated binding names (`name#1`, `name#2`, ...).
- Overload selection in JS uses argument count + coarse type matching (`string`, `number`, `boolean`, `object`, `array`, `any`).
- `null` and `undefined` are accepted for any expected type.

### 4.2 Native argument decode

Generated C++ reads:

- `arguments_json` -> `std::tuple<std::optional<...>, ...>` via `rfl::json::read`.
- Then calls implementation method with:
  - `const std::string& extension_id`
  - `Promise<ReturnType>`
  - `const std::optional<ParamType>& ...`

### 4.3 Native -> JS promise payload

`Promise<T>::resolve/reject` sends:

```json
{
  "promise": 42,
  "resolve": true,
  "arguments": [ ... ]
}
```

JS `__bridge__.handlePromise(...)` resolves/rejects the pending JS Promise.

### 4.4 Events

JS listener management sends through `nativeEventBridge`:

```json
{ "class":"Runtime", "event":"onMessage", "command":"add", "listener":7 }
```

Native `Listener<...>` stores listener ids per webview and emits notifications as:

```json
{ "listener":7, "arguments":[ ... ] }
```

Received by JS `__bridge__.handleNotify(...)`.

### 4.5 `browser` vs `chrome`

Generated JS builds both `window.browser.*` and `window.chrome.*` objects from the same IDL `global=...` path.

- `browser.*`: Promise-first usage.
- `chrome.*`: if the last arg is `function` or `null`, bridge treats it as callback and chains Promise `.then(callback)`.

## 5. How Bridge Script Injection Works

Injection entry point is `WebExtensionsRuntimeImpl::configure_web_view(...)` in `core/webextensions/runtime.cpp`.

For each installed extension:

1. Build allow-list:
   - `"<scheme>://<extension_id>/*"`
   - scheme comes from extension type:
     - Chrome: `chrome-extension`
     - Firefox: `moz-extension`
2. Inject scripts (in order):
   - bootstrap: `window.__extern__ = {}`
   - extension-specific extern scripts from implementations (`script_source_for_extension`)
   - generated bridge script (`native_bridge_script()`)
3. Register message handlers on webview:
   - `"nativeCallBridge"`
   - `"nativeEventBridge"`

Security context mapping:

- Native side obtains current host using `ScriptBridge::web_view_security_origin_get_host`.
- Host must match installed `extension_id`; otherwise calls are ignored as inaccessible context.

## 6. What You Must Implement In Host App

`core_module` depends on app-provided `orion::ScriptBridge` (see `core/webkit_bridge/API/script_bridge.h`):

1. `web_view_add_script(...)`
2. `web_view_register_script_message_handler(...)`
3. `web_view_call(...)`
4. `web_view_security_origin_get_host(...)`

Minimal integration flow:

1. Create and initialize `CoreApi`.
2. Call `core_api->set_script_bridge(your_bridge)`.
3. Create runtime for profile: `core_api->create_runtime(profile_id)`.
4. For each relevant webview, call `runtime->configure_web_view(webview)`.

Important:

- If `set_script_bridge` is not called, default `NullScriptBridge` is used and nothing is injected.
- Runtime loads extensions asynchronously (`reload_extensions()`); if you configure too early, there may be no extension scripts yet.
- Reconfigure or hook into runtime extension updates when extension set changes.

## 7. Extending Or Adding APIs

When adding a new WebExtensions API:

1. Add/modify IDL in `core/webkit_bridge/codegen/`.
2. Ensure new IDL file is listed in `add_interface(...)` in `CMakeLists.txt` (if new file).
3. Rebuild (or build target `orion_core_codegen`) to regenerate bridge files.
4. Implement generated virtual methods in runtime implementation classes (`core/webextensions/private/*` or new file).
5. Add implementation class to `Implements<...>` in `core/webextensions/runtime_impl.h`.
6. Wire pointer in `Implementations impls{...}` in `configure_web_view(...)`.
7. If IDL has `extern` symbols, provide JS extern linker script via `script_source_for_extension(...)`:
   - `window.__extern__.ServiceName = (instance) => { ... }`

## 8. Useful Gotchas

- Unimplemented RPC methods call `native_call_not_implemented(...)` only (log warning) and do not auto-reject JS Promise.
- If native dispatch fails (unknown class/function/type mismatch), current flow logs errors; pending JS Promise may remain unresolved.
- To test and run an extension page:
  - Open in browser `moz-extension://<extension_id>/manifest.json` there you can eiter read popup page url or background page
  - Open in browser `moz-extension://<extension_id>/<popup_page_url>`
  If you properly register schemes in script browser, this page should run with WebExtension environment:
  - Open console `browser.*` api should be available

## 9. What was planned (not exhausive)

- Adding more IDLs as per WebExtension spec and implementations. Some implementations can be directly
in `core_module` but some will require to be injected from browser host app (windows, tabs) via interface.
- Creating actual runtime webviews. Extension background page will have to run in hidden WebView with background page url, opening extension popup should spawn different WebView (popups communicate with background via `Runtime.Port`) with popup url
- Injecting extension Content scripts into real WebViews

## 10. Why this way?

- Code on our side, not in browser engine
- Doesn't require injecting shared libraries into browser engine (`WebKitExtensions`, idk about chrome equivalent) - usually involves heavy IPC
- Easier to debug (single process)

## 11. WebExtensions API State

Last reviewed: 2026-05-21.

This document tracks the WebExtensions API surface generated from
`app/src/core_module/core/webkit_bridge/codegen/*.idl` and the current
handwritten implementations under `app/src/core_module/core/webextensions`.

`implemented` means there is observable behavior beyond the generated bridge or
base stub. It does not mean the API is spec-complete. `wired only` means the
API namespace, method, or listener is exposed by the generated bridge and wired
into `WebExtensionsRuntimeImpl`, but there is no real behavior behind it yet.

The generated bridge exposes both `browser.*` and `chrome.*` aliases for these
APIs.

| API | State | Current behavior |
| --- | --- | --- |
| `browser.alarms` | implemented | In-memory alarm map with `create`, `clear`, `clearAll`, `get`, `getAll`, and `onAlarm` dispatch. Alarms are not persisted across runtime restarts. |
| `browser.runtime` | implemented | `id`, `lastError`, `getManifest`, `getFrameId`, and `getURL` are injected per extension. JS overload wrappers exist for `sendMessage`, `connect`, and `connectNative`, but most native runtime RPCs still fall back to generated stubs. |
| `browser.runtime.__port` | wired only | Internal bridge helper for runtime ports. `connect` overloads are overridden but empty; `connectNative`, `disconnect`, `postMessage`, `onDisconnect`, and `onMessage` have no real port implementation. |
| `browser.storage` | wired only | Top-level `onChanged` listener is exposed, but no code currently drives it. |
| `browser.storage.local` | implemented | `get`, `getKeys`, `set`, `remove`, and `clear` call the profile DB-backed `ExtensionStorageAreas` backend with `storage_id = "local"`. `getBytesInUse` returns `0`, `setAccessLevel` is still a stub, and change events are incomplete. |
| `browser.storage.session` | implemented | Same implementation path as `storage.local`, with `storage_id = "session"`. This is real storage behavior, but it is backed by the profile storage backend rather than a clearly memory-only session store. `getBytesInUse`, `setAccessLevel`, and change events have the same limitations as `storage.local`. |
| `browser.i18n` | implemented | `getUILanguage` and `getMessage` are injected from the extension default locale and `_locales/<locale>/messages.json`. Native language APIs return fixed `en-US` values or a fixed detect-language response. Message substitutions are not implemented. |
| `browser.tabs` | wired only | `query` and tab lifecycle listeners are exposed, but no real tab query or event dispatch is implemented. |
| `browser.windows` | wired only | Window focus/removal listeners are exposed, but no real event dispatch is implemented. |
| `browser.webNavigation` | wired only | `onBeforeNavigate` is exposed, but no navigation event dispatch is implemented. |
| `browser.webRequest` | wired only | `onAuthRequired` is exposed, but no webRequest event dispatch is implemented. The extension HTTP(S) proxy is separate plumbing and does not implement this API. |
| `browser.contextMenus` | wired only | `onClicked` is exposed. There are no menu creation/update/remove APIs or real click dispatch. |
| `browser.browserAction` | wired only | `onClicked` is exposed. Toolbar button and popup loading exist as extension UI plumbing, but the browserAction API itself has no real behavior. |
| `browser.pageAction` | wired only | `onClicked` is exposed, but no real pageAction behavior is implemented. |

## 12. API Implementation Notes

- API exposure is declared in the IDLs under
  `app/src/core_module/core/webkit_bridge/codegen/`.
- `WebExtensionsRuntimeImpl` wires every generated service into the native
  bridge in `app/src/core_module/core/webextensions/runtime_impl.h` and
  `app/src/core_module/core/webextensions/runtime.cpp`.
- Real API behavior currently lives in:
  - `app/src/core_module/core/webextensions/private/alarms.cpp`
  - `app/src/core_module/core/webextensions/private/runtime.cpp`
  - `app/src/core_module/core/webextensions/private/storage.cpp`
  - `app/src/core_module/core/webextensions/private/i18n.cpp`
- Namespaces with no handwritten implementation use generated base methods that
  log `native_call_not_implemented`, or generated listeners that only register
  callbacks until application code calls them.
- Extension package loading, custom `chrome-extension` / `moz-extension` scheme
  serving, action popup loading, and the `extension-proxy` fetch/XHR rewrite are
  runtime infrastructure. They are useful for extensions, but they are not
  counted as API implementation in the table above.
