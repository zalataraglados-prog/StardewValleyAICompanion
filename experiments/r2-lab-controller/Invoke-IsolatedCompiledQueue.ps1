param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][string]$QueuePath,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [ValidateRange(1, 64)][int]$MaximumQueueItemAttempts = 12
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$session = Get-Content -LiteralPath (Join-Path $runtime 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($row in $session.processes) {
    $process = Get-Process -Id $row.id
    if ($process.Path -cne $row.exe -or $process.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($row.started).UtcDateTime) {
        throw 'Original isolated process identity changed.'
    }
}
$queue = Get-Content -LiteralPath $QueuePath -Raw | ConvertFrom-Json
$body = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
$source = $body | ConvertFrom-Json
if ($queue.status -cne 'pending' -or !$queue.items.Count -or $queue.state_hash -cne $source.state_hash -or
    $queue.execution_mode -cne 'training_singleplayer' -or $source.save_id.value -cne 'ProofFarm' -or
    $source.player_id.value -cne '319842636613009663' -or
    $source.state.environment.training_run_id.value -cne $session.run_id -or
    $settings.STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT -cne $session.save_root) {
    throw 'The original pending compiler output no longer binds the exact isolated source.'
}
$out = New-Item -ItemType Directory -Path (Join-Path $RunDirectory $Attempt)
[IO.File]::WriteAllText((Join-Path $out.FullName 'before.json'), $body)
$argsPath = Join-Path $RunDirectory 'spring27-chest-adapter-replay\execution-arguments.json'
$arguments = [string[]](Get-Content -LiteralPath $argsPath -Raw | ConvertFrom-Json)
foreach ($binding in @{
    '--root' = (Join-Path $RunDirectory 'training')
    '--run-id' = $session.run_id
    '--backend-url' = $settings.ASPNETCORE_URLS
    '--bridge-snapshot-url' = $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL
    '--executor-url' = $settings.STARDEWAI_PRODUCT_EXECUTOR_URL
    '--save-isolation-path' = $session.save_root
    '--save-slot' = $session.slot
}.GetEnumerator()) {
    $index = [Array]::IndexOf($arguments, $binding.Key)
    if ($index -lt 0 -or $arguments[$index + 1] -cne $binding.Value) { throw 'Original controller binding changed.' }
}
foreach ($binding in @{
    '--goal' = $queue.goal_id
    '--precompiled-queue' = [IO.Path]::GetFullPath($QueuePath)
    '--max-persisted-iterations' = '600'
    '--max-queue-item-attempts' = $MaximumQueueItemAttempts.ToString()
}.GetEnumerator()) {
    $index = [Array]::IndexOf($arguments, $binding.Key)
    if ($index -lt 0) { throw 'Original controller argument missing.' }
    $arguments[$index + 1] = $binding.Value
}
[ordered]@{source_queue_path=[IO.Path]::GetFullPath($QueuePath);source_queue_sha256=(Get-FileHash -LiteralPath $QueuePath).Hash;source_state_hash=$queue.state_hash;source_goal=$queue.goal_id;queue_id=$queue.queue_id;queue_rewritten=$false} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out.FullName 'compiler-source-binding.json') -Encoding utf8
$arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out.FullName 'arguments.json') -Encoding utf8
foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process') }
$dotnet = ($session.processes | Where-Object role -ceq 'backend').exe
& $dotnet @arguments *> (Join-Path $out.FullName 'native.log')
$code = $LASTEXITCODE
$result = Get-Content -LiteralPath (Join-Path $out.FullName 'native.log') -Raw | ConvertFrom-Json
if ($code -ne 0 -or $result.status -cne 'ok' -or $result.verified_actions -lt 1) {
    throw ('Original compiled native queue did not verify; inspect ' + $out.FullName)
}
$result | Select-Object status,run_id,verified_actions
