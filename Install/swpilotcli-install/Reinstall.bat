@echo off
setlocal
chcp 65001 >nul

rem Elevate to admin if needed.
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs -Wait"
    exit /b
)

set "REGASM=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
set "DLL=%~dp0bin\Release\SwpilotCLIAddin.dll"

echo ================================================
echo   SwpilotCLI Reinstall
echo ================================================
echo   DLL: %DLL%
echo ================================================
echo.

if not exist "%DLL%" (
    echo [ERROR] DLL not found. Run the build step first.
    pause
    exit /b 1
)

echo Unregistering old version...
"%REGASM%" "%DLL%" /unregister
echo.
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
