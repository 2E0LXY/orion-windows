# Orion Android — source and UX audit (2026-11-28)

## Scope and verification boundary

Reviewed the current Android development branch `chatgpt/geckoview-home-splash`: MainActivity and native WebView/GeckoView shell, homepage and weather, DNS settings/VPN bridge, ad-filter sources, download implementation, cookie/bookmark/password import code, README/roadmap, Android manifest and GitHub CI. This is a **source and automated-check audit**, not a claim of comprehensive runtime, device, penetration or network testing. ShipProof statuses remain independent: implemented, source-tested, APK built, release published, device-verified.

## Confirmed defects corrected

| Severity | Finding | Correction | Evidence/check |
| --- | --- | --- | --- |
| HIGH — privacy | “Clear normal browsing data” cleared WebView history but left Orion's separate `recent_history` list intact. | Clear stored recent URLs as part of browser-data clear, and WebView history when user clears History. | Source regression test; on-device data retention still UNVERIFIED |
| MEDIUM — misleading UI | Sync presented a clickable sign-in flow even though no Google OAuth client or sync implementation exists. | Grey, disabled and labelled “Unavailable”; disabled explanation shows missing integration. | UI source/test; device appearance UNVERIFIED |
| MEDIUM — misleading UI | Android Autofill settings took users to generic Android Settings rather than integrated Orion autofill. | Disabled grey option; distinguish encrypted password **import/storage** from absent **autofill**. | UI source/test |
| MEDIUM — dead commands | Reload, Find, Save, Reader, Print and Share appeared available with no webpage, and Incognito could offer normal-data import actions. | Disable page-dependent commands when no normal loaded URL; grey the import action in Incognito. | UI source/test |
| MEDIUM — DNS controls | DNS fallback and selected DoH server test appeared usable when the provider had no DoH endpoint. Local VPN configuration similarly had no endpoint. | Grey and disable controls until a valid DoH endpoint is selected. | UI source/test; live DNS testing UNVERIFIED |
| MEDIUM — filter switches | EasyList and EasyPrivacy switches could be enabled before their downloaded rule sets existed, producing no effective new list rules. | Grey each list switch until its local domain file exists and is nonempty, with guidance to update filters. | UI source/test; real blocking efficacy UNVERIFIED |
| MEDIUM — preference inconsistency | The separately presented YouTube request-filtering switch was silently gated by the master ad-block preference. | Give the narrow YouTube request filter its own interception condition. This is not a promise to remove all YouTube ads. | Source/test; content-blocking coverage UNVERIFIED |
| LOW — content accuracy | About section still described obsolete “community” branding and suggested all private browsing was absent. | Use Orion Private Browser wording and describe actual single-session Gecko Incognito limitations. | Source/test |
| LOW — UX consistency | Disabled controls were visually indistinguishable from active controls, or acted as no-op links. | Grey text, disabled semantics, clear “Unavailable” labels and no click handlers. | UI source/test; contrast/gesture/device checks UNVERIFIED |

## Features deliberately shown disabled/unavailable

- Google account sign-in and cloud synchronisation; Orion Connect, cross-device push links and encrypted sync.
- Imported-password autofill; extension installation/management.
- Autofill integration entry in the import/export menu.
- Browser actions requiring a loaded webpage when only the native start screen is shown.
- Normal browser-data import while inside GeckoView Incognito.
- DNS options dependent on a configured DoH endpoint.
- EasyList/EasyPrivacy domain switches until the lists have been installed.

Disabled does **not** mean broken: a feature can be on the roadmap but have no working implementation. Conversely, features implemented in source may still require a device acceptance test.

## Subsequent cookie lifecycle corrections

- **Clear browser data:** Android cookie deletion is asynchronous. Orion previously displayed “cleared” immediately after scheduling deletion. The completion message now waits for the cookie-removal callback and flushes the cookie manager.
- **Import cookies:** WebView `setCookie` with callbacks is asynchronous. Orion previously flushed and displayed success immediately after scheduling writes. Import now tracks acknowledged set-cookie callbacks, flushes once all complete and reports `accepted / requested` rather than implying every cookie was stored.
- Both changes have source regression coverage. **Real-device cookie persistence and process-restart behaviour remain UNVERIFIED.**

## Extended audit findings and build hardening

