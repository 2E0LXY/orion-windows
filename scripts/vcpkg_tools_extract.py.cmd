1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0.. & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Extract vcpkg-tools.json from the vcpkg submodule and apply overrides:
#   - Upgrade CMake (windows/amd64) to a newer version
#   - Remove gsutil (we use gcloud CLI instead)
#
# Usage:
#   scripts\vcpkg_tools_extract.py.cmd

import json
import sys
from pathlib import Path

# ── Overrides ─────────────────────────────────────────────────────────────────

CMAKE_VERSION_AMD64 = "4.1.2"
CMAKE_SHA512_AMD64 = "5d1f8ce7253412daff6db095fc0f8147daa7450f4d0fb57060e44e390de4e47389c49e5886c3e9002c832ee41c9410c6537c5a32e19b2d9103b265a271153541"

REMOVE_TOOLS = {"gsutil"}

# ── Paths ─────────────────────────────────────────────────────────────────────

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "external" / "vcpkg" / "scripts" / "vcpkg-tools.json"
DST = ROOT / "vcpkg-tools.json"


def main():
    if not SRC.exists():
        print(f"ERROR: {SRC.relative_to(ROOT)} not found.")
        sys.exit(1)

    data = json.loads(SRC.read_text("utf-8"))
    tools = data.get("tools", [])

    # Remove unwanted tools.
    tools = [t for t in tools if t.get("name") not in REMOVE_TOOLS]

    # Override CMake windows/amd64.
    for t in tools:
        if (t.get("name") == "cmake"
                and t.get("os") == "windows"
                and t.get("arch") == "amd64"):
            old_ver = t["version"]
            t["version"] = CMAKE_VERSION_AMD64
            t["executable"] = f"cmake-{CMAKE_VERSION_AMD64}-windows-x86_64/bin/cmake.exe"
            t["url"] = f"https://github.com/Kitware/CMake/releases/download/v{CMAKE_VERSION_AMD64}/cmake-{CMAKE_VERSION_AMD64}-windows-x86_64.zip"
            t["sha512"] = CMAKE_SHA512_AMD64
            t["archive"] = f"cmake-{CMAKE_VERSION_AMD64}-windows-x86_64.zip"
            print(f"  cmake windows/amd64: {old_ver} -> {CMAKE_VERSION_AMD64}")

    data["tools"] = tools

    DST.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(f"  Wrote {DST.relative_to(ROOT)}")
    print(f"  {len(tools)} tools ({len(REMOVE_TOOLS)} removed: {', '.join(sorted(REMOVE_TOOLS))})")


if __name__ == "__main__":
    main()
