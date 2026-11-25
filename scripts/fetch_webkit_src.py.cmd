1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0.. & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Puts the pruned WebKit source tarball in vcpkg's downloads directory, where
# ports/webkit-win/portfile.cmake picks it up.  It cannot be fetched from the
# portfile itself: vcpkg builds its own PATH for portfiles, and gcloud is not
# on it.  Failing here is fatal; running vcpkg.cmd by hand skips this step and
# falls back to cloning.

import hashlib
import shutil
import subprocess
import sys
from pathlib import Path

BUCKET = "gs://kagi-orion-windows-artifacts/webkit-src"
DOWNLOADS = Path("external/vcpkg/downloads")


def main() -> int:
    ref = Path("ports/webkit-win/WEBKIT_REF").read_text().strip()
    name = f"webkit-src-{ref}.tar.zst"
    if (DOWNLOADS / name).exists():
        return 0

    gcloud = shutil.which("gcloud.cmd") or shutil.which("gcloud")
    if not gcloud:
        print("gcloud not found.", file=sys.stderr)
        return 1

    temp = DOWNLOADS / "temp"
    temp.mkdir(parents=True, exist_ok=True)
    print(f"Fetching {name}...", flush=True)
    fetch = subprocess.run(
        [gcloud, "storage", "cp",
         f"{BUCKET}/{name}", f"{BUCKET}/{name}.sha256", str(temp)]
    )
    if fetch.returncode != 0:
        print(f"No published tarball for {ref}.", file=sys.stderr)
        return 1

    expected = (temp / f"{name}.sha256").read_text().split()[0]
    with open(temp / name, "rb") as f:
        actual = hashlib.file_digest(f, "sha256").hexdigest()
    if actual != expected:
        print(f"{name} failed its checksum.", file=sys.stderr)
        return 1

    (temp / name).replace(DOWNLOADS / name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
