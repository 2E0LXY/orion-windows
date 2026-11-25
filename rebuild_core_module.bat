@echo off
setlocal
cd %~dp0
call "%~dp0vsenv.bat" || exit /b
call "%~dp0clean_core_module.bat" || exit /b
call "%~dp0noenv_update_core_module.bat" || exit /b
