# WebExtension API Test Frameworks: Research & Adoption Options

> Note (2026-09): the test extension referred to as `APITestExtension` below now lives in the core module as `external/orion-gtk/app/src/core_module/extensions/shared/orion-api-test` (see `docs_ai/web_extensions_porting.md` section 4).

Research date: 2026-07. Question: can Orion replace/augment the home-grown
`APITestExtension` with existing extension-API test frameworks or corpora for
broader coverage of the `browser.*`/`chrome.*` implementation?

## Bottom line

The industry has converged on one pattern: a small test-only API namespace
(`chrome.test` in Chromium, `browser.test` in Firefox/WebKit, now being
standardized by the W3C WebExtensions Community Group) that test extensions
call, with the browser collecting pass/fail. Implementing that namespace in
Orion's IDL-generated bridge unlocks three ready-made corpora — two of which
are already on local disk. No third-party "framework" needs to be adopted;
the test *corpora* are the prize, and they are plain JS + manifest
directories.

## The corpora

| Corpus | Where | Size | Fit |
|---|---|---|---|
| WPT `web-extensions/` | github.com/web-platform-tests/wpt (`web-extensions/`, landed 2025 via WPT RFC 219) | 6 MV3 suites: alarms, bookmarks, browsingData, idle, runtime, storage (~39 test fns) | The official cross-browser conformance suite, spec'd against the WECG WebExtensions spec, reviewed by Apple/Google/Mozilla. Small but growing; alarms/storage/runtime map exactly to Orion's implemented APIs. Chrome Dev is already passing it (wpt.fyi, 2026-07). Self-contained — runnable WITHOUT WebDriver by installing the extension dirs directly. |
| Chromium `api_test` | **Already vendored**: `vendor/cefs/chromium/src/chrome/test/data/extensions/api_test` | 243 suite dirs, ~1209 test extensions, ~16 MB (863 MV2 / 344 MV3) | Covers every API Orion has implemented or stubbed. ~84 suites are fully self-contained (no `getConfig`/`sendMessage` C++ choreography); the alarms/storage(settings)/i18n/runtime suites are nearly coordination-free. `chrome.test`'s runner is a self-contained 578-line JS file (`extensions/renderer/resources/test_custom_bindings.js`) over only ~8 native functions. |
| Apple WKWebExtension tests | **Already on disk**: `C:\kagi\webkit\Tools\TestWebKitAPI\Tests\WebKitCocoa\WKWebExtension*.mm` (31 files) | ~650-680 TEST cases | Test logic is extension-side JS embedded as NSString line arrays — mechanically extractable (manifest dict + JS → extension dir). ~55 self-contained tests cover exactly Orion's implemented APIs (Alarms 11/11, Storage 13/18, Localization 20/31, Runtime 8/48). Caveat: `assertThrows` regexes encode Apple's exact error strings. |
| Firefox `toolkit/components/extensions/test` | mozilla-central (not local) | ~250 xpcshell + ~150 mochitest files | Extension-side JS is pure `browser.*` + `browser.test` (~17 fns, schema-defined in `test.json`), but the outer harness (ExtensionTestUtils, xpcshell heads) is Gecko-coupled → per-file manual porting only. Best used as reference semantics for the moz-extension flavor. |

## The enabling step: implement `browser.test` / `chrome.test`

- WECG has a formal `browser.test` proposal (`w3c/webextensions`
  `proposals/browser_test_api.md`, 2025-07, Apple + Mozilla authors):
  `runTests`, `assertTrue/assertFalse/assertEq` (deep-equality rules
  specified), `assertThrows`, plus `onTestStarted`/`onTestFinished` events.
- Chromium's `chrome.test` needs only ~8 native methods (`notifyPass`,
  `notifyFail`, `log`, `sendMessage`, `sendScriptResult`, `getConfig`,
  `openFileUrl`, `waitForRoundTrip`); all asserts/runner logic is
  renderer-side JS that can be ported into Orion's bridge JS.
