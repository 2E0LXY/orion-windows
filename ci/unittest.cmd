@echo off
setlocal
cd /d %~dp0..
dotnet test UnitTest -c Release -p:Platform=x64 --logger "trx;LogFileName=results.trx" || exit /b
