1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0 & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Unified CEF management script for Orion Windows.
#
# Usage:
#   cef.py.cmd                             — show help + status
#   cef.py.cmd switch official latest      — switch to latest official (Spotify CDN)
#   cef.py.cmd switch official beta        — switch to latest beta
#   cef.py.cmd switch official VERSION     — switch to specific official version
#   cef.py.cmd switch source <name>        — switch to source build from GCS
#   cef.py.cmd switch list                 — list available builds
#   cef.py.cmd setup                       — download/unpack CEF + build wrapper
#   cef.py.cmd symbols                     — download debug symbols for libcef.dll
#   cef.py.cmd upload [name]               — package + upload source build to GCS
#   cef.py.cmd build [target]              — build CEF from source (auto/nopgo/pgo1/pgo2/fullpgo)
#   cef.py.cmd copy <name>                 — copy source build output to vendor/cef_builds/<name>
#   cef.py.cmd clean                       — delete source build output
#   cef.py.cmd rebuild [target]            — clean + build
#   cef.py.cmd benchmark [target|exe]      — run Speedometer 3.1
#   cef.py.cmd profile                     — run PGO profiling benchmarks
#   cef.py.cmd record [target]             — record WPR archives
#   cef.py.cmd record_upload               — re-upload WPR archives to GCS
#   cef.py.cmd source switch <branch>      — manage Chromium+CEF source checkout
#   cef.py.cmd source status               — show source checkout status

import ast
import glob
import gzip
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import uuid
import zipfile
from pathlib import Path
from urllib.parse import urlparse

sys.path.insert(0, str(Path(__file__).resolve().parent / "scripts"))
from vsenv import ensure_vsenv

# Import ensure_kagi_gcs_auth from sibling .cmd polyglot script.
_auth_ns = {}
exec(compile(
    (Path(__file__).resolve().parent / "scripts" / "ensure_kagi_gcs_auth.py.cmd").read_text(encoding="utf-8"),
    "ensure_kagi_gcs_auth.py.cmd", "exec"), _auth_ns)
ensure_kagi_gcs_auth = _auth_ns["ensure_kagi_gcs_auth"]

# ── Paths ────────────────────────────────────────────────────────────────────

ROOT = Path(os.getcwd())
VENDOR_DIR = ROOT / "vendor"
CEF_DIR = VENDOR_DIR / "cef"                    # unified target for both official & source
VERSION_FILE = VENDOR_DIR / "CEF_VERSION"
INSTALLED_VERSION_FILE = CEF_DIR / "INSTALLED_CEF_VERSION"

# Source build paths.
CEFS_DIR = VENDOR_DIR / "cefs"
DEPOT_TOOLS = CEFS_DIR / "depot_tools"
CHROMIUM_SRC = CEFS_DIR / "chromium" / "src"
CEF_SRC = CHROMIUM_SRC / "cef"
OUT_DIR = CHROMIUM_SRC / "out" / "Release_GN_x64"
CEF_BUILDS_DIR = VENDOR_DIR / "cef_builds"

# WPR paths.
CEF_WPR_DIR = VENDOR_DIR / "cef_wpr"
WPR_ID_FILE = VENDOR_DIR / "WPR_ID"
GCS_WPR_BUCKET = "gs://kagi-orion-windows-artifacts/cef_wpr"
WPR_DATA_DIR = CHROMIUM_SRC / "tools" / "perf" / "page_sets" / "data"

# GCS paths.
GCS_CEF_BUCKET = "gs://kagi-orion-windows-artifacts/cef"
GCS_PROFDATA_BUCKET = "gs://kagi-orion-windows-artifacts/cef_profdata"
PROFDATA_DIR = VENDOR_DIR / "cef_profdata"
PROFDATA_ID_FILE = VENDOR_DIR / "PROFDATA_ID"

VCPKG_EXE = ROOT / "external" / "vcpkg" / "vcpkg.exe"

# Official CEF distribution.
CEF_PLATFORM = "windows64"
INDEX_URL = "https://cef-builds.spotifycdn.com/index.json"

# Source checkout.
AUTOMATE_URL = (
    "https://raw.githubusercontent.com/chromiumembedded/cef"
    "/refs/heads/master/tools/automate/automate-git.py"
)

# ── Build targets (source builds) ───────────────────────────────────────────

BASE_ARGS = {
    "alternate_cdm_storage_id_key": '"968b476909da4373b08903c28e859454"',
    "clang_use_chrome_plugins": "false",
    "disable_fieldtrial_testing_config": "true",
    "enable_background_mode": "false",
    "enable_cdm_host_verification": "true",
    "enable_cdm_storage_id": "true",
    "enable_downgrade_processing": "false",
    "enable_resource_allowlist_generation": "false",
    "enable_rlz": "true",
    "enable_widevine": "true",
    "is_component_build": "false",
    "is_debug": "false",
    "is_official_build": "true",
    "optimize_webui": "true",
    "target_cpu": '"x64"',
    "v8_used_in_shared_library": "false",
}

TARGET_ARGS = {
    "auto": {},
    "nopgo": {"chrome_pgo_phase": "0"},
    "pgo1": {"chrome_pgo_phase": "1"},
    "pgo2": {
        "chrome_pgo_phase": "2",
        "pgo_data_path": '"//chrome/build/pgo_profiles/custom_profile.profdata"',
    },
}

RECORD_TARGETS = {
    "jetstream2": ("jet_stream2", "jetstream2"),
    "system_health": ("desktop_common_system_health", "system_health_desktop"),
    "rendering": ("rendering_desktop", "rendering_desktop"),
}

# ── Help text ────────────────────────────────────────────────────────────────

HELP_TEXT = """\
CEF Management Tool

Usage:
  cef.py.cmd <command> [args]

Commands:
  switch official latest|beta|VER  Switch to official CEF build (Spotify CDN)
  switch source <name>             Switch to source build from GCS
  switch list                      List available official + source builds
  setup                            Download/unpack CEF + build wrapper lib
  symbols                          Download debug symbols for libcef.dll
  upload [name]                    Package source build + upload to GCS
  build [target]                   Build CEF from source
  copy <name>                      Copy source build to vendor/cef_builds/<name>
  clean                            Delete source build output
  rebuild [target]                 Clean + build
  benchmark [target|exe]           Run Speedometer 3.1
  profile                          Run PGO profiling benchmarks
  record [target]                  Record WPR archives
  record_upload                    Re-upload WPR archives to GCS
  source switch <branch>           Manage Chromium+CEF source checkout
  source status                    Show source checkout status

Build targets:
  auto     Official build with Google's PGO profiles (default)
  nopgo    Official build with PGO disabled
  pgo1     PGO instrumentation build (generates .profraw at runtime)
  pgo2     Official build using custom profile (profile.profdata)
  fullpgo  Full PGO pipeline: build pgo1 -> profile -> build pgo2

Record targets:
  all            Record all targets below (default)
  jetstream2     JetStream 2 benchmark
  system_health  System health desktop stories
  rendering      Rendering desktop stories

Examples:
  cef.py.cmd switch official latest     Switch to latest stable official build
  cef.py.cmd switch source pgo2         Switch to pgo2 source build from GCS
  cef.py.cmd setup                      Download + build wrapper (run by VS)
  cef.py.cmd build                      Build with Google PGO (auto)
  cef.py.cmd build fullpgo              Full PGO pipeline
  cef.py.cmd benchmark                  Run Speedometer on current source build
  cef.py.cmd benchmark pgo2             Run on vendor/cef_builds/pgo2
  cef.py.cmd benchmark C:\\path\\chrome.exe  Run on any Chromium browser
"""


# ══════════════════════════════════════════════════════════════════════════════
#  Version file parsing
# ══════════════════════════════════════════════════════════════════════════════

def read_version_file() -> str:
    if not VERSION_FILE.exists():
        return ""
    return VERSION_FILE.read_text(encoding="utf-8").strip()


def parse_version(raw: str) -> tuple[str, str]:
    """Parse CEF_VERSION content into (type, value).

    Returns:
        ("official", version_string) or ("source", name).
        Bare strings (no prefix) are treated as official.
    """
    if raw.startswith("official:"):
        return "official", raw[len("official:"):]
    if raw.startswith("source:"):
        return "source", raw[len("source:"):]
    # Bare string — legacy official format.
    return "official", raw


def write_version_file(kind: str, value: str):
    VERSION_FILE.parent.mkdir(parents=True, exist_ok=True)
    VERSION_FILE.write_text(f"{kind}:{value}\n", encoding="utf-8")


# ══════════════════════════════════════════════════════════════════════════════
#  Switch commands
# ══════════════════════════════════════════════════════════════════════════════

def fetch_cef_index():
    with urllib.request.urlopen(INDEX_URL) as resp:
        return json.loads(resp.read())


def cmd_switch_official(arg: str | None):
    """Switch to an official CEF build from Spotify CDN."""
    if arg is None:
        cmd_switch_list()
        return

    print(f"Resolving '{arg}'...")
    data = fetch_cef_index()
    versions = data[CEF_PLATFORM]["versions"]

    if "+" in arg:
        # Specific version string.
        version = arg
    else:
        # Channel name (stable, beta, etc.).
        version = None
        for v in versions:
            if v["channel"] == arg:
                version = v["cef_version"]
                break
        if not version:
            print(f"ERROR: No '{arg}' builds found.")
            sys.exit(1)

    print(f"Selected: CEF {version}")
    write_version_file("official", version)
    print(f"Updated {VERSION_FILE}\n")
    cmd_setup()


def cmd_switch_source(name: str | None):
    """Switch to a source build from GCS."""
    if name is None:
        cmd_switch_list()
        return

    if not ensure_kagi_gcs_auth():
        sys.exit(1)

    # Verify the artifact exists in GCS.
    manifest = _fetch_gcs_manifest()
    if manifest and name not in manifest.get("builds", {}):
        available = ", ".join(sorted(manifest.get("builds", {}).keys()))
        print(f"WARNING: '{name}' not found in GCS manifest.")
        if available:
            print(f"Available: {available}")
        answer = input("Continue anyway? [y/N] ").strip().lower()
        if answer != "y":
            sys.exit(1)

    write_version_file("source", name)
    print(f"Updated {VERSION_FILE}\n")
    cmd_setup()


