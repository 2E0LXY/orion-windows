@echo off
setlocal
cd %~dp0..
call "%~dp0..\vsenv.bat" || exit /b
msbuild Orion.slnx /target:Orion:Rebuild /p:Configuration=Debug /p:BuildProjectReferences=false /p:TreatWarningsAsErrors=true || exit /b
