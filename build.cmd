@echo off
rem Builds health-reminder into public\: build.cmd [-Bump major^|minor^|patch] [-Version x.y.z] [-SkipTests]
rem See build.ps1 for the options.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
