@echo off
rem Wrapper that runs vcpkg.exe with reproducible VS-bundled tools in PATH.

call "%~dp0scripts\ensure_vcpkg.bat" || exit /b 1

call "%~dp0vsenv.bat" || exit /b 1

if not defined VCPKG_BINARY_SOURCES set "VCPKG_BINARY_SOURCES=clear"

set VCPKG_FORCE_DOWNLOADED_BINARIES=1

"%~dp0external\vcpkg\vcpkg.exe" %* --z-tool-data-file="%~dp0vcpkg-tools.json"
exit /b %errorlevel%
