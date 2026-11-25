@echo off
rem Ensure vcpkg.exe exists, bootstrapping if needed.
rem Can be called from any directory — uses SolutionDir or falls back to script location.

if defined SolutionDir (
    set "_VCPKG_ROOT=%SolutionDir%external\vcpkg"
) else (
    set "_VCPKG_ROOT=%~dp0..\external\vcpkg"
)

if exist "%_VCPKG_ROOT%\vcpkg.exe" exit /b 0

echo vcpkg.exe not found, bootstrapping...
call "%_VCPKG_ROOT%\bootstrap-vcpkg.bat" -disableMetrics
if errorlevel 1 (
    echo ERROR: vcpkg bootstrap failed.
    exit /b 1
)
exit /b 0
