@echo off
setlocal
set "COMPOSE_FILE=%~dp0docker-compose.yml"
set "ENV_FILE=%~dp0.env"

where docker >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop is required but the docker command was not found.
  echo Install and start Docker Desktop, then run this script again.
  exit /b 1
)

if not exist "%ENV_FILE%" (
  copy /Y "%~dp0.env.example" "%ENV_FILE%" >nul
  echo Created deploy\.env from the safe local template.
  echo Change MONGO_ROOT_PASSWORD in deploy\.env before using this outside your own computer.
)

if /I "%~1"=="down" goto down
if /I "%~1"=="reset" goto reset
if /I "%~1"=="logs" goto logs
if not "%~1"=="" (
  echo Usage: worldseed-local.bat [down^|reset^|logs]
  exit /b 1
)

:up
docker compose --env-file "%ENV_FILE%" -f "%COMPOSE_FILE%" up --build --detach
if errorlevel 1 exit /b %errorlevel%
echo.
echo WorldSeed API is ready when http://localhost:5080/health returns healthy.
echo The native WorldSeed desktop app stays on this computer and will connect to that API.
exit /b 0

:down
docker compose --env-file "%ENV_FILE%" -f "%COMPOSE_FILE%" down
exit /b %errorlevel%

:reset
echo This permanently deletes all WorldSeed database data in Docker.
choice /M "Continue with reset"
if errorlevel 2 exit /b 0
docker compose --env-file "%ENV_FILE%" -f "%COMPOSE_FILE%" down --volumes --remove-orphans
if errorlevel 1 exit /b %errorlevel%
goto up

:logs
docker compose --env-file "%ENV_FILE%" -f "%COMPOSE_FILE%" logs --follow
exit /b %errorlevel%
