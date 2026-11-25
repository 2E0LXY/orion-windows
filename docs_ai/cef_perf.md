# CEF Performance Notes

## Background

Chrome's official builds use PGO (phase 2) by default. The profile data (`chrome-win64-*.profdata`) is downloaded automatically during the build. These profiles are generated from Chrome's CI bots running automated benchmarks — **not** from real user telemetry.

CEF inherits this: when `is_official_build=true` on Windows x64, `chrome_pgo_phase` auto-sets to 2 in `build/config/compiler/pgo/pgo.gni`. The profdata is trained on `chrome.exe`, but applied to `libcef.dll` which has a different binary layout due to the extra CEF wrapper code (cpptoc/ctocpp, handler implementations, Mojo interfaces).

## How Chrome Generates PGO Profiles

Source: `tools/pgo/generate_profile.py`

CI bots run an instrumented build (`chrome_pgo_phase=1`) through these benchmarks:

1. **`speedometer3`** — JS/DOM benchmark (PUBLIC_BUCKET, freely available)
2. **`jetstream2`** — JS engine benchmark (PUBLIC_BUCKET, freely available)
3. **`system_health.common_desktop`** — real-world browsing scenarios (PARTNER_BUCKET, requires Google partner access)
4. **`rendering.desktop` (MotionMark stories)** — GPU/rendering benchmark (PARTNER_BUCKET, requires Google partner access)

Profiles are merged with `llvm-profdata merge` and uploaded to Google Cloud Storage. Official builds download them automatically.

The script supports `--run-public-benchmarks-only` which runs only Speedometer 3 and JetStream 2.

## GN Args

```
chrome_pgo_phase=0   # PGO disabled
chrome_pgo_phase=1   # Instrumentation (generates .profraw files at runtime)
chrome_pgo_phase=2   # Optimization (uses .profdata to guide compilation) — default for official builds
pgo_data_path="//path/to/profile.profdata"  # Custom profile (use with phase 2)
```

## Creating a CEF-Specific PGO Profile

### Phase 1: Build Instrumented Binary

```gn
# args.gn
is_official_build=true
chrome_pgo_phase=1
target_cpu="x64"
# ... other CEF args
```

Note: `generate_profile.py` recommends disabling symbols as the linker uses a LOT of memory with instrumented builds.

### Phase 2: Collect Profiles

**Option A: Use generate_profile.py (public benchmarks only)**

The script hardcodes `chrome.exe` (line 394) so it can't be used directly with CEF. But the workflow is replicable manually.

**Option B: Manual profiling (recommended for CEF)**

Set the environment variable before running cefclient:
```cmd
set LLVM_PROFILE_FILE=C:\path\to\profiles\default-%2m.profraw
bootstrap.exe --module=cefclient --no-sandbox
```

Then exercise diverse workloads:
- Run Speedometer 3.0 and JetStream 2 (covers JS/DOM paths)
- Browse popular sites: Google Search, Wikipedia, Reddit, YouTube, GitHub, Amazon, news sites
- Scroll, interact with pages, watch video (covers rendering/compositing paths)
- 10-15 minutes of varied browsing produces a useful profile

### Phase 3: Merge Profiles

```cmd
vendor\cefs\chromium\src\third_party\llvm-build\Release+Asserts\bin\llvm-profdata.exe merge -o cef.profdata *.profraw
```

### Phase 4: Build Optimized Binary

```gn
# args.gn
is_official_build=true
# Don't set chrome_pgo_phase — it defaults to 2 for official builds
pgo_data_path="//out/Release_GN_x64/cef.profdata"
target_cpu="x64"
# ... other CEF args
```

## Web Page Replay (WPR) Archives

Chromium uses WPR for deterministic page load recording/replay. Archives are stored in GCS:

- **PUBLIC_BUCKET (`chromium-telemetry`)** — Speedometer, JetStream, Octane, WebRTC. Freely downloadable.
- **PARTNER_BUCKET (`chrome-partner-telemetry`)** — system_health, rendering/MotionMark. Requires partner access (request via telemetry@chromium.org).
- **INTERNAL_BUCKET** — Google-only.

No third-party WPR archives exist publicly. You can record your own with:
```
tools/perf/record_wpr --browser=exact --browser-executable=out/Release_GN_x64/chrome.exe <pageset>
```

## Investigation: CEF Performance Gap

CEF cefclient scores ~17 on Speedometer 3.0 vs Chrome's ~20.5 (with `--disable-field-trial-config`).

### Ruled out:
- **Field trials** — Chrome with `--disable-field-trial-config` still gets 20.5
- **GPU/rendering config** — `chrome://gpu` pages are identical between CEF and Chrome
- **CEF message pump** — cefclient uses Chrome runtime style with Chrome's native message loop (not the 10ms external pump)
- **OSR overhead** — cefclient uses windowed rendering, not off-screen