def cmd_switch_list():
    """List available official + source builds."""
    raw = read_version_file()
    kind, value = parse_version(raw) if raw else ("", "")
    current = f"{kind}:{value}" if kind else "(none)"
    print(f"Current: {current}\n")

    # Official builds.
    print("Official builds (Spotify CDN):")
    try:
        data = fetch_cef_index()
        versions = data[CEF_PLATFORM]["versions"]
        print(f"  {'Channel':<10} {'CEF Version':<55} {'Chromium'}")
        print(f"  {'-'*10} {'-'*55} {'-'*25}")
        for v in versions[:15]:
            print(f"  {v['channel']:<10} {v['cef_version']:<55} {v['chromium_version']}")
    except Exception as e:
        print(f"  (failed to fetch: {e})")

    # Source builds from GCS.
    print(f"\nSource builds (GCS):")
    try:
        manifest = _fetch_gcs_manifest()
        if manifest and manifest.get("builds"):
            builds = manifest["builds"]
            print(f"  {'Name':<20} {'Chromium':<25} {'Date'}")
            print(f"  {'-'*20} {'-'*25} {'-'*20}")
            for name in sorted(builds):
                info = builds[name]
                cv = info.get("chromium_version", "?")
                date = info.get("date", "?")[:10]
                print(f"  {name:<20} {cv:<25} {date}")
        else:
            print("  (none)")
    except Exception as e:
        print(f"  (failed to fetch: {e})")

    print(f"\nUsage:")
    print(f"  cef.py.cmd switch official latest    -- latest stable")
    print(f"  cef.py.cmd switch official beta       -- latest beta")
    print(f"  cef.py.cmd switch official VERSION    -- specific version")
    print(f"  cef.py.cmd switch source <name>       -- source build from GCS")


def _fetch_gcs_manifest() -> dict | None:
    """Fetch the GCS manifest (index.json) listing available source builds."""
    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        return None
    try:
        manifest_path = Path(tempfile.mktemp(suffix=".json"))
        result = subprocess.run(
            [gcloud, "storage", "cp",
             f"{GCS_CEF_BUCKET}/index.json", str(manifest_path)],
            capture_output=True, text=True, timeout=30,
        )
        if result.returncode != 0:
            return None
        data = json.loads(manifest_path.read_text("utf-8"))
        manifest_path.unlink(missing_ok=True)
        return data
    except Exception:
        return None


# ══════════════════════════════════════════════════════════════════════════════
#  Setup command
# ══════════════════════════════════════════════════════════════════════════════

def cmd_setup():
    """Download/unpack CEF and build the wrapper library."""
    raw = read_version_file()
    if not raw:
        print(f"ERROR: {VERSION_FILE} not found or empty.")
        print(f"Run  cef.py.cmd switch official latest  or  cef.py.cmd switch source <name>")
        sys.exit(1)

    kind, value = parse_version(raw)
    installed = ""
    if INSTALLED_VERSION_FILE.exists():
        installed = INSTALLED_VERSION_FILE.read_text("utf-8").strip()

    # If vendor/cef/ has local source build junctions and CEF_VERSION hasn't
    # been changed to official/source, preserve them.  A local builder who
    # runs VS (which triggers `cef.py.cmd setup`) should not have their junctions
    # wiped.  To switch away, they run `cef.py.cmd switch official/source`.
    if installed == "local:source" and _is_local_junction():
        wrapper_ok = _check_wrapper_libs("source")
        if wrapper_ok:
            print("CEF linked to local source build, skipping setup.")
            return
        print("CEF linked to local source build but wrapper not built, rebuilding...")
        _build_wrapper("source")
        return

    desired_tag = f"{kind}:{value}"

    if desired_tag == installed:
        # Check if wrapper libs exist.
        wrapper_ok = _check_wrapper_libs(kind)
        if wrapper_ok:
            print(f"CEF {desired_tag} already set up, skipping.")
            return
        print(f"CEF {desired_tag} installed but wrapper not built, rebuilding...")
    else:
        if installed:
            print(f"CEF version mismatch: want {desired_tag}, have {installed}.")
        else:
            print(f"Setting up CEF {desired_tag}...")
        if kind == "official":
            _setup_official(value)
        else:
            _setup_source(value)

    _build_wrapper(kind)

    # Record installed version.
    INSTALLED_VERSION_FILE.parent.mkdir(parents=True, exist_ok=True)
    INSTALLED_VERSION_FILE.write_text(desired_tag + "\n", encoding="utf-8")
    print(f"CEF {desired_tag} setup complete.")


def _is_local_junction() -> bool:
    """Check if vendor/cef/Release is a junction to the local source build."""
    release_link = CEF_DIR / "Release"
    if not release_link.exists():
        return False
    try:
        return release_link.is_junction()
    except Exception:
        return False


def _check_wrapper_libs(kind: str) -> bool:
    """Check if wrapper libraries exist for the given build kind."""
    if kind == "official":
        debug = CEF_DIR / "build" / "libcef_dll_wrapper" / "Debug" / "libcef_dll_wrapper.lib"
        release = CEF_DIR / "build" / "libcef_dll_wrapper" / "Release" / "libcef_dll_wrapper.lib"
        return debug.exists() and release.exists()
    else:
        # Source builds from GCS: wrapper in build/ like official.
        debug = CEF_DIR / "build" / "libcef_dll_wrapper" / "Debug" / "libcef_dll_wrapper.lib"
        release = CEF_DIR / "build" / "libcef_dll_wrapper" / "Release" / "libcef_dll_wrapper.lib"
        return debug.exists() and release.exists()


def _get_official_channel(version: str) -> str:
    """Query the CEF builds index to determine the channel for a version."""
    try:
        data = fetch_cef_index()
        for v in data[CEF_PLATFORM]["versions"]:
            if v["cef_version"] == version:
                return v["channel"]
    except Exception:
        pass
    return "stable"


def _official_download_url(version: str, channel: str) -> str:
    encoded = version.replace("+", "%2B")
    suffix = f"_{channel}" if channel != "stable" else ""
    return f"https://cef-builds.spotifycdn.com/cef_binary_{encoded}_{CEF_PLATFORM}{suffix}.tar.bz2"


def _official_extracted_dir_name(version: str, channel: str) -> str:
    suffix = f"_{channel}" if channel != "stable" else ""
    return f"cef_binary_{version}_{CEF_PLATFORM}{suffix}"


def _ensure_vcpkg():
    subprocess.run(
        [str(ROOT / "scripts" / "ensure_vcpkg.bat")], shell=True, check=True,
    )


def _fetch_vcpkg_tool(name: str) -> str:
    """Return the path to a vcpkg tool, downloading it if needed."""
    _ensure_vcpkg()
    result = subprocess.run(
        [str(VCPKG_EXE), "fetch", name, "--x-wait-for-lock"],
        capture_output=True, text=True, check=True,
    )
    return result.stdout.strip().splitlines()[-1]


def _extract_tar_bz2(archive: Path, dest: Path):
    """Extract a .tar.bz2 archive using 7z (decompress) piped to 7z (untar)."""
    seven_zip = _fetch_vcpkg_tool("7zip")
    decompress = subprocess.Popen(
        [seven_zip, "x", str(archive), "-so", "-bso0"],
        stdout=subprocess.PIPE,
    )
    subprocess.run(
        [seven_zip, "x", "-si", "-ttar", f"-o{dest}", "-bso0"],
        stdin=decompress.stdout,
        check=True,
    )
    decompress.wait()


def _remove_cef_dir():
    """Safely remove vendor/cef/, handling directory junctions.

    shutil.rmtree follows junctions on Windows and would delete the
    junction *target* contents (e.g. the source build tree).  We must
    unlink junctions first, then remove the rest normally.
    """
    if not CEF_DIR.exists():
        return
    for item in CEF_DIR.iterdir():
        if item.is_junction():
            item.unlink()
        elif item.is_dir():
            shutil.rmtree(item, ignore_errors=True)
        else:
            item.unlink(missing_ok=True)
    CEF_DIR.rmdir()


def _clear_cef_cache():
    """Delete the CEF browser cache to avoid stale/incompatible profile data."""
    local_app_data = os.environ.get("LOCALAPPDATA", "")
    if not local_app_data:
        return
    cache_dir = Path(local_app_data) / "Kagi" / "Orion" / "cef_cache"
    if cache_dir.exists():
        print(f"Removing CEF cache: {cache_dir}")
        shutil.rmtree(cache_dir)


def _setup_official(version: str):
    """Download and extract official CEF binary distribution."""
    channel = _get_official_channel(version)
    url = _official_download_url(version, channel)
    archive_name = url.rsplit("/", 1)[1].replace("%2B", "+")
    archive_path = VENDOR_DIR / archive_name
    extracted = VENDOR_DIR / _official_extracted_dir_name(version, channel)

    if CEF_DIR.exists():
        print("Removing old CEF...")
        _remove_cef_dir()

    _clear_cef_cache()

    VENDOR_DIR.mkdir(parents=True, exist_ok=True)

    print(f"Downloading {url}...")
    urllib.request.urlretrieve(url, archive_path)

    print("Extracting...")
    _extract_tar_bz2(archive_path, VENDOR_DIR)

    if not extracted.exists():
        archive_path.unlink(missing_ok=True)
        print(f"ERROR: Expected extracted directory not found: {extracted}")
        sys.exit(1)

    extracted.rename(CEF_DIR)
    archive_path.unlink(missing_ok=True)



def _debug_symbols_url(version: str, channel: str) -> str:
    encoded = version.replace("+", "%2B")
    suffix = f"_{channel}" if channel != "stable" else ""
    return f"https://cef-builds.spotifycdn.com/cef_binary_{encoded}_{CEF_PLATFORM}{suffix}_debug_symbols.tar.bz2"


def _debug_symbols_extracted_dir_name(version: str, channel: str) -> str:
    suffix = f"_{channel}" if channel != "stable" else ""
    return f"cef_binary_{version}_{CEF_PLATFORM}{suffix}_debug_symbols"


def _download_debug_symbols(version: str, channel: str):
    """Download and extract debug symbols (PDB) for libcef.dll."""
    url = _debug_symbols_url(version, channel)
    archive_name = url.rsplit("/", 1)[1].replace("%2B", "+")
    archive_path = VENDOR_DIR / archive_name
    extracted = VENDOR_DIR / _debug_symbols_extracted_dir_name(version, channel)

    print(f"Downloading debug symbols {url}...")
    urllib.request.urlretrieve(url, archive_path)

    print("Extracting debug symbols...")
    _extract_tar_bz2(archive_path, VENDOR_DIR)
    archive_path.unlink(missing_ok=True)

    if extracted.is_dir():
        debug_dir = CEF_DIR / "Debug"
        for f in extracted.iterdir():
            shutil.move(str(f), str(debug_dir / f.name))
        extracted.rmdir()


def cmd_symbols():
    """Download debug symbols for the installed CEF version."""
    raw = read_version_file()
    if not raw:
        print(f"ERROR: {VERSION_FILE} not found. Run  cef.py.cmd setup  first.")
        sys.exit(1)
    kind, value = parse_version(raw)
    if kind != "official":
        print("Debug symbols are only available for official CEF builds.")
        sys.exit(1)
    channel = _get_official_channel(value)
    _download_debug_symbols(value, channel)
    print("Debug symbols installed.")


