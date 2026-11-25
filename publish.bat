@echo off
setlocal
cd %~dp0
call "%~dp0vsenv.bat" || (echo ERROR: vsenv.bat failed & exit /b 1)

set /p VERSION=<VERSION
:: msbuild inherits environment variables as properties; a variable named
:: OUTDIR would override every vcxproj's OutDir
set PUBLISH_DIR=publish\orion-win-%VERSION%
set TFM=net10.0-windows10.0.26100.0
set RID=win-x64
set PUB_SRC=Orion\bin\x64\Release\%TFM%\%RID%\publish

:: 1. NUKE stale build artifacts
if exist Orion\bin rmdir /s /q Orion\bin || (echo ERROR: rmdir failed & exit /b 1)
if exist Orion\obj rmdir /s /q Orion\obj || (echo ERROR: rmdir failed & exit /b 1)
if exist UnitTest\bin rmdir /s /q UnitTest\bin
if exist UnitTest\obj rmdir /s /q UnitTest\obj
if exist UnitTest\publish rmdir /s /q UnitTest\publish
if exist %PUBLISH_DIR% rmdir /s /q %PUBLISH_DIR% || (echo ERROR: rmdir failed & exit /b 1)

:: 2. Run unit tests
echo.
echo === Running unit tests ===
dotnet test UnitTest --nologo -v quiet || (echo ERROR: Unit tests failed & exit /b 1)

:: 3. Publish — one Orion.exe; both engines under engines\
echo.
echo === Publishing Release ===
msbuild Orion.slnx /t:Restore /p:Configuration=Release /p:Platform=x64 || (echo ERROR: Restore failed & exit /b 1)
msbuild Orion.slnx /t:Publish /p:Configuration=Release /p:Platform=x64 /p:SelfContained=true /p:RuntimeIdentifier=%RID% || (echo ERROR: Publish failed & exit /b 1)

:: 4. Copy payload to output
echo   payload: %PUB_SRC%
robocopy "%PUB_SRC%" %PUBLISH_DIR% /e /nfl /ndl /njh /njs /nc /ns
if %errorlevel% geq 8 (echo ERROR: copy failed & exit /b 1)

:: 5. Verify the layout (engine subdirs complete, no engine binary at the root)
call scripts\check_publish_layout.py.cmd %PUBLISH_DIR% || (echo ERROR: layout check failed & exit /b 1)

:: 6. Create zip archive
echo.
echo Creating zip archive...
if exist %PUBLISH_DIR%.zip del %PUBLISH_DIR%.zip
pushd publish
tar -a -cf "orion-win-%VERSION%.zip" "orion-win-%VERSION%"
popd
if not exist %PUBLISH_DIR%.zip (echo ERROR: Zip failed & exit /b 1)

echo.
echo SUCCESS: %PUBLISH_DIR%.zip
