@echo off
REM Run client only, no build step - use when server is already running
setlocal
set "REPO_DIR=%~dp0"
set "DOTNET_CLI_HOME=%REPO_DIR%.dotnet-home"

dotnet run --project "%REPO_DIR%Content.Omu.Client" --no-build
pause
endlocal
