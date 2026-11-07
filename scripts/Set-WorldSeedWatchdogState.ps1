[CmdletBinding()]
param(
    [ValidateSet('Start', 'Heartbeat', 'AwaitingOwner', 'Completed', 'IntentionalStop', 'Status', 'TestInactivity', 'TestInterruption')]
    [string]$Action = 'Heartbeat',
    [int]$InactivityMinutes = 15,
    [int]$SessionProcessId,
    [string]$Reason = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($InactivityMinutes -lt 1) {
    throw 'InactivityMinutes must be at least 1.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stateDirectory = Join-Path $repositoryRoot '.worldseed-notifications'
$statePath = Join-Path $stateDirectory 'watchdog-state.json'

if (-not (Test-Path -LiteralPath $stateDirectory)) {
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
}

function New-DefaultState {
    return [ordered]@{
        version = 1
        state = 'NotStarted'
        startedUtc = $null
        lastHeartbeatUtc = $null
        inactivityMinutes = 15
        sessionProcessId = $null
        reason = ''
        detection = $null
        lastAlertFingerprint = $null
        updatedUtc = [DateTime]::UtcNow.ToString('o')
    }
}

function Read-State {
    if (-not (Test-Path -LiteralPath $statePath)) {
        return (New-DefaultState)
    }

    try {
        $raw = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return (New-DefaultState)
        }
        return (ConvertFrom-Json -InputObject $raw)
    }
    catch {
        throw "The watchdog state file is unreadable: $($_.Exception.Message)"
    }
}

function Set-StateValue {
    param(
        [object]$State,
        [string]$Name,
        [object]$Value
    )

    $property = $State.PSObject.Properties[$Name]
    if ($property) {
        $property.Value = $Value
    }
    else {
        $State | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function Write-State {
    param([object]$State)

    Set-StateValue -State $State -Name 'updatedUtc' -Value ([DateTime]::UtcNow.ToString('o'))
    $temporaryPath = "$statePath.$PID.tmp"
    $State | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
    Move-Item -LiteralPath $temporaryPath -Destination $statePath -Force
}

$state = Read-State
$now = [DateTime]::UtcNow

switch ($Action) {
    'Start' {
        Set-StateValue $state 'state' 'Active'
        Set-StateValue $state 'startedUtc' $now.ToString('o')
        Set-StateValue $state 'lastHeartbeatUtc' $now.ToString('o')
        Set-StateValue $state 'inactivityMinutes' $InactivityMinutes
        Set-StateValue $state 'sessionProcessId' $(if ($SessionProcessId -gt 0) { $SessionProcessId } else { $null })
        Set-StateValue $state 'reason' $Reason
        Set-StateValue $state 'detection' $null
        Set-StateValue $state 'lastAlertFingerprint' $null
    }
    'Heartbeat' {
        if ($state.state -notin @('AwaitingOwner', 'Completed', 'IntentionalStop')) {
            Set-StateValue $state 'state' 'Active'
            Set-StateValue $state 'lastHeartbeatUtc' $now.ToString('o')
            Set-StateValue $state 'inactivityMinutes' $InactivityMinutes
            if ($SessionProcessId -gt 0) { Set-StateValue $state 'sessionProcessId' $SessionProcessId }
            if ($Reason) { Set-StateValue $state 'reason' $Reason }
            Set-StateValue $state 'detection' $null
        }
    }
    'AwaitingOwner' {
        Set-StateValue $state 'state' 'AwaitingOwner'
        Set-StateValue $state 'reason' $Reason
    }
    'Completed' {
        Set-StateValue $state 'state' 'Completed'
        Set-StateValue $state 'reason' $Reason
    }
    'IntentionalStop' {
        Set-StateValue $state 'state' 'IntentionalStop'
        Set-StateValue $state 'reason' $Reason
    }
    'TestInactivity' {
        Set-StateValue $state 'state' 'Active'
        Set-StateValue $state 'startedUtc' $now.ToString('o')
        Set-StateValue $state 'lastHeartbeatUtc' $now.AddMinutes(-($InactivityMinutes + 1)).ToString('o')
        Set-StateValue $state 'inactivityMinutes' $InactivityMinutes
        Set-StateValue $state 'sessionProcessId' $null
        Set-StateValue $state 'reason' 'Manual simulated inactivity test.'
        Set-StateValue $state 'detection' $null
        Set-StateValue $state 'lastAlertFingerprint' $null
    }
    'TestInterruption' {
        Set-StateValue $state 'state' 'Active'
        Set-StateValue $state 'startedUtc' $now.ToString('o')
        Set-StateValue $state 'lastHeartbeatUtc' $now.ToString('o')
        Set-StateValue $state 'inactivityMinutes' $InactivityMinutes
        Set-StateValue $state 'sessionProcessId' 999999
        Set-StateValue $state 'reason' 'Manual simulated missing-process test.'
        Set-StateValue $state 'detection' $null
        Set-StateValue $state 'lastAlertFingerprint' $null
    }
    'Status' { }
}

if ($Action -ne 'Status') {
    Write-State -State $state
}

$state | ConvertTo-Json -Depth 5
