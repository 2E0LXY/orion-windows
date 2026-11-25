@echo off
setlocal
cd /d %~dp0..
git submodule update --init || exit /b
