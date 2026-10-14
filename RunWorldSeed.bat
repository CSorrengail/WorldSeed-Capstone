@echo off
setlocal
set "ROOT=%~dp0"
set "APP_PROJECT=%ROOT%src\WorldSeed.App\WorldSeed.App.csproj"

if /I "%~1"=="--check" goto check
if not "%~1"=="" (
  echo Usage: RunWorldSeed.bat [--check]
  exit /b 1
)

:start
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK is required but the dotnet command was not found.
  echo Install the .NET 8 SDK, then run this launcher again.
  exit /b 1
)

call "%ROOT%deploy\worldseed-local.bat"
if errorlevel 1 (
  echo.
  echo WorldSeed could not start its local Docker services.
  exit /b 1
)

:check
echo Waiting for the WorldSeed local API...
powershell -NoProfile -Command "$deadline = (Get-Date).AddSeconds(45); do { try { $response = Invoke-WebRequest -UseBasicParsing 'http://localhost:5080/health'; if ($response.Content -match 'healthy') { exit 0 } } catch { } ; Start-Sleep -Seconds 1 } while ((Get-Date) -lt $deadline); exit 1"
if errorlevel 1 (
  echo The Docker services did not become healthy within 45 seconds.
  echo Run deploy\worldseed-local.bat logs to inspect the service output.
  exit /b 1
)

if /I "%~1"=="--check" (
  echo WorldSeed local API is healthy.
  exit /b 0
)

echo Starting the WorldSeed desktop application...
echo The application window uses the Docker-backed local database service.
dotnet run --project "%APP_PROJECT%" --disable-build-servers
exit /b %errorlevel%
