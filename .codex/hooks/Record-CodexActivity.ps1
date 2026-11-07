[CmdletBinding()]
param(
    [ValidateSet('SessionStart', 'Activity')]
    [string]$Event = 'Activity'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$stateScript = Join-Path $repositoryRoot 'scripts\Set-WorldSeedWatchdogState.ps1'

try {
    $payload = [Console]::In.ReadToEnd()
    if ($Event -eq 'SessionStart') {
        & $stateScript -Action Start -Reason 'Codex session started.' | Out-Null
    }
    else {
        & $stateScript -Action Heartbeat -Reason 'Codex lifecycle activity.' | Out-Null
    }
}
catch {
    # Hooks must not interrupt development merely because local notification state is unavailable.
}
