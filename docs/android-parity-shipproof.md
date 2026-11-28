# Orion Android — six-phase implementation and ShipProof gate

Updated 2026-11-28. **Source committed does not imply compilation, on-device acceptance or production release.**

| Phase | Implemented in source | Outstanding for acceptance | Verification |
| --- | --- | --- | --- |
| 1. Engine/UI/security | GeckoView in shared Orion shell; web page/history/bookmark navigation; WebView site permission choices; imported-password vault | GeckoView site permission bridging, credential autofill, credential audit, crash and lifecycle testing | Source regression tests; device check UNVERIFIED |
| 2. Extensions/filters | GeckoView dependency; basic WebView domain blocking | Signed extension installation, permissions, uBlock compatibility, full filter syntax, exceptions, element blocker | UNVERIFIED |
| 3. Productivity | WebView tabs, bookmarks, reader prototype, reading list, recent URL history, search | Groups, pinned tabs, thumbnails, folders, session recovery, rich reader | Basic source paths; device check UNVERIFIED |
| 4. Performance | Four-connection experimental range download; 20-tab cap | Pause/resume, reliable service, metrics, memory/battery benchmarks, ABI split optimisation | APK build; network/benchmark UNVERIFIED |
| 5. Mobile enhancements | Android share, print/PDF, toolbar shortcut, dark screen, animation | PiP, translation, full-page screenshot, advanced gestures, responsive and accessibility test matrix | UNVERIFIED |
| 6. Orion Connect/release | Go development notification relay, architecture, debug GitHub Actions and versioned release | Authenticated OAuth, E2E encryption, Windows/Linux interoperability, stable signing, release/device acceptance | Connect CI; production sync and release UNVERIFIED |

## Acceptance matrix — manual Android device

Record build SHA, device, Android version, screen size, renderer, date and reviewer. A successful Gradle build alone is **insufficient**.

- [ ] Cold launch from launcher: icon visible; orbit/star visibly moves; animation completes; settings and tabs remain accessible.
- [ ] Launch with system animator scale disabled: static fallback is legible and ends.
- [ ] Reopen a running activity: boot behaviour is consistent; no duplicate splash screens.
- [ ] Settings → Replay launch animation: visually animates again without crashing.
- [ ] Main WebView: address bar, keyboard Go, back/forward/reload, home, menu, tab count.
- [ ] Switch to GeckoView: **same fixed browser chrome** and toolbar placement; independent Gecko renderer works.
- [ ] GeckoView: enter URL, navigate, go back/forward, use browser menu, open bookmarks, return to WebView retaining previous tab.
- [ ] Android Back: expected behaviour in each engine.
- [ ] Rotate to landscape; expand text; inspect gesture/navigation insets and keyboard overlap.
- [ ] Test network errors, invalid URL, HTTP warnings, permission-blocked camera/mic/location.
- [ ] Import bookmarks from HTML and JSON; confirm counts and duplicates; verify no data loss.
- [ ] Import/cancel password CSV and cookie file; verify confirmation, encryption and delete controls. **Use synthetic test credentials/cookies, not real accounts.**
- [ ] Download HTTPS file in parallel and standard modes; check size and hash, cancellation and fallback.
- [ ] Install APK, close/relaunch, check retained settings and bookmarks.
- [ ] Upgrade from a previously *compatibly signed* APK; document debug signing failures and data retention.

## Automated gate

- `python3 -m unittest discover -s tests -v` from `platforms/android`: **source-level regression contract**, not interaction test.
- `gradle --no-daemon assembleDebug` with JDK 17, API 36, AGP 8.9.2, Gradle 8.11.1.
- GitHub Actions verifies nonempty APK and prints SHA256; publishes versioned debug APK and a stable latest-test alias.
- `shared/orion-connect` Go CI exists separately. It **does not** prove OAuth, encrypted sync or production readiness.
- **Required before release:** Android emulator or real-device instrumentation, signing-key continuity, network and data security review, extension/sync end-to-end testing, dependency inventory, reproducible artefact hash.

## Critical boundaries

WebView and GeckoView have separate site data, cookie stores, permission mechanisms and extension capabilities. Imported passwords are stored locally encrypted but are **not** connected to browser autofill. Do not copy exported password CSV or session-cookie files to CI, the repository, logs or bug reports.

**Roadmap state:** Phase 1 and Phase 3 have incremental source work; **none of the six phases are accepted as complete**. Stop claiming a fully finished browser until gates and relevant acceptance tests pass.

[Android builds](https://github.com/2E0LXY/orion-windows/actions/workflows/android-prototype.yml) · [Experimental releases](https://github.com/2E0LXY/orion-windows/releases) · [Development branch](https://github.com/2E0LXY/orion-windows/tree/chatgpt/drive-sync).
