# WebDriver / WebDriver BiDi for Orion — Design & Feasibility

Research date: 2026-07. Goal: expose a WebDriver (classic) + WebDriver BiDi
endpoint from Orion so that (a) `wpt run` can drive the **whole browser** for
conformance testing, and (b) Orion is drivable by standard automation
tooling (Selenium, Puppeteer, wptrunner) generally. Must be cleanly
separated and compiled only under `ORION_WEBDRIVER_BIDI` (initially gated to
Debug builds / `_DEBUG` / `DEBUG`).

Companion research: `docs_ai/extension_testing_research.md` (why WPT matters),
`scripts/wpt_run.py.cmd` (local WPT server).

## Background: what the protocols are

- **WebDriver (classic, W3C)** — HTTP request/response. Client → `POST
  /session`, then `POST .../url`, `.../element`, `.../execute/sync`,
  `.../window/handles`, etc. One-directional (no events). Usually brokered
  by a *driver* process (chromedriver, geckodriver, WebKit's
  `WebViewDriver.exe`).
- **WebDriver BiDi** — a WebSocket channel layered on the *same* session
  (client requests capability `webSocketUrl: true`; the New Session response
  returns a `ws://` URL). Bidirectional: JSON commands in modules
  (`session`, `browsingContext`, `script`, `network`, `log`, `input`,
  `webExtension`, …) plus **events** pushed from the browser. Message
  envelopes: `{id, method, params}` → `{type:"success"|"error", id, ...}`;
  events `{type:"event", method, params}`.
- **Key fact for us:** wpt (and Selenium/WebdriverIO) never use BiDi alone —
  they always open a classic session first, then attach BiDi. Only tests
  tagged `bidi` and the `web-extensions/` suite actually require BiDi
  commands. So classic WebDriver alone unlocks the large majority of WPT.

## The unavoidable Orion-specific work (any path)

Engines automate only what happens *inside a web view* (script eval, element
lookup, input synthesis, screenshots). The browser-chrome model — what a
"browsing context" is, tab/window create/close/activate, HWND↔context
mapping, window rects, profiles — is Orion policy no engine can supply. Both
engines delegate exactly this back to the embedder. Mapping onto existing
Orion surface (verified):

| BiDi / WebDriver need | Existing Orion surface |
|---|---|
| `browsingContext.create` (tab) | `MainWindowViewModel.CreateNewTab` |
| `browsingContext.create` (window) | `new MainWindow(...)` / `CreateTabGroup` |
| `browsingContext.close` | `MainWindowViewModel.CloseTab` |
| `browsingContext.activate` | `SwitchToTab` / `SwitchToTabByIndex` |
| `browsingContext.getTree` | `MainWindow.AllWindows`, `TabGroups`, `ActiveTab` |
| `browsingContext.navigate` | `MainWindowContent.NavigateToUrl` / `NativeWebView.LoadUrl` |
| reload / back / forward | `NativeWebView.Reload/GoBack/GoForward` |
| `webExtension.install/uninstall` | `InstallExtension` / `UninstallExtension` exports |
| navigation/url/title events | `BrowserCallbacks` (ActiveURLChanged, TitleChanged, NavigationError, WebProcessTerminated) |

**Native capability gaps (need new code, both engines):**
1. **Script eval *with result*** — `ExecuteJavaScript` today is
   fire-and-forget (`webkit.cpp` passes null completion handler). BiDi
   `script.evaluate`/`callFunction` need the result serialized as BiDi
   `RemoteValue`. Pattern already exists in `script_bridge_webkit.cpp`
   (`WKPageCallAsyncJavaScript` + marshaling a JS value back to native).
2. **Load-finished event** with committed URL + status (only
   `LoadingStateChanged` exists today; no distinct DOMContentLoaded/load).
3. **Console/log capture** for `log.entryAdded` (no console callback in
   `BrowserCallbacks` today).
4. **Stable string context IDs** — tabs are keyed by DB `long`/object
   identity; BiDi needs `context-<guid>` ⇄ (window, tab) registry.

