@echo off
setlocal
set "ROOT=%~dp0"
set "APP_PROJECT=%ROOT%src\WorldSeed.App\WorldSeed.App.csproj"
set "APP_EXECUTABLE=%ROOT%src\WorldSeed.App\bin\Debug\net8.0\WorldSeed.App.exe"

:parse_arguments
if "%~1"=="" goto arguments_valid
if /I "%~1"=="--check" (
  set "CHECK_ONLY=1"
  shift
  goto parse_arguments
)
if /I "%~1"=="--rebuild" (
  set "FORCE_DOCKER_BUILD=1"
  set "FORCE_DESKTOP_BUILD=1"
  shift
  goto parse_arguments
)
if /I "%~1"=="--rebuild-app" (
  set "FORCE_DESKTOP_BUILD=1"
  shift
  goto parse_arguments
)
if /I "%~1"=="--restart" (
  set "RESTART_DESKTOP=1"
  shift
  goto parse_arguments
)
if /I "%~1"=="--update-only" (
  set "UPDATE_ONLY=1"
  shift
  goto parse_arguments
)
if /I "%~1"=="-v" (
  set "VERBOSE=1"
  shift
  goto parse_arguments
)
if /I "%~1"=="--verbose" (
  set "VERBOSE=1"
  shift
  goto parse_arguments
)
echo Usage: RunWorldSeed.bat [--check] [--rebuild] [--rebuild-app] [--restart] [--update-only] [-v^|--verbose]
exit /b 1

:arguments_valid
goto start

:api_is_healthy
powershell -NoProfile -Command "try { $response = Invoke-WebRequest -UseBasicParsing 'http://localhost:5080/health' -TimeoutSec 2; if ($response.Content -match 'healthy') { exit 0 } } catch { } ; exit 1"
exit /b %errorlevel%

:app_is_running
powershell -NoProfile -Command "$path = [IO.Path]::GetFullPath('%APP_EXECUTABLE%'); if (Get-Process -Name WorldSeed.App -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $path }) { exit 0 }; exit 1"
exit /b %errorlevel%

:stop_existing_app
powershell -NoProfile -Command "$path = [IO.Path]::GetFullPath('%APP_EXECUTABLE%'); $processes = Get-Process -Name WorldSeed.App -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $path }; if ($null -eq $processes) { exit 0 }; $processes | Stop-Process -Force; Start-Sleep -Milliseconds 700; if (Get-Process -Name WorldSeed.App -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $path }) { exit 1 }; exit 0"
exit /b %errorlevel%

:desktop_build_required
powershell -NoProfile -Command "$executable = [IO.Path]::GetFullPath('%APP_EXECUTABLE%'); if (-not (Test-Path -LiteralPath $executable)) { exit 0 }; $built = (Get-Item -LiteralPath $executable).LastWriteTimeUtc; $newer = Get-ChildItem -LiteralPath '%ROOT%src' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.Extension -in @('.cs', '.axaml', '.csproj') -and $_.LastWriteTimeUtc -gt $built } | Select-Object -First 1; if ($null -ne $newer) { exit 0 }; exit 1"
exit /b %errorlevel%

:docker_build_required
powershell -NoProfile -Command "$createdText = docker image inspect worldseed-api --format '{{.Created}}' 2>$null; if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($createdText)) { exit 0 }; try { $built = [DateTimeOffset]::Parse($createdText).UtcDateTime } catch { exit 0 }; $appRoot = [IO.Path]::GetFullPath('%ROOT%src\WorldSeed.App') + [IO.Path]::DirectorySeparatorChar; $newerSource = Get-ChildItem -LiteralPath '%ROOT%src' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and -not $_.FullName.StartsWith($appRoot, [StringComparison]::OrdinalIgnoreCase) -and $_.Extension -in @('.cs', '.csproj') -and $_.LastWriteTimeUtc -gt $built } | Select-Object -First 1; $newerDeploy = Get-ChildItem -LiteralPath '%ROOT%deploy' -File | Where-Object { $_.Name -in @('Dockerfile', 'docker-compose.yml') -and $_.LastWriteTimeUtc -gt $built } | Select-Object -First 1; if ($null -ne $newerSource -or $null -ne $newerDeploy) { exit 0 }; exit 1"
exit /b %errorlevel%

:start
if defined CHECK_ONLY goto check
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK is required but the dotnet command was not found.
  echo Install the .NET 8 SDK, then run this launcher again.
  exit /b 1
)

