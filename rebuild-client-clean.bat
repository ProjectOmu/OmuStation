@echo off
REM SPDX-FileCopyrightText: 2026 OmuStation Contributors
REM SPDX-License-Identifier: AGPL-3.0-or-later

setlocal
set "REPO_DIR=%~dp0"
set "DOTNET_CLI_HOME=%REPO_DIR%.dotnet-home"
set "SERVER_BIN_DIR=%REPO_DIR%bin\Content.Server"
set "CLIENT_PROJECT=%REPO_DIR%Content.Client\Content.Client.csproj"
set "DOTNET_EXE=dotnet"

if exist "%REPO_DIR%.local-tools\dotnet\dotnet.exe" (
    set "DOTNET_EXE=%REPO_DIR%.local-tools\dotnet\dotnet.exe"
)

echo == Cerrando procesos Content.Omu.Server y Content.Omu.Client ==
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Process | Where-Object { $_.ProcessName -eq 'Content.Omu.Server' -or $_.ProcessName -eq 'Content.Omu.Client' } | Stop-Process -Force -ErrorAction SilentlyContinue"
timeout /t 3 /nobreak >nul

echo == Recreando %SERVER_BIN_DIR% ==
if exist "%SERVER_BIN_DIR%" (
    rmdir /s /q "%SERVER_BIN_DIR%"
)
mkdir "%SERVER_BIN_DIR%" >nul 2>&1

echo == Compilando cliente ==
"%DOTNET_EXE%" build "%CLIENT_PROJECT%"
set "BUILD_EXIT=%ERRORLEVEL%"

if not "%BUILD_EXIT%"=="0" (
    echo.
    echo Build failed with exit code %BUILD_EXIT%.
    pause
    endlocal
    exit /b %BUILD_EXIT%
)

echo.
echo Build completed successfully.
pause
endlocal
