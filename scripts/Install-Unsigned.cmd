@echo off
setlocal EnableExtensions EnableDelayedExpansion

if /I "%~1"=="--help" goto :help
if /I "%~1"=="-h" goto :help
if /I "%~1"=="/?" goto :help

set "SCRIPT_DIR=%~dp0"
set "PACKAGE=%~1"

if not defined PACKAGE (
    set "FOUND_COUNT=0"
    for %%F in ("%SCRIPT_DIR%CmdPalDockPlus-*.msixbundle") do (
        if exist "%%~fF" (
            set /a FOUND_COUNT+=1
            set "PACKAGE=%%~fF"
        )
    )
    if not "!FOUND_COUNT!"=="1" (
        echo Expected exactly one CmdPalDockPlus-*.msixbundle next to this installer; found !FOUND_COUNT!.
        exit /b 2
    )
) else (
    for %%F in ("%PACKAGE%") do set "PACKAGE=%%~fF"
)

if not exist "%PACKAGE%" (
    echo Package not found: %PACKAGE%
    exit /b 3
)

set "CMDPAL_INSTALLER=%~f0"
set "CMDPAL_PACKAGE=%PACKAGE%"

net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator elevation...
    powershell.exe -NoProfile -Command "$q=[char]34; $cmd=$q+$env:CMDPAL_INSTALLER+$q+' '+$q+$env:CMDPAL_PACKAGE+$q; $p=Start-Process -FilePath $env:ComSpec -Verb RunAs -Wait -PassThru -ArgumentList @('/d','/c',$cmd); exit $p.ExitCode"
    exit /b !errorlevel!
)

echo Installing unsigned CmdPal Dock Plus package:
echo   %PACKAGE%
powershell.exe -NoProfile -Command "Add-AppxPackage -Path $env:CMDPAL_PACKAGE -AllowUnsigned"
if errorlevel 1 (
    echo Installation failed with exit code !errorlevel!.
    exit /b !errorlevel!
)

echo Installation completed. Restart PowerToys Command Palette if the extension is not detected immediately.
exit /b 0

:help
echo CmdPal Dock Plus unsigned MSIX installer
echo.
echo Usage:
echo   Install-Unsigned.cmd [path-to-msixbundle]
echo.
echo If no package path is supplied, exactly one CmdPalDockPlus-*.msixbundle must be next to this file.
echo This bootstrapper uses inline PowerShell commands so PowerShell script execution policy does not need to allow unsigned .ps1 files.
exit /b 0