call :app_is_running
if not errorlevel 1 (
  if defined RESTART_DESKTOP (
    if defined VERBOSE echo Stopping the existing WorldSeed desktop application...
    call :stop_existing_app
    if errorlevel 1 (
      echo WorldSeed could not stop the existing desktop application.
      exit /b 1
    )
  ) else (
  echo The WorldSeed desktop application is already running.
  echo Run RunWorldSeed.bat --restart to close only this WorldSeed instance and launch a new one.
  exit /b 0
  )
)

if not defined FORCE_DOCKER_BUILD (
  call :docker_build_required
  if not errorlevel 1 (
    set "FORCE_DOCKER_BUILD=1"
    if defined VERBOSE echo WorldSeed API sources changed; rebuilding the Docker service...
  )
)

if not defined FORCE_DOCKER_BUILD (
  call :api_is_healthy
  if not errorlevel 1 (
    if defined VERBOSE echo Reusing the healthy WorldSeed Docker services.
    goto check
  )
)

if defined FORCE_DOCKER_BUILD (
  if defined VERBOSE echo Rebuilding WorldSeed Docker services by request...
) else (
  if defined VERBOSE echo WorldSeed Docker services are not healthy. Starting or rebuilding them now...
)
if defined VERBOSE (
  call "%ROOT%deploy\worldseed-local.bat"
) else (
  call "%ROOT%deploy\worldseed-local.bat" >nul 2>nul
)
if errorlevel 1 (
  echo.
  echo WorldSeed could not start its local Docker services.
  exit /b 1
)

:check
if defined VERBOSE echo Waiting for the WorldSeed local API...
powershell -NoProfile -Command "$deadline = (Get-Date).AddSeconds(45); do { try { $response = Invoke-WebRequest -UseBasicParsing 'http://localhost:5080/health'; if ($response.Content -match 'healthy') { exit 0 } } catch { } ; Start-Sleep -Seconds 1 } while ((Get-Date) -lt $deadline); exit 1"
if errorlevel 1 (
  echo The Docker services did not become healthy within 45 seconds.
  echo Run deploy\worldseed-local.bat logs to inspect the service output.
  exit /b 1
)

if defined CHECK_ONLY (
  echo WorldSeed local API is healthy.
  exit /b 0
)

if defined VERBOSE echo Starting the WorldSeed desktop application...
if defined VERBOSE echo The application window uses the Docker-backed local database service.
if defined FORCE_DESKTOP_BUILD goto build_desktop
if not exist "%APP_EXECUTABLE%" goto build_desktop
call :desktop_build_required
if not errorlevel 1 (
  if defined VERBOSE echo WorldSeed application sources changed; rebuilding the desktop client...
  goto build_desktop
)
goto launch_desktop

:build_desktop
set "MSBUILDDISABLENODEREUSE=1"
if defined VERBOSE goto build_desktop_verbose
dotnet restore "%APP_PROJECT%" --nologo --disable-parallel -m:1 -nr:false -p:BuildInParallel=false >nul 2>nul
if errorlevel 1 goto build_desktop_failed
dotnet build "%APP_PROJECT%" --nologo --no-restore -m:1 -nr:false -p:BuildInParallel=false -p:UseSharedCompilation=false >nul 2>nul
if errorlevel 1 goto build_desktop_failed
goto build_desktop_complete

:build_desktop_verbose
echo Restoring WorldSeed desktop dependencies with one build worker...
dotnet restore "%APP_PROJECT%" --nologo --disable-parallel -m:1 -nr:false -p:BuildInParallel=false
if errorlevel 1 goto build_desktop_failed
echo Building the WorldSeed desktop application with one build worker...
dotnet build "%APP_PROJECT%" --nologo --no-restore -m:1 -nr:false -p:BuildInParallel=false -p:UseSharedCompilation=false
if errorlevel 1 goto build_desktop_failed
goto build_desktop_complete

:build_desktop_failed
dotnet build-server shutdown >nul 2>nul
echo WorldSeed could not build the desktop application.
exit /b 1

:build_desktop_complete
dotnet build-server shutdown >nul 2>nul

:launch_desktop
if defined UPDATE_ONLY (
  echo WorldSeed is up to date.
  exit /b 0
)
start "" "%APP_EXECUTABLE%"
if errorlevel 1 (
  echo WorldSeed could not open the desktop application.
  exit /b %errorlevel%
)
if defined VERBOSE echo WorldSeed was launched. This terminal can close now.
exit /b 0
