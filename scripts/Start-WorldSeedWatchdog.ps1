[CmdletBinding()]
param(
    [ValidateRange(1, 1440)]
    [int]$InactivityMinutes = 15,
    [ValidateRange(15, 3600)]
    [int]$IntervalSeconds = 60,
    [int]$SessionProcessId,
    [switch]$Once,
    [switch]$Worker,
    [string]$WorkerRunId
)

<#
.SYNOPSIS
Starts or runs the independent WorldSeed interruption watchdog.

.DESCRIPTION
The watchdog reads only the project-owned watchdog state file. A missing explicit
session process ID means it can report only *suspected* inactivity. When a supplied
process ID disappears while the state is Active, it records a confirmed observable
process termination. It never restarts Codex.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stateDirectory = Join-Path $repositoryRoot '.worldseed-notifications'
$statePath = Join-Path $stateDirectory 'watchdog-state.json'
$pidPath = Join-Path $stateDirectory 'watchdog.pid'
$workerPath = Join-Path $stateDirectory 'watchdog-worker.json'
$stopRequestPath = Join-Path $stateDirectory 'watchdog-stop-request.json'
$stateScript = Join-Path $PSScriptRoot 'Set-WorldSeedWatchdogState.ps1'
$notificationScript = Join-Path $PSScriptRoot 'Notify-Owner.ps1'

function Read-State {
    if (-not (Test-Path -LiteralPath $statePath)) { return $null }
    return (Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Write-State {
    param([object]$State)

    $State.updatedUtc = [DateTime]::UtcNow.ToString('o')
    $temporaryPath = "$statePath.$PID.tmp"
    $State | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
    Move-Item -LiteralPath $temporaryPath -Destination $statePath -Force
}

function Test-WatchdogState {
    $state = Read-State
    if ($null -eq $state) {
        return [pscustomobject]@{ checked = $true; alert = $false; result = 'NoState' }
    }

    if ($state.state -ne 'Active') {
        return [pscustomobject]@{ checked = $true; alert = $false; result = "Suppressed:$($state.state)" }
    }

    $detection = $null
    $details = ''
    if ($null -ne $state.sessionProcessId -and [int]$state.sessionProcessId -gt 0) {
        $process = Get-Process -Id ([int]$state.sessionProcessId) -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            $detection = 'ConfirmedProcessTermination'
            $details = "The monitored process ID $($state.sessionProcessId) is no longer running."
        }
    }

    if ($null -eq $detection) {
        $lastHeartbeat = [DateTime]::Parse([string]$state.lastHeartbeatUtc).ToUniversalTime()
        $elapsed = [DateTime]::UtcNow - $lastHeartbeat
        if ($elapsed.TotalMinutes -ge [double]$state.inactivityMinutes) {
            $detection = 'SuspectedInactivity'
            $details = "No heartbeat was recorded for $([Math]::Floor($elapsed.TotalMinutes)) minutes."
        }
    }

    if ($null -eq $detection) {
        return [pscustomobject]@{ checked = $true; alert = $false; result = 'Active' }
    }

    $fingerprint = "$detection|$($state.lastHeartbeatUtc)|$($state.sessionProcessId)"
    if ($state.lastAlertFingerprint -eq $fingerprint) {
        return [pscustomobject]@{ checked = $true; alert = $false; result = 'DuplicateSuppressed'; detection = $detection }
    }

    & $notificationScript -Category Interruption | Out-Null
    $state.state = 'Interrupted'
    $state.detection = $detection
    $state.reason = $details
    $state.lastAlertFingerprint = $fingerprint
    Write-State -State $state

    return [pscustomobject]@{ checked = $true; alert = $true; result = $detection; detail = $details }
}

if (-not (Test-Path -LiteralPath $stateDirectory)) {
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
}

if (-not $Worker -and -not $Once) {
    & $stateScript -Action Start -InactivityMinutes $InactivityMinutes -SessionProcessId $SessionProcessId -Reason 'Watchdog started.' | Out-Null
    $enginePath = (Get-Process -Id $PID).Path
    $quotedScript = '"{0}"' -f $PSCommandPath.Replace('"', '\"')
    $newRunId = [Guid]::NewGuid().ToString('N')
    $arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File $quotedScript -Worker -WorkerRunId $newRunId -InactivityMinutes $InactivityMinutes -IntervalSeconds $IntervalSeconds"
    if ($SessionProcessId -gt 0) { $arguments += " -SessionProcessId $SessionProcessId" }
    $watchdogProcess = Start-Process -FilePath $enginePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath $pidPath -Value $watchdogProcess.Id -Encoding ASCII
    [ordered]@{ processId = $watchdogProcess.Id; runId = $newRunId; startedUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath $workerPath -Encoding UTF8
    [pscustomobject]@{ started = $true; watchdogProcessId = $watchdogProcess.Id; runId = $newRunId; inactivityMinutes = $InactivityMinutes; intervalSeconds = $IntervalSeconds } | ConvertTo-Json -Compress
    exit 0
}

if ($Once) {
    Test-WatchdogState | ConvertTo-Json -Compress
    exit 0
}

if ([string]::IsNullOrWhiteSpace($WorkerRunId)) {
    throw 'Worker mode requires WorkerRunId.'
}

Set-Content -LiteralPath $pidPath -Value $PID -Encoding ASCII
function Test-StopRequest {
    if (-not (Test-Path -LiteralPath $stopRequestPath)) { return $false }
    try {
        $request = Get-Content -LiteralPath $stopRequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        return ($request.runId -eq $WorkerRunId)
    }
    catch {
        return $false
    }
}

try {
    while ($true) {
        if (Test-StopRequest) { break }
        Test-WatchdogState | Out-Null
        for ($second = 1; $second -le $IntervalSeconds; $second++) {
            if (Test-StopRequest) { break }
            Start-Sleep -Seconds 1
        }
        if (Test-StopRequest) { break }
    }
}
finally {
    if (Test-Path -LiteralPath $pidPath) {
        $recordedPid = (Get-Content -LiteralPath $pidPath -Raw).Trim()
        if ($recordedPid -eq [string]$PID) {
            Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
        }
    }
    if (Test-Path -LiteralPath $workerPath) {
        $workerRecord = Get-Content -LiteralPath $workerPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($workerRecord.runId -eq $WorkerRunId) {
            Remove-Item -LiteralPath $workerPath -Force -ErrorAction SilentlyContinue
        }
    }
}
