
@echo off
setlocal EnableExtensions DisableDelayedExpansion

echo ==========================================
echo   Apply PR Files from a Specific Commit
echo ==========================================
echo.

REM Verify this is a Git working directory.
git rev-parse --show-toplevel >nul 2>&1
if errorlevel 1 (
    echo ERROR: Run this inside your destination Git repository.
    goto :fail
)

REM Check required tools.
where git >nul 2>&1
if errorlevel 1 (
    echo ERROR: Git is not installed.
    goto :fail
)

where gh >nul 2>&1
if errorlevel 1 (
    echo ERROR: Install GitHub CLI from https://cli.github.com/
    goto :fail
)

gh auth status >nul 2>&1
if errorlevel 1 (
    echo ERROR: Authenticate using gh auth login.
    goto :fail
)

REM Ensure tracked files have no existing modifications.
git diff --quiet
if errorlevel 1 (
    echo ERROR: You have unstaged tracked changes.
    echo Commit or stash them before continuing.
    goto :fail
)

git diff --cached --quiet
if errorlevel 1 (
    echo ERROR: You have staged changes.
    echo Commit or stash them before continuing.
    goto :fail
)

REM Check for existing unresolved conflicts.
git diff --name-only --diff-filter=U | findstr . >nul
if not errorlevel 1 (
    echo ERROR: Resolve existing merge conflicts first.
    goto :fail
)

echo.
set /p "PR_URL=GitHub PR URL: "
if not defined PR_URL goto :fail

set /p "COMMIT=Commit hash to retrieve file versions from: "
if not defined COMMIT goto :fail

echo.
echo Configured Git remotes:
git remote -v
echo.
set /p "PR_REMOTE=Remote containing the PR repository: "
if not defined PR_REMOTE goto :fail

git remote get-url "%PR_REMOTE%" >nul 2>&1
if errorlevel 1 (
    echo ERROR: Remote "%PR_REMOTE%" does not exist.
    goto :fail
)

REM Get the PR number and its current changed-file list.
set "PR_NUMBER="
for /f "delims=" %%N in ('gh pr view "%PR_URL%" --json number --jq ".number"') do set "PR_NUMBER=%%N"

if not defined PR_NUMBER (
    echo ERROR: Could not read PR information.
    goto :fail
)

set "FILES_FILE=%TEMP%\pr_files_%RANDOM%_%RANDOM%.txt"

gh pr view "%PR_URL%" --json files --jq ".files[].path" > "%FILES_FILE%"
if errorlevel 1 (
    echo ERROR: Could not retrieve the PR file list.
    del "%FILES_FILE%" >nul 2>&1
    goto :fail
)

REM Fetch the requested commit from the specified remote.
echo.
echo Fetching commit %COMMIT% from remote %PR_REMOTE%...

git fetch "%PR_REMOTE%" "%COMMIT%"
git cat-file -e "%COMMIT%^{commit}" >nul 2>&1

if errorlevel 1 (
    echo.
    echo Direct fetch did not make the commit available.
    echo Trying the PR head reference...

    git fetch "%PR_REMOTE%" "+refs/pull/%PR_NUMBER%/head:refs/pr/%PR_NUMBER%/head"

    if errorlevel 1 (
        echo ERROR: Could not fetch the PR head.
        del "%FILES_FILE%" >nul 2>&1
        goto :fail
    )

    git cat-file -e "%COMMIT%^{commit}" >nul 2>&1
    if errorlevel 1 (
        echo ERROR: The requested commit is not available.
        echo It may not belong to the fetched PR history.
        del "%FILES_FILE%" >nul 2>&1
        goto :fail
    )
)

echo.
echo Using commit:
git rev-parse --short "%COMMIT%"
echo.
echo Applying file versions...
echo.

set "FAILED=0"

for /f "usebackq delims=" %%F in ("%FILES_FILE%") do call :process_file "%%F"

del "%FILES_FILE%" >nul 2>&1

echo.
if "%FAILED%"=="1" (
    echo Some files failed. Review the errors and git status.
    goto :fail
)

echo ==========================================
echo   All selected files have been staged
echo ==========================================
echo.
git status
echo.
echo Review staged changes:
echo   git diff --cached
echo.
echo Review staged file names:
echo   git diff --cached --name-status
echo.
echo Commit when satisfied:
echo   git commit -m "Apply PR files from specified commit"
echo.
echo No commit or push was performed automatically.
pause
exit /b 0


:process_file
set "FILE=%~1"

echo Processing: %FILE%

REM If the file exists at the specified commit, restore it.
git cat-file -e "%COMMIT%:%FILE%" >nul 2>&1

if not errorlevel 1 (
    git restore --source="%COMMIT%" --staged --worktree -- "%FILE%"
    if errorlevel 1 (
        echo ERROR: Could not restore %FILE%
        set "FAILED=1"
    )
    exit /b 0
)

REM If absent at the commit, remove it if tracked locally.
git ls-files --error-unmatch -- "%FILE%" >nul 2>&1
if not errorlevel 1 (
    git rm -- "%FILE%"
    if errorlevel 1 (
        echo ERROR: Could not remove %FILE%
        set "FAILED=1"
    ) else (
        echo Removed: %FILE%
    )
) else (
    echo Absent at commit and not tracked locally: %FILE%
)

exit /b 0


:fail
echo.
echo Operation stopped. Inspect git status before retrying.
pause
exit /b 1
