@echo off
setlocal

cd %~dp0..

set HOOK_FILE=.git\hooks\pre-push

if exist %HOOK_FILE% (
    del %HOOK_FILE%
    echo Pre-push hook removed.
) else (
    echo No pre-push hook found.
)

exit /b 0
