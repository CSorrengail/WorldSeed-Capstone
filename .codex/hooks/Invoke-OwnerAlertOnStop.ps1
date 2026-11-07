[CmdletBinding()]
param(
    [string]$PayloadJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$notificationScript = Join-Path $repositoryRoot 'scripts\Notify-Owner.ps1'
$stateScript = Join-Path $repositoryRoot 'scripts\Set-WorldSeedWatchdogState.ps1'
$dedupeDirectory = Join-Path $repositoryRoot '.worldseed-notifications\stop-deduplication'

function Get-Hash {
    param([string]$Value)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    $hash = [Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

try {
    $rawPayload = if ($PSBoundParameters.ContainsKey('PayloadJson')) { $PayloadJson } else { [Console]::In.ReadToEnd() }
    if ([string]::IsNullOrWhiteSpace($rawPayload)) { throw 'The Stop hook did not receive a payload.' }
    $payload = ConvertFrom-Json -InputObject $rawPayload

    # This project configures only Stop, not SubagentStop. The exact event check,
    # repository cwd check, and marker-at-character-zero rule avoid helper output
    # and historical/background messages producing an owner alert.
    $expectedRoot = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\')
    $payloadRoot = if ($payload.cwd) { [IO.Path]::GetFullPath([string]$payload.cwd).TrimEnd('\') } else { '' }
    $message = if ($null -eq $payload.last_assistant_message) { '' } else { [string]$payload.last_assistant_message }
    if ($payload.hook_event_name -ne 'Stop' -or -not [string]::Equals($payloadRoot, $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        @{ continue = $true } | ConvertTo-Json -Compress
        exit 0
    }

    $category = $null
    if ($message.StartsWith('ACTION REQUIRED: OWNER DECISION', [StringComparison]::Ordinal)) { $category = 'OwnerDecision' }
    elseif ($message.StartsWith('ACTION REQUIRED: HUMAN INPUT', [StringComparison]::Ordinal)) { $category = 'HumanInput' }
    elseif ($message.StartsWith('ACTION REQUIRED: BLOCKER', [StringComparison]::Ordinal)) { $category = 'Blocker' }

    if ($null -ne $category) {
        if (-not (Test-Path -LiteralPath $dedupeDirectory)) { New-Item -ItemType Directory -Path $dedupeDirectory -Force | Out-Null }
        $turnId = if ($payload.turn_id) { [string]$payload.turn_id } else { 'unknown-turn' }
        $sessionId = if ($payload.session_id) { [string]$payload.session_id } else { 'unknown-session' }
        $dedupePath = Join-Path $dedupeDirectory ("{0}.json" -f (Get-Hash "$sessionId|$turnId|$category"))
        if (-not (Test-Path -LiteralPath $dedupePath)) {
            @{ timestampUtc = [DateTime]::UtcNow.ToString('o'); category = $category; sessionId = $sessionId; turnId = $turnId } | ConvertTo-Json -Compress | Set-Content -LiteralPath $dedupePath -Encoding UTF8
            & $notificationScript -Category $category | Out-Null
        }
        & $stateScript -Action AwaitingOwner -Reason "Codex requested $category." | Out-Null
    }
    else {
        & $stateScript -Action Heartbeat -Reason 'Codex Stop hook completed without owner-action marker.' | Out-Null
    }
}
catch {
    # A notification failure is advisory and must not alter Codex's stop behavior.
}

# Stop hooks require JSON output on stdout when exiting successfully.
@{ continue = $true } | ConvertTo-Json -Compress