**Threading:** every engine/model call is UI-thread-only. A WS server runs on
background threads (HttpListener async) and must marshal each command via
`DispatcherQueue.TryEnqueue` (the pattern the core-watcher thread already
uses), completing a `TaskCompletionSource` to send the reply.

## WebKit engine — findings

**Classic WebDriver is nearly free on the Windows port.** Upstream ships and
the Win port builds:
- `WebViewDriver.exe` from `Source/WebDriver` (socket transport, links only
  WTF + JavaScriptCore — no libsoup/curl on Windows).
- Browser-side automation auto-wires: `WebProcessPoolWin::platformInitialize`
  starts `RemoteInspectorServer` when env `WEBKIT_INSPECTOR_SERVER` is set
  and registers `AutomationClientWin` — which creates a `WebAutomationSession`
  and spawns automation pages via the embedder's `WKPageUIClient.createNewPage`
  (the same callback Orion's popups use).
- `ENABLE_WEBDRIVER` and `ENABLE_REMOTE_INSPECTOR` default to
  `ENABLE_EXPERIMENTAL_FEATURES`, which Orion turns ON — so Orion is very
  likely *already compiling* this. Not yet exercised.

To turn classic WebDriver on for Orion/WebKit:
1. Make `ENABLE_WEBDRIVER` + `ENABLE_REMOTE_INSPECTOR` explicit in
   `ports/webkit-win/webkit-options.cmake`.
2. Set `WEBKIT_INSPECTOR_SERVER=127.0.0.1:<port>` before the first
   `WKContext` is created; ensure `InspectorBackendCommands.js` ships.
3. Ensure `WKPageUIClient.createNewPage` honors
   `setControlledByAutomation(true)` and `page.viewWidget()` returns a real
   HWND (used for window ops).
4. Run `WebViewDriver.exe --target 127.0.0.1:<port>` against a running Orion
   (or patch `SessionHostSocket.cpp::launchBrowser()` — currently hard-codes
   `MiniBrowser.exe`; the FIXME anticipates this).

**BiDi on WebKit — one real missing component.** The browser-side BiDi engine
(`WebDriverBidiProcessor` + `Bidi*Agent`s, gated `#if ENABLE(WEBDRIVER_BIDI)`)
is mature, cross-platform, and would compile on Windows as-is. Implemented
modules in the Kagi checkout: browser, browsingContext (activate/close/create/
getTree/handleUserPrompt/navigate/reload/traverseHistory), script (evaluate/
callFunction/getRealms), storage (cookies), permissions, log.entryAdded.
**Missing: network, input, emulation, webExtension, captureScreenshot.**
The gap is the **driver-side WebSocket server**: `Source/WebDriver/
WebSocketServer` has only a libsoup impl (`soup/WebSocketServerSoup.cpp`); the
socket/Windows backend has none, and `WebDriverServiceWin::platformSupportBidi()`
returns false. `ENABLE_WEBDRIVER_BIDI` is never set for Win.

Two ways to get BiDi on WebKit:
- **(W1) Complete the upstream driver path:** enable `ENABLE_WEBDRIVER_BIDI`
  for Win, write `Source/WebDriver/socket/WebSocketServer*.cpp` (analogous to
  the soup one, built on `Inspector::RemoteInspectorSocketEndpoint` that the
  Win port already uses), add `USE(INSPECTOR_SOCKET_SERVER)` branches to
  `WebSocketServer.h`/`HTTPServer.h`, flip `platformSupportBidi()`. Keeps us
  on the upstream architecture; upstreamable; but it's WebKit-fork surgery.
- **(W2) In-process endpoint feeding the transport-agnostic seam:**
  `WebDriverBidiProcessor::processBidiMessage(String)` /
  `sendBidiMessage(String)` is transport-agnostic, so an Orion-hosted WS
  server could feed BiDi JSON straight in. Open problem: no C API exposes the
  automation session on Windows (`WebProcessPool::setAutomationSession`,
  `API::AutomationClient` are internal, not in `UIProcess/API/C`) — would need
  a small C API addition to the fork.

## CEF / Blink engine — findings

CEF is well-positioned; Orion's `cef.cpp` **already speaks CDP in-process**
(credential scraping uses `ExecuteDevToolsMethod` + a `DevToolsMessageObserver`).
- `CefBrowserHost` gives raw in-process CDP: `SendDevToolsMessage`,
  `ExecuteDevToolsMethod`, `AddDevToolsMessageObserver` — explicitly *no*
  remote-debugging port required.
- `CefSettings.remote_debugging_port` gives the standard Chromium CDP
  HTTP+WebSocket endpoint (`/json/version`, per-target WS) for free — one
  field in `ensureCefInitialized()`.
- Chromium's official BiDi mapper (`third_party/bidimapper/mapper.js`, the
  same TS→CDP mapper chromedriver embeds) is **vendored in our checkout**.

