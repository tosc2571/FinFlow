@echo off
REM FinFlow launcher for people who don't want to touch PowerShell/Git/Docker at all.
REM Double-click this file. It fetches finflow.ps1 straight from GitHub and runs it with
REM this .bat's own folder as the install location - no separate file to download, no
REM "right-click -> Run with PowerShell" needed (Windows won't run a .ps1 on double-click,
REM but it will run a .bat). Safe to re-run any time; same update/start/open-browser
REM behavior as finflow.ps1 itself - see README.
setlocal
title FinFlow Launcher

powershell -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Invoke-RestMethod 'https://raw.githubusercontent.com/tosc2571/FinFlow/main/scripts/finflow.ps1'))) -LauncherDir '%~dp0'"

if errorlevel 1 (
    echo.
    echo FinFlow could not be started - see the error above.
    pause
)
