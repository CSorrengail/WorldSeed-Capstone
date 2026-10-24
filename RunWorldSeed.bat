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
  shift
  goto parse_arguments
)
if /I "%~1"=="--restart" (
  set "RESTART_DESKTOP=1"
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
echo Usage: RunWorldSeed.bat [--check] [--rebuild] [--restart] [-v^|--verbose]
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
  call "%ROOT%deploy\worldseed-local.bat" >nul
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
if defined FORCE_DOCKER_BUILD goto build_desktop
if not exist "%APP_EXECUTABLE%" goto build_desktop
goto launch_desktop

:build_desktop
if defined VERBOSE (
  echo Building the WorldSeed desktop application...
  dotnet build "%APP_PROJECT%" --nologo
) else (
  dotnet build "%APP_PROJECT%" --nologo >nul
)
if errorlevel 1 exit /b %errorlevel%

:launch_desktop
start "" "%APP_EXECUTABLE%"
if errorlevel 1 (
  echo WorldSeed could not open the desktop application.
  exit /b %errorlevel%
)
if defined VERBOSE echo WorldSeed was launched. This terminal can close now.
exit /b 0
