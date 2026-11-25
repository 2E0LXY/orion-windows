1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0.. & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Ensure the user is authenticated to gcloud with a @kagi.com account.
# Used by cefs_build.cmd before profile/fullpgo commands.
#
# Exit codes:
#   0 — authenticated with @kagi.com account
#   1 — not authenticated (user declined or auth failed)

import shutil
import subprocess
import sys


def ensure_kagi_gcs_auth() -> bool:
    """Check gcloud auth and prompt to log in if needed. Returns True if OK."""

    # Check if gcloud is installed.
    gcloud = shutil.which("gcloud") or shutil.which("gcloud.cmd")
    if not gcloud:
        print("ERROR: gcloud CLI not found in PATH.")
        print("Install Google Cloud CLI from:")
        print("  https://docs.cloud.google.com/sdk/docs/install-sdk")
        return False

    # Check if already authenticated with a @kagi.com account.
    account = _get_kagi_account(gcloud)
    if account:
        print(f"  gcloud: authenticated as {account}")
        return True

    # Not authenticated.
    print("PGO profiling requires gcloud authentication with a @kagi.com account")
    print("to download benchmark assets from Google Cloud Storage.")
    answer = input("Do you want to open gcloud authentication? (Y/N): ").strip().lower()
    if answer not in ("y", "yes"):
        return False

    # Run gcloud auth login (opens browser).
    result = subprocess.run([gcloud, "auth", "login"], shell=False)
    if result.returncode != 0:
        print("ERROR: gcloud auth login failed.")
        return False

    # Verify authentication succeeded with a @kagi.com account.
    account = _get_kagi_account(gcloud)
    if account:
        print(f"  gcloud: authenticated as {account}")
        return True

    print("ERROR: Authentication did not result in an active @kagi.com account.")
    return False


def _get_kagi_account(gcloud: str) -> str | None:
    """Return the active @kagi.com account, or None."""
    try:
        output = subprocess.check_output(
            [gcloud, "auth", "list",
             "--filter=account~@kagi.com AND status=ACTIVE",
             "--format=value(account)"],
            text=True, stderr=subprocess.DEVNULL,
        ).strip()
        return output if output else None
    except (subprocess.CalledProcessError, FileNotFoundError):
        return None


if __name__ == "__main__":
    sys.exit(0 if ensure_kagi_gcs_auth() else 1)
