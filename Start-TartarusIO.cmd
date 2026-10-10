@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-TartarusIO.ps1"
if errorlevel 1 (
  echo.
  echo Tartarus IO Companion could not start.
  pause
)