CEF BiDi paths:
- **(C1) `remote-debugging-port` + stock chromedriver (attach mode):** run
  chromedriver, connect via `goog:chromeOptions.debuggerAddress`. Chromedriver
  injects `mapper.js` and exposes the BiDi WS endpoint. Highest reuse. Risk:
  chromedriver's launch path is unusable (it wants to spawn the binary); only
  *attach* fits, and its hidden-target/tab model assumes **Chrome runtime
  style** while Orion uses **Alloy** (`cef.cpp` sets
  `CEF_RUNTIME_STYLE_ALLOY`) — likely must switch to
  `CEF_RUNTIME_STYLE_CHROME` or hit target-management friction.
- **(C2) chromium-bidi mapper in Node over CEF's CDP** (no chromedriver):
  Puppeteer does this via `BidiOverCDP`. Decouples from chromedriver's
  launcher; adds a Node dependency.
- **(C3) In-process: host `mapper.js` in a hidden CEF view + WS server in
  Orion** — mirrors what chromedriver does internally but self-contained, no
  external process, works even under Alloy. Most code, most robust for a
  shipped browser.
- **(C4) Hand-roll BiDi over `ExecuteDevToolsMethod`** — only for a small
  curated subset; `mapper.js` exists precisely to avoid this.

## Cross-cutting: what wpt actually needs

- **testharness tests** run over **classic** WebDriver; `testdriver.js`
  actions are classic. Only `?feature=bidi` tests and `web-extensions/` need
  BiDi. → classic WebDriver alone gets most of WPT working.
- **Minimal BiDi surface wpt exercises** (testharness-over-BiDi):
  `session.subscribe/unsubscribe`, `script.callFunction` (the indispensable
  testdriver message pump), `browsingContext.getTree/handleUserPrompt`, event
  push for `browsingContext.userPromptOpened`. For web-extensions add
  `webExtension.install/uninstall`.
