[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stateDirectory = Join-Path $repositoryRoot '.worldseed-notifications'
$pidPath = Join-Path $stateDirectory 'watchdog.pid'
$workerPath = Join-Path $stateDirectory 'watchdog-worker.json'
$stopRequestPath = Join-Path $stateDirectory 'watchdog-stop-request.json'
$stateScript = Join-Path $PSScriptRoot 'Set-WorldSeedWatchdogState.ps1'

if (-not (Test-Path -LiteralPath $workerPath)) {
    [pscustomobject]@{ stopped = $false; reason = 'No cooperative watchdog worker record exists.' } | ConvertTo-Json -Compress
    exit 0
}

$workerRecord = Get-Content -LiteralPath $workerPath -Raw -Encoding UTF8 | ConvertFrom-Json
$watchdogPid = [int]$workerRecord.processId
@{ runId = [string]$workerRecord.runId; requestedUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath $stopRequestPath -Encoding UTF8
& $stateScript -Action IntentionalStop -Reason 'Watchdog stopped manually.' | Out-Null
[pscustomobject]@{ stopRequested = $true; watchdogProcessId = $watchdogPid; runId = [string]$workerRecord.runId } | ConvertTo-Json -Compress
