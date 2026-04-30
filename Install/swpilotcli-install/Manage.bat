@echo off
setlocal
chcp 65001 >nul

rem Elevate to admin if needed.
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "REGASM=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
set "DLL_DEBUG=%~dp0bin\Debug\SwpilotCLIAddin.dll"
set "DLL_RELEASE=%~dp0bin\Release\SwpilotCLIAddin.dll"
set "LOADER_SOURCE=%~dp0lib\WebView2\WebView2Loader.dll"
set "DLL="
set "BUILD="
set "DLL_DIR="
set "LOADER_TARGET="

rem Prefer Release, fallback to Debug.
if exist "%DLL_RELEASE%" (
    set "DLL=%DLL_RELEASE%"
    set "BUILD=Release"
) else (
    if exist "%DLL_DEBUG%" (
        set "DLL=%DLL_DEBUG%"
        set "BUILD=Debug"
    )
)

if not defined DLL (
    echo [ERROR] DLL not found. Build the project first.
    pause
    exit /b 1
)
for %%I in ("%DLL%") do set "DLL_DIR=%%~dpI"
set "LOADER_TARGET=%DLL_DIR%WebView2Loader.dll"

:MENU
cls
echo ================================================
echo   SwpilotCLI Addin Manager
echo ================================================
echo   Build: %BUILD%
echo   DLL  : %DLL%
echo   Loader target: %LOADER_TARGET%
echo ================================================
echo.
echo   1. Register
echo   2. Unregister
echo   3. Reinstall (Unregister + Register)
echo   4. Exit
echo.
set /p choice=Choose (1/2/3/4): 

if "%choice%"=="1" goto INSTALL
if "%choice%"=="2" goto UNINSTALL
if "%choice%"=="3" goto REINSTALL
if "%choice%"=="4" exit /b 0
goto MENU

:INSTALL
echo.
call :ENSURE_LOADER
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Cannot continue because WebView2Loader.dll is not ready.
    pause
    goto MENU
)
"%REGASM%" "%DLL%" /codebase
if %errorlevel% equ 0 (
    echo.
    echo [OK] Register succeeded.
) else (
    echo.
    echo [ERROR] Register failed. code=%errorlevel%
)
echo.
pause
goto MENU

:UNINSTALL
echo.
"%REGASM%" "%DLL%" /unregister
if %errorlevel% equ 0 (
    echo.
    echo [OK] Unregister succeeded.
) else (
    echo.
    echo [ERROR] Unregister failed. code=%errorlevel%
)
echo.
pause
goto MENU

:REINSTALL
echo.
call :ENSURE_LOADER
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Cannot continue because WebView2Loader.dll is not ready.
    pause
    goto MENU
)
echo Unregistering old version...
"%REGASM%" "%DLL%" /unregister
echo Registering new version...
"%REGASM%" "%DLL%" /codebase
if %errorlevel% equ 0 (
    echo.
    echo [OK] Reinstall succeeded.
) else (
    echo.
    echo [ERROR] Reinstall failed. code=%errorlevel%
)
echo.
pause
goto MENU

:ENSURE_LOADER
if exist "%LOADER_TARGET%" (
    echo [OK] WebView2Loader.dll found: %LOADER_TARGET%
    exit /b 0
)
if not exist "%LOADER_SOURCE%" (
    echo [ERROR] Source not found: %LOADER_SOURCE%
    exit /b 1
)
copy /y "%LOADER_SOURCE%" "%LOADER_TARGET%" >nul
if %errorlevel% neq 0 (
    echo [ERROR] Copy failed: %LOADER_SOURCE% ^> %LOADER_TARGET%
    exit /b 1
)
echo [OK] Copied WebView2Loader.dll to: %LOADER_TARGET%
exit /b 0
