@echo off
REM SPDX-FileCopyrightText: 2026 OmuStation Contributors
REM SPDX-License-Identifier: AGPL-3.0-or-later

setlocal
set "REPO_DIR=%~dp0"
set "DOTNET_EXE=dotnet"
set "CLIENT_PROJECT=%REPO_DIR%Content.Client\Content.Client.csproj"

if exist "%REPO_DIR%.local-tools\dotnet\dotnet.exe" (
    set "DOTNET_EXE=%REPO_DIR%.local-tools\dotnet\dotnet.exe"
)

echo == Verificando errores de compilacion ==
"%DOTNET_EXE%" build "%CLIENT_PROJECT%" --nologo -v q --property WarningLevel=0 /clp:ErrorsOnly
set "BUILD_EXIT=%ERRORLEVEL%"

if "%BUILD_EXIT%"=="0" (
    echo.
    echo Sin errores. Listo para testear.
) else (
    echo.
    echo Build fallo con codigo %BUILD_EXIT%.
)

pause
endlocal
