@echo off
setlocal
cd /d %~dp0..
call "%~dp0..\vsenv.bat" || exit /b 1
msbuild Orion.slnx -restore -p:Configuration=Debug -p:Platform=x64 -maxCpuCount || exit /b 1
