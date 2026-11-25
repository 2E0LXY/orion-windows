@echo off
cd %~dp0
REM Non-recursive: orion-gtk's nested submodules (zlib, libuv, libzip, sqlite,
REM pugixml, reflectcpp, etc.) are not needed on Windows — those dependencies
REM are provided by vcpkg via ORION_USE_SYSTEM_DEPS=ON.
git submodule update --init

call "%~dp0noenv_update_core_module.bat" || exit /b