def _setup_source(name: str):
    """Download and extract a source build from GCS, normalizing to official layout."""
    if not ensure_kagi_gcs_auth():
        sys.exit(1)

    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        print("ERROR: gcloud CLI not found.")
        sys.exit(1)

    # Try to find the artifact in the manifest.
    manifest = _fetch_gcs_manifest()
    artifact_name = None
    if manifest and name in manifest.get("builds", {}):
        artifact_name = manifest["builds"][name].get("artifact")

    if not artifact_name:
        # Guess artifact name pattern: try listing GCS.
        print(f"Looking for source build '{name}' in GCS...")
        result = subprocess.run(
            [gcloud, "storage", "ls", f"{GCS_CEF_BUCKET}/{name}*"],
            capture_output=True, text=True,
        )
        if result.returncode != 0 or not result.stdout.strip():
            print(f"ERROR: Source build '{name}' not found in GCS.")
            sys.exit(1)
        # Take the last matching artifact.
        artifacts = result.stdout.strip().splitlines()
        artifact_url = artifacts[-1].strip()
        artifact_name = artifact_url.rsplit("/", 1)[-1]

    # Download.
    download_dir = VENDOR_DIR / "cef_download"
    download_dir.mkdir(parents=True, exist_ok=True)
    archive_path = download_dir / artifact_name
    gcs_src = f"{GCS_CEF_BUCKET}/{artifact_name}"

    print(f"Downloading {gcs_src}...")
    result = subprocess.run(
        [gcloud, "storage", "cp", gcs_src, str(archive_path)],
    )
    if result.returncode != 0:
        print(f"ERROR: Download failed.")
        sys.exit(result.returncode)

    # Extract to a temp directory first.
    extract_tmp = download_dir / "extract_tmp"
    if extract_tmp.exists():
        shutil.rmtree(extract_tmp)
    extract_tmp.mkdir()

    print("Extracting...")
    subprocess.run(
        ["tar", "xf", str(archive_path), "-C", str(extract_tmp)],
        check=True,
    )

    # Find the extracted root (may be nested one level).
    extracted_dirs = list(extract_tmp.iterdir())
    if len(extracted_dirs) == 1 and extracted_dirs[0].is_dir():
        src_root = extracted_dirs[0]
    else:
        src_root = extract_tmp

    # Remove old CEF dir.
    if CEF_DIR.exists():
        print("Removing old CEF...")
        _remove_cef_dir()

    _clear_cef_cache()

    # Normalize into official-like layout.
    CEF_DIR.mkdir(parents=True)
    _normalize_source_to_official(src_root, CEF_DIR)

    # Cleanup.
    shutil.rmtree(extract_tmp, ignore_errors=True)
    archive_path.unlink(missing_ok=True)


def _normalize_source_to_official(src: Path, dest: Path):
    """Normalize a flat source build into the vendor/cef/ layout.

    Source (flat): libcef.dll, chrome_100_percent.pak, locales/, include/, libcef_dll/, ...
    Target:
      Release/  — all runtime files flat (DLLs, EXEs, PAKs, DATs, BINs) + locales/
      include/  — CEF headers
      libcef_dll/ — wrapper source code
      *.gypi, args.gn, BUILD_INFO — metadata at root

    Release/ is kept flat (matching source build output layout) so that
    Orion.csproj's CefSourceDir can point at it without changes.
    """
    release_dir = dest / "Release"
    release_dir.mkdir(exist_ok=True)

    # Extensions that go into Release/ (all runtime files).
    runtime_exts = {".dll", ".exe", ".lib", ".bin", ".json", ".pak", ".dat"}

    for item in src.iterdir():
        if item.name in ("include", "libcef_dll"):
            shutil.copytree(item, dest / item.name, dirs_exist_ok=True)
        elif item.name == "locales":
            shutil.copytree(item, release_dir / "locales", dirs_exist_ok=True)
        elif item.name in ("args.gn", "BUILD_INFO", "CMakeLists.txt",
                           "cef_paths.gypi", "cef_paths2.gypi"):
            shutil.copy2(item, dest / item.name)
        elif item.name == "benchmark_results" and item.is_dir():
            shutil.copytree(item, dest / "benchmark_results", dirs_exist_ok=True)
        elif item.is_file():
            ext = item.suffix.lower()
            if ext in runtime_exts:
                shutil.copy2(item, release_dir / item.name)
            else:
                shutil.copy2(item, dest / item.name)

    print(f"  Normalized to vendor/cef/ layout in {dest}")


def _build_wrapper(kind: str):
    """Build libcef_dll_wrapper for the installed CEF."""
    ensure_vsenv()
    print("Building libcef_dll_wrapper...")

    if kind == "official" or (CEF_DIR / "CMakeLists.txt").exists():
        # Official distribution has its own CMakeLists.txt.
        _build_wrapper_cmake()
    else:
        # Source build from GCS — use gypi-based build.
        _build_wrapper_gypi()


def _build_wrapper_cmake():
    """Build wrapper using the official CEF CMakeLists.txt."""
    cmake_configure = [
        "cmake",
        "-S", str(CEF_DIR),
        "-B", str(CEF_DIR / "build"),
        "-G", "Visual Studio 18 2026",
        "-A", "x64",
        "-T", "host=x64",
        "-DCEF_RUNTIME_LIBRARY_FLAG=/MD",
    ]
    if subprocess.run(cmake_configure).returncode != 0:
        print("ERROR: CMake configure failed.")
        sys.exit(1)

    for config in ("Debug", "Release"):
        cmake_build = [
            "cmake",
            "--build", str(CEF_DIR / "build"),
            "--config", config,
            "--target", "libcef_dll_wrapper",
        ]
        if subprocess.run(cmake_build).returncode != 0:
            print(f"ERROR: libcef_dll_wrapper {config} build failed.")
            sys.exit(1)


def _build_wrapper_gypi():
    """Build wrapper from .gypi files (source builds without CMakeLists.txt)."""
    gypi_path = CEF_DIR / "cef_paths2.gypi"
    if not gypi_path.exists():
        print("ERROR: No CMakeLists.txt or .gypi files found in CEF directory.")
        print("The source build artifact may be incomplete.")
        sys.exit(1)

    # Parse source list.
    sources = _parse_wrapper_sources(CEF_DIR)
    print(f"  {len(sources)} source files from .gypi")

    # Generate CMakeLists.txt.
    cmake_content = _generate_wrapper_cmake(sources)
    build_dir = CEF_DIR / "build"
    staging = build_dir / "staging"
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
    (staging / "CMakeLists.txt").write_text(cmake_content, encoding="utf-8")

    # Symlink source directories.
    _junction(staging / "include", CEF_DIR / "include")
    (staging / "libcef_dll").mkdir(exist_ok=True)
    for subdir in ("base", "cpptoc", "ctocpp", "wrapper"):
        src = CEF_DIR / "libcef_dll" / subdir
        if src.exists():
            _junction(staging / "libcef_dll" / subdir, src)
    for f in (CEF_DIR / "libcef_dll").iterdir():
        if f.is_file() and f.suffix in (".cc", ".h"):
            dst = staging / "libcef_dll" / f.name
            if not dst.exists():
                shutil.copy2(f, dst)

    cmake_build_dir = build_dir / "cmake_build"
    if cmake_build_dir.exists():
        shutil.rmtree(cmake_build_dir)

    cmake_configure = [
        "cmake",
        "-S", str(staging),
        "-B", str(cmake_build_dir),
        "-G", "Visual Studio 18 2026",
        "-A", "x64",
        "-T", "host=x64",
        f"-DCEF_SRC_DIR={CEF_DIR}",
        f"-DGEN_DIR={CEF_DIR}",
        f"-DCHROMIUM_SRC_DIR={CEF_DIR}",
    ]
    if subprocess.run(cmake_configure).returncode != 0:
        print("ERROR: CMake configure failed.")
        sys.exit(1)

    for config in ("Debug", "Release"):
        cmake_build = [
            "cmake",
            "--build", str(cmake_build_dir),
            "--config", config,
            "--target", "libcef_dll_wrapper",
        ]
        if subprocess.run(cmake_build).returncode != 0:
            print(f"ERROR: libcef_dll_wrapper {config} build failed.")
            sys.exit(1)

        # Copy output to expected location.
        src_lib = cmake_build_dir / config / "libcef_dll_wrapper.lib"
        dst_dir = build_dir / "libcef_dll_wrapper" / config
        dst_dir.mkdir(parents=True, exist_ok=True)
        if src_lib.exists():
            shutil.copy2(src_lib, dst_dir / "libcef_dll_wrapper.lib")


def _parse_gypi(path: Path) -> dict:
    """Parse a .gypi file and return the variables dict."""
    text = path.read_text(encoding="utf-8")
    lines = []
    for line in text.splitlines():
        stripped = re.sub(r"(?<!['\"])#.*$", "", line)
        lines.append(stripped)
    text = "\n".join(lines)
    data = ast.literal_eval(text)
    return data.get("variables", data)


def _parse_wrapper_sources(cef_dir: Path) -> list[str]:
    """Read .gypi files and return wrapper source .cc files."""
    gypi2 = _parse_gypi(cef_dir / "cef_paths2.gypi")
    gypi1 = _parse_gypi(cef_dir / "cef_paths.gypi")

    sources = []
    sources += gypi2.get("libcef_dll_wrapper_sources_base", [])
    sources += gypi2.get("libcef_dll_wrapper_sources_common", [])
    sources += gypi2.get("libcef_dll_wrapper_sources_win", [])
    sources += gypi1.get("autogen_client_side", [])
    return [s for s in sources if s.endswith(".cc")]


def _generate_wrapper_cmake(source_files: list[str]) -> str:
    """Generate a standalone CMakeLists.txt for building libcef_dll_wrapper."""
    sources_str = "\n    ".join(f'"{f}"' for f in source_files)
    return f"""\
cmake_minimum_required(VERSION 3.20)
project(libcef_dll_wrapper LANGUAGES CXX)

set(WRAPPER_SOURCES
    {sources_str}
)

add_library(libcef_dll_wrapper STATIC ${{WRAPPER_SOURCES}})

target_compile_definitions(libcef_dll_wrapper PRIVATE
    WRAPPING_CEF_SHARED
    NOMINMAX
    WIN32_LEAN_AND_MEAN
    UNICODE
    _UNICODE
)

target_include_directories(libcef_dll_wrapper PRIVATE
    "${{CEF_SRC_DIR}}"
    "${{GEN_DIR}}"
    "${{CHROMIUM_SRC_DIR}}"
)

target_compile_options(libcef_dll_wrapper PRIVATE
    /MD /std:c++20 /MP /Gy /GF /GR- /W0
    /wd4100 /wd4127 /wd4244 /wd4267 /wd4312 /wd4324 /wd4481
    /wd4701 /wd4702 /wd4996
)

target_link_libraries(libcef_dll_wrapper PRIVATE
    crypt32.lib
    wintrust.lib
)
"""


