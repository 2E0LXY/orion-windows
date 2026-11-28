# Orion Community Browser — Android

Experimental native Java browser for Android 8.0+ with Android System WebView and Mozilla GeckoView. Not an official Kagi release.

**[Latest verified ARM64 test build](https://github.com/2E0LXY/orion-windows/actions/runs/46121304241/artifacts/15253277892)** (98.4 MB ZIP; extract app-debug.apk). [Build logs](https://github.com/2E0LXY/orion-windows/actions/runs/46121304241) · [Draft PR #3](https://github.com/2E0LXY/orion-windows/pull/3) · [Full feature matrix](../../README.md).

## Features

- Shared Orion home branding, GeckoView attribution, common browser menus, animated startup and replay option (device retest needed).
- WebView tabs, experimental GeckoView session, configurable search, bookmarks, reading list, history, desktop mode, text zoom, dark appearance.
- Bookmark import, local encrypted credential migration store, user-confirmed password CSV export and WebView cookie import. No autofill or GeckoView cookie migration.
- WebView and GeckoView camera, microphone and approximate location controls, subject to Android permissions; GeckoView notifications blocked.
- WebView third-party cookie blocking, basic ad/tracker domain filtering, optional filter-list updates and data clearing.
- Standard downloads and experimental four-connection HTTPS downloads, WebView reader, find, sharing and PDF printing.

### DNS and network — Android

Open **Orion menu → DNS and network** in either engine, or **Settings → Privacy and Security → DNS and Secure DNS**.

- Cloudflare is the default GeckoView DNS-over-HTTPS (DoH) provider; strict mode is the default, with optional system DNS fallback.
- Presets: Cloudflare, Control D, Quad9, Google, OpenDNS, NextDNS, AdGuard, CleanBrowsing, DNS.Watch and Mullvad, plus Android system DNS and custom HTTPS DoH URL. NextDNS requires account configuration; DNS.Watch is reference-only.
- WebView uses Android-managed DNS; the app links to Android Private DNS settings and shows provider DoT hostnames where available.
- **Experimental WebView Secure DNS:** optional Android VpnService DNS-only tunnel scoped to Orion, with explicit Android consent, a persistent foreground notification, stop/restart controls and DNS-over-HTTPS forwarding to the selected supported preset. IPv4 and IPv6 virtual resolver paths are configured; normal internet traffic is not sent through this tunnel. It can conflict with existing VPNs. DNS-only TUN handling does not support TCP DNS, IPv6 extension headers or fragmented/large responses and cannot prove full WebView DNS coverage; **real-device DNS leak and connectivity tests are required before production use**. Cloudflare is the initial selected preset. Switching providers stops the tunnel until explicitly restarted.
- Live network details show assigned IPv4/IPv6 addresses and Android DNS servers. Selected-provider diagnostics query each listed primary/secondary IPv4/IPv6 address independently over UDP port 53, reporting response time or error. These diagnostic queries are unencrypted.
- Separate encrypted DoH A/AAAA query tests measure endpoint response times. AAAA resolution is not proof of IPv6 transport availability.
- Android manages dual-stack routing. Forced IP-family selection, independent WebView DNS, automatic provider benchmarking and verification of the resolver used by every browser request are not implemented.
- Physical-device testing of DNS enforcement, permission prompts, startup animation and UI remains outstanding.

## Build and test

JDK 17, Android SDK API 36, Gradle 8.11.1, AGP 8.9.2.

```bash
gradle --no-daemon -PorionArm64=true assembleDebug
```

Omit the property for universal packaging. Output: app/build/outputs/apk/debug/app-debug.apk.

Latest referenced CI: Android compilation, source regression checks, ABI verification and style checks passed. Physical-device testing and security review remain outstanding. Debug signing may vary between builds; back up data before uninstalling. No production signing, full private browsing, extension management, password autofill or functional cross-device sync yet.
