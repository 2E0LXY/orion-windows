1>2# : ^
''' vim: set ft=python:
@echo off & cd %~dp0.. & set "VSCMD_VER=" & set "INCLUDE=" & set "EXTERNAL_INCLUDE=" & set "LIB=" & set "LIBPATH=" & call "%~dp0..\vsenv.bat" || exit /b 1 & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

import os
import subprocess
import sys
from pathlib import Path

src = Path("../webkit")
build = src / "WebKitBuild" / "Debug"
options = Path("ports/webkit-win/webkit-options.cmake")
prefix = Path("vcpkg_installed/x64-windows")
tools = Path("external/vcpkg/downloads/tools")

if not (src / "Source" / "WebKit" / "CMakeLists.txt").exists():
    print("WebKit source not found. Clone ../webkit first.", file=sys.stderr)
    sys.exit(1)

# Use the pinned clang-cl from vcpkg-tools.json.
# Clang 21+ has a mangling regression that breaks WebKit:
# https://github.com/llvm/llvm-project/issues/191588
vcpkg = Path("external/vcpkg/vcpkg.exe")
tools_json = Path("vcpkg-tools.json")
r = subprocess.run([str(vcpkg), "fetch", "clang-cl", f"--z-tool-data-file={tools_json.resolve()}"],
                   capture_output=True, text=True)
if r.returncode != 0:
    print("Failed to fetch pinned clang-cl via vcpkg.", file=sys.stderr)
    sys.exit(1)
clang_cl = Path(r.stdout.strip().splitlines()[-1]).resolve()

# Add vcpkg-acquired tools to PATH so cmake can find Ruby, Perl, Gperf.
tool_dirs = []
for pattern in ["perl/*/perl/bin", "python/*", "ruby/*/bin"]:
    matches = sorted(tools.glob(pattern))
    if matches:
        tool_dirs.append(str(matches[0].resolve()))
gperf = tools / "gperf" / "bin"
if gperf.exists():
    tool_dirs.append(str(gperf.resolve()))

env = os.environ.copy()
env["PATH"] = os.pathsep.join([str(clang_cl.parent)] + tool_dirs + [env.get("PATH", "")])

r = subprocess.run([
    "cmake",
    "-C", str(options.resolve()),
    "-S", str(src),
    "-B", str(build),
    "-G", "Ninja",
    f"-DCMAKE_PREFIX_PATH={prefix.resolve() / 'debug'};{prefix.resolve()}",
    "-DCMAKE_BUILD_TYPE=Debug",
], env=env)
if r.returncode != 0:
    sys.exit(r.returncode)

r = subprocess.run(["cmake", "--build", str(build)], env=env)
sys.exit(r.returncode)