def _junction(link: Path, target: Path):
    """Create a Windows directory junction."""
    subprocess.run(
        ["cmd", "/c", "mklink", "/J", str(link), str(target)],
        check=True, stdout=subprocess.DEVNULL,
    )


# ══════════════════════════════════════════════════════════════════════════════
#  Upload command
# ══════════════════════════════════════════════════════════════════════════════

def cmd_upload(name: str | None):
    """Package a source build and upload to GCS."""
    if not ensure_kagi_gcs_auth():
        sys.exit(1)

    if name is None:
        # Find most recent cef_builds entry.
        if not CEF_BUILDS_DIR.exists():
            print("ERROR: No builds in vendor/cef_builds/.")
            sys.exit(1)
        candidates = sorted(
            [d for d in CEF_BUILDS_DIR.iterdir() if d.is_dir()],
            key=lambda d: d.stat().st_mtime, reverse=True,
        )
        if not candidates:
            print("ERROR: No builds in vendor/cef_builds/.")
            sys.exit(1)
        name = candidates[0].name
        print(f"Using most recent build: {name}")

    build_dir = CEF_BUILDS_DIR / name
    if not build_dir.exists():
        print(f"ERROR: Build not found: {build_dir}")
        sys.exit(1)

    # Read chromium version from BUILD_INFO.
    chromium_version = "unknown"
    build_info = build_dir / "BUILD_INFO"
    if build_info.exists():
        for line in build_info.read_text("utf-8").splitlines():
            if line.startswith("Chromium version:"):
                chromium_version = line.split(":", 1)[1].strip()
                break

    artifact_name = f"{name}-{chromium_version}.tar.gz"
    archive_path = VENDOR_DIR / "cef_download" / artifact_name
    archive_path.parent.mkdir(parents=True, exist_ok=True)

    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        print("ERROR: gcloud CLI not found.")
        sys.exit(1)

    # Create tar.gz.
    print(f"Creating {artifact_name}...")
    with tarfile.open(archive_path, "w:gz") as tar:
        tar.add(build_dir, arcname=name)
    size_mb = archive_path.stat().st_size / 1024 / 1024
    print(f"  {artifact_name} ({size_mb:.1f} MB)")

    # Upload.
    gcs_dest = f"{GCS_CEF_BUCKET}/{artifact_name}"
    print(f"Uploading to {gcs_dest}...")
    result = subprocess.run(
        [gcloud, "storage", "cp", str(archive_path), gcs_dest],
    )
    if result.returncode != 0:
        print(f"ERROR: Upload failed.")
        sys.exit(result.returncode)

    archive_path.unlink(missing_ok=True)

    # Update manifest.
    _update_gcs_manifest(name, artifact_name, chromium_version)

    print(f"\nUploaded successfully.")
    print(f"To use: cef.py.cmd switch source {name}")


def _update_gcs_manifest(name: str, artifact_name: str, chromium_version: str):
    """Update the GCS index.json manifest."""
    from datetime import datetime, timezone

    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        return

    # Download existing manifest.
    manifest = _fetch_gcs_manifest() or {"builds": {}}

    # Read CEF branch from BUILD_INFO.
    cef_branch = ""
    build_info = CEF_BUILDS_DIR / name / "BUILD_INFO"
    if build_info.exists():
        for line in build_info.read_text("utf-8").splitlines():
            if line.startswith("CEF branch:"):
                cef_branch = line.split(":", 1)[1].strip()
                break

    manifest["builds"][name] = {
        "artifact": artifact_name,
        "chromium_version": chromium_version,
        "cef_branch": cef_branch,
        "date": datetime.now(timezone.utc).isoformat(),
    }

    # Upload updated manifest.
    manifest_path = Path(tempfile.mktemp(suffix=".json"))
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    subprocess.run(
        [gcloud, "storage", "cp", str(manifest_path),
         f"{GCS_CEF_BUCKET}/index.json"],
        capture_output=True,
    )
    manifest_path.unlink(missing_ok=True)
    print(f"  Updated GCS manifest.")


# ══════════════════════════════════════════════════════════════════════════════
#  Source build commands (ported from cefs_build.cmd)
# ══════════════════════════════════════════════════════════════════════════════

def logical_cpu_count() -> int:
    try:
        return os.cpu_count() or 16
    except Exception:
        return 16


def check_source_prerequisites():
    if not CEF_SRC.exists():
        print("ERROR: CEF source not found.")
        print("Run  cef.py.cmd source switch <branch>  first.")
        sys.exit(1)
    if not DEPOT_TOOLS.exists():
        print("ERROR: depot_tools not found.")
        print("Run  cef.py.cmd source switch <branch>  first.")
        sys.exit(1)


def make_env() -> dict:
    env = os.environ.copy()
    env["PATH"] = str(DEPOT_TOOLS) + os.pathsep + env["PATH"]
    env["DEPOT_TOOLS_WIN_TOOLCHAIN"] = "0"
    return env


def write_args_gn(target: str):
    args = dict(BASE_ARGS)
    args.update(TARGET_ARGS[target])
    content = "\n".join(f"{k}={v}" for k, v in sorted(args.items())) + "\n"
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    args_gn = OUT_DIR / "args.gn"
    if args_gn.exists() and args_gn.read_text() == content:
        print(f"  args.gn unchanged ({target})")
    else:
        args_gn.write_text(content)
        print(f"  args.gn written ({target})")
    return content


def gn_gen(env: dict):
    print("\nRunning gn gen...")
    result = subprocess.run(
        f'gn gen "{OUT_DIR}"',
        cwd=str(CHROMIUM_SRC), env=env, shell=True,
    )
    if result.returncode != 0:
        print("ERROR: gn gen failed.")
        sys.exit(result.returncode)


def has_oom_errors() -> bool:
    siso_output = OUT_DIR / "siso_output"
    if not siso_output.exists():
        return False
    try:
        text = siso_output.read_text(errors="replace")
        return "LLVM ERROR: out of memory" in text
    except Exception:
        return False


def ninja_build(env: dict, jobs: int) -> int:
    cmd = f'autoninja -C "{OUT_DIR}" cef cefclient -j{jobs}'
    print(f"  autoninja -j{jobs}")
    result = subprocess.run(cmd, cwd=str(CHROMIUM_SRC), env=env, shell=True)
    return result.returncode


def cmd_build(target: str):
    if target == "fullpgo":
        if not ensure_kagi_gcs_auth():
            sys.exit(1)
        print("=== Full PGO Pipeline ===\n")
        print("Step 1/3: Building instrumented binary (pgo1)...\n")
        cmd_build("pgo1")
        print("\n\nStep 2/3: Collecting profile data...\n")
        cmd_profile()
        print("\n\nStep 3/3: Building optimized binary (pgo2)...\n")
        cmd_build("pgo2")
        return

    check_source_prerequisites()
    env = make_env()

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / ".build_command").write_text(
        " ".join(sys.argv), encoding="utf-8"
    )

    if target == "pgo2":
        profdata_src = _resolve_profdata()
        profdata_dst = CHROMIUM_SRC / "chrome" / "build" / "pgo_profiles" / "custom_profile.profdata"
        profdata_dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(profdata_src, profdata_dst)
        print(f"  Copied profile.profdata to {profdata_dst.relative_to(CHROMIUM_SRC)}")

    print(f"Target: {target}")
    write_args_gn(target)
    gn_gen(env)

    cpus = logical_cpu_count()
    jobs = cpus
    print(f"\nBuilding CEF + cefclient (Release x64, {cpus} logical CPUs)...")
    print(f"  Output: {OUT_DIR}\n")

    while jobs >= 1:
        rc = ninja_build(env, jobs)
        if rc == 0:
            break
        if has_oom_errors() and jobs > 1:
            jobs = max(1, jobs // 2)
            print(f"\n  OOM detected, retrying with -j{jobs}...\n")
            continue
        print(f"\nERROR: Build failed (exit code {rc}).")
        sys.exit(rc)

    exe = OUT_DIR / "cefclient.exe"
    print(f"\nBuild complete: {OUT_DIR}")
    if exe.exists():
        print(f"  cefclient: {exe}")
    if target == "pgo1":
        print(f"\nNext steps:")
        print(f"  1. Set LLVM_PROFILE_FILE=C:\\path\\to\\profiles\\default-%2m.profraw")
        print(f"  2. Run {exe} and exercise diverse workloads")
        print(f"  3. Merge profiles:")
        llvm_profdata = CHROMIUM_SRC / "third_party/llvm-build/Release+Asserts/bin/llvm-profdata.exe"
        print(f"       {llvm_profdata} merge -o {OUT_DIR / 'profile.profdata'} *.profraw")
        print(f"  4. Build with: cef.py.cmd build pgo2")

    # Link vendor/cef/ to the local build output so VS can find everything.
    print()
    _link_local_build()
    _build_wrapper("source")
    INSTALLED_VERSION_FILE.parent.mkdir(parents=True, exist_ok=True)
    INSTALLED_VERSION_FILE.write_text("local:source\n", encoding="utf-8")


def _link_local_build():
    """Populate vendor/cef/ with junctions to the local source build output.

    After `cef.py.cmd build`, the build output lives in vendor/cefs/chromium/src/out/Release_GN_x64/.
    VS project files reference vendor/cef/ exclusively, so we create junctions there:

      vendor/cef/Release/       → junction to out/Release_GN_x64/
      vendor/cef/include/       → junction to cef/include/
      vendor/cef/libcef_dll/    → junction to cef/libcef_dll/
      vendor/cef/build/         → real dir for wrapper libs (built by _build_wrapper)

    Also copies generated cef_config.h into include/ (since the junction points
    at the source include/ which lacks the generated file).
    """
    if not OUT_DIR.exists():
        print("WARNING: Build output not found, skipping vendor/cef/ link.")
        return

    # Remove old vendor/cef/ if it's not already our junction layout.
    if CEF_DIR.exists():
        # Check if Release/ is already a junction to our OUT_DIR.
        release_link = CEF_DIR / "Release"
        if release_link.exists() and release_link.is_junction():
            existing_target = os.readlink(release_link)
            if Path(existing_target).resolve() == OUT_DIR.resolve():
                print(f"vendor/cef/ already linked to local build, skipping.")
                # Still copy cef_config.h in case it was regenerated.
                _copy_generated_cef_config()
                return

        # Wipe it — could be an old official/GCS setup.
        print("Removing old vendor/cef/...")
        _remove_cef_dir()

    CEF_DIR.mkdir(parents=True)

    # Release/ → build output (DLLs, EXEs, PAKs, locales, etc.)
    _junction(CEF_DIR / "Release", OUT_DIR)
    print(f"  Release/ → {OUT_DIR.relative_to(ROOT)}")

    # Source builds name it libcef.dll.lib; VS expects libcef.lib.
    dll_lib = OUT_DIR / "libcef.dll.lib"
    normalized_lib = OUT_DIR / "libcef.lib"
    if dll_lib.exists() and not normalized_lib.exists():
        shutil.copy2(dll_lib, normalized_lib)
        print(f"  Copied libcef.dll.lib → libcef.lib")

    # include/ → CEF headers from source tree
    if CEF_SRC.exists():
        _junction(CEF_DIR / "include", CEF_SRC / "include")
        print(f"  include/ → cef/include/")

        # libcef_dll/ → wrapper source from source tree
        _junction(CEF_DIR / "libcef_dll", CEF_SRC / "libcef_dll")
        print(f"  libcef_dll/ → cef/libcef_dll/")

        # Copy .gypi files for wrapper build.
        for gypi_name in ("cef_paths.gypi", "cef_paths2.gypi"):
            gypi = CEF_SRC / gypi_name
            if gypi.exists():
                shutil.copy2(gypi, CEF_DIR / gypi_name)

    # Copy generated cef_config.h into include/ (overlay on the junction).
    _copy_generated_cef_config()

    # build/ directory for wrapper libs (will be populated by _build_wrapper).
    (CEF_DIR / "build").mkdir(exist_ok=True)

    print(f"\nvendor/cef/ linked to local source build.")


def _copy_generated_cef_config():
    """Copy generated cef_config.h from build output into vendor/cef/include/."""
    gen_config = OUT_DIR / "gen" / "cef" / "include" / "cef_config.h"
    if gen_config.exists():
        dest = CEF_DIR / "include" / "cef_config.h"
        if dest.parent.exists():
            shutil.copy2(gen_config, dest)


def cmd_clean():
    if OUT_DIR.exists():
        print(f"Deleting {OUT_DIR}...")
        shutil.rmtree(OUT_DIR, ignore_errors=True)
        print("Done.")
    else:
        print("Nothing to clean.")


# ══════════════════════════════════════════════════════════════════════════════
#  Copy build (ported from cefs_build.cmd)
# ══════════════════════════════════════════════════════════════════════════════

def _git_info(repo_dir: Path) -> tuple[str, str]:
    try:
        result = subprocess.run(
            ["git", "log", "--format=%H%n%s", "-1"],
            cwd=repo_dir, capture_output=True, text=True, timeout=10,
        )
        if result.returncode == 0:
            lines = result.stdout.strip().split("\n", 1)
            return lines[0], lines[1] if len(lines) > 1 else ""
    except Exception:
        pass
    return "unknown", ""


def _read_chromium_version() -> str:
    version_file = CHROMIUM_SRC / "chrome" / "VERSION"
    if not version_file.exists():
        return "unknown"
    parts = {}
    for line in version_file.read_text("utf-8").splitlines():
        if "=" in line:
            k, v = line.split("=", 1)
            parts[k.strip()] = v.strip()
    return f"{parts.get('MAJOR','?')}.{parts.get('MINOR','?')}.{parts.get('BUILD','?')}.{parts.get('PATCH','?')}"


def _write_build_info(dest: Path):
    from datetime import datetime, timezone

    chromium_hash, chromium_subject = _git_info(CHROMIUM_SRC)
    cef_hash, cef_subject = _git_info(CEF_SRC)
    chromium_version = _read_chromium_version()
    cef_branch = ""
    branch_file = CEFS_DIR / ".branch"
    if branch_file.exists():
        cef_branch = branch_file.read_text("utf-8").strip()

    args_gn_path = dest / "args.gn"
    args_gn_content = args_gn_path.read_text("utf-8") if args_gn_path.exists() else "(not found)"

    build_cmd_file = OUT_DIR / ".build_command"
    build_command = build_cmd_file.read_text("utf-8").strip() if build_cmd_file.exists() else "(unknown)"

    lines = [
        f"Build command:    {build_command}",
        f"Build date:       {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}",
        f"",
        f"Chromium version: {chromium_version}",
        f"Chromium commit:  {chromium_hash}",
        f"                  {chromium_subject}",
        f"",
        f"CEF branch:       {cef_branch}",
        f"CEF commit:       {cef_hash}",
        f"                  {cef_subject}",
        f"",
        f"args.gn:",
        *[f"  {line}" for line in args_gn_content.splitlines()],
        "",
    ]

    info_path = dest / "BUILD_INFO"
    info_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"  BUILD_INFO: written")


