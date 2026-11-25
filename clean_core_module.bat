@echo off
cd %~dp0
if exist external\orion-gtk\app\src\core_module\build (
    powershell -c "rm -r -fo external\orion-gtk\app\src\core_module\build"
)
if exist external\orion-gtk-build-version.txt (
    del external\orion-gtk-build-version.txt
)
