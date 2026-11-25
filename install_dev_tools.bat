@echo off
setlocal

REM run as admin:
set "params=%*"
cd /d "%~dp0" && ( if exist "%temp%\getadmin.vbs" del "%temp%\getadmin.vbs" ) && fsutil dirty query %systemdrive% 1>nul 2>nul || (  echo Set UAC = CreateObject^("Shell.Application"^) : UAC.ShellExecute "cmd.exe", "/k cd ""%~sdp0"" && ""%~s0"" %params%", "", "runas", 1 >> "%temp%\getadmin.vbs" && "%temp%\getadmin.vbs" && exit /B )

REM set long paths enabled
powershell -Command "New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name 'LongPathsEnabled' -Value 1 -PropertyType DWORD -Force"

REM Check if VS 2026 is already installed via vswhere.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "VSPATH="
if exist "%VSWHERE%" (
    for /F "usebackq delims=" %%I in (`"%VSWHERE%" -version 18.0 -property installationPath`) do set "VSPATH=%%I"
)

if defined VSPATH (
    echo === Adding missing components to existing VS installation ===
    echo   %VSPATH%
    "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\setup.exe" modify --installPath "%VSPATH%" --config "%~dp0.vsconfig" --passive
) else (
    echo === Installing Visual Studio Community 2026 with required workloads ===
    winget install --source winget --id Microsoft.VisualStudio.Community --exact --accept-source-agreements --accept-package-agreements --override "--passive --config %~dp0.vsconfig"
)

REM Install Google Cloud SDK if not already installed.
where gcloud >nul 2>nul
if errorlevel 1 (
    echo.
    echo === Installing Google Cloud SDK ===
    winget install --source winget --id Google.CloudSDK --exact --accept-source-agreements --accept-package-agreements
) else (
    echo.
    echo === Google Cloud SDK already installed, skipping ===
)



echo.
echo === Done ===
echo.
echo Next steps:
echo   1. Restart your terminal to pick up PATH changes
echo   2. Run: gcloud auth login
echo   3. Verify GCS access: scripts\gsutil.cmd ls gs://kagi-orion-windows-artifacts/
echo   4. Run setup_orion_gtk_submodule.bat
echo.
echo   NOTE: Webkit projects exist in the solution, but are by default not generated.
echo   Visual Studio might complain about some projects failing to load, but this is OK.

endlocal
