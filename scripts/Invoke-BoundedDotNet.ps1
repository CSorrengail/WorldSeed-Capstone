[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Restore', 'Build', 'Test', 'Publish')]
    [string]$Operation,
    [Parameter(Mandatory)]
    [string]$Project,
    [string[]]$AdditionalArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Project -PathType Leaf)) { throw 'The project or solution file does not exist.' }
if ($AdditionalArguments | Where-Object { $_ -match '^(?:[-/](?:m|maxcpucount|nr|nodeReuse)(?::|$)|--(?:restore|no-restore|disable-parallel)(?:=|$)|[-/](?:p|property):(?:BuildInParallel|RestoreDisableParallel|UseSharedCompilation)=)' }) {
    throw 'Worker controls and restore behavior are owned by Invoke-BoundedDotNet; do not override them.'
}

$taskEnvironment = @{
    DOTNET_PROCESSOR_COUNT = '2'
    MSBUILDDISABLENODEREUSE = '1'
    DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
}
$taskPreviousEnvironment = @{}
foreach ($taskKey in $taskEnvironment.Keys) {
    $taskPreviousEnvironment[$taskKey] = [Environment]::GetEnvironmentVariable($taskKey, 'Process')
}

try {
    foreach ($taskKey in $taskEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskKey, $taskEnvironment[$taskKey], 'Process')
    }
    $taskArguments = @($Operation.ToLowerInvariant(), (Resolve-Path -LiteralPath $Project).Path)
    if ($Operation -eq 'Restore') { $taskArguments += '--disable-parallel' }
    else { $taskArguments += '--no-restore' }
    $taskArguments += $AdditionalArguments
    $taskArguments += @('-m:1', '-nr:false', '-p:BuildInParallel=false', '-p:UseSharedCompilation=false', '-p:UsedAvaloniaProducts=')
    Write-Verbose ('dotnet ' + ($taskArguments -join ' '))
    & dotnet @taskArguments
    if ($LASTEXITCODE -ne 0) { throw "Bounded dotnet $Operation failed with exit code $LASTEXITCODE. Stop; do not retry with unbounded commands." }
}
finally {
    foreach ($taskKey in $taskEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskKey, $taskPreviousEnvironment[$taskKey], 'Process')
    }
}
