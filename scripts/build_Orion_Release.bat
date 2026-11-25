@echo off
setlocal
cd %~dp0..
call "%~dp0..\vsenv.bat" || exit /b 1
msbuild Orion.slnx /target:Build /p:Configuration=Release