- **wptrunner product** ("orion"): a `browsers/orion.py` module. Two flavors:
  - *Tier A (WebDriver):* subclass `WebDriverBrowser`, override
    `make_command` (Ladybird's is 3 lines), reuse `WebDriverTestharnessExecutor`.
    Needs our WebDriver server + `webSocketUrl` capability.
  - *Tier B (servo-style, no protocol):* `NullBrowser` + an executor that
    launches Orion per-test and scrapes `ALERT: RESULT: {json}` from stdout.
    Pure testharness only; no testdriver, no extensions. Cheapest smoke path.
- **Certs/hosts:** wptserve hosts http+https+ws+wss. Browser must trust wpt's
  generated CA or accept insecure certs (WebDriver `acceptInsecureCerts`
  capability, or a `--ignore-certificate-errors`-style flag). Hosts entries
  (`web-platform.test` → 127.0.0.1) required regardless — already handled by
  `scripts/wpt_run.py.cmd`.
- **Conformance:** `webdriver/tests/bidi/` (wdspec) is the BiDi test suite,
  live on wpt.fyi; its `bidi_session` fixture uses the classic-attached flow
  (needs a classic `/session` endpoint, not just a static WS).

## Recommended phased plan

Ordered by value-per-effort; each phase stands alone.

**Phase 0 — classic WebDriver on WebKit (days, mostly config).** Enable the
flags, set `WEBKIT_INSPECTOR_SERVER`, make `createNewPage` automation-aware,
run `WebViewDriver.exe --target`. Deliverable: Selenium can drive Orion/WK;
foundation for the wpt product.

**Phase 1 — wptrunner "orion" product, tier B first (days).** `browsers/
orion.py` with the servo-style stdout scraper to get *some* whole-browser
testharness numbers immediately without any protocol; then tier A over the
Phase-0 WebDriver server. Deliverable: `scripts/wpt_run.py.cmd run orion <dir>`.

**Phase 2 — the Orion tab/window glue (the unavoidable app-specific code).**
The `browsingContext`↔`MainWindowViewModel` mapping + context-ID registry +
the four native capability gaps (script-eval-with-result, load-finished,
console capture, plus serialization). This is reusable by *every* BiDi path
below, so do it once, engine-agnostic, in C# under `#if ORION_WEBDRIVER_BIDI`.

**Phase 3 — BiDi, per engine:**
- CEF: start with **C1** (remote-debugging-port + chromedriver attach),
  resolving the Alloy→Chrome runtime-style question; fall back to **C3**
  (in-process mapper) if we want no external process in shipped builds.
- WebKit: **W1** (write the socket `WebSocketServer` in the fork, enable
  `ENABLE_WEBDRIVER_BIDI`) is the upstreamable path; **W2** (in-process via
  `processBidiMessage` + a new C API) if we want to avoid a separate driver
  process.

**Phase 4 — web-extensions over BiDi + wpt.fyi-style reporting.** Add
`webExtension.install/uninstall` mapping; run the WPT `web-extensions/` suite
end-to-end.

## Isolation / flag wiring (`ORION_WEBDRIVER_BIDI`)

- **C#** (`Orion.csproj`): add a PropertyGroup for the debug configs
  (`Debug`/`LocalDebug`) appending `ORION_WEBDRIVER_BIDI` to
  `DefineConstants` (mirrors the existing `DEBUG` block). Keep it distinct
  from `DEBUG` so it can be enabled independently later. Guard all
  `Orion/WebDriverBidi/*` and the server start/stop in `App.OnLaunched`.
- **C++** (`OrionBackend.vcxproj`): append `ORION_WEBDRIVER_BIDI;` to the
  Debug `ItemDefinitionGroup`'s `PreprocessorDefinitions` only (leave
  release/`NDEBUG` untouched). Guard the new exports/callbacks. Use
  the **same** flag name on both sides (avoid the existing
  `ORION_ENGINE_WK` vs `ORION_ENGINE_WEBKIT` split).
- **New files:** `Orion/WebDriverBidi/` (BidiServer, BidiSession,
  BidiDispatcher, BidiEventBridge, `Commands/*Module.cs`);
  `OrionBackend/webdriver_bidi/` for the native capability-gap exports.
- **No new dependencies:** `System.Net.HttpListener` +
  `AcceptWebSocketAsync` + `System.Text.Json` are in-box on the target
  framework; no existing HTTP/WS infra to conflict with.

## Effort reality check

- Phase 0/1: small — mostly configuration + a small Python module.
- Phase 2: medium — real UI-path code, threading care, but 1:1-ish mappings.
- Phase 3 CEF (C1): small-medium if the runtime-style switch is clean.
- Phase 3 WebKit (W1): medium-high — WebKit-fork C++ (a socket WebSocket
  server) + verifying the BiDi tunnel over the socket transport.
- Full from-scratch BiDi (if we didn't reuse either engine's engine) would be
  large: the `script` RemoteValue/realm serialization model alone is ~half
  the effort, and Igalia has spent 1.5+ years reaching partial coverage.
  We should reuse WebKit's BiDi engine and Chromium's mapper, never
  hand-roll (C4/none).
