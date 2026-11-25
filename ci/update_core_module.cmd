@echo off
setlocal
cd /d %~dp0..
call "%~dp0..\noenv_update_core_module.bat" || exit /b