- **Bookmark import bug (MEDIUM):** `BookmarkImporter` stripped the literal characters `\\uFEFF`, not the actual UTF-8 BOM character. UTF-8 BOM-prefixed HTML and JSON exports could be rejected. The BOM conversion was corrected and a source regression guard added.
- **Android Lint (24 errors observed):** Existing Java files used 17 raw `setOrientation(1)` calls and seven raw `Toast.makeText(..., 0)` duration values; Android Lint identified these as `WrongConstant` errors. All were replaced with `LinearLayout.VERTICAL` and `Toast.LENGTH_SHORT` respectively, with static regression checks. Green compilation does not imply green Lint; verify the latest CI run.
- **Android data extraction (security hardening):** The manifest already disabled backup, but did not define Android 12+ extraction rules. Added explicit cloud-backup and device-transfer exclusions for local browser data, credentials and preferences. Real-device data-transfer tests remain **UNVERIFIED**.
- **Android Lint CI:** `lintDebug` is now an enforced APK gate before packaging/release. The workflow also prints its report on failure; no Lint errors are suppressed or baselined.
- **Nonblocking Lint warnings:** Initial lint surfaced 50 warnings, mostly non-localised hard-coded labels, plus touch accessibility, view allocation, old target SDK, ChromeOS ABI scope and WebView JavaScript security caution. These remain to be addressed or deliberately documented by their appropriate release gate; do not claim a warning-free build.

## Other limitations / remaining risk (not represented as verified fixes)

1. **WebView vs GeckoView:** UI parity is partial. WebView caching, JavaScript/image controls, reader, print, ad-filter interception and cookie import do not automatically apply to GeckoView. Feature labels must state scope.
2. **Cache semantics:** `LOAD_NO_CACHE` plus public cache-clearing APIs cannot guarantee zero temporary engine files. GeckoView engine cache-write prevention, DNS leaks, and file-system persistence must be tested independently.
3. **Incognito:** A separate Gecko private session exists. Teardown, recovery after process death, screenshots and third-party storage isolation still require device evidence.
4. **Downloads:** Four-connection HTTPS transfer and fallback are experimental; retries, resumption, redirection, on-device transfer hashes, cancellation, large files and storage exhaustion need real tests. System DownloadManager and parallel downloads have different progress reporting.
5. **Imported credentials:** The local password migration vault is encrypted but does not autofill websites; exporting creates a user-selected plaintext CSV. No authenticated cloud sync, biometric gate or independent security audit.
6. **DNS/VPN:** User-consented app-scoped DNS VPN is experimental. It is not a universal VPN, and the ability to observe HTTPS DoH or enforce WebView DNS globally is limited. Leakage/recovery and IPv4/IPv6 tests need real devices/networks.
7. **Filter engine:** Domain-only EasyList/EasyPrivacy subsets and narrow YouTube endpoint matching are not complete filter-rule implementations. They cannot guarantee that every advert/tracker is blocked.
8. **Phone/tablet:** Adaptive layout, accessibility service, small screens, large text, landscape, Android system-back gestures, lifecycle, low memory and battery usage are not automatically exercised by the current source tests.
9. **Release:** The APK is an experimental debug/test build; a green CI build is not evidence of Play Store, signing continuity, compatibility upgrades or production security review.

## ShipProof verification

- **Static/source regression:** Run `python3 -m unittest discover -s tests -v` from `platforms/android`.
- **Android build:** `gradle --no-daemon -PorionArm64=true assembleDebug` in CI; verify the actual job status and staged APK.
- **Packaging:** Check ARM64 native libraries, APK manifest, checksum and release asset where CI publishes.
- **Manual acceptance:** Install/upgrade on a real phone; inspect all grey controls and TalkBack announcements; check homepage, WebView and Gecko navigation, Incognito, import flows, file downloads, cache setting and DNS; ensure clear-history removes all UI recent URLs; perform landscape/large-text testing.
- **Outcome reporting:** A passing source test is a PASS **only for that test**. Without Android screenshots, user interaction, filesystem/network captures and install/launch evidence, runtime tests are UNVERIFIED.

### Follow-up priorities

1. Instrumented device tests, accessibility/contrast and lifecycle tests for the corrected screens.
2. End-to-end password/cookie migration test with synthetic data; strict handling of exported CSV.
3. TLS/network/DoH VPN and cache storage validation.
4. HTTP download streaming, failure/cancellation, hash validation and buffer/storage-pressure tests.
5. Full GeckoView feature parity evaluation before exposing any further controls as available.