def cmd_copy(name: str):
    if not OUT_DIR.exists():
        print("ERROR: Build output not found. Run a build first.")
        sys.exit(1)

    dest = CEF_BUILDS_DIR / name
    if dest.exists():
        print(f"Deleting existing {dest}...")
        shutil.rmtree(dest, ignore_errors=True)
    dest.mkdir(parents=True)

    files = [
        "libcef.dll", "libcef.dll.lib", "chrome_elf.dll",
        "bootstrap.exe", "bootstrapc.exe", "cefclient.exe", "cefclient.dll",
        "d3dcompiler_47.dll", "dxcompiler.dll", "dxil.dll",
        "libEGL.dll", "libGLESv2.dll",
        "v8_context_snapshot.bin", "snapshot_blob.bin",
        "vk_swiftshader.dll", "vk_swiftshader_icd.json", "vulkan-1.dll",
        "chrome_100_percent.pak", "chrome_200_percent.pak",
        "icudtl.dat", "resources.pak",
    ]

    copied = 0
    for f in files:
        src = OUT_DIR / f
        if src.exists():
            shutil.copy2(src, dest / f)
            copied += 1
        else:
            print(f"  WARNING: {f} not found, skipping")
    print(f"  Files: {copied}")

    dll_lib = dest / "libcef.dll.lib"
    if dll_lib.exists():
        dll_lib.rename(dest / "libcef.lib")

    sandbox_lib = OUT_DIR / "obj" / "cef" / "cef_sandbox.lib"
    if sandbox_lib.exists():
        shutil.copy2(sandbox_lib, dest / "cef_sandbox.lib")

    locales_src = OUT_DIR / "locales"
    if locales_src.exists():
        shutil.copytree(locales_src, dest / "locales")
        locale_count = sum(1 for _ in (dest / "locales").iterdir())
        print(f"  Locales: {locale_count}")

    include_src = CEF_SRC / "include"
    if include_src.exists():
        shutil.copytree(include_src, dest / "include")
        print(f"  include/: copied")

    libcef_dll_src = CEF_SRC / "libcef_dll"
    if libcef_dll_src.exists():
        shutil.copytree(libcef_dll_src, dest / "libcef_dll")
        print(f"  libcef_dll/: copied")

    # Copy .gypi files for wrapper build.
    for gypi_name in ("cef_paths.gypi", "cef_paths2.gypi"):
        gypi = CEF_SRC / gypi_name
        if gypi.exists():
            shutil.copy2(gypi, dest / gypi_name)

    # Copy generated cef_config.h.
    gen_config = OUT_DIR / "gen" / "cef" / "include" / "cef_config.h"
    if gen_config.exists():
        config_dest = dest / "include" / "cef_config.h"
        shutil.copy2(gen_config, config_dest)
        print(f"  cef_config.h: copied (generated)")

    args_gn = OUT_DIR / "args.gn"
    if args_gn.exists():
        shutil.copy2(args_gn, dest / "args.gn")

    bench_src = OUT_DIR / "benchmark_results"
    if bench_src.exists() and any(bench_src.iterdir()):
        shutil.copytree(bench_src, dest / "benchmark_results")
        bench_count = sum(1 for _ in (dest / "benchmark_results").iterdir())
        print(f"  benchmark_results/: {bench_count} file(s)")

    _write_build_info(dest)

    launcher = ROOT / "vendor" / "cefclient.bat"
    if launcher.exists():
        shutil.copy2(launcher, dest / "cefclient.bat")

    total_size = sum(f.stat().st_size for f in dest.rglob("*") if f.is_file())
    print(f"\nCopied to {dest} ({total_size / 1024 / 1024:.0f} MB)")

    launcher_dest = dest / "cefclient.bat"
    if launcher_dest.exists():
        rel = launcher_dest.relative_to(ROOT)
        print(f"\nRun:  {rel}")
        print(f"WSL:  {rel.as_posix()}")


# ══════════════════════════════════════════════════════════════════════════════
#  Benchmark (ported from cefs_build.cmd)
# ══════════════════════════════════════════════════════════════════════════════

def _resolve_browser_exe(target: str | None) -> Path:
    if target is None:
        exe = OUT_DIR / "cefclient.exe"
        if not exe.exists():
            print(f"ERROR: cefclient.exe not found at {OUT_DIR}")
            print("Run a build first (e.g. cef.py.cmd build auto).")
            sys.exit(1)
        return exe

    target_path = Path(target).resolve()
    if target_path.suffix.lower() == ".exe":
        if not target_path.exists():
            print(f"ERROR: Browser executable not found: {target}")
            sys.exit(1)
        return target_path

    build_dir = CEF_BUILDS_DIR / target
    if not build_dir.exists():
        print(f"ERROR: Build not found: {build_dir}")
        avail = ", ".join(d.name for d in CEF_BUILDS_DIR.iterdir() if d.is_dir()) if CEF_BUILDS_DIR.exists() else "(none)"
        print(f"Available builds: {avail}")
        sys.exit(1)
    exe = build_dir / "cefclient.exe"
    if not exe.exists():
        print(f"ERROR: cefclient.exe not found in {build_dir}")
        sys.exit(1)
    return exe


def cmd_benchmark(target: str | None):
    check_source_prerequisites()
    browser_exe = _resolve_browser_exe(target)
    env = make_env()
    vpython3 = DEPOT_TOOLS / "vpython3.bat"
    output_dir = Path(tempfile.mkdtemp(prefix="cef_benchmark_"))

    # Use the exe's parent directory as the chromium output directory so
    # telemetry can find chrome.dll/cefclient.dll and helper binaries.
    chromium_output_dir = browser_exe.resolve().parent

    cmd = [
        str(vpython3),
        "tools/perf/run_benchmark", "speedometer3",
        "--browser=exact",
        f"--browser-executable={browser_exe}",
        f"--chromium-output-directory={chromium_output_dir}",
        "--assert-gpu-compositing",
        "--compatibility-mode=no-field-trials",
        f"--output-dir={output_dir}",
        "--pageset-repeat=5",
        "--extra-browser-args=--no-sandbox",
        "-v",
    ]

    print(f"Running Speedometer 3.1 (5 iterations)...")
    print(f"  Browser:    {browser_exe}")
    print(f"  Results:    {output_dir}")
    print(f"  This will launch a browser window — do not close it.\n")

    result = subprocess.run(cmd, cwd=str(CHROMIUM_SRC), env=env, shell=True)
    if result.returncode != 0:
        print(f"\nERROR: Benchmark failed (exit code {result.returncode}).")
        sys.exit(result.returncode)

    metrics = _extract_speedometer_results(output_dir)
    _print_speedometer_results(metrics, output_dir)
    if metrics.get("Score"):
        _save_benchmark_result(metrics, browser_exe, target)


