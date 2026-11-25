@echo off
setlocal enabledelayedexpansion

set VCPKG_FORCE_DOWNLOADED_BINARIES=

cd %~dp0

set SOURCE_DIR=%~dp0external\orion-gtk\app\src\core_module
set BUILD_DIR=%~dp0external\orion-gtk\app\src\core_module\build\core_module
set INSTALL_DIR=%~dp0external\orion-gtk\app\src\core_module\build\install
set VERSION_FILE=%~dp0external\orion-gtk-build-version.txt
REM Full VS environment needed: sqlite.cmake requires nmake + VCToolsInstallDir
call "%~dp0vsenv.bat" || exit /b 1

REM Set VCPKG_ROOT after vsenv.bat — vcvars64 overrides it to the VS-bundled copy
set VCPKG_ROOT=%~dp0external\vcpkg

REM Get current HEAD hash from the submodule
pushd %~dp0external\orion-gtk
for /f %%i in ('git rev-parse HEAD') do set CURRENT_HASH=%%i
popd

if not defined CURRENT_HASH (
    echo ERROR: Failed to get git hash from external\orion-gtk
    exit /b 1
)

set DIRTY=
pushd %~dp0external\orion-gtk
git diff --quiet HEAD || set DIRTY=1
popd

REM Check if version file exists and compare hashes
set NEED_BUILD=1
if defined DIRTY (
    echo orion-gtk has uncommitted changes, reconfiguring...
) else if exist "%VERSION_FILE%" (
    set /p STORED_HASH=<"%VERSION_FILE%"
    if "!STORED_HASH!"=="!CURRENT_HASH!" (
        REM Hash matches, but verify build directory and cache exist
        if not exist "%BUILD_DIR%" (
            echo Build directory missing, rebuilding...
        ) else if not exist "%BUILD_DIR%\CMakeCache.txt" (
            echo CMake cache missing, rebuilding...
        ) else (
            echo orion deps are up to date [!CURRENT_HASH:~0,8!]
            set NEED_BUILD=0
        )
    ) else (
        echo Hash changed: !STORED_HASH:~0,8! -^> !CURRENT_HASH:~0,8!
    )
) else (
    echo No version file found, building...
)

if !NEED_BUILD!==0 (
    powershell -NoProfile -Command "(Get-Item -LiteralPath $env:VERSION_FILE).LastWriteTime = Get-Date"
    exit /b 0
)

REM Seed the project GUIDs from Orion.slnx (see fix_project_uuids.py.cmd):
REM without this CMake hashes the build directory path into each GUID, the
REM generated vcxproj files disagree with the solution, and Visual Studio
REM rewrites Orion.slnx to match them.
set GUID_CACHE=%~dp0external\orion-gtk\app\src\core_module\build\project_guids.cmake
call "%~dp0fix_project_uuids.py.cmd" --write-cmake-cache "%GUID_CACHE%" || exit /b

REM Run cmake configure. --fresh discards any existing CMakeCache.txt: the
REM cache stores absolute find_package/find_library results, and stale hits
REM (e.g. paths from the removed x64-windows-webkit triplet) are never
REM re-searched, breaking reconfigures. We only get here when the submodule
REM hash changed, so a fresh configure costs little. -C is applied after the
REM wipe, so the GUID seeds survive it.
echo Configuring orion deps (common, sync, core_module)...
cmake --fresh -S "%SOURCE_DIR%" -B "%BUILD_DIR%" -G "Visual Studio 18" ^
    -C "%GUID_CACHE%" ^
    -DCMAKE_INSTALL_PREFIX="%INSTALL_DIR%" ^
    -DCMAKE_PREFIX_PATH="%~dp0vcpkg_installed\x64-windows" ^
    -DCMAKE_CXX_FLAGS="/utf-8 /EHsc" ^
    -DCMAKE_C_FLAGS="/utf-8" ^
    -DORION_USE_SYSTEM_DEPS=ON ^
    -DORION_COMMON_BUILD_SHARED=OFF ^
    -DORION_CORE_BUILD_SHARED=OFF ^
    -DORION_SYNC_BUILD_SHARED=OFF

if errorlevel 1 (
    echo ERROR: CMake configuration failed
    exit /b 1
)

REM Fallback GUID sync + stale tlog sweep. Runs right after configure, before
REM the (slow, failure-prone) codegen build below: the vcxproj files exist
REM from this point on and Visual Studio may reload them at any moment, so
REM they must never sit around with GUIDs the solution does not know.
cd /d %~dp0
call "%~dp0fix_project_uuids.py.cmd" || exit /b

REM Generate IDL bindings (webkit_bridge_api.h etc.) now — OrionCodeGen and
REM OrionBackend include the core module DTO headers that pull these in, so the
REM generated headers must exist before the C++ build chain runs.
echo Generating core module IDL bindings (orion_core_codegen)...
cmake --build "%BUILD_DIR%" --target orion_core_codegen
if errorlevel 1 (
    echo ERROR: orion_core_codegen build failed
    exit /b 1
)

cd /d %~dp0

REM Write hash to version file on success
echo %CURRENT_HASH%>"%VERSION_FILE%"
echo orion deps configured successfully [%CURRENT_HASH:~0,8%]

exit /b 0
