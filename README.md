# Orion Browser — community development

Open-source **Orion** work covering **Windows, Linux, and Android**. This repository originated from Kagi's Orion for Windows release and now contains community experiments, including a native Android browser. **This is not an official Kagi-maintained release.**

> **Release status:** Android is an **experimental debug APK**, Windows is a public-source build/recovery effort, and Linux is a preserved source snapshot. Features, build success, and security reviews are **different** things. Please see the platform-specific status below.

<img src="assets/orion-mark.svg" width="76" height="76" alt="Orion blue ring, silver orbit and star symbol">

## Downloads — start here

**[Download the latest Android ARM64 test APK directly](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk)** — a single `.apk` file, **no ZIP to extract**. The test-branch CI publishes a versioned prerelease plus updates the stable test-download link after all automated checks pass. [Build history](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml) · [Draft PR #3](https://github.com/2E0LXY/orion-windows/pull/3). The link is ready when the first new direct-release job succeeds.

| Platform | Latest accessible download / source | Status |
| --- | --- | --- |
| **Android** | **[Download latest Orion Android test APK](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk)** | Published directly from successful `chatgpt/geckoview-home-splash` push builds; not a production-signed release. |
| Android — versioned | [All Orion releases](https://github.com/2E0LXY/orion-windows/releases) · [Android build runs](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml) | Earlier working APKs remain available for comparison and rollback |
| Android — previous verified | [Download verified test APK (build 46091875384)](https://github.com/2E0LXY/orion-windows/releases/download/android-dev-46091875384/app-debug.apk) | Versioned debug build; **does not contain every subsequent import change** |
| **Windows** | [Windows branch and source](https://github.com/2E0LXY/orion-windows) · [Public WebKit build PR](https://github.com/2E0LXY/orion-windows/pull/2) | No verified community Windows installer available yet |
| **Linux** | [Linux snapshot branch](https://github.com/2E0LXY/orion-windows/tree/archive/orion-linux) · [Cross-platform branch](https://github.com/2E0LXY/orion-windows/tree/chatgpt/cross-platform) | Source snapshot only; no verified Linux package available |

**Android requirements:** Android 8.0+ (API 26); **compiled against API 36**; test package includes Android System WebView and Mozilla GeckoView. Direct Android test releases package **arm64-v8a only** as standalone APK assets. Universal packaging remains available locally. Test APKs are **not security-reviewed or production-signed**. CI's debug signing key may change between builds, so an update might require uninstalling the earlier version and losing local app data. **Back up bookmarks and credentials before uninstalling.** Never assume imported credentials or cookies survive uninstall.

**Latest AI-sharing development branch:** [`chatgpt/default-browser-ai-connect`](https://github.com/2E0LXY/orion-windows/tree/chatgpt/default-browser-ai-connect) · **last published Android APK branch:** [`chatgpt/geckoview-home-splash`](https://github.com/2E0LXY/orion-windows/tree/chatgpt/geckoview-home-splash) · [GitHub Actions overview](https://github.com/2E0LXY/orion-windows/actions) · [Report bugs](https://github.com/2E0LXY/orion-windows/issues).

**[Android setup, menu map and complete feature guide](platforms/android/README.md)** · [Android implementation roadmap](platforms/android/ROADMAP.md) · [Source-level regression tests](platforms/android/tests/test_ui_contract.py). The Android guide documents how to use each new control, its engine scope, verification status and known limitations.

## Android default browser and AI sharing — development update (28 November 2026)

The current Android development work is in **[PR #4](https://github.com/2E0LXY/orion-windows/pull/4)** on branch **[`chatgpt/default-browser-ai-connect`](https://github.com/2E0LXY/orion-windows/tree/chatgpt/default-browser-ai-connect)**. This remains an **experimental draft**, separate from the last published direct-download APK on `chatgpt/geckoview-home-splash`.

- **Set as default browser:** the Android system browser-role prompt, HTTP/HTTPS link handlers and external-link navigation are implemented in source; users make the final choice in Android Settings.
- **Share a page with an AI app:** the Orion privacy hub offers Android's share sheet with the current normal tab's title and URL; Incognito pages are excluded.
- **Orion AI Connect (local MCP prototype):** an opt-in, loopback-only connection at `127.0.0.1:18796/mcp`, with a temporary bearer token, 15-minute expiry, a persistent sharing indicator, explicit approval for every tool request and immediate disconnect. Supports listing tabs, title/URL information, HTTPS navigation, back/forward/reload, plus experimental WebView page text, scroll, guarded click and guarded typing.
- **Privacy boundaries:** no Incognito sharing, cookies, stored credentials or password extraction. Screenshots, downloads and automated form submission are not available through AI Connect. GeckoView automation does not yet match WebView.
- **Important:** a local MCP interface is **not** a working automatic ChatGPT, Claude or Gemini remote-control connection. Those services need a compatible client/secure relay and separate integration. A page can be sent to an installed AI app using Android's share sheet without giving that app browser control.

The latest **development validation** can be followed in [Android Actions](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml). Source tests, compilation, static lint and a physically installed APK are **different verification steps**. The feature build and on-device security checks must pass before advertising AI Connect as stable or updating a public test-download link.

See the [detailed Android guide](platforms/android/README.md) for capabilities and limitations.

## Android — feature and implementation status

Android is implemented in **native Java** in [`platforms/android/`](https://github.com/2E0LXY/orion-windows/tree/chatgpt/geckoview-home-splash/platforms/android). The application label is **Orion Browser**. A passing CI build confirms compilation, **not** behaviour on a physical device.

| Area | Present in development source | Limitations / verification needed |
| --- | --- | --- |
| Branding and launch | Orion launcher icon and shared native Orion start page | **Custom boot animation and replay controls removed** following device feedback; Android provides its standard operating-system launch screen |
| Browser engines | Android System **WebView** (main browser) and optional **GeckoView** session via **Open in GeckoView** | Shared Orion home screen with GeckoView attribution and common menu; separate experimental session, not seamless per-tab switching; cookies remain separate |
| Homepage and weather | Press-and-hold Home saves the current website, triple-tap resets to the built-in Orion homepage. Optional native **ECMWF IFS five-day forecast** with colourful drawn weather icons | Approximate GPS-area location only after Android permission consent, or user-chosen town without location permission. Forecast comes through the Open-Meteo ECMWF API, cached for offline use and refreshed after 90 minutes; Settings can hide weather or erase saved location. Current device testing pending |
| GeckoView UI | Orion-styled top bar, address field, back/forward/reload/home controls, menu and return to WebView | Extension manager and native tab integration remain outstanding; camera, microphone and location delegates added, device testing pending |
| Tabs | Multi-tab WebView, tab counter, 20-tab limit, tab picker, tablet strip, manual URL archiving/restoration | No full crash-safe session restore, tab groups or intelligent tab suspension |
| Navigation | HTTP/HTTPS, address/search field, site navigation, back/forward/home/reload, configurable search | HTTP is unencrypted; user warning on explicit HTTP navigation; no universal HTTPS-only guarantee |
| Search | Google, DuckDuckGo, Bing, Yahoo, Kagi, Brave Search, Startpage, Ecosia, Qwant, Mojeek, Swisscows, Wikipedia | Search integrations have not all been device-verified |
| Appearance | Dark navy UI, bottom toolbar, configurable shortcut, tab-swipe option, text zoom, experimental webpage darkening, desktop-site UA | Inset/rotation, accessibility, larger fonts and GeckoView parity need testing |
| Bookmarks | Add/edit/delete, search and persistent local URL list | No folders, bookmark title management or cross-device synchronisation |
| Bookmark import | HTML Netscape-style export and JSON URL-tree import; bounds, URL checks, confirmation and duplicate handling | Importer is experimental; exported file formats differ between browsers |
| Password migration | Import browser/password-manager CSV, merge by URL and username, **Android Keystore AES-GCM encrypted local storage**, delete local vault | **Imported passwords do not yet autofill** and are not connected to GeckoView or Orion Connect; security audit pending |
| Password export | User-confirmed export of imported credentials as standard **plaintext CSV** | CSV reveals passwords; store and transfer securely, then delete when no longer needed; no encrypted cross-device backup yet |
| Cookie import | Opt-in Netscape `cookies.txt` or browser JSON export, domain preview, size/entry limits, WebView cookie import | Cookies may contain active sessions; GeckoView has a separate cookie store; **cookie export is not yet provided** |
| Website permissions | WebView camera, microphone and approximate location: Ask every time, Block, or Allow on request; Android runtime permission remains required | GeckoView camera, microphone and location handlers added; notification requests blocked; device verification pending; no remembered per-site allowlist |
| Privacy | Orion logo on far right opens Security and privacy: Incognito first, ad/tracker controls, HTTPS warnings, site permissions, DNS/local VPN and normal data clearing | Incognito uses a separate GeckoView private session, keeps its navigation out of Orion history and prevents normal bookmarking. One private tab only; real-device isolation tests and robust teardown are pending. Network/website logs, fingerprints and external downloads persist |
| Filter lists | Optional EasyList/EasyPrivacy **domain-rule subset** updates; selected YouTube ad-request endpoints | No complete rules engine, cosmetic filtering, or guarantee of removing video ads |
| Downloads | Native Android DownloadManager integration; experimental four-connection HTTPS range downloader with progress and cancellation | Range eligibility and validators required; single standard download fallback; no pause/resume/background foreground service |
| Reader and page export | Experimental article-text reader; Android printing/**Save as PDF** | Reader extracts text, not full article formatting; full-page screenshot/translation remain outstanding |
| Find and share | Find text on current WebView page; Android share sheet | GeckoView find-in-page not yet exposed |
| Data controls | Clear normal WebView data from the Orion security panel; bookmarks and imported passwords stored locally | Private GeckoView mode is separate from normal browsing, but device verification is pending. Clearing WebView data does not erase all GeckoView data |
| Account and sync | Orion Connect prototype documents and development relay | **Google OAuth sign-in, encryption, cross-device sync and push are not connected in the Android app** |
| Extensions | GeckoView engine dependency included | WebExtensions/uBlock management **not yet implemented** |
| Other outstanding | Background media controls, password autofill, passkeys, full-page translation, robust tab restore, production signing, comprehensive testing | Do not treat roadmap items as shipped features |

### How to use the new Android features

| What you want to do | Where to find it | Behaviour / scope |
| --- | --- | --- |
| **Open the original Orion homepage** | Home button, with no custom page selected | Native Orion logo and ECMWF forecast card for normal browsing in both engines. The operating system displays its standard launch screen; the unreliable custom boot overlay and replay entry are removed. |
| **Set a website as homepage** | Open the website → **press and hold Home** | The URL is saved across restarts. With a custom homepage, the built-in weather card is not shown. |
| **Restore the built-in Orion homepage** | **Tap Home three times quickly** | Removes the saved homepage URL. Set the optional toolbar shortcut back to **Home** if previously changed. |
| **Get local weather** | Built-in Orion start page → **Use my location** | Explicit Android coarse-location permission; rounded coordinates, local place label when available, five forecast days and cached results. |
| **Choose weather location without GPS** | Start page → **Choose a town** | Searches location names, then loads ECMWF IFS forecasts from Open-Meteo. |
| **Hide / clear weather data** | **Settings → Homepage weather** | Toggle the card or delete the saved location and forecast cache. |
| **Open all privacy controls** | **Orion icon on far right of bottom toolbar** | One Security and privacy panel for Incognito, ad-block/filter settings, HTTPS, website permissions, JavaScript, images, cache, DNS and data clearing. |
| **Start / end Incognito** | Orion security panel → **Start Incognito mode** or **End Incognito session** | One experimental GeckoView private-mode tab, separate from normal WebView and GeckoView browsing; screenshots restricted during the session. Does **not** hide network traffic or external downloads. |
| **Change secure DNS** | Orion security panel → **DNS and Secure DNS** | Cloudflare is the default GeckoView DoH provider. Android-managed WebView DNS is unchanged unless the optional DNS-only VPN is enabled with Android consent. |
| **Use the WebView DNS VPN** | DNS and Secure DNS → **Local DNS VPN** | Opt-in Orion-scoped DNS-over-HTTPS forwarding; IPv4/IPv6 virtual routes and service controls. Still experimental and **not DNS-leak verified**. |
| **Manage camera, microphone or location** | Orion security panel → **Camera, microphone and location** | Ask, Block or Allow-on-request behaviour, subject to Android runtime permissions and engine limitations. |
| **Import browser data** | Menu → **Import browser data** | Bookmark HTML/JSON, password CSV into local encrypted migration storage, or compatible WebView cookie exports. Autofill and cross-engine cookie sync are not implemented. |

**Privacy statement:** The GeckoView Incognito session is configured for separate temporary browser storage; normal Orion history/bookmark writes are disabled, but data teardown and crash/restart behaviour are **not yet hardware verified**. Your IP address, network traffic patterns, signed-in accounts, device fingerprint and downloaded files may remain observable or persist after you close the session. Android security and network policies continue to apply.

### Orion security and Incognito — Android

The **Orion icon on the far right of the bottom toolbar** opens a native privacy and security panel. Incognito is the first action. Ad-block and filter rules, HTTPS warnings, JavaScript/image controls, camera/microphone/location permissions, DNS and the experimental local VPN, and normal browsing-data management now live in this menu.

Incognito starts a **GeckoView private-mode session**, even when the ordinary engine is Android WebView. Cookies and temporary site data are isolated from ordinary tabs by GeckoView; Orion skips private navigations in its saved history and disallows private bookmarks and saving a normal homepage. Homepage weather is hidden; Android blocks screenshots via FLAG_SECURE. Closing Incognito returns to normal tabs. This version supports **one private tab** only, and the actual data teardown and crash/relaunch behaviour still need real-device testing.

Incognito **does not provide network anonymity**: network operators and websites still observe connections; websites can fingerprint browser characteristics; downloads, shares, external accounts and logs can persist. VPN and device management policies are unaffected. Private extensions and multi-tab Incognito are not implemented.

### Homepage weather — Android

The built-in Orion homepage displays a native weather card for both WebView and GeckoView. Colour weather icons indicate sun, clouds, rain, snow and thunderstorms, with Celsius temperatures, humidity, wind, precipitation and five local forecast days. The card requests **ECMWF IFS data via Open-Meteo's ECMWF forecast endpoint** and shows visible attribution; it does not use an ECMWF private API key. A user can explicitly choose approximate location or search for a town. Coordinates from Android location are rounded to two decimals before storage or transmission. Saved forecast data are cached for offline display, with refreshes after approximately 90 minutes. Clear the stored location or hide the card from Settings → Homepage weather. No automatic GPS permission prompt is made on launch, and custom external homepages do not display the card. Hosted API licensing must be checked before commercial distribution. [Data source](https://open-meteo.com/en/docs/ecmwf-api).

### DNS and network — Android

Open **Orion logo (right of bottom toolbar) → Security and privacy → DNS and Secure DNS**.

- Cloudflare is the default GeckoView DNS-over-HTTPS (DoH) provider; strict mode is the default, with optional system DNS fallback.
- Presets: Cloudflare, Control D, Quad9, Google, OpenDNS, NextDNS, AdGuard, CleanBrowsing, DNS.Watch and Mullvad, plus Android system DNS and custom HTTPS DoH URL. NextDNS requires account configuration; DNS.Watch is reference-only.
- WebView uses Android-managed DNS by default; Android Private DNS settings and DoT hostnames are linked. **An experimental, user-consented local DNS VPN** is also included for selected DoH providers. It scopes its virtual DNS routes to Orion, but does not yet guarantee all WebView DNS queries are captured. Physical-device testing and DNS-leak verification are required.
- Live network details show assigned IPv4/IPv6 addresses and Android DNS servers. Selected-provider diagnostics query each listed primary/secondary IPv4/IPv6 address independently over UDP port 53, reporting response time or error. These diagnostic queries are unencrypted.
- Separate encrypted DoH A/AAAA query tests measure endpoint response times. AAAA resolution is not proof of IPv6 transport availability.
- Android manages dual-stack routing. Forced IP-family selection, independent WebView DNS, automatic provider benchmarking and verification of the resolver used by every browser request are not implemented.
- Physical-device testing of DNS enforcement, weather location permissions, forecast cards and UI remains outstanding. The custom boot animation has been removed.

### Import browser data — Android

Open **Orion menu → Import browser data** or **Settings → Browser data → Import browser data**.

1. **Bookmarks:** Choose a bookmark-export file (HTML or JSON). Orion previews the number of new entries and preserves existing bookmarks.
2. **Passwords:** Choose a CSV exported from your *own* browser or password manager. Common fields are website URL, username and password (column names vary). Orion stores imported data in its Android Keystore-encrypted vault. **This is a migration store, not yet a password autofill manager.**
3. **Export passwords:** Use the password export action to choose a destination for a **plaintext CSV**. Anyone with that file can read the credentials; move it only to trusted storage and remove unneeded copies.
4. **Cookies:** Select an export in Netscape cookies.txt or supported JSON cookie-array format. Review the domains and explicitly confirm import. Cookies are applied **only to WebView**, not GeckoView; imported login sessions might not work or might be rejected by sites.
5. **Android password manager:** Android Autofill settings are a separate workflow; Orion cannot silently access another app's saved passwords, cookies or encrypted profile database.

**Supported export sources vary by browser:** Chrome, Edge, Firefox, Brave, Vivaldi, Opera, and others can supply compatible formats where their export tooling permits. **This does not imply direct profile access or support for every proprietary export.** Files are opened with Android's system file picker, not by requesting access to another browser's private app storage.

### Local development — Android

Prerequisites: **JDK 17**, Android SDK with **API 36**, Android Gradle Plugin **8.9.2**, Gradle **8.11.1**, and access to Mozilla's Maven repository.

```bash
git clone https://github.com/2E0LXY/orion-windows.git
cd orion-windows
git checkout chatgpt/geckoview-home-splash
cd platforms/android
gradle --no-daemon -PorionArm64=true assembleDebug
# Omit -PorionArm64=true for universal packaging
```

Output: `platforms/android/app/build/outputs/apk/debug/app-debug.apk`. This is an experimental debug APK, not a signed distribution release. Push builds of the Android development branch publish direct APK GitHub Releases; PR checks validate builds but do not publish user-downloadable ZIP artifacts.

See the [Android roadmap](platforms/android/ROADMAP.md), [Android APK workflow](.github/workflows/android-prototype.yml), and [Android source tree](platforms/android/app/src/main/java/uk/community/orion/).

## Windows — community development

The original Windows code is **C# / WinUI 3** plus C++ backend and pluggable **WebKit** and **CEF/Blink** engines. The source archive's comprehensive intended/implemented feature list and known gaps appear in the preserved Windows documentation below.

Community work is being tracked separately:

- [Public WebKit port and build work](https://github.com/2E0LXY/orion-windows/pull/2) · [`chatgpt/upstream-webkit` source](https://github.com/2E0LXY/orion-windows/tree/chatgpt/upstream-webkit).
- Original Kagi-hosted WebKit archives and binary caches are not generally available to community builders; the public-source replacement still needs validation and may require build-system fixes.
- **No Windows executable should be presented as downloadable or production-ready until CI and a Windows machine verify it.** Features listed in the archived Windows README describe the **upstream code**, not a verified community binary.

## Linux — community source snapshot

- [Preserved Linux upstream snapshot](https://github.com/2E0LXY/orion-windows/tree/archive/orion-linux) and [cross-platform integration branch](https://github.com/2E0LXY/orion-windows/tree/chatgpt/cross-platform).
- The source snapshot is preserved for continued development; external dependencies and full build reproducibility have not been validated.
- **No verified Linux installer/package** is published here yet.

## Orion Connect — shared-device plans and prototype

[Orion Connect architecture](shared/orion-connect/ARCHITECTURE.md) · [Development relay](shared/orion-connect/relay/) · [Connect CI](.github/workflows/orion-connect.yml).

Design goals: user-authorised sharing of links, bookmarks and settings between Android, Windows and Linux; explicit device pairing; end-to-end encrypted data; user revocation; no server-side access to decrypted content. **These are goals, not deployed app functionality.** The Go relay currently provides development-only notification tests, not a production authenticated public sync service. No passwords or cookies are synchronised.

## Releases, testing and support

- **[Latest Android APK](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk)** — direct download with no ZIP extraction after a successful new CI publication.
- **[All downloadable releases](https://github.com/2E0LXY/orion-windows/releases)** — versioned Android builds and any future installers.
- **[Android Actions](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml)** — status, compile errors and build artefacts.
- **[Connect tests](https://github.com/2E0LXY/orion-windows/actions/workflows/orion-connect.yml)**.
- **[Report an issue](https://github.com/2E0LXY/orion-windows/issues/new)**. Include Android version, device model, engine (WebView/GeckoView), build number, steps and screenshots. **Never attach passwords, cookies, session tokens or unredacted personal browsing data.**

The community source is derived from Kagi's [public Orion Windows archive](https://github.com/kagisearch/orion-windows) and [Linux archive](https://github.com/kagisearch/orion-linux), licensed under **Apache 2.0** where applicable; bundled third-party components retain their own licences. Check the repository's licensing files before redistribution.

---

## Windows upstream reference documentation

**Note:** Everything below this heading is retained from the original Windows README as a source/architecture reference. It is **not** a promise that a new community Windows installer exists or that all original features work in current Android builds.

## Windows upstream archive: architecture and feature reference

Orion is a web browser for Windows built on two interchangeable browser
engines: **WebKit** and **CEF (Chromium/Blink)**. The engine is chosen at
runtime, not at build time. The app pairs a native Windows UI with a
cross-platform core shared with Orion for Linux (the `orion-linux` module).

This repository is the public archive of the Windows browser. See the
[Public archive](#public-archive) section below.

### Public archive

Kagi ended development of Orion for Linux and Windows in October 2026 and
open-sourced both so the community can carry them forward. This repository is
that public release of the Windows browser. The planned Windows launch did not
ship from Kagi; the code is here for the community to build on.

Read the announcement for full details: [An Update on Orion for Linux and Windows](https://blog.kagi.com/update-orion-linux-windows)

The code is released under the Apache License 2.0. Bundled third-party
components keep their own licenses.

Many core browsing features work, and several features expected of a
production browser are still missing or partial. See
[Browser features](#browser-features) for the current status.

### Architecture

```
┌─────────────────────────────────────────┐
│  Orion (C# / WinUI 3)                   │
│  MainWindow, ViewModels, Models         │
│  Tab management, UI, MVVM               │
└──────────────┬──────────────────────────┘
               │ P/Invoke (OrionBackend.dll only)
               ▼
┌─────────────────────────────────────────┐
│  OrionBackend.dll (C++, engine-free)    │
│  exports.cpp + engine_loader            │
│  + orion-linux core module (static)     │
└──────┬───────────────────┬──────────────┘
       │ LoadBrowserEngine │ (exactly one per process)
       ▼                   ▼
┌──────────────────┐ ┌──────────────────┐
│ OrionEngineWK.dll│ │ OrionEngineB.dll │
│ WebKit 2 (vcpkg) │ │ CEF (Blink)      │
└──────────────────┘ └──────────────────┘
```

The app is composed of three primary layers:

- **`Orion/` — C# / WinUI 3 frontend.** The browser UI: windows, tabs,
  settings, library, dialogs. It follows MVVM using
  [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/).
  It talks to native code only through P/Invoke into `OrionBackend.dll`.
- **`OrionBackend/` — C++ backend.** A thin, engine-free facade DLL
  (`exports.cpp`) that the C# layer calls. It links the cross-platform
  `orion-linux` core module statically and loads exactly one engine plugin at
  runtime. The engine plugins (`OrionEngineWK.dll`, `OrionEngineB.dll`)
  implement a common `BrowserEngine` interface and are loaded from
  `engines\webkit` or `engines\blink`.
- **`external/orion-gtk` — cross-platform core module.** Shared with Orion for
  Linux. It owns the database, settings, profiles, history, bookmarks,
  passwords, reading list, sync, and the WebExtensions runtime. It is built
  with CMake and linked into the facade.

#### Key design points

- **Dual engine, runtime selection.** Every build produces one `Orion.exe`, the
  engine-free facade, and both engine plugins. The engine is picked at startup
  from `--engine=webkit|blink` or the `browser_engine` setting (default
  `webkit`). One process runs exactly one engine; switching takes effect on
  relaunch.
- **The engine seam.** Engine code never links the core directly. The facade
  hands each plugin a versioned `HostServices` function table, and a shared
  `host_shim.cpp` forwards logging, download, and profile-path calls through it.
  Any new core symbol used from engine code fails to link, which keeps the
  boundary clean.
- **Process model.** On Windows, one browser process runs per profile. The
  process that holds a profile's single-instance mutex owns that profile's
  database and serves `open`/`quit`/notification requests from other
  `Orion.exe` launches over a named pipe. The core module itself stays
  profile-agnostic for Linux, where everything runs in one process.
- **Run loop integration.** The core uses a libuv event loop. WinUI owns the UI
  thread, so the loop is pumped from the UI thread (`TickOrionCore`) via a
  watcher thread that waits on the core's wakeup event (and CEF's, under Blink).
- **Generated wire structs.** The C#/C++ interop structs are generated by
  `OrionCodeGen` from the core module's DTO definitions. `native_types.h` and
  `NativeTypes.cs` are generated, not hand-written.

### Browser features

The browser runs on either engine unless noted. "WebKit only" features do not
work under the Blink/CEF engine.

#### Implemented

**Tabs and windows**
- Multiple windows, each with its own tab group
- Open, close, reorder, and drag tabs; nested tab tree and tab groups
- Pinned tabs (persisted per profile)
- Tab hover previews and tab thumbnails
- Tab overview (all-tabs grid)
- Recently closed tabs and windows; reopen last closed; tab close undo/redo
- Shared pinned-tabs across windows
- Fullscreen mode (with optional always-show-toolbar)

**Profiles**
- Multiple profiles, each with its own data and engine data directory
- Create, rename, recolor, and re-icon profiles in Settings
- Per-profile keyboard shortcut to launch
- Profile snapshots (take and restore)
- Per-profile single-instance process model

**Navigation**
- Load, reload, reload-bypassing-cache, back, forward
- Address-bar navigation with a configurable search engine (default Kagi)
- Custom and per-site user agent
- Minimum font size, JavaScript on/off
- Custom error pages (DNS, SSL, timeout, etc.)
- PDF viewing (engine built-in viewer)

**History**
- Visit recording and history menu grouped by day
- History library with search and per-entry and bulk delete
- Clear all history

**Bookmarks**
- Bookmark tree with folders (create, rename, move, delete)
- Bookmarks bar (toggle)
- Add bookmark and folder from the current page
- Recently bookmarked menu

**Reading list**
- Add pages, mark read/unread, delete
- Reading list library and sidebar

**Downloads**
- Download manager page (`orion://downloads`) with progress
- Cancel and restart downloads
- Download history and delete
- Configurable download folder and "ask where to save"

**Passwords and autofill**
- Password manager UI in Settings (view, search, edit, delete)
- Save-password prompt on form submit
- Autofill of saved credentials into focused fields
- "Never save for this site"
- Import passwords from CSV
- Credentials encrypted with Windows DPAPI

**Web extensions (WebKit only, partial)**
- Install `.xpi` / `.crx` packages and a popular-extensions catalog
- Enable, disable, uninstall
- `browser.storage`, `browser.alarms`, `browser.i18n`, `browser.notifications`
- See [Limitations](#web-extension-limitations) below for what is missing

**Web platform**
- Find in page (find bar, next/previous)
- Developer tools (WebKit Web Inspector)
- HTTP basic/digest authentication prompts
- Popup blocking
- Context menus (page, link, image, media) with copy/open/save actions
- Camera / microphone / screen-share permission prompts (Blink engine only)
- Web notifications (WebKit engine only, Windows toast integration)

**Appearance and settings**
- Light/dark theme and system-following appearance
- Per-site settings (reader mode, JavaScript, downloads, user agent)
- Settings categories: General, Appearance, Browsing, Tabs, Search, Privacy,
  Passwords, Websites, Extensions, Profiles, Sync, Advanced
- Customizable toolbar

**Privacy and data**
- Cookie and tracking-prevention settings
- Per-site permission controls
- Profile data export and import (folder-based)

#### Not yet implemented

These are features a user would expect from a production browser that are
missing or only stubbed today. Many appear in the menu bar as disabled items.

**Browsing modes**
- Private / incognito windows (the data model supports them, but the "New
  Private Window" command is disabled)
- Focus / guest windows

**Page tools**
- Page zoom (the engine primitive exists, but it is not wired to the UI)
- Printing on the WebKit engine (printing works on the Blink engine via the
  page context menu; the main-menu Print item is disabled)
- Spell-check and grammar checking
- Page translation
- Screenshots (page and full-page)
- Browser-level picture-in-picture control (the menu item is disabled)
- Reader mode (a per-site setting exists, but the reader view is not exposed)
- "Install site as an app" / progressive web apps
- Notes, page tweaks, QR codes, summarize-page

**Import and export**
- Bookmark export (the menu command is a stub)
- Bookmark import from a bookmarks HTML file (disabled)
- Password export to CSV (the menu command is a stub; import works)
- "Export as PDF" (the dialog is a stub)

**Address bar**
- Address-bar search suggestions / autocomplete are not wired. The search
  settings expose autocomplete options, but the address bar does not fetch or
  show suggestions.

**Permissions**
- Geolocation permission prompts are not implemented.
- Camera / microphone / screen-share prompts work only on the Blink engine; the
  WebKit engine does not yet handle them.

**Tab and window management**
- Mute tab / mute other tabs
- Duplicate tab, suspend tab, move tab to new window, merge all windows
- Close window / close all windows / close tabs to the left (menu items)
- Low Power Mode (dialog only, backend not wired)

**Sync**
- Cross-device cloud sync is not functional. The "Sync" settings page offers
  local folder export/import of profile data and a cross-device toggle that is
  not yet backed by a sync service.

**Developer tools**
- WebDriver / WebDriver BiDi automation is a design document only
  (`docs_ai/webdriver_bidi_design.md`); no endpoint is implemented.
- Standalone console/network/storage panels, inspect-element, responsive design
  mode, and user-agent switching from the menu are disabled (the Web Inspector
  itself is available).

**Other**
- "Set as default browser"
- "Reset Orion" (the reset window does not yet perform the reset)
- JavaScript dialogs (`alert` / `confirm` / `prompt`) are not routed on the
  Blink engine
- In-app update check
- Profile create/delete from the menu (use Settings instead)
- Notes, dictation, and macOS-style Edit-menu text services

#### Web extension limitations

Extensions run only on the WebKit engine. Under Blink/CEF, the extension
exports exist but there is no scheme handler or script bridge, so extensions do
not function. Even on WebKit, the support is partial:

- No content-script injection (manifest `content_scripts` is not applied).
- No extension background pages or popup windows yet.
- No extension toolbar buttons or badges.
- `browser.tabs`, `browser.windows`, `browser.webNavigation`,
  `browser.webRequest`, `browser.contextMenus`, and `browser.browserAction`
  are dispatch stubs that do not settle.
- `browser.runtime` messaging (ports, `sendMessage`) is not implemented.
- A WebKit engine gap currently blocks loading custom-scheme URLs on Windows;
  until it is fixed in the pinned WebKit, extension pages may not load.

### Development setup

#### Install development tools

Run `install_dev_tools.bat`. This installs Visual Studio 2026 Community with the
correct workloads (from `.vsconfig`) and other native requirements (for
example, gcloud for the binary build cache, so you do not have to build WebKit
locally).

After installation, check Google Cloud Storage bucket access:

```
gcloud auth login
# test that bucket access works
./scripts/gsutil.cmd ls gs://kagi-orion-windows-artifacts/
```

#### Initialize submodules

Run `setup_orion_gtk_submodule.bat`. This fetches `orion-linux` and its
submodules (except WebKit) and generates the `orion-linux` projects for the Orion
solution.

Then configure the core module:

```
update_core_module.bat
```

Then open `Orion.slnx` in Visual Studio and build. Command-line builds:

```
ci\build_debug.cmd      # or build_release.cmd
ci\unittest.cmd         # dotnet test UnitTest (Release, x64)
```

#### WebKit

WebKit is built via vcpkg. Source is fetched automatically during the build
from a tarball that the `kagisearch/webkit` CI publishes to GCS for every
orion-windows commit. Binaries are cached in GCS too, so most developers never
build WebKit locally.

To build and upload new WebKit binaries to the cache (for example, after
updating the `REF` in `ports/webkit-win/portfile.cmake`), either build normally
in Visual Studio, or run:

```
./vcpkg.cmd install
```

Both approaches set `VCPKG_BINARY_SOURCES` to read and write the GCS bucket
automatically.

#### CEF

CEF is provisioned automatically by the `OrionCefSetup` project, which runs
`cef.py.cmd setup`. The version and source are pinned in `vendor/CEF_VERSION`.

### Documentation

- `CLAUDE.md` — build system, architecture, and code-style guide for the repo.
- `docs/` — manually edited documentation.
- `docs_ai/` — generated/managed notes (architecture, web extensions, profile
  storage, CEF performance, WebDriver design).


### Canonical Orion brand and direct APK publishing (November 2026)

The canonical **Orion** identity is the blue circular ring, pale orbit and white star shown in the original Android launcher. Source: [scalable Orion symbol](assets/orion-mark.svg); Android uses the matching [vector launcher drawable](platforms/android/app/src/main/res/drawable/orion_icon.xml). Windows and Linux upstream assets are retained for build and licence compatibility; this independent community artwork is used for new Orion promotional materials and pages. The previous custom startup overlay was removed; the operating system can show its own icon-based splash.

[**Download Android test APK directly**](https://github.com/2E0LXY/orion-windows/releases/download/android-latest-test/Orion-Browser-Android-test.apk). The Android workflow now tests and builds ARM64, verifies its native libraries, publishes a **versioned prerelease** and updates the **same direct .apk link** plus a SHA-256 checksum file. Unlike GitHub Actions artifacts, Release assets do **not** require ZIP extraction. No build output, debug APK, copied archive or IDE cache is tracked in the repository. Check the [CI workflow](.github/workflows/android-prototype.yml) for publishing conditions.

**Release gate:** automated tests and compilation must pass before publication. This is an **experimental debug build**, not a device-certified or production-signed release. Real-device browsing/privacy/DNS tests remain a separate gate. A changed debug signature may require uninstall/reinstall, which deletes local browser data.
