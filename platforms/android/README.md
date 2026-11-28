# Orion Community Browser for Android

<img src="../../assets/orion-mark.svg" width="76" height="76" alt="Orion ring and star browser logo">

Experimental **native Java** Android browser combining **Android System WebView** and **Mozilla GeckoView**. Community-developed from the Orion project; **not an official Kagi release**.

**Android 8.0+ (API 26)** · **ARM64 development APKs** · **AI-sharing draft [PR #4](https://github.com/2E0LXY/orion-windows/pull/4)** · [Earlier Android PR #3](https://github.com/2E0LXY/orion-windows/pull/3) · [Root README](../../README.md) · [Android roadmap](ROADMAP.md)

> **Development-status note (28 November 2026):** Default-browser support and AI sharing are in the [draft AI Connect branch](https://github.com/2E0LXY/orion-windows/tree/chatgpt/default-browser-ai-connect), not necessarily in the latest published test APK. GitHub Actions source checks/builds are not evidence of real-device pairing. Do not use the direct APK download link as proof that it contains these changes.

## Customisable menu and website shortcuts (development)

The Orion WebView and GeckoView menus now include **Install and create shortcut** and a persistent **Customise menu** control, matching the requested Android menu options without copying Brave branding.

- **Install and create shortcut** requests a pinned Android Home screen launcher shortcut for the current normal HTTP/HTTPS page. Android's launcher must support the pin request and may ask for confirmation. This creates a **website shortcut opening Orion**, not a standalone installed PWA or isolated web app. It is unavailable on Orion's start screen and in Incognito.
- **Customise menu** opens native checkboxes to show/hide individual menu entries. Choices are stored locally and shared across WebView and GeckoView by stable action identifier. Save applies changes; Reset restores all defaults. The Customise menu action remains visible so it cannot be hidden accidentally.
- Both functions are included on development branch `chatgpt/default-browser-ai-connect`. Installation and launcher-specific behavior require real-device testing before release approval.

## WebView and GeckoView menu parity

Both engines now use the same menu ordering, labels and saved visibility preferences. **Customise menu**, **Add website to Home screen**, bookmarks, reading list, history, imports, share link, downloads, Settings, reload and engine switching appear consistently. The shortcut action works from a normal loaded webpage in either engine; Android's launcher must approve creation.

**Important engine limitations:** Find in page, Reader mode and Print / Save PDF currently use Android WebView APIs. In GeckoView they remain visible but greyed out and marked **WebView only**, rather than silently operating on a different, hidden WebView tab. Normal GeckoView is still a single separate session; full GeckoView multi-tab parity, downloads initiated by GeckoView pages, extension management and Gecko-specific find/print integrations require further implementation and device testing. A shared menu alone does not establish feature parity.

## Android default browser and AI control

**Default browser (development branch):** Settings → General → Default browser opens Android's system-managed browser-role prompt (Android 10+) or the default-apps settings screen on older devices. Orion advertises HTTP and HTTPS `VIEW` intents, accepts shared external website links on launch and while running, validates schemes, and closes an Incognito GeckoView session before opening a normal external link. Android retains the final choice. This has not yet been checked on a physical device.

**Orion AI Connect (experimental local MCP):** Settings → AI Control → Orion AI Connect, or Orion security icon → AI Sharing. Start sharing on the Android device, then copy its temporary bearer token. The bridge listens at `http://127.0.0.1:18796/mcp` on this device **only**, never on the local network. The 256-bit token expires after 15 minutes; Stop and disconnect immediately revokes it. All tool calls require individual approval on the phone and are denied if Orion is not visible or Incognito is open. Entering Incognito stops the session. The bottom browser toolbar shows a persistent AI-sharing indicator while connected.

MCP methods include `initialize`, `ping`, `notifications/initialized`, `tools/list` and `tools/call`. Available user-approved tools are `browser_info`, `tabs`, `navigate` (HTTPS only), `back`, `forward`, `reload`, `page_text`, `scroll`, `click`, and `type`. Text reading and direct page interactions only work in **normal WebView**, not GeckoView. The click tool blocks form controls and form descendants; typing blocks password and other sensitive field types, and does not submit a form. All operations are experimental and require real-device validation. Screenshots, automated form submission, file downloads, cookies and password extraction are not supported.

**Sharing with installed ChatGPT, Claude, Gemini or other Android apps:** Orion security icon → AI Sharing → **Share current page with an AI app** opens Android's share chooser with only the normal tab's page title and URL. This is a one-way share action, not a remote-control connection; Incognito pages cannot be shared.

**Connecting an AI controller:** A compatible MCP client on the Android device can POST JSON-RPC to the local address using `Authorization: Bearer <temporary token>`. For a developer-controlled desktop connection, Android Debug Bridge port forwarding can be configured on an explicitly authorised device (`adb forward tcp:18796 tcp:18796`); then the client's localhost requests reach the phone through USB. This does **not** make the service publicly reachable. Browser-based MCP clients are blocked by Origin checks. Direct cloud-hosted ChatGPT, Claude and Gemini connections are **not implemented**: each needs a compatible secured relay and provider-specific integration, with authentication, revocation and policy review. Do not paste the session token into an AI conversation or expose the loopback service through a public tunnel.

To confirm the MCP schema from a trusted client on the same device, use a local terminal with a temporary token:

```sh
curl -sS http://127.0.0.1:18796/mcp \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $ORION_SESSION_TOKEN" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

This local API and its foreground approval dialogs still require install, security and usability tests on a real Android device. The existing Android CI checks are not a substitute for those tests.

## Download and install

**[Download the latest Orion Android ARM64 APK directly](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk)** — a standalone APK file with **no ZIP extraction** after the first successful direct-release CI run. The [Actions history](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml) shows source tests, Gradle compilation and release status.

- A successful **push** build on the designated published-APK branch (`chatgpt/geckoview-home-splash`) publishes the direct APK to both a versioned GitHub prerelease and the [stable latest test link](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk). A PR validation run does **not** publish an installer.
- Current direct test builds contain **arm64-v8a** GeckoView libraries. A universal APK can be built without the ARM64 Gradle flag but is not distributed by this workflow.
- Test APKs use **debug signing** and have **not undergone a production security review**. A signing mismatch may prevent an in-place upgrade; uninstalling erases local app data. Export important browser data before changing signatures.
- **Build success is not physical-device confirmation.** Live weather, location permissions, WebView DNS tunnelling and Incognito teardown require Android device testing.

## Using the browser

| Control | Behaviour |
| --- | --- |
| **Address/search bar** | Open HTTP/HTTPS addresses or search with the configured provider. An explicit HTTP navigation triggers an unencrypted-connection warning. |
| **Back / Forward** | Navigate within the active engine's history. |
| **Home — single tap** | Open the saved homepage; without a custom homepage, show the native Orion start page. |
| **Home — press and hold** | Save the currently open website as your homepage. The homepage setting persists after restart. Not available in Incognito. |
| **Home — three quick taps** | Reset the saved homepage to the original Orion start page. The toolbar shortcut must be set to **Home**. |
| **Tabs button** | Switch or close WebView tabs, and access manually archived tabs. WebView supports up to 20 open tabs; the GeckoView session is separate. |
| **Browser menu** | Tabs, GeckoView/WebView switching, bookmarks, reading list, history, imports, downloads, sharing, reader mode, find and printing where supported. |
| **Orion icon — far right** | Open **Security and privacy**, including **Incognito** at the top, site permissions, content blocking, DNS/local VPN and data clearing. |
| **Settings** | Choose search provider, Homepage weather visibility/data, toolbar shortcut, appearance, site display, browser data and app information. Sensitive settings are accessed via the Orion security icon. |

The bottom toolbar uses native Orion branding in **both engines**. The custom animated boot overlay and its replay entries have been **removed** because the animation failed on-device. Android's own system splash/launcher icon may still appear briefly at startup.

## Orion homepage and ECMWF weather

The same native Orion start page is used by WebView and GeckoView. GeckoView includes its engine attribution. The weather card appears **only** on the built-in homepage, **not** on a separately saved website homepage or in Incognito.

**[ECMWF IFS forecast data via Open-Meteo](https://open-meteo.com/en/docs/ecmwf-api)** is displayed in a navy card with vector-drawn, colourful sun, moon, cloud, rain, snow and thunderstorm icons. The card includes:

- Current forecast temperature and feels-like temperature in °C, conditions, wind in km/h, humidity and precipitation in mm.
- A **five-day** local forecast with daily high/low temperatures, weather icons and a visible forecast provider label.
- A timestamp, cached offline display, and refreshes when saved data are approximately **90 minutes** old.
- **Use my location:** only after a user action and Android's **approximate location** permission. Orion rounds the location to **0.01°** before saving or sending it to Open-Meteo; Android may return a nearby town name. Precise GPS permission is not required.
- **Choose a town:** search and pick a result using Open-Meteo geocoding without granting device location.
- **Settings → Homepage weather:** hide/show the card or delete saved location, coordinates and forecast cache.

This is **model forecast information, not a live observation**. Orion does not connect directly to an authenticated ECMWF Weather Intelligence service; it consumes the **Open-Meteo ECMWF endpoint**. Check Open-Meteo's [terms and applicable commercial licence](https://open-meteo.com/en/terms) before production/commercial distribution. Android may provide an operating-system location fix; Orion does not automatically request GPS permission when the browser starts.

## Security and privacy hub

Tap the **Orion logo at the far right of the bottom toolbar**. The full-height native panel groups:

1. **Private browsing:** Start / End Incognito and **What Incognito does and doesn't hide**.
2. **Trackers and content:** WebView ad blocker, EasyList, EasyPrivacy, YouTube ad-request filtering, rule-list update and limitations.
3. **Website security:** HTTPS/HTTP warnings, JavaScript, image loading and no HTTP cache option.
4. **Site permissions:** camera, microphone and approximate location — Ask every time, Block or Allow on request, subject to Android permission and browser-engine support.
5. **Encrypted DNS and network:** provider choice, diagnostics, Android Private DNS and experimental local DNS VPN.
6. **Browsing data:** explicitly clear normal WebView cookies, site storage, cache and tab history **and Orion's separate recent-URL history**.

The standard browser menu and general Settings screen no longer duplicate the main privacy controls.

### Disabled controls and feature availability

Orion greys out unfinished functions and actions that cannot work in the current context. Grey rows are visibly marked **Unavailable** and cannot be activated. **Sync, imported-password autofill, extension management and Orion Connect** are disabled; the import/export menu also disables the unimplemented autofill shortcut. On the native start screen, **Refresh start page** reloads Orion and the weather card; **Add bookmark** opens manual URL entry; **Share Orion website** shares the public Orion project page. Find, Reader mode and Print/Save PDF remain grey until a real webpage is open. Importing into the normal browser is disabled while GeckoView Incognito is active. DNS diagnostics requiring a configured DoH endpoint remain grey. When EasyList or EasyPrivacy is absent, an **Install and enable** action downloads supported domain rules; the switch appears when its file is installed. These are domain-rule subsets, not full cosmetic-filter support.

Available features may still be experimental or only support WebView. UI availability is not a substitute for on-device acceptance. See the [full Android audit and outstanding verification matrix](../../docs/android-audit-2026-11-28.md).


### No HTTP cache

**Orion icon → Security and privacy → Website security → No HTTP cache (WebView).** Enabling this sets WebView's `LOAD_NO_CACHE` mode (network loads rather than use of the HTTP cache), clears existing WebView HTTP cache data, and requests further clearing after page loads and on activity destruction. The same switch also requests GeckoView HTTP cache clearing after completed GeckoView loads; **GeckoView's HTTP cache write prevention is not controlled by WebView's `LOAD_NO_CACHE` setting**.

**Clear cached resources now** calls both engines' public cache-clearing APIs without intentionally clearing cookies, bookmarks or saved site data. This does **not** guarantee that no temporary files are ever created: Android System WebView, GeckoView, HTTP networking, downloads and Android itself can maintain internal files, memory caches, GPU caches and temporary state outside app-level guarantees. The application does not delete internal browser-engine directories by file-system traversal. A strict no-disk-write guarantee would require engine-level modifications and on-device filesystem verification. The toggle does not change the website's HTTP caching headers or the Android DownloadManager files.


### Incognito (experimental)

**Start Incognito mode** opens a **separate GeckoView session configured with `GeckoSessionSettings.Builder().usePrivateMode(true)`**. Even if normal browsing uses Android WebView, this mode uses GeckoView to keep ordinary WebView cookies and active logins separate.

- Orion does **not add private-page navigation to its persistent browsing history**; it disables normal bookmarking and saving the homepage from Incognito.
- The Incognito homepage has a distinctive private-mode banner, **no weather card**, and a button to **End Incognito session**. The security hub also exposes the exit action.
- Android's `FLAG_SECURE` prevents screenshots and recent-app previews while the private session is active.
- Closing Incognito destroys that session and returns to normal WebView tabs. Opening a normal tab or switching back to WebView also ends the private session. **Only one private GeckoView tab is supported in this experimental implementation.**
- Private mode is designed to isolate temporary cookies, site data and browsing history, but actual deletion and process-crash behaviour **have not been verified on a phone**. Do **not** treat this as forensic erasure.

**What Incognito does not hide:** ISPs, employers, schools, mobile operators and local networks can still observe connection information or DNS queries depending on network protection. Websites can see IP addresses, fingerprint browser/device characteristics and track activity while a page is open. Other accounts, Android logs, and external monitoring retain their own records. Shared and downloaded files can remain on the device. Extensions are not enabled in private browsing. The mode does not bypass Android security policies or another VPN.

### DNS, IPv4/IPv6 and optional WebView VPN

Open **Orion icon → Security and privacy → DNS and Secure DNS**.

| Function | Implementation | Important boundary |
| --- | --- | --- |
| **GeckoView DNS-over-HTTPS** | Cloudflare is the default; strict DoH mode with an optional system-DNS fallback | Applies to GeckoView runtime, **not automatically to WebView** |
| **Resolver presets** | Cloudflare, Control D, Quad9, Google, OpenDNS, NextDNS, AdGuard, CleanBrowsing, DNS.Watch, Mullvad, Android system and custom HTTPS DoH | NextDNS needs account-specific configuration; DNS.Watch is reference-only |
| **Custom resolver** | HTTPS DoH URL validation and GeckoView configuration | No generic unauthenticated assumption about all provider-specific endpoints |
| **Android Private DNS** | Shortcut to Android settings plus available DNS-over-TLS hostnames | Device-level configuration needs user action |
| **IPv4 and IPv6 checks** | Query listed primary/secondary resolver addresses over UDP/53, and show latency or failures | **Unencrypted diagnostics**; not evidence that the browser used those servers |
| **DoH A / AAAA checks** | Independent encrypted DNS queries with response timing | Not end-to-end browser DNS leak detection; an AAAA record does not establish IPv6 transport |
| **WebView local DNS VPN (experimental)** | Opt-in Android `VpnService`, explicit OS permission, Orion-scoped virtual DNS routes for IPv4 and IPv6, HTTPS forwarding to the selected DoH endpoint, service notification and start/stop/restart controls | Can replace another active VPN. TCP DNS, fragmented/large packets and some modern browser DNS paths are not supported; **real-device DNS leak tests are still required** |

**Default WebView behaviour remains Android-managed DNS** unless the optional local VPN is explicitly activated. Android routing and device policies remain authoritative. Do not enable experimental VPN functionality on a device where losing another active VPN would be problematic.

## Touch gestures (experimental)

- **Swipe left across a webpage:** Back, if history is available.
- **Swipe right across a webpage:** Forward, if history is available.
- **Pull down at the top of the page:** Refresh. This is disabled while the page is scrolled down.
- **Swipe up from the bottom navigation toolbar:** Open the Downloads status panel.
- **Normal scrolls and multi-touch:** Pass through to the webpage. Gestures require deliberate movement and are best tested on-device, especially on horizontal carousels and Android system-gesture navigation.
- **Alternative controls:** Existing Back, Forward, Reload and Menu → Downloads controls remain available.

The downloads panel is **not** the Android notification shade. It can be closed while downloads continue. System-managed transfers do not expose a live transfer speed through Android DownloadManager; parallel transfers display an approximate average speed.

## Other implemented Android features

| Area | Functions and limitations |
| --- | --- |
| **Tabs and navigation** | Multi-tab WebView, tab picker, tablet strip, 20-tab limit, back/forward/reload, search engines and manually archived tabs. Native gestures in both engines: swipe left for Back, swipe right for Forward, pull down from the top of a page to refresh. GeckoView is an experimental separate session. |
| **Search** | Google, DuckDuckGo, Bing, Yahoo, Kagi, Brave Search, Startpage, Ecosia, Qwant, Mojeek, Swisscows and Wikipedia; per-provider behaviour not all tested on device. |
| **Bookmarks / reading list / history** | Locally stored bookmarks with editing/search, reading list, URL history and HTML/JSON bookmark imports. Cloud synchronisation is not available. |
| **Password migration** | Import supported CSV into an Android Keystore AES-GCM-encrypted local migration vault; export imported records as **plaintext CSV** after confirmation. **No browser autofill or password sync.** |
| **Cookie migration** | User-confirmed WebView import of compatible Netscape cookies.txt or browser JSON. GeckoView cookie import is not implemented; cookie exports can contain live sessions. |
| **Downloads** | Swipe up from the **bottom toolbar** to open the downloads panel, or use Menu → Downloads. The panel shows live progress and cancellation for Android DownloadManager transfers and experimental four-connection HTTPS range downloads (speed and per-connection progress on parallel transfers). It can be dismissed without interrupting the transfer. DownloadManager IDs are retained locally for status after restarting; direct parallel transfers only retain the latest in-process state. Downloaded files persist outside Incognito. |
| **Page tools** | Share sheet, WebView find-in-page, basic reader mode and Android Print / Save as PDF. GeckoView feature coverage differs. |
| **Appearance** | Dark native interface, adjustable text zoom, desktop-site option, optional webpage darkening and swiping on the address bar to switch tabs. |
| **Website permissions** | Site-level camera/microphone/location choices integrated with Android permission requests and GeckoView delegates; GeckoView notification requests are blocked in this build. |
| **Data clearing** | Opt-in normal WebView cookie, WebStorage, cache, history and form-data clearing. It does not promise to erase every GeckoView record. |
| **Account / extensions** | Sync sign-in and cross-device Orion Connect remain prototypes, not connected login flows. WebExtension management, private multi-tab browsing, passkeys, biometric locking and comprehensive autofill remain outstanding. |

## Build, validation and release

**Requirements:** JDK 17, Android SDK API 36, Gradle 8.11.1 and Android Gradle Plugin 8.9.2. From the repository root:

```bash
git clone https://github.com/2E0LXY/orion-windows.git
cd orion-windows
git checkout chatgpt/geckoview-home-splash
cd platforms/android
gradle --no-daemon -PorionArm64=true assembleDebug
# Omit -PorionArm64=true to package all supported ABIs.
```

Output: `platforms/android/app/build/outputs/apk/debug/app-debug.apk`.

**Verification levels:** GitHub Actions source contracts, Gradle compilation, ARM64 package inspection, SHA-256 checksum and style checks are automated. **On-device install/launch, weather and location permission UX, Incognito isolation and teardown, VPN DNS routing/leaks, and complete GeckoView parity remain UNVERIFIED unless explicitly reported with real test evidence.** Test-branch pushes publish direct GitHub Release APK assets after CI passes; PR-only runs do not publish a release.

**Source and review:**

- [MainActivity — native browser, homepage and privacy UI](app/src/main/java/uk/community/orion/MainActivity.java)
- [HomeWeather — forecast UI, geolocation and cache](app/src/main/java/uk/community/orion/HomeWeather.java)
- [DnsSettings — resolver settings and diagnostics](app/src/main/java/uk/community/orion/DnsSettings.java)
- [OrionDnsVpnService — experimental DNS tunnel](app/src/main/java/uk/community/orion/OrionDnsVpnService.java)
- [Source regression tests](tests/test_ui_contract.py) · [Android CI workflow](../../.github/workflows/android-prototype.yml) · [Roadmap](ROADMAP.md)

Feedback: [Report an issue](https://github.com/2E0LXY/orion-windows/issues/new) with device/Android version, build run, engine, steps and screenshots **without passwords, session cookies or private browsing data**.


### Branding, repository hygiene and downloads

The [official symbol for new community development](../../assets/orion-mark.svg) has the same blue ring, pale orbital streak and star as the Android launcher [vector asset](app/src/main/res/drawable/orion_icon.xml). Use it for new Orion-branded pages and collateral. Legacy Windows/Linux upstream assets remain where required and are not removed as expendable build debris.

The repository intentionally retains **source code, Android tests, the Android build recipe and platform-preservation materials**. No obsolete Android APK/ZIP files were tracked at audit time. Generated Gradle directories, local Android SDK settings, IDE caches, `.apk` and `.aab` outputs are now explicitly excluded by [.gitignore](../../.gitignore).

**[Standalone ARM64 test APK](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk)** · [matching SHA-256 checksum](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk.sha256) · [versioned prereleases](https://github.com/2E0LXY/orion-windows/releases). No unzip step is required. GitHub Release assets only become available following successful CI and release publishing; never treat a compile as device/security verification.
