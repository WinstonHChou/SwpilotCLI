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
echo   SwpilotCLI Unregister
echo ================================================
echo   DLL: %DLL%
echo ================================================
echo.

if not exist "%DLL%" (
    echo [ERROR] DLL not found.
    pause
    exit /b 1
)

echo Unregistering...
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
