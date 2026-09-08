@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0test-editmode.ps1" %*
exit /b %ERRORLEVEL%