def _extract_speedometer_results(output_dir: Path) -> dict[str, list[float]]:
    artifacts_dir = output_dir / "artifacts"
    if not artifacts_dir.exists():
        return {}
    run_dirs = sorted(artifacts_dir.iterdir())
    if not run_dirs:
        return {}
    results_file = run_dirs[-1] / "_test_results.jsonl"
    if not results_file.exists():
        return {}

    all_metrics: dict[str, list[float]] = {}
    for line in results_file.read_text("utf-8").splitlines():
        if not line.strip():
            continue
        test_result = json.loads(line).get("testResult", {})
        artifacts = test_result.get("outputArtifacts", {})
        measurements_artifact = artifacts.get("measurements.json", {})
        measurements_path = measurements_artifact.get("filePath")
        if not measurements_path or not Path(measurements_path).exists():
            continue
        measurements = json.loads(Path(measurements_path).read_text("utf-8"))
        for name, data in measurements.get("measurements", {}).items():
            if "/" in name or name.startswith("Iteration-") or name == "Geomean":
                continue
            values = data.get("samples", [])
            if values:
                all_metrics.setdefault(name, []).append(
                    sum(values) / len(values))
    return all_metrics


def _print_speedometer_results(metrics: dict[str, list[float]], output_dir: Path):
    scores = metrics.get("Score", [])
    if not scores:
        print("\nWARNING: Could not extract Speedometer score from results.")
        print(f"Check results manually at: {output_dir}")
        return
    mean = sum(scores) / len(scores)
    print(f"\n{'=' * 44}")
    print(f"  Speedometer 3.1 Score: {mean:.2f}")
    if len(scores) > 1:
        scores_str = ", ".join(f"{s:.2f}" for s in scores)
        print(f"  Runs: {scores_str}")
    print(f"{'=' * 44}")


def _get_exe_version(exe: Path) -> str:
    try:
        result = subprocess.run(
            ["powershell.exe", "-Command",
             f"(Get-Item '{exe}').VersionInfo.ProductVersion"],
            capture_output=True, text=True, timeout=10,
        )
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip()
    except Exception:
        pass
    return "unknown"


def _save_benchmark_result(metrics: dict[str, list[float]],
                           browser_exe: Path, target: str | None):
    from datetime import datetime, timezone

    version = _get_exe_version(browser_exe)

    if target is None:
        results_dir = OUT_DIR / "benchmark_results"
    elif Path(target).suffix.lower() == ".exe":
        results_dir = (ROOT / "vendor" / "cef_external_benchmarks"
                       / version / "benchmark_results")
    else:
        results_dir = CEF_BUILDS_DIR / target / "benchmark_results"

    try:
        results_dir.mkdir(parents=True, exist_ok=True)
    except OSError:
        results_dir = Path(tempfile.mkdtemp(prefix="cef_benchmark_results_"))
        print(f"  No write access, saving to {results_dir}")

    now = datetime.now(timezone.utc)
    timestamp = now.strftime("%Y%m%d_%H%M%S")
    scores = metrics["Score"]
    mean = sum(scores) / len(scores)
    scores_str = ", ".join(f"{s:.2f}" for s in scores)

    lines = [
        f"Speedometer 3.1",
        f"Date:    {now.strftime('%Y-%m-%d %H:%M UTC')}",
        f"Browser: {browser_exe}",
        f"Version: {version}",
        f"Score:   {mean:.2f}",
        f"Runs:    {scores_str}",
        f"",
    ]

    suites = {k: v for k, v in metrics.items() if k != "Score"}
    if suites:
        width = max(len(k) for k in suites)
        lines.append("Suites:")
        for name in sorted(suites):
            values = suites[name]
            avg = sum(values) / len(values)
            lines.append(f"  {name:<{width}}  {avg:>8.2f}")
        lines.append("")

    result_file = results_dir / f"speedometer_{timestamp}.txt"
    result_file.write_text("\n".join(lines), encoding="utf-8")
    try:
        rel = result_file.relative_to(ROOT)
        print(f"  Saved to {rel}")
        print(f"  WSL:     {rel.as_posix()}")
    except ValueError:
        print(f"  Saved to {result_file}")


# ══════════════════════════════════════════════════════════════════════════════
#  Profile (ported from cefs_build.cmd)
# ══════════════════════════════════════════════════════════════════════════════

def _apply_benchmark_patches():
    patches = []

    js2 = CHROMIUM_SRC / "tools" / "perf" / "page_sets" / "jetstream2_pages.py"
    if js2.exists():
        original = js2.read_text()
        patched = re.sub(r"timeout=\d+\)", "timeout=15)", original)
        if patched != original:
            restored = re.sub(r"timeout=\d+\)", "timeout=5)", original)
            js2.write_text(patched)
            patches.append((js2, restored))
            print(f"  Patched jetstream2_pages.py timeout -> 15s")

    sp3 = CHROMIUM_SRC / "tools" / "perf" / "page_sets" / "speedometer3_pages.py"
    if sp3.exists():
        original = sp3.read_text()
        patched = original
        patched = re.sub(r"DEFAULT_ITERATIONS\s*=\s*\d+",
                         "DEFAULT_ITERATIONS = 3", patched)
        patched = re.sub(r"timeout=1800\b", "timeout=3600", patched)
        if patched != original:
            sp3.write_text(patched)
            patches.append((sp3, original))
            print(f"  Patched speedometer3_pages.py: iterations 10->3, timeout 1800->3600s")

    return patches


def _restore_benchmark_patches(patches):
    for path, original in patches:
        path.write_text(original)


def _prune_wpr_json(json_name: str) -> int:
    json_path = WPR_DATA_DIR / json_name
    if not json_path.exists():
        return 0
    try:
        data = json.loads(json_path.read_text())
    except (json.JSONDecodeError, OSError):
        return 0

    archives = data.get("archives", {})
    pruned = {}
    local_wprgo = set()
    for story_name, platforms in archives.items():
        wprgo_name = None
        for v in platforms.values():
            if isinstance(v, str) and v.endswith(".wprgo"):
                wprgo_name = v
                break
        if not wprgo_name:
            continue
        wprgo_path = WPR_DATA_DIR / wprgo_name
        if not wprgo_path.exists():
            continue
        pruned[story_name] = platforms
        local_wprgo.add(wprgo_name)

    if len(pruned) < len(archives):
        data["archives"] = pruned
        json_path.write_text(json.dumps(data, indent=4, sort_keys=True) + "\n")

    for wprgo_name in local_wprgo:
        sha1 = WPR_DATA_DIR / (wprgo_name + ".sha1")
        if sha1.exists():
            sha1.unlink()

    return len(pruned)


def _get_wpr_id() -> str | None:
    if WPR_ID_FILE.exists():
        return WPR_ID_FILE.read_text().strip()
    return None


def _wpr_dir(wpr_id: str) -> Path:
    return CEF_WPR_DIR / wpr_id


def _ensure_wpr_downloaded():
    wpr_id = _get_wpr_id()
    if not wpr_id:
        print("  WARNING: vendor/WPR_ID not found. No WPR archives to download.")
        print("  Run  cef.py.cmd record  to create WPR archives.")
        return

    wpr_dir = _wpr_dir(wpr_id)
    if wpr_dir.exists():
        return

    print(f"  WPR archives not found locally ({wpr_id})")
    print(f"  Downloading from GCS...")

    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        print("ERROR: gcloud CLI not found. Cannot download WPR archives.")
        sys.exit(1)

    zip_path = CEF_WPR_DIR / f"{wpr_id}.zip"
    gcs_src = f"{GCS_WPR_BUCKET}/{wpr_id}.zip"

    CEF_WPR_DIR.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(
        [gcloud, "storage", "cp", gcs_src, str(zip_path)],
    )
    if result.returncode != 0:
        print(f"ERROR: Failed to download {gcs_src}")
        sys.exit(result.returncode)

    print(f"  Extracting {zip_path.name}...")
    with zipfile.ZipFile(zip_path, "r") as zf:
        zf.extractall(CEF_WPR_DIR)

    if not wpr_dir.exists():
        print(f"ERROR: Extraction did not produce expected directory: {wpr_dir}")
        sys.exit(1)

    zip_path.unlink()
    size_mb = sum(f.stat().st_size for f in wpr_dir.rglob("*") if f.is_file()) / 1024 / 1024
    print(f"  WPR archives ready ({size_mb:.0f} MB)")


def _generate_profdata_id() -> str:
    """Generate a short human-readable ID for a new profile: YYYYMMDD-HHMM."""
    from datetime import datetime, timezone
    return datetime.now(timezone.utc).strftime("%Y%m%d-%H%M")


def _write_profdata_build_info(dest_dir: Path):
    """Write BUILD_INFO metadata alongside a cached profile."""
    from datetime import datetime, timezone
    chromium_version = _read_chromium_version()
    cef_hash, cef_subject = _git_info(CEF_SRC)
    cef_branch = ""
    branch_file = CEFS_DIR / ".branch"
    if branch_file.exists():
        cef_branch = branch_file.read_text("utf-8").strip()

    pgo1_args = dict(BASE_ARGS)
    pgo1_args.update(TARGET_ARGS["pgo1"])
    args_str = "\n".join(f"  {k}={v}" for k, v in sorted(pgo1_args.items()))

    lines = [
        f"Profile date:     {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}",
        f"Chromium version: {chromium_version}",
        f"CEF branch:       {cef_branch}",
        f"CEF commit:       {cef_hash}",
        f"                  {cef_subject}",
        f"",
        f"pgo1 args.gn:",
        args_str,
        "",
    ]
    (dest_dir / "BUILD_INFO").write_text("\n".join(lines), encoding="utf-8")


def _cache_and_upload_profdata(profdata: Path):
    """Copy profile.profdata to vendor/cef_profdata/<id>/ and upload to GCS."""
    prof_id = _generate_profdata_id()
    cache_dir = PROFDATA_DIR / prof_id
    cache_dir.mkdir(parents=True, exist_ok=True)

    _write_profdata_build_info(cache_dir)
    dest = cache_dir / "profile.profdata"
    shutil.copy2(profdata, dest)
    size_mb = dest.stat().st_size / 1024 / 1024
    print(f"\nCached profile: {cache_dir.relative_to(ROOT)} ({size_mb:.0f} MB)")
    print(f"  ID: {prof_id}")

    # Update PROFDATA_ID.
    PROFDATA_ID_FILE.write_text(prof_id + "\n", encoding="utf-8")

    # Upload to GCS.
    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        print("WARNING: gcloud not found, skipping upload.")
        return

    for fname in ("profile.profdata", "BUILD_INFO"):
        src = cache_dir / fname
        if src.exists():
            gcs_dest = f"{GCS_PROFDATA_BUCKET}/{prof_id}/{fname}"
            result = subprocess.run(
                [gcloud, "storage", "cp", str(src), gcs_dest],
            )
            if result.returncode != 0:
                print(f"WARNING: Failed to upload {fname}.")
                return
    print(f"  Uploaded to {GCS_PROFDATA_BUCKET}/{prof_id}/")


