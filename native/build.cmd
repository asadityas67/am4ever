@echo off
rem Build uc_host for win-x64. Needs the VS C++ tools and a built Unicorn:
rem   set UNICORN_INCLUDE=<dir containing unicorn\unicorn.h>
rem   set UNICORN_BIN=<dir containing unicorn.dll and unicorn-import.lib>
rem Build Unicorn 2.1.x from source with MSVC Release (x86-only) first; see README.md.
setlocal
cd /d "%~dp0"
if "%UNICORN_INCLUDE%"=="" ( echo set UNICORN_INCLUDE and UNICORN_BIN first ^(see README.md^) & exit /b 1 )
if "%UNICORN_BIN%"=="" ( echo set UNICORN_INCLUDE and UNICORN_BIN first ^(see README.md^) & exit /b 1 )
if "%VSCMD_VER%"=="" call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul 2>nul
if not exist out\win-x64 mkdir out\win-x64
rem plain exe (no /CETCOMPAT) so Unicorn's longjmp works
cl /nologo /O2 /I "%UNICORN_INCLUDE%" uc_host\uc_host.c /Fe:out\win-x64\uc_host.exe /link "%UNICORN_BIN%\unicorn-import.lib" || exit /b 1
copy /y "%UNICORN_BIN%\unicorn.dll" out\win-x64\unicorn.dll >nul
echo built out\win-x64\uc_host.exe
endlocal
