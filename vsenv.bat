@set /p MSVC_VER=<"%~dp0MSVC_VERSION"

@rem If VS environment is already loaded, verify it matches the pinned version.
@rem Do NOT return early after that: a Developer Prompt defines VSCMD_VER but
@rem has neither our tool order nor scripts\ on PATH, and without scripts\ in
@rem front vcpkg picks the Cloud SDK's gsutil over scripts\gsutil.cmd and talks
@rem to the GCS binary cache anonymously (401 -> every package is a "miss").
@if defined VSCMD_VER (
    if "%VCToolsVersion%" == "%MSVC_VER%" goto :prepend
    echo ERROR: VS environment loaded with VCToolsVersion=%VCToolsVersion%, but MSVC_VERSION requires %MSVC_VER% 1>&2
    exit /b 1
)

path %ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%path%
for /F "usebackq delims=" %%I in (`vswhere.exe -version 18.0 -property installationPath`) do set VSPATH=%%I

call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" -vcvars_ver=%MSVC_VER%

@rem Verify vcvars actually loaded the requested version (it silently falls back).
@if "%VCToolsVersion%" neq "%MSVC_VER%" (
    echo ERROR: vcvars64 loaded VCToolsVersion=%VCToolsVersion%, but MSVC_VERSION requires %MSVC_VER% 1>&2
    exit /b 1
)

:prepend
@rem Once per environment: every build script calls vsenv.bat, and nested
@rem calls (vcpkg.cmd from OrionCoreGen inside an IDE build, ...) would
@rem otherwise keep growing PATH.
@if defined ORION_VSENV_PATH exit /b 0
@set ORION_VSENV_PATH=1

rem vcvars64 *appends* VS-bundled LLVM, CMake, and Ninja to PATH; we *prepend*
rem instead so that we build reproducibly even if stand-alone tools are installed.
rem MSVC (cl.exe) must come before LLVM because CMake's Ninja generator uses
rem NAMES_PER_DIR (CMakeDetermineCompiler.cmake:65): it tries all candidate
rem compiler names per directory before moving to the next, so clang.exe in the
rem LLVM dir would be found before cl.exe if LLVM were first.
path %VCToolsInstallDir%bin\Hostx64\x64;%VSINSTALLDIR%VC\Tools\Llvm\x64\bin;%VSINSTALLDIR%Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin;%VSINSTALLDIR%Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja;%~dp0scripts;%path%
