# Owner Notifications And Interruption Watchdog

WorldSeed includes a local, project-scoped Windows notification system for an
autonomous development session. It uses only Windows PowerShell and built-in
audio APIs. It does not contact a service, restart Codex, or inspect chat
transcripts.

## Notification Categories

- **Owner action:** a three-repetition alarm for an owner decision, human input,
  or a blocker. The project Stop hook starts it only when the main assistant's
  final message begins at character zero with one of the approved markers.
- **Interruption:** a distinct three-repetition alarm when the independent
  watchdog detects either a confirmed monitored-process termination or suspected
  inactivity.
- **MVP complete:** a distinct three-repetition completion notification. The
  coordinator invokes this explicitly only after the approved MVP is completed
  and verified.

The notification process writes an ignored local receipt. A receipt proves only
that the playback process ran; it cannot prove the owner heard the sound.

## Manual Tests

From the repository root, run these commands in PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Notify-Owner.ps1 -Category OwnerDecision -Test
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Notify-Owner.ps1 -Category Interruption -Test
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Notify-Owner.ps1 -Category MvpComplete -Test
```

To test the Stop-hook decision filter without waiting for Codex, use a payload
whose message starts with an approved marker. The script prints the valid Stop
hook response and queues the owner-action sound:

```powershell
$payload = @{ hook_event_name = 'Stop'; cwd = (Get-Location).Path; session_id = 'manual-test'; turn_id = '1'; last_assistant_message = 'ACTION REQUIRED: OWNER DECISION`nChoose a fixture.' } | ConvertTo-Json -Compress
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex\hooks\Invoke-OwnerAlertOnStop.ps1 -PayloadJson $payload
```

For the negative test, replace the message with `Completed normally.`. It must
print its JSON response without queuing a sound.

To simulate the watchdog categories:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Set-WorldSeedWatchdogState.ps1 -Action TestInactivity -InactivityMinutes 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-WorldSeedWatchdog.ps1 -Once

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Set-WorldSeedWatchdogState.ps1 -Action TestInterruption
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-WorldSeedWatchdog.ps1 -Once
```

The first is a **suspected** interruption; the second is a **confirmed observable
process termination** for a deliberately nonexistent process ID.

## Start, Stop, And Disable

Start the independent watchdog (15 minutes is the default):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-WorldSeedWatchdog.ps1 -InactivityMinutes 15
```

It runs in a separate hidden PowerShell process. Stop it safely with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Stop-WorldSeedWatchdog.ps1
```

The stop script sends only the recorded worker's random cooperative stop signal;
it never terminates a PowerShell process. The watchdog is also suppressed when its state is
`AwaitingOwner`, `Completed`, or `IntentionalStop`.

## Codex Hook Trust

The tracked project hook lives at `.codex/hooks.json`. Codex requires the owner
to review and trust a new or changed non-managed hook before it runs. In Codex,
open **`/hooks`** (or **Settings > Hooks** in Desktop), review the WorldSeed
project hook, and trust it. Do not use a hook-trust bypass for normal work.

Until it is trusted, the coordinator uses the documented fallback: it explicitly
invokes `Notify-Owner.ps1` immediately before any final response that begins with
an approved owner-action marker.

## Limits

Codex exposes no reliable event for subscription or usage-limit exhaustion, and
this project cannot reliably identify the host process for a hosted agent
session. With no supplied session PID, the watchdog can report only suspected
inactivity after the configured threshold. If a supplied process ID disappears
while the state is Active, it records a confirmed observable termination. These
signals are advisory; the watchdog never restarts Codex or bypasses limits.
