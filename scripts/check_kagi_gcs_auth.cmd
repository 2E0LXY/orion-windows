@echo off
REM Check that the user is authenticated to gcloud with a @kagi.com account.
REM Non-interactive — prints instructions and exits with errorlevel 1 on failure.
REM Used as a PreBuildEvent gate before vcpkg tries to download from GCS.

where gcloud >nul 2>nul
if errorlevel 1 (
    echo.
    echo ============================================================
    echo  ERROR: gcloud CLI not found in PATH.
    echo.
    echo  Run install_dev_tools.bat to install it, then restart
    echo  Visual Studio so it picks up the new PATH.
    echo ============================================================
    echo.
    exit /b 1
)

REM Query for an active @kagi.com account.
for /F "usebackq delims=" %%A in (`gcloud auth list --filter^="account~@kagi.com AND status^=ACTIVE" --format^="value(account)" 2^>nul`) do (
    exit /b 0
)

echo.
echo ============================================================
echo  ERROR: No active @kagi.com gcloud account found.
echo.
echo  WebKit binaries are cached in Google Cloud Storage and
echo  require authentication with a @kagi.com account.
echo.
echo  Open a terminal and run:
echo    gcloud auth login
echo  then sign in with your @kagi.com account.
echo ============================================================
echo.
exit /b 1