### Ruled out (continued):
- **PGO profile mismatch (partial)** — No-PGO build scores ~14, confirming Chrome's PGO profile *helps* CEF (14 → 17). The profile is trained on `chrome.dll` but applied to `libcef.dll` with different code layout. A CEF-specific profile could improve further.
- **CEF wrapper overhead** — cpptoc/ctocpp wrappers are on the embedder↔engine boundary, not in the renderer hot path. Speedometer runs entirely in the renderer process where CEF's code is identical to Chrome's.
- **Disabled features** — CEF disables `kTcpSocketIoCompletionPortWin`, `kEnableHangWatcher`, `kGlicActorUi`, `kLensOverlay`. None affect JS execution.
- **Thread pool / scheduler** — CEF uses Chrome's default thread pool configuration. No differences found.
- **Memory allocator** — PartitionAlloc-Everywhere is used on Windows (same as Chrome). Only Linux disables it.
- **Blink TLS** — `blink_heap_inside_shared_library` defaults to `false` for non-component builds on Windows, so Blink heap TLS is already using `init-exec` (same as Chrome).
- **Binary size/structure** — `libcef.dll` (258 MB) is similar to `chrome.dll` (272 MB). Both are monolithic DLLs.

### Remaining hypotheses (18.5 vs 21, ~12% gap):
1. **PGO profile mismatch** — Chrome's profdata is trained on `chrome.dll`; `libcef.dll` has different code layout due to CEF wrapper code. Call graph profile sort (function reordering) is based on Chrome's hot paths, which may not align with CEF's. A CEF-specific PGO profile (phase 1 → collect → phase 2) is the most actionable next step.
2. **Process architecture subtleties** — CEF's renderer spawning, IPC routing, or content layer integration may have subtle differences that add latency per frame or per task.
3. **Unknown compile-time differences** — There may be other `enable_cef`-guarded code paths in Chromium that affect behavior (reviewed main ones; no obvious candidates).

### Confirmed: V8 TLS access mode (9% improvement)

CEF patches V8 to set `v8_used_in_shared_library=true` (`cef/patch/patches/v8_build.patch`), which defines `V8_TLS_USED_IN_LIBRARY`. This forces V8's hot TLS getters (`Isolate::TryGetCurrent`, `LocalHeap::TryGetCurrent`) to use the `local-dynamic` TLS model with `V8_NOINLINE` — a non-inlined function call on every access.

Chrome (which links V8 statically into `chrome.dll`) uses the `init-exec` TLS model with `V8_INLINE` — direct inline access, no function call overhead. Since these getters are called on virtually every JS operation, the difference is significant.

**Fix:** Set `v8_used_in_shared_library=false` in `args.gn`. This is safe because `libcef.dll` is the main executable module (loaded at process start, not dynamically via `dlopen`), so `init-exec` TLS works correctly.

**Result:** 17 → 18.5 (~9% improvement).

### Test results

| Build | Speedometer 3.0 | Notes |
|-------|-----------------|-------|
| Chrome (with `--disable-field-trial-config`) | ~20.5 | Baseline |
| CEF cefclient (Google PGO + V8 TLS fix) | ~18.5 | `v8_used_in_shared_library=false` |
| CEF cefclient (Google PGO, default) | ~17 | Default official build |
| CEF cefclient (no PGO, `chrome_pgo_phase=0`) | ~14 | PGO disabled |

**Summary of gains:**
- PGO (Google profiles): 14 → 17 (~21% boost)
- V8 TLS fix: 17 → 18.5 (~9% boost)
- **Remaining gap: 18.5 vs 21 (~12%)** — cause TBD

Note: `chrome_pgo_phase=0` also disables **call graph profile sort** (function reordering for icache locality), so the 14 → 17 improvement includes both PGO branch hints and linker-level function layout optimization.

## TODO: Report upstream

Report the V8 TLS performance finding to CEF upstream. The existing issue #3803 focused on Linux ARM64 `dlopen` compatibility — nobody has documented the **Windows performance impact** of `v8_used_in_shared_library`.

Key points for the report:
- On Windows, `v8_used_in_shared_library=true` (CEF default) forces `local-dynamic` TLS + `V8_NOINLINE` for `Isolate::TryGetCurrent` and `LocalHeap::TryGetCurrent`
- Setting `v8_used_in_shared_library=false` switches to `init-exec` TLS + `V8_INLINE`, yielding ~9% Speedometer 3.0 improvement (17 → 18.5)
- Safe for all normal CEF use cases where `libcef.dll` is loaded at process start (not via `LoadLibrary` at runtime)
- CEF could default to `false` on Windows (and possibly Linux when `dlopen` isn't needed), or at minimum document the performance trade-off
- File at: https://github.com/chromiumembedded/cef/issues