def _resolve_profdata() -> Path:
    """Find or download profile.profdata for the current pgo2 build.

    Check order:
      1. out/Release_GN_x64/profile.profdata (just-collected in this session)
      2. vendor/cef_profdata/<id>/profile.profdata (local cache, ID from PROFDATA_ID)
      3. GCS download to local cache

    Returns the path to the profdata file, or exits on failure.
    """
    # 1. Fresh profile in build output.
    local = OUT_DIR / "profile.profdata"
    if local.exists():
        return local

    # 2. Read PROFDATA_ID.
    if not PROFDATA_ID_FILE.exists():
        print("ERROR: No profile available.")
        print("  vendor/PROFDATA_ID not found and no profile.profdata in build output.")
        print("Run  cef.py.cmd profile  (or  cef.py.cmd build fullpgo) to collect one.")
        sys.exit(1)

    prof_id = PROFDATA_ID_FILE.read_text("utf-8").strip()
    if not prof_id:
        print("ERROR: vendor/PROFDATA_ID is empty.")
        sys.exit(1)

    cached = PROFDATA_DIR / prof_id / "profile.profdata"
    if cached.exists():
        size_mb = cached.stat().st_size / 1024 / 1024
        print(f"  Using cached profile: {prof_id} ({size_mb:.0f} MB)")
        return cached

    # 3. Download from GCS.
    print(f"  Profile '{prof_id}' not found locally, downloading from GCS...")
    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        print("ERROR: gcloud not found and no local profile available.")
        sys.exit(1)

    if not ensure_kagi_gcs_auth():
        sys.exit(1)

    cached.parent.mkdir(parents=True, exist_ok=True)
    for fname in ("profile.profdata", "BUILD_INFO"):
        gcs_src = f"{GCS_PROFDATA_BUCKET}/{prof_id}/{fname}"
        result = subprocess.run(
            [gcloud, "storage", "cp", gcs_src, str(cached.parent / fname)],
            capture_output=True, text=True,
        )
        if result.returncode != 0 and fname == "profile.profdata":
            cached.unlink(missing_ok=True)
            print(f"ERROR: Profile '{prof_id}' not found in GCS.")
            print("Run  cef.py.cmd profile  to collect one, or update vendor/PROFDATA_ID.")
            sys.exit(1)

    size_mb = cached.stat().st_size / 1024 / 1024
    print(f"  Downloaded profile: {prof_id} ({size_mb:.0f} MB)")
    return cached


def cmd_profile():
    if not ensure_kagi_gcs_auth():
        sys.exit(1)
    check_source_prerequisites()

    cefclient = OUT_DIR / "cefclient.exe"
    if not cefclient.exists():
        print(f"ERROR: cefclient.exe not found at {OUT_DIR}")
        print("Run  cef.py.cmd build pgo1  first.")
        sys.exit(1)

    env = make_env()
    _ensure_wpr_downloaded()

    restored = _restore_wpr()
    if restored:
        print(f"  Restored {restored} cached WPR archive(s)")

    script_src = ROOT / "scripts" / "cef_generate_profile.py"
    script_dest = CHROMIUM_SRC / "tools" / "pgo" / "cef_generate_profile.py"
    if not script_src.exists():
        print(f"ERROR: {script_src} not found.")
        sys.exit(1)
    shutil.copy2(script_src, script_dest)
    print(f"  Copied cef_generate_profile.py to chromium tree")

    llvm_profdata = CHROMIUM_SRC / "third_party" / "llvm-build" / "Release+Asserts" / "bin" / "llvm-profdata.exe"
    if not llvm_profdata.exists():
        print("  Downloading llvm-profdata (coverage_tools)...")
        update_py = CHROMIUM_SRC / "tools" / "clang" / "scripts" / "update.py"
        result = subprocess.run(
            f'python "{update_py}" --package=coverage_tools',
            cwd=str(CHROMIUM_SRC), env=env, shell=True,
        )
        if result.returncode != 0:
            print("ERROR: Failed to download coverage_tools.")
            sys.exit(result.returncode)

    has_jetstream2 = any(WPR_DATA_DIR.glob("jetstream2_*.wprgo"))
    system_health_stories = _prune_wpr_json("system_health_desktop.json")
    rendering_stories = _prune_wpr_json("rendering_desktop.json")

    vpython3 = DEPOT_TOOLS / "vpython3.bat"
    benchmarks = ["Speedometer 3"]
    flags = []

    if has_jetstream2:
        benchmarks.append("JetStream 2")
    else:
        flags.append("--speedometer-only")

    if system_health_stories > 0 or rendering_stories > 0:
        if system_health_stories > 0:
            benchmarks.append(f"system_health ({system_health_stories} stories)")
        if rendering_stories > 0:
            benchmarks.append(f"rendering ({rendering_stories} stories)")
    else:
        flags.append("--run-public-benchmarks-only")

    cmd = (f'"{vpython3}" "{script_dest}" '
           f'-C "{OUT_DIR}" '
           f'{" ".join(flags)} -v'.strip())

    benchmarks_str = ", ".join(benchmarks)
    if not has_jetstream2:
        benchmarks_str += "  (run 'record jetstream2' to add JetStream 2)"
    if system_health_stories == 0 and rendering_stories == 0:
        benchmarks_str += "  (run 'record' to add system_health + rendering)"

    patches = _apply_benchmark_patches()

    print(f"\nRunning PGO profiling benchmarks...")
    print(f"  Benchmarks: {benchmarks_str}")
    print(f"  Build dir:  {OUT_DIR}")
    print(f"  This will launch cefclient windows — do not close them.\n")

    try:
        result = subprocess.run(cmd, cwd=str(CHROMIUM_SRC), env=env, shell=True)
    finally:
        _restore_benchmark_patches(patches)

    if result.returncode != 0:
        print(f"\nERROR: Profiling failed (exit code {result.returncode}).")
        sys.exit(result.returncode)

    profdata = OUT_DIR / "profile.profdata"
    if not profdata.exists():
        print("ERROR: profile.profdata was not generated.")
        sys.exit(1)

    size_mb = profdata.stat().st_size / 1024 / 1024
    print(f"\nProfile collected: {profdata} ({size_mb:.0f} MB)")

    _cache_and_upload_profdata(profdata)


# ══════════════════════════════════════════════════════════════════════════════
#  Record WPR (ported from cefs_build.cmd)
# ══════════════════════════════════════════════════════════════════════════════

def _check_us_ip():
    try:
        data = json.loads(urllib.request.urlopen(
            "https://ipinfo.io/json", timeout=5).read())
        if data.get("country") == "US":
            return
        country = data.get("country", "unknown")
        print(f"WARNING: Your IP appears to be in {country}, not the US.")
        print("Language auto-detection and cookie/privacy regulations in")
        print("countries outside the US can cause story scripts to fail.")
        print("It is recommended to record WPR files using a US VPN.")
        answer = input("Do you want to continue? (Y/N): ").strip().lower()
        if answer not in ("y", "yes"):
            sys.exit(0)
    except Exception:
        print("WARNING: Could not determine your location automatically.")
        print("It is recommended to record WPR files using a US VPN to avoid")
        print("cookie/privacy popups and language issues that break story scripts.")
        answer = input("Do you want to continue? (Y/N): ").strip().lower()
        if answer not in ("y", "yes"):
            sys.exit(0)


def _restore_wpr():
    wpr_id = _get_wpr_id()
    if not wpr_id:
        return 0
    wpr_dir = _wpr_dir(wpr_id)
    if not wpr_dir.exists():
        return 0

    restored = 0
    for benchmark_dir in wpr_dir.iterdir():
        if not benchmark_dir.is_dir():
            continue
        for f in benchmark_dir.iterdir():
            dest = WPR_DATA_DIR / f.name
            if f.suffix == ".json":
                shutil.copy2(f, dest)
                restored += 1
            elif f.suffix == ".wprgo" and not dest.exists():
                shutil.copy2(f, dest)
                restored += 1
    return restored


def cmd_record(target: str):
    if not ensure_kagi_gcs_auth():
        sys.exit(1)
    _check_us_ip()

    wpr_id = str(uuid.uuid4())
    wpr_dir = _wpr_dir(wpr_id)

    if target == "all":
        wpr_dir.mkdir(parents=True, exist_ok=True)
        for name in RECORD_TARGETS:
            _record_single(name, wpr_dir)
    else:
        old_id = _get_wpr_id()
        if old_id and _wpr_dir(old_id).exists():
            shutil.copytree(_wpr_dir(old_id), wpr_dir)
            print(f"  Copied existing WPR data from {old_id}")
        else:
            wpr_dir.mkdir(parents=True, exist_ok=True)
        _record_single(target, wpr_dir)

    cmd_record_upload(wpr_id)


def _record_single(target: str, wpr_dir: Path):
    benchmark, json_stem = RECORD_TARGETS[target]
    check_source_prerequisites()

    cefclient = OUT_DIR / "cefclient.exe"
    if not cefclient.exists():
        print(f"ERROR: cefclient.exe not found at {OUT_DIR}")
        print("Run  cef.py.cmd build  first (any target works for recording).")
        sys.exit(1)

    env = make_env()
    patches = _apply_benchmark_patches()

    try:
        print(f"\nRecording WPR archive: {target} ({benchmark})")
        print(f"  Browser: {cefclient}")
        print(f"  This will launch cefclient — do not interact with it.\n")

        cmd = (f'vpython3 tools/perf/record_wpr {benchmark} '
               f'--browser=exact '
               f'--browser-executable="{cefclient}"')
        result = subprocess.run(cmd, cwd=str(CHROMIUM_SRC), env=env, shell=True)
    finally:
        _restore_benchmark_patches(patches)

    had_failures = result.returncode != 0
    if had_failures:
        print(f"\n  WARNING: Some stories failed (exit code {result.returncode}).")
        print(f"  WPR archives for successful stories are still usable.\n")

    dest = wpr_dir / target
    if dest.exists():
        shutil.rmtree(dest, ignore_errors=True)
    dest.mkdir(parents=True)

    copied = 0
    json_file = WPR_DATA_DIR / f"{json_stem}.json"
    if json_file.exists():
        try:
            data = json.loads(json_file.read_text())
        except (json.JSONDecodeError, OSError):
            data = {}
        shutil.copy2(json_file, dest / json_file.name)
        copied += 1
        for story_data in data.get("archives", {}).values():
            for wprgo_name in story_data.values():
                if not isinstance(wprgo_name, str) or not wprgo_name.endswith(".wprgo"):
                    continue
                wprgo = WPR_DATA_DIR / wprgo_name
                if wprgo.exists() and not (dest / wprgo.name).exists():
                    shutil.copy2(wprgo, dest / wprgo.name)
                    size_mb = wprgo.stat().st_size / 1024 / 1024
                    print(f"  {wprgo.name} ({size_mb:.1f} MB)")
                    copied += 1

    if copied <= 1:
        print("WARNING: No WPR .wprgo files found after recording.")
    else:
        print(f"\nSaved {copied} files to {dest}")
        rel = dest.relative_to(ROOT)
        print(f"  Path: {rel}")


