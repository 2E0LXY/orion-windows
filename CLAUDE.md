# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Search Warning

**Never run whole-project file or content searches** (e.g. searching from the repo root). Always scope searches to specific subdirectories like `Orion/`, `OrionBackend/`, `scripts/`, etc. Directories that will stall or pollute searches:

- `external/` — massive WebKit source tree; any recursive search takes minutes
- `vendor/` — full CEF distribution, plus Chromium source + depot_tools under `vendor/cefs/`
- `publish/` and `*/publish/` — old release builds; `Orion/publish/` contains recursively nested copies of previous releases many levels deep and will hang a glob
- `vcpkg_installed/`, `x64/`, `bin/`, `obj/` — build output

## Cross-Repo Research

When researching code in other repositories, **prefer searching local clones** over using `gh search` or the GitHub API — local searches are much faster. If a local clone doesn't exist, suggest the user clone it first.

## Project Overview

Orion Windows is a **dual-engine** browser for Windows, supporting both **WebKit** and **CEF (Chromium/Blink)**. It combines a WinUI 3 (C#) frontend with a native C++ backend and a cross-platform core module from orion-linux.

## Documentation

Two documentation directories with different ownership rules:

- **`docs/`** — manually edited documentation. Do not modify unless explicitly instructed.
- **`docs_ai/`** — AI-generated/managed documentation. Claude Code creates and updates these files as part of normal work; keep them current when you change the code they describe.

Key documents:

| Document | Covers |
|---|---|
| `docs_ai/orion_architecture.md` | Orion/ MVVM layer rules — what goes in Models vs ViewModels vs code-behind, decision tests, naming |
| `docs_ai/web_extensions_porting.md` | WebExtensions Windows port: platform split, done/TODO checklist, code map. Read this before touching extension code |
| `docs/web_extensions.md` | WebExtensions core-module internals (IDL codegen, JS↔native bridge, how to add APIs) |
| `docs_ai/cef_perf.md` | CEF performance investigation notes (PGO, Speedometer) |
| `docs_ai/webkit_c_api_refcounting.md` | WebKit C API ref-counting rules: adoptWK vs retain, Create/Copy rule, callback ref lifetimes. Read before touching `WK*Ref` handling |
| `docs_ai/msbuild_custom_build_steps.md` | IDE up-to-date checks: vcxproj CustomBuild item pattern, csproj None-vs-Content / PRI wedge / copy-item visibility. Read before adding/changing build steps or runtime-file items |
| `docs_ai/profile_storage.md` | Per-profile databases (`Profiles\<id>\profile.db`), the common registry DB (`orion.db`), the process-per-profile model, and the legacy split migration. Read before touching profile or settings storage |
| `README.md` | Human getting-started guide (tool install, submodules, WebKit cache) |

File listings and status tables in this file and in `docs_ai/` are best-effort snapshots — when code contradicts docs, trust the code and update the doc.

## Build Commands

### Prerequisites
Run `install_dev_tools.bat` — installs Visual Studio 2026 Community with the required workloads (from `.vsconfig`) plus gcloud for the binary build cache. Manual equivalent:
- Visual Studio 2026
- Workloads: "Desktop development with C++", "WinUI application development"
- Individual Components: "C++ Clang Compiler for Windows", "MSBuild support for LLVM (clang-cl) toolset"

### Initial Setup
```batch
git submodule init
git submodule update
setup_orion_gtk_submodule.bat   # Initialize orion-linux nested submodules
```

### Build Steps
```batch
update_core_module.bat          # Configure orion-linux core module (CMake)
```

Then open `Orion.slnx` in Visual Studio and build. WebKit is built and cached automatically via vcpkg (see [WebKit Build](#webkit-build-vcpkg) below).

### Command-Line Builds (ci/)
The `ci/` scripts are the canonical non-interactive build/test commands (they set up the VS environment themselves):
```batch
ci\build_debug.cmd       # or build_release.cmd — builds everything (facade, both engine plugins, Orion)
ci\unittest.cmd          # dotnet test UnitTest (Release, x64)
```

### Other Build Scripts
- `vsenv.bat` / `vscmd.bat` - Visual Studio environment helpers
- `update_wk_solution.py.cmd` - Generate WebKit VS browsing projects from the WebKit source tree (prefers `../webkit/` if present, falls back to vcpkg buildtrees)
- `noenv_update_core_module.bat` - Update core module without env setup
- `fix_project_uuids.py.cmd` - Keep CMake-generated project GUIDs equal to `Orion.slnx` (seeds them via `cmake -C`, plus a fallback rewrite; runs automatically via update_core_module.bat)

### WebKit Build (vcpkg)

WebKit is built as a vcpkg overlay port (`ports/webkit-win/`). Binaries are cached in GCS (`gs://kagi-orion-windows-artifacts/`) so most developers never need to build WebKit locally.

**Prerequisites:**
- `gcloud auth login` — authenticate with a Google account that has access to the GCS bucket
- gcloud CLI installed (gsutil wrapper in `scripts/gsutil.cmd` translates to `gcloud storage` commands)

**How it works:**
- `Directory.Build.props` sets `VCPKG_BINARY_SOURCES` env var to read and write GCS, pins `VCToolsVersion` from `MSVC_VERSION`, and prepends VS-bundled CMake/Ninja + `scripts/` to PATH before vcpkg runs
- `MSVC_VERSION` pins the MSVC toolset (e.g. `14.50.35717`). `triplets/x64-windows.cmake` forces vcpkg onto it and hashes the file into every ABI in place of vcpkg's compiler tracking, because a VS update might rewrite cl.exe inside the pinned directory without renaming it (the toolset's "servicing version"); the whole cache is invalidated only by bumping the pin
- `vcpkg.cmd` (repo root) wraps `external/vcpkg/vcpkg.exe` with the same VS tool PATH setup for command-line use
- vcpkg talks to GCS through whatever `gsutil` is first on PATH. `vsenv.bat` (and `_SetupVcpkgEnv`) put `scripts/` first so it is `scripts/gsutil.cmd` (which uses `gcloud storage` and your `gcloud auth login`). If a build prints `ServiceException: 401 Anonymous caller does not have storage.objects.list access`, that is the Cloud SDK's own `gsutil` being found instead — you are logged in, PATH is wrong — and vcpkg treats it as a cache miss and builds WebKit from source
- Building OrionBackend in Visual Studio triggers vcpkg, which checks the binary cache first
- On cache hit: downloads pre-built WebKit binaries (~seconds)
- On cache miss: builds WebKit from source via the portfile (~3.5 hours)

**To build and upload new WebKit binaries to the cache:**
```bash
./vcpkg.cmd install
```
(or just build in VS)

**Iterating on local WebKit source:**
Use the `LocalDebug` build configuration. Clone WebKit to `../webkit/`, select LocalDebug in VS, and press F5. OrionEngineWK's PreBuildEvent runs `scripts/wk_local_build.py.cmd` which auto-configures cmake on first run and does incremental Ninja builds. Local WebKit headers/libs take priority over vcpkg-installed ones.

**Key files:**
- `ports/webkit-win/portfile.cmake` — Build recipe (CMake configure, build, manual install)
- `ports/webkit-win/WEBKIT_REF` — WebKit commit SHA; change it to update the WebKit version. Read by both the portfile and `scripts/fetch_webkit_src.py.cmd`, which downloads the matching source tarball before vcpkg runs (the portfile cannot: vcpkg builds its own PATH for portfiles and gcloud is not on it)
- `ports/webkit-win/webkit-options.cmake` — Shared WebKit build options (cmake initial-cache file); used by both the portfile and `scripts/wk_local_build.py.cmd`
- `ports/webkit-win/vcpkg.json` — Port manifest with dependency features (skia, woff2, jpeg-xl, lcms)
- `ports/zlib-ng/portfile.cmake` — Overlay port that enables zlib compatibility mode (`ZLIB_COMPAT ON`)
- `vcpkg.json` — Root manifest declaring webkit-win dependency
- `vcpkg-configuration.json` — Registry config with overlay-ports path
- `MSVC_VERSION` — Pinned MSVC toolset version, read by `Directory.Build.props`, `vsenv.bat`, and `triplets/x64-windows.cmake`
- `triplets/x64-windows.cmake` — Same-name overlay of vcpkg's x64-windows triplet: `VCPKG_PLATFORM_TOOLSET_VERSION` from `MSVC_VERSION`, compiler tracking disabled, `MSVC_VERSION` hashed into every ABI
- `vcpkg.cmd` — Command-line wrapper that runs vcpkg.exe with VS-bundled tools in PATH; defaults `VCPKG_BINARY_SOURCES` to read/write GCS if not set
- `scripts/gsutil.cmd` — Wrapper translating gsutil commands to `gcloud storage` (vcpkg expects gsutil but we use gcloud)
- `Directory.Build.props` — Sets `VCPKG_BINARY_SOURCES` (read/write GCS), prepends VS CMake/Ninja + `scripts/` to PATH before vcpkg runs

### CEF Acquisition

CEF is provisioned by the `OrionCefSetup` project, a Makefile-type project that runs `cef.py.cmd setup`:
- `vendor/CEF_VERSION` pins the CEF version and mode — `official:` prefix fetches an official build (Spotify CDN), source builds come from GCS, or a local build can be used
- Output lands in `vendor/cef/` (distribution) and `vendor/cef/build/` (libcef_dll_wrapper)
- With runtime engine selection, every configuration builds both engine plugins, so `OrionCefSetup` runs in all solution configurations (self-gating: a no-op when `vendor/cef` matches the pin)

### Python Scripts
Python scripts use `uv` for dependency management. Run with:
```batch
uv run python scripts/script_name.py
```
The `pyproject.toml` defines the project; `uv sync` creates the `.venv`.

**Polyglot Batch/Python scripts (`*.py.cmd`):** several build scripts (`cef.py.cmd`, `fix_project_uuids.py.cmd`, `update_wk_solution.py.cmd`, `scripts/wk_local_build.py.cmd`, ...) are single files that cmd/MSBuild execute as batch but whose body is Python — a 3-line batch header re-invokes the file via the repo's `bin\uv.exe`. **All polyglot scripts must be named `*.py.cmd`.** The header template and a full explanation of how the trick works live in `scripts/python_preamble.py.cmd` — read it before creating or editing one.

**Never write script logic in batch** — batch is only acceptable as the polyglot launcher header. Among cmd's many hazards: every `set` variable is inherited by child processes, and a name colliding with an MSBuild property silently overrides it (publish.bat's `OUTDIR` redirected every vcxproj's `OutDir`).

### Custom Build Steps and Up-to-Date Checks

VS's IDE-level up-to-date check for .vcxproj reads only `.tlog` files;
`Inputs`/`Outputs` attributes on custom targets (and `PreBuildEvent`s) are
invisible to it, so the project gets skipped forever after one successful
build and the step silently stops running. Declare custom steps as
**`CustomBuild` items** (tlog-visible; e.g. the CMake-generated core module projects), or
keep always-run NMake projects with a self-gating command (`OrionCefSetup`).

In Orion.csproj, path-loaded runtime files (native DLLs, extension packages)
must be **`None` items** with explicit copy/publish metadata: `Content`
makes them inputs of the WinUI PRI up-to-date check (permanent rebuild
wedge), and ProjectReference `OutputItemType` injection is invisible to the
check entirely (stale copies while VS reports up to date).

Read **`docs_ai/msbuild_custom_build_steps.md`** before adding or changing
any custom build step or runtime-file item.

## Architecture

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
│ engines\webkit\  │ │ engines\blink\   │
│ WebKit 2 (vcpkg) │ │ CEF (Blink)      │
└──────────────────┘ └──────────────────┘
```

## Project Structure

### Top-Level Directory Map

| Directory | Purpose |
|---|---|
| `Orion/` | C# WinUI 3 application (main browser UI) |
| `OrionBackend/` | Three C++ projects in one directory: `OrionBackend.dll` (engine-free facade: exports + core module + engine loader) and the engine plugins `OrionEngineWK.dll` / `OrionEngineB.dll`, loaded at runtime from `engines\webkit\` / `engines\blink\` |
| `OrionPackage/` | MSIX packaging project |
| `OrionCodeGen/` | C++ code generator exe; reflects on core module DTOs and emits C++/C# wire structs |
| `OrionCoreGen/` | Makefile-type project orchestrating `vcpkg install` + core module regeneration (don't confuse with OrionCodeGen) |
| `OrionCefSetup/` | Makefile-type project that provisions CEF via `cef.py.cmd setup` |
| `UnitTest/` | C# unit tests (xunit v3) |
| `ci/` | Canonical command-line build/test/format scripts |
| `scripts/` | Build helper scripts (WebKit local build, gcloud auth, vsenv) |
| `ports/` | vcpkg overlay ports: `webkit-win/` (WebKit build), `zlib-ng/` (zlib compat mode) |
| `external/` | Submodules: `vcpkg/`, `orion-gtk/` (cross-platform core module). **Do not search recursively** |
| `vendor/` | Fetched CEF binaries/source, version pins (`CEF_VERSION`), PGO data. **Do not search recursively** |
| `docs/` | Manually edited documentation |
| `docs_ai/` | AI-generated/managed documentation |
| `icons/` | App icons |
| `publish/`, `crash_dumps/`, `x64/`, `bin/` | Build/runtime output — ignore |

### Orion/ MVVM Architecture

The frontend follows MVVM using [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/). **See `docs_ai/orion_architecture.md`** for the layer responsibilities (Models/Adapters/Services/ViewModels/Views/Controls/Converters/Interop/Utilities), decision tests for what goes where, and examples — consult it before adding new classes.

All classes use a flat `namespace Orion` (except `DirectShow` which keeps its own namespace). Folders organize files on disk but do not affect namespaces.

Folders not covered by the architecture doc:
- **InternalPages/** - Static `error_*.html` pages served for navigation errors (DNS, SSL, timeout, etc.)
- **Interop/Generated/** - `NativeTypes.cs`, auto-generated by OrionCodeGen — do not edit

### OrionBackend/ Structure
```
OrionBackend/                      # three projects share this directory
├── OrionBackend.vcxproj           # facade DLL (engine-free): exports.cpp, engine_loader.cpp/.h,
│                                  #   utils.cpp/.h, dpapi_password_storage.cpp/.h, dllmain.cpp
├── OrionEngineWK.vcxproj          # WebKit plugin: webkit.cpp, webkit_engine_plugin.cpp,
│                                  #   extension_scheme_webkit.cpp/.h, script_bridge{.h,_webkit.cpp},
│                                  #   webkit/ (WebKitBrowserWindow, BrowserWindow.h, Common*, WKPtr, DialogHelper)
├── OrionEngineB.vcxproj           # CEF plugin: cef.cpp, cef_engine_plugin.cpp
├── host_shim.cpp/.h               # compiled into BOTH plugins: defines orion::log_*, bridge*,
│                                  #   getProfileDataPath, ... as forwarders into HostServices
├── browser_engine.h               # BrowserEngine interface + OrionCreateEngine plugin contract
├── browser_engine_types.h         # BrowserCallbacks, EngineId, popup callback types
├── engine_impl.h                  # engine free-function declarations (webkit.cpp / cef.cpp implement)
├── host_api.h                     # facade services engine code consumes (popup getters, extension runtimes)
├── host_services.h                # versioned function table handed to OrionCreateEngine
├── context_menu_types.h           # context-menu wire ABI (reflected into C# by OrionCodeGen)
├── download_bridge.h              # download bridge declarations (state lives in exports.cpp)
├── pch.cpp/.h                     # precompiled header (each project compiles its own copy)
└── generated/                     # auto-generated (gitignored) — do not edit
    ├── native_types.h             # wire struct definitions (from OrionCodeGen)
    └── orion_version.h            # ORION_VERSION (from VERSION via gen_version_header.py.cmd)
```

## Configuration System

The solution uses configurations named by build type only — the browser engine is a **runtime** choice (see [Dual Engine](#dual-engine-webkit--cef)). Both C# and C++ projects share the configuration names; OrionEngineWK additionally has `LocalDebug`.

**Configuration Mapping:**
| Configuration | NativeConfiguration | Packaging | Notes |
|---|---|---|---|
| `Debug` | Debug | Unpackaged | Debug WebKit + CEF binaries |
| `LocalDebug` | Debug (OrionEngineWK: LocalDebug) | Unpackaged | WebKit built from `../webkit/` via Ninja |
| `Release` | Release | Unpackaged | |
| `Release-Packaged` | Release | MSIX | |

`$(NativeConfiguration)` defaults to `$(Configuration)`; `Release-Packaged` maps to `Release` and `LocalDebug` to `Debug`. A separate `$(WkEngineConfiguration)` keeps `LocalDebug` for the WebKit plugin only. Both are used for the native ProjectReferences (`SetConfiguration`) and the `..\x64\<config>\` DLL copy items in Orion.csproj.

## Dual Engine (WebKit / CEF)

Orion supports two browser engines, selected at **runtime** per process. Every build produces one `Orion.exe`, the engine-free `OrionBackend.dll` facade, and both engine plugins:

- **`engines\webkit\OrionEngineWK.dll`** — WebKit 2, plus the entire WebKit runtime (WebKit2.dll, helper exes, ANGLE, icu, `WebKit.resources\`)
- **`engines\blink\OrionEngineB.dll`** — CEF/Blink, plus the CEF runtime (libcef.dll, paks, icudtl.dat, `locales\`, CEF's ANGLE)

The subdirectories are load-bearing: both engines ship `libEGL.dll`/`libGLESv2.dll`, which would collide in a flat layout. The facade loads a plugin with `LoadLibraryExW(..., LOAD_WITH_ALTERED_SEARCH_PATH)` so the plugin's static imports resolve from its own directory; WebKit's helper exes are likewise found next to `WebKit2.dll`.

**Selection:** `App.OnLaunched` takes `--engine=webkit|blink` from the command line, else the default profile's `settings::general::browser_engine` setting (default `webkit`), and calls the `LoadBrowserEngine` export before the first window. **Exactly one engine per process** — the loader refuses a second, different engine; an engine change takes effect on relaunch.

**The seam:** engine code implements the free functions declared in `engine_impl.h`; a small plugin entry TU (`webkit_engine_plugin.cpp` / `cef_engine_plugin.cpp`) adapts them to the `BrowserEngine` interface (`browser_engine.h`) and exports `OrionCreateEngine(const HostServices*)`. The facade fills a `HostServices` function table (`host_services.h`), and `host_shim.cpp` — compiled into both plugins — defines the facade-provided symbols (`orion::log_*`, the `bridge*` download functions, `getProfileDataPath`, `orion::data_directory_path`, `orion::get_extension_runtimes`, the popup callback getters) as forwarders through that table. Plugins do **not** link `orion_core.lib`, so any new use of a core symbol from engine code fails the plugin link — that is the seam-enforcement mechanism. C++ types cross the boundary; all three DLLs build in one solution with the pinned toolset and dynamic CRT.

**Engine-specific details:**

| | WebKit | CEF (Blink) |
|---|---|---|
| Plugin DLL | `OrionEngineWK.dll` | `OrionEngineB.dll` |
| Source files | `webkit.cpp` + `webkit/` + extension scheme/bridge glue | `cef.cpp` |
| Message pump | Driven by `TickOrionCore()` only | External pump via `CefDoMessageLoopWork()` |
| Subprocess handling | Helper exes spawned from `engines\webkit\` (found relative to WebKit2.dll) | `CheckBrowserSubprocess()` self-gates on CEF's `--type=` switch, loads the blink plugin, and runs `CefExecuteProcess` — CEF re-launches the exe for renderer/GPU/utility processes |
| Profile data | `getProfileDataPath(id, EngineId::WebKit)` | `getProfileDataPath(id, EngineId::Blink)` |
| Cache directory | N/A | `%LOCALAPPDATA%\com.kagi.orion\data\blink\<uuid>\` — per-profile CEF user data dir (site data in `Default\`) |

**CEF message pump integration:**
CEF uses `external_message_pump` mode — no dedicated CEF message loop thread. Instead:
1. `OnScheduleMessagePumpWork()` signals a Win32 auto-reset event (`g_cef_wakeup_event`)
2. A C# watcher thread waits on both the core wakeup event and CEF wakeup event
3. When either fires, it enqueues `PumpCefMessageLoop()` + `TickOrionCore()` on the UI thread via `DispatcherQueue`

**Callbacks (`BrowserCallbacks` struct in `browser_engine.h`):**
Both engines route page events through the same `BrowserCallbacks` struct (e.g. `titleChanged`, `activeURLChanged`, `navigationError`). C# passes these when creating a browser view; the engine calls them on the UI thread — possibly already inside `CreateOrionWindow` (CEF loads `about:blank` synchronously in `CreateBrowserSync`), before `WireWebViewEvents` has subscribed. State-style callbacks therefore need a mirror property on `NativeWebView` that the wiring reads once after subscribing (see `IsLoading`).

## Process Model (Windows vs Linux)

**Windows: one browser process per profile.** The `Orion.exe` that holds a profile's single-instance mutex (`SingleInstance.TryAcquire` in `Program.Main`) is that profile's only browser process; it opens the profile's own `Profiles\<id>\profile.db` (the profile registry lives in the common `orion.db`; see `docs_ai/profile_storage.md`) and serves `open`, `quit`, profile-change and notification-activation requests from other processes over a named pipe (`Services/SingleInstance.cs`). Other `Orion.exe` processes exist but never initialize the core: a launch for an already-running profile (e.g. the OS opening a URL) forwards `open` and returns from `Main`, and under Blink CEF re-launches `Orion.exe` with `--type=` for its renderer/GPU/utility children, which exit from `CheckBrowserSubprocess()`. Consequently the `CoreApi` in a browser process serves exactly one profile: one `ProfileHost`, one activated extension world, a per-plugin extension scheme table (`docs_ai/web_extensions_porting.md` 3.2).

**Linux: everything in one process.** One `CoreApi`, one shared database, every profile attached to the same core with both of its worlds (normal + private).

Rules that follow, in both repos:

- The core module (orion-linux) is written for the Linux shape and must stay profile-agnostic. Never add "one profile per process" / "this `CoreApi` has one profile" assumptions to core code, and never assume a profile id passed to the core is "the" profile.
- The Windows glue may rely on one profile per browser process, but must not assume the reverse either: `profile_id` parameters on the exports are consistency checks (they must match the process's profile), not selectors, and `activateProfileWorld` refuses a second id. Cross-profile work (quit, toast activation landing in the wrong process, registry changes) goes over the single-instance pipe or the common DB, never through per-process state.
- When porting GTK code, check whether it iterates or looks up *other* profiles' state in-process; on Windows that state lives in another process.

## Application Startup

**Unpackaged Mode (`Program.cs`):**
1. `Main()` calls `OrionBackend.Initialize()` to set DLL search path
2. `CheckBrowserSubprocess()` — self-gated on CEF's `--type=` switch: the main process returns -1 without touching any engine plugin; a CEF child loads the blink plugin and exits with `CefExecuteProcess`'s code
3. `Application.Start()` creates WinUI app (blocks until exit — COM ASTA loop, cannot be customized)
4. `App.OnLaunched()` calls `InitOrionCore()`, selects the engine (`--engine=` arg, else the `browser_engine` setting) and calls `LoadBrowserEngine()`, starts watcher thread for core + CEF pumps, creates `MainWindow`
5. `MainWindow` initializes browser view via `CreateOrionWindow()`

**Packaged Mode:**
- WinUI handles startup directly
- `App.OnLaunched()` still calls `OrionBackend.Initialize()`

**Run loop initialization (in `App.OnLaunched`):**
A background watcher thread waits on both the core wakeup event and the CEF wakeup event (if present). When either fires, it enqueues both `PumpCefMessageLoop()` and `TickOrionCore()` on the UI thread via `DispatcherQueue.TryEnqueue`. WebKit builds return a null CEF event handle, so only the core event is waited on.

**Shutdown:** See `MainWindow.OnWindowClosed()` — runs on last window close. Cleanup → stop watcher → `ShutdownOrionCore()` → drain run loop.

## Code Style

### Formatting
Code formatting and style rules (line endings, var preferences, naming conventions, etc.) are configured via `.editorconfig`. Run `format.sh` to auto-format.

### Banned APIs
`Orion/BannedSymbols.txt` + `.globalconfig` enforce banned-API analyzer rules. Currently: `Debug.WriteLine` / `Console.WriteLine` are banned — use `OrionBackend.Log*()` instead (routes through the native logging pipeline).

### MVVM Toolkit
- Uses [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) v8.4 for MVVM patterns
- Models and ViewModels inherit from `ObservableObject`
- **Prefer `[ObservableProperty]`** over manual `SetProperty`/`OnPropertyChanged` boilerplate
- **Use partial property syntax** (not field-backed), per [MVVMTK0045](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/errors/mvvmtk0045):

```csharp
// Correct — partial property
[ObservableProperty]
public partial string Name { get; set; } = "default";

// Wrong — field-backed (triggers MVVMTK0045 warning)
[ObservableProperty]
private string _name = "default";
```

- Use `[NotifyPropertyChangedFor(nameof(DerivedProp))]` to notify dependent computed properties
- Use `[NotifyCanExecuteChangedFor(nameof(MyCommand))]` to invalidate a command's `CanExecute` when a property changes
- Use `partial void OnXxxChanged(T oldValue, T newValue)` for side effects when a property changes
- Commands use `[RelayCommand]` attribute for source generation
- **No Dependency Injection** currently - services and state are passed directly or accessed as singletons
- Fall back to manual properties only when `[ObservableProperty]` can't be used (e.g., private setters, complex multi-step logic in the setter)

### Commands vs Click Handlers

**Prefer `Command` binding over `Click` event handlers** when the action represents Model/ViewModel logic — see `docs_ai/orion_architecture.md` ("Commands vs. Click Handlers") for the full guidance, including when Click handlers are still appropriate.

### XAML Binding
Prefer `{x:Bind}` (compiled, defaults to `OneTime`; see `docs_ai/orion_architecture.md`). WinUI-specific gotchas:
- **Use `Mode=OneWay`** for Command bindings when the command source can change (e.g., `ViewModel.ActiveTab.GoBackCommand` — the `ActiveTab` changes as you switch tabs)
- **WinUI 3 limitation:** `Window` is not a `FrameworkElement`, so `x:Bind` with `OneWay`/`TwoWay` silently fails in Window XAML. Workaround: put all content in a `UserControl` hosted by the Window (e.g., `MainWindow.xaml` hosts `MainWindowContent` UserControl where all real bindings live)

## Key Patterns

### Wire Struct Code Generation (OrionCodeGen)

Wire structs for P/Invoke are **generated automatically** from the orion-linux core module's DTO definitions. Do not write or edit `OrionBackend/generated/native_types.h` or `Orion/Interop/Generated/NativeTypes.cs` by hand.

**How it works:**
1. `OrionCodeGen` (C++ exe, `OrionCodeGen/gen_native_types.cpp`) uses [reflectcpp](https://github.com/getml/reflect-cpp) to iterate over the fields of each DTO struct at compile time via `rfl::named_tuple_t<Dto>`.
2. Each field's C++ type is mapped to a C wire type (e.g. `int64_t`) and a C# type (e.g. `Int64`) via the `ToWireType<T>` template.
3. `orion::StrongTypedef<T, Tag>` fields generate a C# `readonly record struct` wrapper (e.g. `public readonly record struct ProfileId(Int64 Value)`). The wrapper name is extracted from MSVC's `__FUNCSIG__` because the tag types are intentionally incomplete (forward-declared only, so `typeid` cannot be used).
4. The generator outputs two files:
   - `OrionBackend/generated/native_types.h` — C++ wire struct with blittable fields
   - `Orion/Interop/Generated/NativeTypes.cs` — C# wire struct (`[StructLayout(LayoutKind.Sequential)]`) + record structs for strong typedefs

**Build chain:**
- `OrionCodeGen` is a `ProjectReference` of `OrionBackend`, so MSBuild builds it first.
- `OrionCodeGen`'s own `PreBuildEvent` deletes its output exe before each compile, ensuring a failed build leaves no stale binary.
- `OrionBackend`'s `PreBuildEvent` runs `OrionCodeGen.exe $(SolutionDir)`, which writes the generated files before any C++ compilation begins.
- `Orion.csproj` picks up `Interop/Generated/NativeTypes.cs` automatically (SDK-style wildcard).

**To add a new DTO:**
1. Include the DTO header in `OrionCodeGen/gen_native_types.cpp`.
2. Call `get_fields<orion::NewDto>(fields, record_structs)`.
3. Add `write_cpp_header` / `write_cs_file` calls for the new struct.
4. Add a `ToWireType<T>` specialization for any new field type not already handled.

### C#/C++ Interop
- OrionBackend exports are in `OrionBackend/exports.cpp` (~2200 lines, ~100 exports)
- C# P/Invoke declarations in `Orion/Interop/OrionBackend.cs`; the DLL name is always `OrionBackend.dll` (the engine-free facade — engine plugins are loaded by the facade, never P/Invoked directly)
- DLL search path initialized in `App.xaml.cs` before any P/Invoke calls
- Strings and arrays returned from C++ use `CoTaskMemAlloc` via `ReturnString()`/`ReturnWString()` helpers

The export surface is grouped by domain — core lifecycle, browser view control, history, bookmarks, profiles, settings, reading list, web extensions, windows/tab groups, tabs, recently closed + undo/redo, favicons, passwords, snapshots, sync. Most domains follow a `GetX`/`FreeX`/`CreateX`/`UpdateX`/`DeleteX` pattern.

**Settings:** `GetSetting`/`SetSetting` are read-your-writes on the UI thread (set is fire-and-forget, but set and get FIFO through the same main→disk job queues). To *react* to setting changes, use `AddSettingObserver(key, cb)` / `RemoveSettingObserver(id)` — they expose the core module's settings observable (the same one orion-linux uses). Observer callbacks arrive on the UI thread one run-loop tick after the write commits; C# must keep the delegate rooted while registered (example consumer: `Orion/Views/MainWindowContent.Fullscreen.cs`). Do not add C#-side "setting changed" broadcast events around write sites.

**Key lifecycle exports (see `exports.cpp` for the full list):**
| Export | Purpose |
|--------|---------|
| `GetOrionVersion()` | Returns version string (e.g., "v0.0.1") |
| `InitOrionCore()` | Initialize orion-linux core module, returns status |
| `LoadBrowserEngine(name)` | Load `engines\<name>\` plugin ("webkit"/"blink") and bind its script bridge; "OK" or error |
| `GetBrowserEngine()` | Loaded engine's `EngineId` (0=WebKit, 1=Blink), -1 before load |
| `ShutdownOrionCore()` | Clean shutdown (intentionally leaks to avoid destruction order issues) |
| `CreateOrionWindow(HWND, BrowserCallbacks, int64_t profileId)` | Create browser HWND |
| `DestroyOrionWindow(HWND)` | Destroy browser HWND |
| `ResizeOrionWindow(HWND, width, height)` | Resize browser window |
| `LoadURL(HWND, char* url)` | Navigate to URL (returns HRESULT) |
| `Reload(HWND)` / `Forward(HWND)` / `Backward(HWND)` | Navigation |
| `TickOrionCore()` | Pump run loop; returns 0 when stopped |
| `GetWakeupEventHandle()` / `GetCefWakeupEventHandle()` | Event handles for watcher thread |

**P/Invoke Type Notes:**
- All HWND parameters use `Win32.HWND` (type-safe wrapper) instead of raw `IntPtr`

**String Marshaling Pattern:**

Use `LPWStr` (`wchar_t*`) or `LPUTF8Str` (`char*` UTF-8) depending on what is more convenient on the C++ side. **Never use `LPStr`** — its encoding depends on the system ANSI codepage and is unreliable cross-platform.

**Memory ownership rules — only two cases require `CoTaskMemAlloc`:**

1. **C++ returns a string or array to C#** (P/Invoke return value, or `ref`/`out` struct fields written by C++): the CLR will call `CoTaskMemFree` on the pointer after copying. Use `ReturnString()` / `ReturnWString()` from `utils.h`:
   ```cpp
   // Return value
   return ReturnString(some_std_string);
   return ReturnWString(some_wstring);

   // Writing into a ref-struct field
   out_struct->str = ReturnString(value);  // CLR will CoTaskMemFree this
   ```
   On the C# side, `[return: MarshalAs(UnmanagedType.LPUTF8Str)]` / `[return: MarshalAs(UnmanagedType.LPWStr)]` — the CLR copies to a managed string and frees automatically.

2. **C# delegate/callback returns a string to C++**: the CLR allocates the buffer with `CoTaskMemAlloc`. C++ receives the pointer and **must call `CoTaskMemFree` on it** after use.

**All other cases require no allocation:**
- **C++ passes a string *into* a C# callback** (callback `[In]` parameter): pass `c_str()` or any raw pointer directly. The CLR copies to a managed string and does **not** free the pointer — C++ retains ownership.
- **C# passes a string *into* a C++ export** (P/Invoke `[In]` parameter): C++ receives the raw pointer for the duration of the call. Do not free it.

**Arrays (`LPArray`)** follow the same rule as strings: only allocate with `CoTaskMemAlloc` when the array is a return value or written through a `ref`/out pointer. The CLR calls `CoTaskMemFree` on the returned pointer. For the history entries pattern, the entire struct array + string data is packed into one `malloc` and freed via an explicit `FreeHistoryEntries()` export — since C# holds the pointer as `IntPtr` and frees it explicitly, `CoTaskMemAlloc` is not required here.

### Embedded HWND Pattern
The browser view (WebKit or CEF) is hosted as a native HWND child window inside WinUI using `HwndHostPanel`:
- `HwndHostPanel` (UserControl) manages multiple native HWNDs with only one visible at a time
- Uses `WS_EX_LAYERED` for WinUI composition compatibility
- Handles DPI-aware positioning via `XamlRoot.RasterizationScale`
- Auto-updates position on `SizeChanged` and `LayoutUpdated`

**HwndHostPanel API:** `Add(hwnd)`, `Show(hwnd)`, `HideAll()`, `Remove(hwnd)`. One HWND visible at a time.

**Important:** Standard `ContentDialog` gets hidden behind embedded HWNDs. Use `Controls/PopupDialog` instead (uses `Popup` with `ShouldConstrainToRootBounds="False"` to render above native windows). API: `PopupDialog.ShowMessageAsync()` for simple messages, or `new PopupDialog { ... }.ShowAsync()` for custom content.

### Win32 API (Interop/Win32.cs)
Common Win32 functions wrapped for C# use:
- **Window manipulation:** `FindWindowEx`, `SetWindowPos`, `ShowWindow`, `SetWindowLong`
- **Drawing:** `GetDC`, `ReleaseDC`, `FillRect`, `CreateSolidBrush`
- **Regions:** `CreateRoundRectRgn`, `SetWindowRgn`, `CombineRgn`
- **DWM:** `DwmSetWindowAttribute` for window corner radius
- **Subclassing:** `SetWindowSubclass`, `DefSubclassProc`

### MainWindow / MainWindowContent Organization
`MainWindow` is a thin `Window` shell that hosts `MainWindowContent` (a `UserControl`) because WinUI 3's `Window` doesn't support `x:Bind` OneWay/TwoWay (see XAML Binding note above). All real content, bindings, and logic live in `MainWindowContent`, split into partial class files by domain.

### Internal Pages (orion:// tabs)
Internal browser pages are displayed as tabs using the `orion://` URL scheme. When the active tab has an `orion://` URL, `NavigateToUrl()` hides the WebKit HWND and shows the corresponding WinUI control in the content area.
- **Settings** (`orion://settings`) - `Views/SettingsPage` UserControl with NavigationView categories. Opened via `OpenSettingsTab(category?)` which reuses an existing settings tab if one exists.

### Secondary Windows
- **LibraryWindow** - Singleton (`ShowHistory()` static method), displays history, bookmarks, and other library categories. History grouped by date (Today/Yesterday/Last 7 Days/Older), supports search/filter and deletion

### Tab Management (MVVM)
- `BrowserTab` model supports hierarchical tabs via `Children` collection
- `TabGroup` contains tabs with selection/collapse state
- `MainWindowViewModel` manages all groups
- Uses `CommunityToolkit.Mvvm` (`ObservableObject` base class, `SetProperty`, `OnPropertyChanged`)
- Uses `ObservableCollection` for data binding

**FlattenedTabCollection (Adapters/):**
TabView displays a flat list while TreeView displays the hierarchy. `FlattenedTabCollection` is an adapter that provides a flattened view of the tab tree for TabView binding. It implements `IList` (non-generic) for WinRT interop - TabView casts `ItemsSource` to `IVector<IInspectable>` and calls `RemoveAt`/`Insert` directly for drag-drop reordering. Reads enumerate the tree on-demand; writes modify the underlying tree.

**Tree Change Notifications:**
When TreeView or TabView modifies the tree, the other view must update. Rather than recursively subscribing to all `Children` collections (which requires tracking subscriptions as the tree changes), we use explicit notifications: `TabGroup.NotifyTreeChanged()` is called after any tree modification (`AddTab`, `RemoveTab`, TreeView's `DragItemsCompleted`), which calls `FlattenedTabCollection.Refresh()` to notify TabView.


### Orion Core Run Loop

The orion-linux core module uses a **libuv** event loop (`RunLoop` in `common/src/run_loop.h`). On Windows, WinUI 3 owns the UI thread, so the orion loop is driven by a `DispatcherTimer` rather than running on a dedicated thread.

**Thread model:**
- `InitOrionCore()` makes the **UI thread the orion main thread** (`tls_run_loop()` returns the UI thread's loop)
- All core module callbacks fire on the UI thread — no cross-thread marshaling needed
- Background threads (e.g. database I/O) post completions back to the main loop via `uv_async_send`

**Key APIs (`OrionBackend/exports.cpp`):**
- `InitOrionCore()` — creates `CoreApi`, calls `prepare_run_loop()` (starts background services without blocking)
- `TickOrionCore()` — calls `tls_run_loop()->pump()` which does one `uv_run(UV_RUN_NOWAIT)` tick; returns 0 when fully stopped
- `ShutdownOrionCore()` — calls `cleanBrowserEngine()` first (silences callbacks), then `request_stop_run_loop()`

**`prepare_run_loop()` vs `start_run_loop()`:**
- `start_run_loop()` — blocking; runs the loop on the calling thread until shutdown (used on Linux/GTK)
- `prepare_run_loop()` — non-blocking; starts scheduled services (history cleanup etc.), then returns. The caller drives the loop via repeated `pump()` calls

**Waiting for async results on the UI thread:**
Never call `future.get()` directly — it blocks the run loop and deadlocks. Instead, pump the run loop in a while loop until the result is ready (see `exports.cpp` for examples). Only pump from top-level entry points (P/Invoke exports): pumping from inside a core callback (e.g. a setting observer) re-enters `uv_run` and is not allowed.

**Shutdown ordering:**
1. `cleanBrowserEngine()` — destroy all browser HWNDs before core stops; prevents title/URL callbacks firing during shutdown. For CEF, pumps `CefDoMessageLoopWork()` until all browsers complete `OnBeforeClose`, then calls `CefShutdown()` (tears down the Chrome profile, flushing cookies and site data to disk).
2. `request_stop_run_loop()` — signals all background threads to drain and stop
3. `TickOrionCore()` returns false once the loop is fully stopped
4. `g_core_api.reset()` happens in `TickOrionCore()` after `pump()` returns false (not inside any callback — avoids destroying `CoreApi` while still on its call stack)

### Core Module
- Built via CMake in `external/orion-gtk/app/src/core_module/`
- Uses IDL code generation (Go scripts) for API bindings
- Provides database, settings, profiles, web extensions
- Build artifacts cached based on git hash (skips rebuild if unchanged)

## Web Extensions

Orion supports Chrome/Firefox WebExtensions. The heavy lifting (manifest parsing, `browser.*` API implementation, IDL codegen, bridge JS) lives in the orion-linux core module; this repo provides the Windows/WebKit glue. **Read `docs_ai/web_extensions_porting.md` (port status + code map) and `docs/web_extensions.md` (core internals) before working here.**

**Windows-side pieces:**
- `exports.cpp` — attaches the core's bridge hosts (`bridge_hosts.{h,cpp}`: one `ProfileHost` + `WebViewFactoryHost` per profile in `g_profile_hosts`, one `WebViewHost` per browsing HWND in `g_web_view_hosts`; the engine's `orion::ScriptBridge` from `script_bridge.h` does the native work); exports `InstallExtension`, `GetInstalledExtensions`, `UninstallExtension`, `SetExtensionEnabled`, `GetExtensionToolbarItems`, `ConfigureWebViewExtensions(hwnd, profileId)`
- `extension_scheme_webkit.cpp` — registers `moz-extension://` / `chrome-extension://` / `extension-proxy://` WKURLScheme handlers and routes each request through the core-provided per-scheme handler (resources, 404s, proxy)
- `script_bridge_webkit.cpp` — `orion::create_webkit_script_bridge()`, the JS↔native message channel (`WKUserContentController` / `WKScriptMessage`)
- `notification_bridge.{h,cpp}` — `HostNotificationProvider`, the core's `orion::NotificationProvider` for Windows: `browser.notifications` display goes as JSON to C# `Services/NotificationService.cs` (Windows App SDK toasts) via the `SetNotificationCallbacks` export; clicks come back through `NotifyNotificationEvent`
- C# UI: `Views/PopularExtensionsWindow*`, `Views/SettingsPage.Extensions.cs`, `Models/ExtensionEntry.cs`, `Models/PopularExtensionItem.cs`

**Engine caveat:** the scheme handlers and script bridge are **WebKit-only** — they live in the OrionEngineWK plugin. Under Blink the extension exports still exist (they talk to the engine-agnostic core runtime) but there is no scheme handler or script bridge, so extensions are non-functional. The CEF port has not started.

**Testing:** the Orion API test extension lives in the core module: `external/orion-gtk/app/src/core_module/extensions/shared/orion-api-test/` (body) plus thin `extensions/chrome/orion-api-test/` (MV3 manifest) and `extensions/firefox/orion-api-test/` (MV2 manifest) directories whose `.orion-overlay` file points at the shared body. The core's CMake `webextensions` target packages them into `build/core_module/orion-api-test.crx` / `.xpi`; `Orion.csproj` copies both next to the exe and, in `$(OrionTestExtensions)` builds, `App.ReinstallTestExtensions()` reinstalls them at launch with the core in debug mode (`set_debug_mode`, required by the harness's `debug` permission). The popup runs built-in suites (storage, runtime, i18n, alarms, notifications, errors), the Chromium/Firefox corpora, and a manual notifications page.

## Unit Tests

The C# layer has unit tests in `UnitTest/`. No tests exist for the C++ backend.

### Running Tests

```bash
dotnet test UnitTest
```

Or via **Test Explorer** in Visual Studio (the project is part of `Orion.slnx` and built automatically with the solution).

### Design Decisions

- **Compile source files directly** via linked `<Compile Include>` items — avoids the `WinExe` output type limitation (can't reference an exe project). The test project re-declares the same package dependencies as `Orion.csproj`.
- **`NativeWebView`, `Log`, etc. are stubs** in `Stubs/` — no P/Invoke, no native code. `NativeTypes.cs` is linked from `Orion/Interop/Generated/`.
- **`HistoryEntry.cs`, `BookmarkTreeNode.cs`, etc. are excluded** from the glob. To add tests for them, ensure the necessary generated types and stubs are wired up.
- **`WebViewFactory` stays null** in all ViewModel tests — the VM handles this gracefully (tabs have `NativeWebView == null`).
- **xunit v3** (`OutputType=Exe`) is required; the solution maps all build types to `Debug` or `Release` for this project.

## WebKit Notes

### Build System
- WebKit is built via vcpkg overlay port (`ports/webkit-win/`), not manually
- Source comes from a pruned tarball in GCS that the `kagisearch/webkit` CI publishes for every commit on an `*orion-windows*` branch; there is no clone fallback, so a `WEBKIT_REF` with no published tarball fails the port
- The portfile uses `vcpkg_cmake_configure` / `vcpkg_cmake_build` with clang-cl
- Build tools (Perl, Python, Ruby, Gperf) are auto-acquired by vcpkg via `vcpkg_find_acquire_program`
- To update the WebKit version, change the `REF` commit SHA in `ports/webkit-win/portfile.cmake`

### C API Ref Counting — rules of thumb

Full rules with rationale and examples: **`docs_ai/webkit_c_api_refcounting.md`**.

- `adoptWK()` **only** on refs returned by functions with `Create`/`Copy` in the name (they return +1; adopt claims that reference without retaining).
- Everything else is **borrowed** — `Get` functions and all `WK*Ref` callback parameters. Use freely during the current call; to keep one longer (async completion), retain it with `WKPtr<T>(ptr)`, never `adoptWK`.
- Getting this wrong in the adopt direction over-releases → heap corruption with a delayed, misleading libpas "Alloc bit not set" panic. Wrong in the retain direction merely leaks. When unsure, retain.
- Every owning reference must be a `WKPtr`, wrapped at the point of acquisition. Raw `WK*Ref` only for non-owning, transient within-call use (parameters, arguments); anything that outlives the call — members, globals, context structs, lambda captures — must be a `WKPtr`.
- Ownership is binary: `Create`/`Copy` returns are +1 (adopt); everything else (Get results, callback params, in-params) is borrowed. Borrowed refs: keep **raw** while they stay within the call (don't retain transient borrows — `WKPtr` should signal ownership); retain into a `WKPtr` the moment one escapes (stored, captured, or returned from your own helper).

### C API and Opaque Types
WebKit's C API uses opaque pointer types for type safety:
```c
typedef const struct OpaqueWKArray* WKArrayRef;  // Never defined - intentionally opaque
```

The actual implementation maps these to internal classes via `WK_ADD_API_MAPPING`:
- `WKArrayRef` → `API::Array`
- `WKPageRef` → `WebPageProxy`
- `WKStringRef` → `API::String`

Conversion functions in `WKSharedAPICast.h`:
- `toImpl(WKArrayRef)` → `API::Array*`
- `toAPI(API::Array*)` → `WKArrayRef`

### String Types
WebKit uses WTF (Web Template Framework) string classes:
- `WTF::String` - Main string class, UTF-16 internally (with Latin-1 optimization)
- `WTF::AtomString` - Interned strings for frequent comparisons
- `WTF::StringView` - Non-owning view
- `WTF::CString` - UTF-8 for C API interop

### WebKit Source Browsing
WebKit source files are browsable in VS Solution Explorer via generated VS projects in the `/WebKit/` solution folder. These are generated by `update_wk_solution.py.cmd` (prefers `../webkit/` if present, falls back to vcpkg buildtrees). The projects are browse-only (`<Build Project="false" />`).

PDB source paths use `/clang:-fdebug-prefix-map` to store repo-relative paths, so the VS debugger can find WebKit source when stepping into code regardless of clone location.

### IntelliSense for WebKit
WebKit's opaque types break IntelliSense navigation. Workaround:
1. Create `WKBase.h` wrapper that defines real types under `__INTELLISENSE__`
2. The wrapper is at `external/WKBase.h` — applied to vcpkg-installed headers manually if needed
3. For normal compilation, includes `WKBase_impl.h` (the original)

## Submodule Notes

### Keeping Submodules Clean
CMake-generated files can dirty submodules. The sqlite build in orion-linux was modified to build out-of-source:
- Source copied to `${BINARY_DIR}/sqlite_build/` before building
- Keeps `external/orion-gtk` clean in `git status`

### Project GUID Synchronization
CMake derives each generated vcxproj GUID from the build directory path
(UUIDv3 of `<binary dir>|<target>`), so every checkout location yields
different GUIDs. If Visual Studio loads a vcxproj whose GUID differs from the
`<Project Id>` in `Orion.slnx`, it silently rewrites the solution to match —
that is what an unexplained `M Orion.slnx` is. `fix_project_uuids.py.cmd`
(driven by `noenv_update_core_module.bat`) prevents it in two layers:
1. `--write-cmake-cache` writes `build/project_guids.cmake` seeding
   `<target>_GUID_CMAKE` for every core module project in `Orion.slnx`; cmake is
   invoked with `-C` on it, so the vcxproj files are generated with the
   solution's GUIDs in the first place (CMake returns a cached
   `<target>_GUID_CMAKE` instead of hashing).
2. Fallback, run immediately after configure (before the codegen build, so a
   later failure can't leave mismatched files behind): rewrites `<ProjectGuid>`
   and `<ProjectReference><Project>` in each generated vcxproj to match the
   solution and sweeps tlog dirs named for a previous GUID. It should report
   "No changes needed".

Never run cmake configure on the core module by hand without the `-C` seed
file, and if `Orion.slnx` shows Id-only changes, `git checkout Orion.slnx` and
rerun `update_core_module.bat` rather than committing the new ids.

## Screenshots
When asked to check or look at a screenshot (and running in WSL), run this to find the latest one:
```
powershell.exe -Command 'Get-ChildItem (Join-Path ([Environment]::GetFolderPath("MyPictures")) "Screenshots") | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName'
```
Then read the file at the returned path.
