@echo off
setlocal
cd %~dp0
call "%~dp0vsenv.bat" || exit /b
if exist external\orion-gtk-build-version.txt del external\orion-gtk-build-version.txt
call "%~dp0noenv_update_core_module.bat" || exit /b
