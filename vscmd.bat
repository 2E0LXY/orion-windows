setlocal
cd %~dp0
call "%~dp0vsenv.bat" || exit /b 1
start powershell
