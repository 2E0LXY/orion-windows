"""
Google Cloud Storage helper for build scripts.

Ensures gcloud CLI is installed and authenticated, then provides
gsutil operations for downloading build artifacts.

Usage:
    uv run scripts/gcloudauth/gcloud_storage.py download gs://bucket/path local/path
    uv run scripts/gcloudauth/gcloud_storage.py ensure-auth
"""

from __future__ import annotations

import ctypes
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request
from pathlib import Path

GCLOUD_INSTALLER_URL = (
    "https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe"
)
GCLOUD_DEFAULT_DIR = (
    Path(os.environ.get("LOCALAPPDATA", "")) / "Google" / "Cloud SDK" / "google-cloud-sdk"
)
IS_CI = "CI" in os.environ


def _log(msg: str) -> None:
    """Print a status message to stderr (keeps stdout clean for machine-readable output)."""
    print(msg, file=sys.stderr)


def _message_box(title: str, message: str, flags: int = 0x01) -> int:
    """Show a Windows message box. Returns button ID (1=OK, 2=Cancel, 6=Yes, 7=No).
    flags: 0x01 = OK/Cancel, 0x04 = Yes/No, 0x40 = Info icon, 0x30 = Warning icon.
    """
    return ctypes.windll.user32.MessageBoxW(0, message, title, flags)  # type: ignore[union-attr]


def _find_gcloud() -> Path | None:
    """Find gcloud.cmd, checking PATH and the default install location."""
    # Check PATH first
    found = shutil.which("gcloud")
    if found:
        return Path(found)

    # Check default install location
    gcloud_cmd = GCLOUD_DEFAULT_DIR / "bin" / "gcloud.cmd"
    if gcloud_cmd.exists():
        return gcloud_cmd

    return None


def _find_gsutil(gcloud_path: Path | None) -> Path | None:
    """Find gsutil.cmd relative to a known gcloud path, or on PATH."""
    if gcloud_path:
        gsutil_cmd = gcloud_path.parent / "gsutil.cmd"
        if gsutil_cmd.exists():
            return gsutil_cmd

    found = shutil.which("gsutil")
    if found:
        return Path(found)

    return None


def _install_gcloud() -> Path | None:
    """Download and silently install the Google Cloud SDK. Returns gcloud.cmd path."""
    result = _message_box(
        "Google Cloud SDK Required",
        "The Google Cloud SDK is required to download build artifacts.\n\n"
        "This will download and install it to:\n"
        f"{GCLOUD_DEFAULT_DIR}\n\n"
        "Proceed with installation?",
        0x01 | 0x40,  # OK/Cancel + Info icon
    )
    if result != 1:  # Not OK
        _log("Installation cancelled by user.")
        return None

    _log("Downloading Google Cloud SDK installer...")
    with tempfile.TemporaryDirectory() as tmp:
        installer = Path(tmp) / "GoogleCloudSDKInstaller.exe"
        urllib.request.urlretrieve(GCLOUD_INSTALLER_URL, installer)
        _log("Downloaded Google Cloud SDK installer.")

        _log("Installing Google Cloud SDK...")
        # NSIS installer: /S = silent, /D= sets install dir (no quotes, must be last flag)
        install_dir = GCLOUD_DEFAULT_DIR.parent  # "Cloud SDK" dir; installer adds google-cloud-sdk
        proc = subprocess.run(
            [str(installer), "/S", f"/D={install_dir}"],
            check=False,
        )
        if proc.returncode != 0:
            _log(f"ERROR: Installer exited with code {proc.returncode}")
            return None

    gcloud_cmd = GCLOUD_DEFAULT_DIR / "bin" / "gcloud.cmd"
    if gcloud_cmd.exists():
        _log(f"Installed Google Cloud SDK to {GCLOUD_DEFAULT_DIR}")
        return gcloud_cmd

    _log("ERROR: Installation completed but gcloud.cmd not found at expected path.")
    return None


def _is_authenticated(gcloud: Path) -> bool:
    """Check if there is an active gcloud auth account."""
    result = subprocess.run(
        [str(gcloud), "auth", "list", "--filter=status:ACTIVE", "--format=value(account)"],
        capture_output=True,
        text=True,
    )
    return bool(result.stdout.strip())


def _authenticate(gcloud: Path) -> bool:
    """Run gcloud auth login (opens browser) with a 60-second timeout."""
    _log("Authentication required. Opening browser for Google login...")
    try:
        result = subprocess.run([str(gcloud), "auth", "login"], timeout=60, check=False)
        return result.returncode == 0
    except subprocess.TimeoutExpired:
        _log("Authentication timed out after 60 seconds.")
        return False


def ensure_auth() -> tuple[Path, Path]:
    """Ensure gcloud is installed and authenticated. Returns (gcloud_path, gsutil_path)."""
    gcloud = _find_gcloud()

    if gcloud:
        _log(f"Found Google Cloud SDK at {gcloud.parent}")
    else:
        if IS_CI:
            _log("ERROR: Google Cloud SDK is not installed and cannot install in CI.")
            sys.exit(1)
        gcloud = _install_gcloud()
        if not gcloud:
            _log("Google Cloud SDK is required. Exiting.")
            sys.exit(1)

    gsutil = _find_gsutil(gcloud)
    if not gsutil:
        _log("ERROR: gsutil not found alongside gcloud.")
        sys.exit(1)

    _log("Checking authentication...")
    if not _is_authenticated(gcloud):
        if IS_CI:
            _log("ERROR: Not authenticated and cannot authenticate interactively in CI.")
            sys.exit(1)
        if not _authenticate(gcloud):
            _log("ERROR: Authentication failed.")
            sys.exit(1)

    _log("Google Cloud SDK is ready.")
    return gcloud, gsutil


def download(gs_url: str, local_path: str) -> None:
    """Download a file or directory from GCS."""
    _, gsutil = ensure_auth()
    _log(f"Downloading {gs_url} -> {local_path}")
    subprocess.run([str(gsutil), "-m", "cp", "-r", gs_url, local_path], check=True)


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    command = sys.argv[1]

    if command == "bin-dir":
        gcloud, _ = ensure_auth()
        # Print only the bin directory — for batch scripts to capture and add to PATH
        print(gcloud.parent)

    elif command == "ensure-auth":
        gcloud, gsutil = ensure_auth()
        print(f"gcloud: {gcloud}")
        print(f"gsutil: {gsutil}")
        print("Authenticated and ready.")

    elif command == "download":
        if len(sys.argv) < 4:
            _log("Usage: gcloud_storage.py download <gs://url> <local_path>")
            sys.exit(1)
        download(sys.argv[2], sys.argv[3])

    elif command == "ls":
        _, gsutil = ensure_auth()
        gs_url = sys.argv[2] if len(sys.argv) > 2 else ""
        subprocess.run([str(gsutil), "ls", gs_url], check=True)

    else:
        _log(f"Unknown command: {command}")
        sys.exit(1)


if __name__ == "__main__":
    main()
