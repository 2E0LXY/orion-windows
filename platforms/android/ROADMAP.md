# Orion Android roadmap

Status reflects prototype source, not device acceptance.

## Ergonomics
- [x] Bottom address bar; verify reachability and insets.
- [ ] Customisable toolbar/menu; persistent action ordering.
- [ ] Forced dark mode; preserve image colours and contrast.
- [ ] Gesture navigation; avoid conflicts with system gestures.
- [ ] Tablet/foldable layouts; test resizing and posture changes.

## Privacy and security
- [ ] Complete ad/tracker filtering: current domain subset only; implement exceptions, rule compatibility, updates and regression tests.
- [ ] Cookie/annoyance filtering: never silently consent to optional tracking.
- [ ] Isolated private sessions and biometric protection; require verified isolation and safety review.
- [ ] HTTPS upgrades and phishing protection: current HTTPS-only and WebView Safe Browsing are partial; test certificate failures and warnings.
- [ ] Anti-fingerprinting and telemetry controls: no app telemetry currently; audit engine behaviour, avoid unsupported anonymity claims.

## Productivity
- [ ] Extensions: evaluate GeckoView WebExtensions and Chromium feasibility; permissions and signed distribution.
- [ ] Tab grouping and archiving: restore state, avoid silent loss.
- [ ] Encrypted cross-device sync: Google Drive app data plus authenticated push relay; OAuth and encryption outstanding.
- [ ] Password/passkey support: platform credential APIs first; no custom vault until independent security audit.
- [ ] Full-page screenshots: bounded memory and explicit export.

## Media and performance
- [ ] Reader mode: sanitised article extraction and adjustable typography.
- [ ] Background media/video controls: platform media session and user controls.
- [ ] Full-page translation: disclose remote processing, opt-in.
- [ ] Downloads: validate destinations and filenames, platform download integration, status and cancellation; resume only when supported.
- [ ] Memory/battery: bounded tabs, lifecycle handling, benchmark before and after.

## Delivery order
1. Reliability, accessibility, bookmark management, toolbar customisation, dark mode, find in page, downloads.
2. Tab lifecycle, reader mode, adaptive layouts, screenshots, filter correctness.
3. Engine feasibility and extension security.
4. Google OAuth, encrypted sync and push integration.
5. Credential integration, private-session isolation and advanced media/translation.

Every release requires CI, device smoke tests, versioned APK, regression checks and honest feature status. No production account credentials in experimental builds. No claim of full extension compatibility or advert-free YouTube.
