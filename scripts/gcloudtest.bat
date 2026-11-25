@echo off
setlocal

call %~dp0gcloudauth\gcloudenv.bat || exit /b

gsutil ls gs://kagi-orion-windows-artifacts/
