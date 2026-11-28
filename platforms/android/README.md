# Orion Community Browser — Android

Experimental native Java browser for Android 8.0+ with Android System WebView and Mozilla GeckoView. Not an official Kagi release.

**[Latest Android ARM64 builds](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml)** (choose a successful run, download the ARM64 artefact and extract app-debug.apk). [Build logs](https://github.com/2E0LXY/orion-windows/actions) · [Draft PR #3](https://github.com/2E0LXY/orion-windows/pull/3) · [Full feature matrix](../../README.md).

## Features

- Shared native Orion home branding, GeckoView attribution and common browser menus. **The custom boot animation and its replay controls are removed**; Android manages the system launch screen.
- WebView tabs, experimental GeckoView session, configurable search, bookmarks, reading list, history, desktop mode, text zoom, dark appearance.
- **Orion security icon at the right of the bottom toolbar** opens Security and privacy. Incognito appears first, followed by ad/tracker controls, HTTPS warnings, JavaScript/image settings, camera/microphone/location permissions, DNS and local DNS VPN, and normal browsing-data controls. These have moved out of the ordinary browser menu and the general Settings screen.
- **Experimental Incognito:** separate GeckoView session using GeckoView's private mode, even when normal browsing is WebView. Private cookies and site storage are partitioned from normal sessions; Orion does not record private navigation in its saved history, allow private bookmarks or save the current private page as a normal homepage. Homepage weather is hidden. The app blocks screenshots and recents previews during the private session. Closing the session destroys the private tab. Only one private tab is currently supported, and physical-device storage-isolation tests are outstanding. Incognito does not hide traffic from networks, websites, accounts, or downloads saved outside the browser.
- **Homepage weather:** colour weather icons and five-day ECMWF IFS forecasts using the Open-Meteo ECMWF API. Requires a user-selected town or a one-time Android coarse-location permission request triggered by tapping **Use my location**. The stored latitude/longitude is rounded to 0.01 degrees (roughly 1 km latitude), not raw GPS precision. Android geocoding labels the nearby town where available. Forecasts are cached and refreshed when older than 90 minutes. Weather appears only on the Orion start screen, not saved external homepages. Settings → Homepage weather can hide the card or clear saved location/data. Network access to Open-Meteo is only for selected weather requests; forecast data are model predictions, not observations.
- Bookmark import, local encrypted credential migration store, user-confirmed password CSV export and WebView cookie import. No autofill or GeckoView cookie migration.
- WebView and GeckoView camera, microphone and approximate location controls, subject to Android permissions; GeckoView notifications blocked.
- WebView third-party cookie blocking, basic ad/tracker domain filtering, optional filter-list updates and data clearing.
- Standard downloads and experimental four-connection HTTPS downloads, WebView reader, find, sharing and PDF printing.

### DNS and network — Android

Open **Orion logo (far right of bottom toolbar) → Security and privacy → DNS and Secure DNS**.

- Cloudflare is the default GeckoView DNS-over-HTTPS (DoH) provider; strict mode is the default, with optional system DNS fallback.
- Presets: Cloudflare, Control D, Quad9, Google, OpenDNS, NextDNS, AdGuard, CleanBrowsing, DNS.Watch and Mullvad, plus Android system DNS and custom HTTPS DoH URL. NextDNS requires account configuration; DNS.Watch is reference-only.
- WebView uses Android-managed DNS; the app links to Android Private DNS settings and shows provider DoT hostnames where available.
- **Experimental WebView Secure DNS:** optional Android VpnService DNS-only tunnel scoped to Orion, with explicit Android consent, a persistent foreground notification, stop/restart controls and DNS-over-HTTPS forwarding to the selected supported preset. IPv4 and IPv6 virtual resolver paths are configured; normal internet traffic is not sent through this tunnel. It can conflict with existing VPNs. DNS-only TUN handling does not support TCP DNS, IPv6 extension headers or fragmented/large responses and cannot prove full WebView DNS coverage; **real-device DNS leak and connectivity tests are required before production use**. Cloudflare is the initial selected preset. Switching providers stops the tunnel until explicitly restarted.
- Live network details show assigned IPv4/IPv6 addresses and Android DNS servers. Selected-provider diagnostics query each listed primary/secondary IPv4/IPv6 address independently over UDP port 53, reporting response time or error. These diagnostic queries are unencrypted.
- Separate encrypted DoH A/AAAA query tests measure endpoint response times. AAAA resolution is not proof of IPv6 transport availability.
- Android manages dual-stack routing. Forced IP-family selection, independent WebView DNS, automatic provider benchmarking and verification of the resolver used by every browser request are not implemented.
- Physical-device testing of DNS enforcement, weather location permissions, forecast layout and UI remains outstanding.

## Build and test

JDK 17, Android SDK API 36, Gradle 8.11.1, AGP 8.9.2.

```bash
gradle --no-daemon -PorionArm64=true assembleDebug
```

Omit the property for universal packaging. Output: app/build/outputs/apk/debug/app-debug.apk.

Check the newest branch CI for compilation, UI regressions and ARM64 ABI checks; a successful build does not verify Incognito isolation on a real device. Physical-device testing and security review remain outstanding. Debug signing may vary between builds; back up data before uninstalling. No production signing, device-verified multi-tab private browsing, extension management, password autofill or functional cross-device sync yet.

### Weather data and attribution

The Orion weather card fetches **ECMWF IFS model forecasts via Open-Meteo** (not direct ECMWF authentication). It requests an hourly series containing temperature, apparent temperature, wind, relative humidity, precipitation and weather code, and aggregates five local days in the device UI. It uses HTTPS and caches the most recent forecast for offline use. Open-Meteo's free hosted API has non-commercial terms; verify the appropriate licence before commercial distribution. [ECMWF API documentation](https://open-meteo.com/en/docs/ecmwf-api) · [Open-Meteo terms](https://open-meteo.com/en/terms).