def cmd_record_upload(wpr_id: str = None):
    if not ensure_kagi_gcs_auth():
        sys.exit(1)

    if wpr_id is None:
        if not CEF_WPR_DIR.exists():
            print("ERROR: No WPR directory found in vendor/cef_wpr/.")
            print("Run  cef.py.cmd record  first.")
            sys.exit(1)
        candidates = []
        for d in CEF_WPR_DIR.iterdir():
            if d.is_dir():
                try:
                    uuid.UUID(d.name)
                    candidates.append(d)
                except ValueError:
                    continue
        if not candidates:
            print("ERROR: No WPR directory found in vendor/cef_wpr/.")
            sys.exit(1)
        candidates.sort(key=lambda d: d.stat().st_mtime, reverse=True)
        wpr_id = candidates[0].name

    wpr_dir = _wpr_dir(wpr_id)
    if not wpr_dir.exists():
        print(f"ERROR: WPR directory not found: {wpr_dir}")
        sys.exit(1)

    zip_path = CEF_WPR_DIR / f"{wpr_id}.zip"
    print(f"\nCreating {zip_path.name}...")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for f in sorted(wpr_dir.rglob("*")):
            if f.is_file():
                arcname = f.relative_to(CEF_WPR_DIR)
                zf.write(f, arcname)
    size_mb = zip_path.stat().st_size / 1024 / 1024
    print(f"  {zip_path.name} ({size_mb:.1f} MB)")

    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    gcs_dest = f"{GCS_WPR_BUCKET}/{wpr_id}.zip"
    print(f"\nUploading to {gcs_dest}...")
    result = subprocess.run(
        [gcloud, "storage", "cp", str(zip_path), gcs_dest],
    )
    if result.returncode != 0:
        print(f"ERROR: Upload failed (exit code {result.returncode}).")
        print(f"You can retry with: cef.py.cmd record_upload")
        sys.exit(result.returncode)

    zip_path.unlink()
    WPR_ID_FILE.write_text(wpr_id + "\n")
    print(f"\nWPR_ID updated: {wpr_id}")
    print(f"  Commit vendor/WPR_ID to track this WPR set in git.")


# ══════════════════════════════════════════════════════════════════════════════
#  Source checkout management (ported from cefs_switch.cmd)
# ══════════════════════════════════════════════════════════════════════════════

def cmd_source_switch(branch: str | None):
    if branch is None:
        cmd_source_status()
        return

    force_clean = "--clean" in sys.argv
    branch_file = CEFS_DIR / ".branch"
    current = branch_file.read_text("utf-8").strip() if branch_file.exists() else ""
    switching = current and current != branch
    first_run = not (CHROMIUM_SRC / "cef").exists()

    if first_run:
        print("=" * 60)
        print("First-time CEF source setup")
        print()
        print("This will download ~30 GB of Chromium + CEF source code.")
        print(f"Target directory: {CEFS_DIR}")
        print("Expect 30-60 minutes on a fast connection.")
        print("=" * 60)
        answer = input("Continue? [y/N] ").strip().lower()
        if answer != "y":
            return

    if switching and not force_clean:
        out_dir = CHROMIUM_SRC / "out"
        if out_dir.exists() and any(out_dir.iterdir()):
            print(f"\nSwitching from branch {current} to {branch}.")
            print(f"Build output exists at: {out_dir}")
            answer = input("Delete build output? (recommended when switching branches) [y/N] ").strip().lower()
            if answer == "y":
                print(f"Cleaning {out_dir}...")
                shutil.rmtree(out_dir)
                print("Done.")

    # Ensure automate-git.py.
    automate_py = CEFS_DIR / "automate-git.py"
    if not automate_py.exists():
        print(f"Downloading automate-git.py...")
        CEFS_DIR.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(AUTOMATE_URL, automate_py)
        print("Done.")

    cmd = [
        sys.executable,
        str(automate_py),
        f"--download-dir={CEFS_DIR}",
        f"--depot-tools-dir={DEPOT_TOOLS}",
        f"--branch={branch}",
        "--x64-build",
        "--no-build",
        "--no-distrib",
        "--no-chromium-history",
    ]
    if force_clean:
        cmd.append("--force-clean")

    print(f"\nSyncing CEF branch {branch}...")
    print(f"  {' '.join(cmd)}\n")

    result = subprocess.run(cmd)
    if result.returncode != 0:
        print(f"\nERROR: automate-git.py failed (exit code {result.returncode}).")
        if not force_clean:
            print("Tip: Try again with --clean to force a clean checkout.")
        sys.exit(result.returncode)

    branch_file.write_text(branch + "\n", encoding="utf-8")
    print(f"\nCEF source synced to branch {branch}.")
    print(f"Run  cef.py.cmd build  to build.")


def cmd_source_status():
    branch_file = CEFS_DIR / ".branch"
    branch = branch_file.read_text("utf-8").strip() if branch_file.exists() else "(none)"
    has_depot = DEPOT_TOOLS.exists()
    has_source = (CHROMIUM_SRC / "cef").exists()
    out_dir = CHROMIUM_SRC / "out"
    has_build = out_dir.exists() and any(out_dir.iterdir()) if out_dir.exists() else False

    print("CEF Source Status")
    print(f"  Branch:       {branch}")
    print(f"  Source dir:   {CEFS_DIR}")
    print(f"  depot_tools:  {'yes' if has_depot else 'no'}")
    print(f"  Source code:  {'yes' if has_source else 'no'}")
    print(f"  Build output: {'yes' if has_build else 'no'}")


# ══════════════════════════════════════════════════════════════════════════════
#  Status display
# ══════════════════════════════════════════════════════════════════════════════

def cmd_status():
    """Show current CEF status."""
    raw = read_version_file()
    if raw:
        kind, value = parse_version(raw)
        print(f"CEF_VERSION:   {kind}:{value}")
    else:
        print(f"CEF_VERSION:   (not set)")

    installed = ""
    if INSTALLED_VERSION_FILE.exists():
        installed = INSTALLED_VERSION_FILE.read_text("utf-8").strip()
    print(f"Installed:     {installed or '(none)'}")

    has_cef = CEF_DIR.exists() and any(CEF_DIR.iterdir()) if CEF_DIR.exists() else False
    print(f"vendor/cef/:   {'populated' if has_cef else 'empty'}")

    has_source = (CHROMIUM_SRC / "cef").exists()
    if has_source:
        branch_file = CEFS_DIR / ".branch"
        branch = branch_file.read_text("utf-8").strip() if branch_file.exists() else "?"
        print(f"Source:        branch {branch}")

    print()
    print(HELP_TEXT)


# ══════════════════════════════════════════════════════════════════════════════
#  Argument parsing + main
# ══════════════════════════════════════════════════════════════════════════════

def main():
    args = sys.argv[1:]

    if not args or args[0] in ("-h", "--help", "help", "/?"):
        cmd_status()
        return

    command = args[0].lower()

    # ── switch ──
    if command == "switch":
        if len(args) < 2:
            cmd_switch_list()
            return
        sub = args[1].lower()
        if sub == "list":
            cmd_switch_list()
        elif sub == "official":
            arg = args[2] if len(args) > 2 else None
            cmd_switch_official(arg)
        elif sub == "source":
            name = args[2] if len(args) > 2 else None
            cmd_switch_source(name)
        else:
            print(f"Unknown switch subcommand: {sub}")
            print("Usage: cef.py.cmd switch official|source|list [args]")
            sys.exit(1)
        return

    # ── setup ──
    if command == "setup":
        cmd_setup()
        return

    # ── symbols ──
    if command == "symbols":
        cmd_symbols()
        return

    # ── upload ──
    if command == "upload":
        name = args[1] if len(args) > 1 else None
        cmd_upload(name)
        return

    # ── source ──
    if command == "source":
        if len(args) < 2:
            cmd_source_status()
            return
        sub = args[1].lower()
        if sub == "switch":
            branch = args[2] if len(args) > 2 else None
            cmd_source_switch(branch)
        elif sub == "status":
            cmd_source_status()
        else:
            print(f"Unknown source subcommand: {sub}")
            sys.exit(1)
        return

    # ── build ──
    if command == "build":
        target = args[1].lower() if len(args) > 1 else "auto"
        all_targets = set(TARGET_ARGS) | {"fullpgo"}
        if target not in all_targets:
            print(f"Unknown target: {target}")
            print(f"Available: {', '.join(sorted(all_targets))}")
            sys.exit(1)
        cmd_build(target)
        return

    # ── copy ──
    if command == "copy":
        name = sys.argv[2] if len(sys.argv) > 2 else None
        if not name:
            print("ERROR: copy requires a name argument.")
            print("Usage: cef.py.cmd copy <name>")
            sys.exit(1)
        cmd_copy(name)
        return

    # ── clean ──
    if command == "clean":
        cmd_clean()
        return

    # ── rebuild ──
    if command == "rebuild":
        target = args[1].lower() if len(args) > 1 else "auto"
        cmd_clean()
        cmd_build(target)
        return

    # ── benchmark ──
    if command == "benchmark":
        bench_target = sys.argv[2] if len(sys.argv) > 2 else None
        cmd_benchmark(bench_target)
        return

    # ── profile ──
    if command == "profile":
        cmd_profile()
        return

    # ── record ──
    if command == "record":
        target = args[1].lower() if len(args) > 1 else "all"
        all_record = set(RECORD_TARGETS) | {"all"}
        if target not in all_record:
            print(f"Unknown record target: {target}")
            print(f"Available: {', '.join(sorted(all_record))}")
            sys.exit(1)
        cmd_record(target)
        return

    # ── record_upload ──
    if command == "record_upload":
        cmd_record_upload()
        return

    # ── Maybe a bare target name (shorthand for build) ──
    all_targets = set(TARGET_ARGS) | {"fullpgo"}
    if command in all_targets:
        cmd_build(command)
        return

    print(f"Unknown command: {command}\n")
    print(HELP_TEXT)
    sys.exit(1)


if __name__ == "__main__":
    main()
