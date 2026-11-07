[CmdletBinding()]
param(
    [ValidateSet('OwnerDecision', 'HumanInput', 'Blocker', 'Interruption', 'MvpComplete')]
    [string]$Category = 'OwnerDecision',
    [switch]$Test,
    [switch]$Playback
)

<#
.SYNOPSIS
Queues a nonblocking local Windows sound for a WorldSeed owner notification.

.DESCRIPTION
The parent invocation starts a short-lived hidden PowerShell playback process and
returns immediately.  The child performs the three-tone sequence, writes a local
receipt under .worldseed-notifications, and exits.  The receipt confirms that the
playback process ran; it cannot confirm that a person heard the sound.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

function Ensure-StateDirectory {
    param([string]$RepositoryRoot)

    $path = Join-Path $RepositoryRoot '.worldseed-notifications'
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }

    return $path
}

function Write-PlaybackReceipt {
    param(
        [string]$StateDirectory,
        [string]$NotificationCategory,
        [string]$PlaybackMethod,
        [string]$SoundPath
    )

    $receipt = [ordered]@{
        timestampUtc = [DateTime]::UtcNow.ToString('o')
        category = $NotificationCategory
        playbackMethod = $PlaybackMethod
        soundPath = $SoundPath
        processId = $PID
    } | ConvertTo-Json -Compress

    Add-Content -LiteralPath (Join-Path $StateDirectory 'playback-events.jsonl') -Value $receipt -Encoding UTF8
}

function Get-SoundPath {
    param([string]$NotificationCategory)

    $mediaDirectory = Join-Path $env:WINDIR 'Media'
    $candidates = switch ($NotificationCategory) {
        'Interruption' { @('Alarm10.wav', 'Alarm09.wav', 'Alarm01.wav') }
        'MvpComplete' { @('chimes.wav', 'notify.wav', 'Windows Notify System Generic.wav') }
        default { @('Alarm01.wav', 'Alarm02.wav', 'Windows Notify System Generic.wav') }
    }

    foreach ($candidate in $candidates) {
        $path = Join-Path $mediaDirectory $candidate
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    return $null
}

function Invoke-Playback {
    param([string]$NotificationCategory)

    $stateDirectory = Ensure-StateDirectory -RepositoryRoot (Get-RepositoryRoot)
    $soundPath = Get-SoundPath -NotificationCategory $NotificationCategory
    $method = 'SystemSoundsFallback'

    try {
        if ($soundPath) {
            $player = New-Object System.Media.SoundPlayer $soundPath
            $player.Load()
            1..3 | ForEach-Object {
                $player.PlaySync()
                Start-Sleep -Milliseconds 250
            }
            $method = 'System.Media.SoundPlayer'
        }
        else {
            $frequencies = if ($NotificationCategory -eq 'Interruption') { @(740, 560, 740) } elseif ($NotificationCategory -eq 'MvpComplete') { @(880, 1047, 1319) } else { @(1047, 784, 1047) }
            foreach ($frequency in $frequencies) {
                try {
                    [Console]::Beep($frequency, 250)
                }
                catch {
                    [System.Media.SystemSounds]::Exclamation.Play()
                    Start-Sleep -Milliseconds 250
                }
                Start-Sleep -Milliseconds 125
            }
        }
    }
    catch {
        $method = 'SystemSoundsFallback'
        1..3 | ForEach-Object {
            [System.Media.SystemSounds]::Exclamation.Play()
            Start-Sleep -Milliseconds 300
        }
    }
    finally {
        Write-PlaybackReceipt -StateDirectory $stateDirectory -NotificationCategory $NotificationCategory -PlaybackMethod $method -SoundPath $soundPath
    }
}

if ($Playback) {
    Invoke-Playback -NotificationCategory $Category
    exit 0
}

$enginePath = (Get-Process -Id $PID).Path
$quotedScript = '"{0}"' -f $PSCommandPath.Replace('"', '\"')
$arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File $quotedScript -Playback -Category $Category"

try {
    $playbackProcess = Start-Process -FilePath $enginePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    [pscustomobject]@{
        queued = $true
        category = $Category
        playbackProcessId = $playbackProcess.Id
        test = [bool]$Test
        note = 'A playback process was started. This does not confirm that the sound was heard.'
    } | ConvertTo-Json -Compress
}
catch {
    [pscustomobject]@{
        queued = $false
        category = $Category
        test = [bool]$Test
        error = $_.Exception.Message
    } | ConvertTo-Json -Compress
    exit 1
}