- Usage frequency across the whole api_test corpus is dominated by ~15
  functions (`assertEq` ~3270 uses, `succeed`, `assertTrue`, `runTests`,
  `fail`, `callbackPass`, ...). `callbackPass`/`listenOnce` pending-callback
  counting semantics are subtle — port them from `test_custom_bindings.js`
  rather than re-inventing.

This slots into the existing IDL codegen + `ScriptBridge` pipeline exactly
like alarms/storage did, gated to test mode / test extensions only.

## Verified facts that change the plan

(Adversarially verified during research.)

- **The custom-scheme-handler engine blocker is gone**: the kagisearch/webkit
  fix has shipped and `ports/webkit-win/portfile.cmake` already pins the
  fixed REF. Extension pages load; tests can actually run.
- **Automation hook already exists**: `ExecuteJavaScript(HWND, script)` is
  exported from OrionBackend (`exports.cpp`, both engines), so a C# harness
  can extract results today. APITestExtension already exposes
  `window.__testResults`; nothing consumes it yet.
- **MV3 service-worker backgrounds are the main technical gap**: the WPT
  suites and the best Chromium suites (`alarms/*`, `settings/simple_test`)
  declare `"background": {"service_worker": ...}`. Orion's APITestExtension
  is MV2. Mitigation: rewrite manifests to MV2 background scripts during
  vendoring (the test JS itself is background-context-agnostic), or
  implement service-worker backgrounds.
- **Chrome Dev is already green** on WPT `web-extensions/` (2026-07 wpt.fyi
  runs: alarms 6/6, bookmarks 7/7, idle 5/5, ...), so expectations are
  stabilizing.
- Some tests assert implementation-exact values (Chromium's 500-alarm-limit
  error string, storage.sync quota constants; Apple's validation-error
  phrasing) — expect a per-test triage/expectations layer.

## Non-options (for the record)

- **Driver-based harnesses** (Playwright, Puppeteer, Selenium, WebDriver
  BiDi `webExtension.install`): all require a CDP or WebDriver/BiDi endpoint
  Orion doesn't have. Worth revisiting as the long-term CI story (a BiDi
  endpoint would also enable full wptrunner integration and wpt.fyi
  presence), but months of unrelated infrastructure.
- **Full wptrunner integration**: needs WebDriver; the direct-load shortcut
  above gets the same corpus without it.
- **Mock libraries** (sinon-chrome, jest-webextension-mock,
  webextensions-api-fake): test extensions against a fake browser — the
  opposite direction of what Orion needs.
- **Apple's native WKWebExtension runtime on Windows**: Orion's fork builds
  with `ENABLE_WK_WEB_EXTENSIONS ON` but the Windows platform files are
  deliberate `CRASH()` stubs; adopting it would abandon the core-module
  architecture. Not a path.

## Recommended sequence

1. **`browser.test`/`chrome.test` namespace in the core module** (~1 week):
   new test IDL service + bridge-JS runner (port `test_custom_bindings.js`
   semantics), ~8 native methods, test-mode gated. Port APITestExtension's
   34 tests to it (mechanical).
2. **C# CI runner**: install a test-extension dir via `InstallExtension`,
   collect results natively via the bridge (or `ExecuteJavaScript` +
   `window.__testResults` as the quick first cut), report like
   `ci/unittest.cmd`.
3. **Vendor WPT `web-extensions/`** (days): immediate standards-conformance
   signal on alarms/storage/runtime; re-sync as upstream grows.
4. **Curated Chromium api_test subset** (1-3 weeks): start with the ~84
   self-contained suites, beginning with alarms/storage/i18n/runtime already
   on disk; per-suite allowlist + expectations file.
5. **Optional**: extractor for Apple's .mm-embedded JS tests (~55
   self-contained now) — valuable second opinion since Orion is
   WebKit-based.
6. **Long-term**: WebDriver BiDi endpoint (webExtension module first) →
   full wptrunner, wpt.fyi participation, and driver-based E2E.
