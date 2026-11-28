# Orion Community Browser for Android

Experimental **native Java** Android browser combining **Android System WebView** and **Mozilla GeckoView**. Community-developed from the Orion project; **not an official Kagi release**.

**Android 8.0+ (API 26)** · **ARM64 development APKs** · **Draft [PR #3](https://github.com/2E0LXY/orion-windows/pull/3)** · [Root README](../../README.md) · [Android roadmap](ROADMAP.md)

## Download and install

**[Latest Android APK build runs](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml)** — select the newest **successful** `Android prototype APK` run on `chatgpt/geckoview-home-splash`, open **Artifacts → orion-community-arm64-debug**, download the ZIP and extract `app-debug.apk`.

- The PR build is **not automatically published** to the stable [release shortcut](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk); that shortcut can be older.
- Current test builds contain **arm64-v8a** GeckoView libraries. A universal APK can be compiled without the ARM64 Gradle property, but is not necessarily published as a test artifact.
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
3. **Website security:** HTTPS/HTTP warnings, JavaScript, image loading and zero-cache option.
4. **Site permissions:** camera, microphone and approximate location — Ask every time, Block or Allow on request, subject to Android permission and browser-engine support.
5. **Encrypted DNS and network:** provider choice, diagnostics, Android Private DNS and experimental local DNS VPN.
6. **Browsing data:** explicitly clear normal WebView cookies, site storage, cache and tab history.

The standard browser menu and general Settings screen no longer duplicate the main privacy controls.

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

## Other implemented Android features

| Area | Functions and limitations |
| --- | --- |
| **Tabs and navigation** | Multi-tab WebView, tab picker, tablet strip, 20-tab limit, back/forward/reload, search engines and manually archived tabs. GeckoView is an experimental separate session. |
| **Search** | Google, DuckDuckGo, Bing, Yahoo, Kagi, Brave Search, Startpage, Ecosia, Qwant, Mojeek, Swisscows and Wikipedia; per-provider behaviour not all tested on device. |
| **Bookmarks / reading list / history** | Locally stored bookmarks with editing/search, reading list, URL history and HTML/JSON bookmark imports. Cloud synchronisation is not available. |
| **Password migration** | Import supported CSV into an Android Keystore AES-GCM-encrypted local migration vault; export imported records as **plaintext CSV** after confirmation. **No browser autofill or password sync.** |
| **Cookie migration** | User-confirmed WebView import of compatible Netscape cookies.txt or browser JSON. GeckoView cookie import is not implemented; cookie exports can contain live sessions. |
| **Downloads** | Android DownloadManager, plus an experimental four-connection HTTPS range downloader with single-request fallback. Downloaded files persist outside Incognito. |
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

**Verification levels:** GitHub Actions source contracts, Gradle compilation, ARM64 packaging, artifact checksum, and style checks are automated. **On-device install/launch, weather and location permission UX, Incognito isolation and teardown, VPN DNS routing/leaks, and complete GeckoView parity remain UNVERIFIED unless explicitly reported with real test evidence.** A draft PR build produces a workflow artifact, not a published release.

**Source and review:**

- [MainActivity — native browser, homepage and privacy UI](app/src/main/java/uk/community/orion/MainActivity.java)
- [HomeWeather — forecast UI, geolocation and cache](app/src/main/java/uk/community/orion/HomeWeather.java)
- [DnsSettings — resolver settings and diagnostics](app/src/main/java/uk/community/orion/DnsSettings.java)
- [OrionDnsVpnService — experimental DNS tunnel](app/src/main/java/uk/community/orion/OrionDnsVpnService.java)
- [Source regression tests](tests/test_ui_contract.py) · [Android CI workflow](../../.github/workflows/android-prototype.yml) · [Roadmap](ROADMAP.md)

Feedback: [Report an issue](https://github.com/2E0LXY/orion-windows/issues/new) with device/Android version, build run, engine, steps and screenshots **without passwords, session cookies or private browsing data**.
