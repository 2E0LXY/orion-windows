@echo off
rem Fast path: if gcloud is already on PATH, just check auth
where gcloud >nul 2>nul && where gsutil >nul 2>nul && goto :check_auth

rem Fast path: check default install location
set "GCLOUD_DEFAULT_BIN=%LOCALAPPDATA%\Google\Cloud SDK\google-cloud-sdk\bin"
if exist "%GCLOUD_DEFAULT_BIN%\gcloud.cmd" if exist "%GCLOUD_DEFAULT_BIN%\gsutil.cmd" (
    set "PATH=%GCLOUD_DEFAULT_BIN%;%PATH%"
    goto :check_auth
)

rem Slow path: install and/or authenticate via Python
goto :slow_path

:check_auth
rem Quick file-based auth check (avoids slow gcloud auth list)
if exist "%APPDATA%\gcloud\credentials.db" exit /b 0

:slow_path
set "GCLOUD_BIN="
%~dp0..\..\bin\uv.exe run %~dp0gcloud_storage.py bin-dir > "%TEMP%\gcloud_bin_dir.txt"
if errorlevel 1 (
    echo ERROR: Failed to ensure gcloud SDK is installed and authenticated.
    exit /b 1
)
set /p GCLOUD_BIN=<"%TEMP%\gcloud_bin_dir.txt"
del "%TEMP%\gcloud_bin_dir.txt"
if "%GCLOUD_BIN%"=="" (
    echo ERROR: Failed to get gcloud bin directory.
    exit /b 1
)
set "PATH=%GCLOUD_BIN%;%PATH%"

where gcloud >nul 2>nul
if errorlevel 1 (
    echo ERROR: gcloud not found on PATH.
    exit /b 1
)
where gsutil >nul 2>nul
if errorlevel 1 (
    echo ERROR: gsutil not found on PATH.
    exit /b 1
)
