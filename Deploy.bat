@echo off
REM Double-click this to run deploy.ps1 without having to open PowerShell yourself.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" %*
pause
