param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$PriorRuntimeAttempt,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$RuntimeAttempt,
    [Parameter(Mandatory)][string]$VerifiedSavePath,
    [Parameter(Mandatory)][string]$ReplacementHarnessDirectory,
    [switch]$ContinueStoppedAttempt,
    [switch]$FromStoppedSavedRun,
    [string]$StoppedRunEvidencePath
)

$ErrorActionPreference = 'Stop'
$prior = Join-Path $RunDirectory $PriorRuntimeAttempt
$session = Get-Content -LiteralPath (Join-Path $prior 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$startup = Get-Content -LiteralPath (Join-Path $prior 'startup.json') -Raw | ConvertFrom-Json
$settings = Get-Content -LiteralPath (Join-Path $prior 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$saved = Get-Content -LiteralPath $VerifiedSavePath -Raw | ConvertFrom-Json
if ($ContinueStoppedAttempt -and $FromStoppedSavedRun) { throw 'Choose one stopped-run entry mode.' }
$stoppedMode = $ContinueStoppedAttempt -or $FromStoppedSavedRun
if (!$saved.native_save_boundary_verified -or !$saved.independent_saved_xml_verified -or $saved.run_id -cne $session.run_id -or
    $saved.save_id -cne 'ProofFarm' -or $saved.player_id -cne '319842636613009663') { throw 'Restart requires the independently accepted same-run native save.' }
$main = Join-Path (Join-Path $session.save_root $session.slot) $session.slot
$expected = @($saved.files | Where-Object name -ceq $session.slot)
if ($expected.Count -ne 1 -or (Get-FileHash -LiteralPath $main).Hash -cne $expected[0].sha256) { throw 'The accepted actual main save changed.' }
if ($FromStoppedSavedRun) {
    $stopped = Get-Content -LiteralPath $StoppedRunEvidencePath -Raw | ConvertFrom-Json
    $stoppedFrame = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $StoppedRunEvidencePath) 'before-stop.json') -Raw | ConvertFrom-Json
    if (!$stopped.unsaved_mine_progress_discarded -or $stopped.state_hash -cne $stoppedFrame.state_hash -or
        $stoppedFrame.save_id.value -cne $saved.save_id -or $stoppedFrame.player_id.value -cne $saved.player_id -or
        $stoppedFrame.state.environment.training_run_id.value -cne $session.run_id -or
        $startup.source_main_sha256 -cne $expected[0].sha256) { throw 'Stopped experiment does not bind this same verified saved morning.' }
}
if (!$stoppedMode) {
    $fresh = Invoke-RestMethod -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20
    if ($fresh.state_hash -cne $saved.after_state_hash -or $fresh.state.time.total_days.value -ne $saved.total_day -or
        $fresh.state.player.location_id.value -cne 'FarmHouse') { throw 'The live game moved beyond the accepted save; preserve it before restarting.' }
}
foreach ($row in $session.processes) {
    $process = Get-Process -Id $row.id -ErrorAction SilentlyContinue
    if ($stoppedMode) {
        if ($process) { throw 'A prior service still exists; do not resume a stopped deployment.' }
    } elseif (!$process -or $process.Path -cne $row.exe -or $process.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($row.started).UtcDateTime) {
        throw 'Original service process identity changed.'
    }
}
$gameExe = ($session.processes | Where-Object role -ceq 'game').exe
$dotnet = ($session.processes | Where-Object role -ceq 'backend').exe
$gameDir = Split-Path -Parent $gameExe
$mod = Join-Path $gameDir 'Mods\StardewAI.RuntimeTestHarness'
foreach ($name in 'StardewAI.Contracts.dll', 'StardewAI.RuntimePrimitives.dll') {
    if ((Get-FileHash -LiteralPath (Join-Path $mod $name)).Hash -cne (Get-FileHash -LiteralPath (Join-Path $ReplacementHarnessDirectory $name)).Hash) {
        throw 'The replacement harness dependencies differ from the retained native runtime.'
    }
}
foreach ($role in 'backend','product') {
    if ((Get-FileHash -LiteralPath $startup.$role.path).Hash -cne $startup.$role.sha256) { throw 'A retained service build changed.' }
}
$oldDll = Join-Path $mod 'StardewAI.RuntimeTestHarness.dll'
$newDll = Join-Path $ReplacementHarnessDirectory 'StardewAI.RuntimeTestHarness.dll'
if ($ContinueStoppedAttempt) {
    $next = Get-Item -LiteralPath (Join-Path $RunDirectory $RuntimeAttempt)
    if (Test-Path -LiteralPath (Join-Path $next.FullName 'live-session.json')) { throw 'This attempt already started services; inspect its exact handles instead.' }
    $intent = Get-Content -LiteralPath (Join-Path $next.FullName 'restart-intent.json') -Raw | ConvertFrom-Json
    if ($intent.previous_session -cne (Join-Path $prior 'live-session.json') -or
        $intent.verified_save_sha256 -cne (Get-FileHash -LiteralPath $VerifiedSavePath).Hash -or
        $intent.replacement_harness_sha256 -cne (Get-FileHash -LiteralPath $newDll).Hash -or
        (Get-FileHash -LiteralPath $oldDll).Hash -cnotin @($intent.old_harness_sha256,$intent.replacement_harness_sha256)) {
        throw 'Stopped deployment intent, accepted save or module content changed.'
    }
    if (Get-NetTCPConnection -LocalPort 8765,8767,18413,18414 -State Listen -ErrorAction SilentlyContinue) { throw 'A native runtime endpoint is already owned.' }
} else {
    if ($FromStoppedSavedRun -and (Get-NetTCPConnection -LocalPort 8765,8767,18413,18414 -State Listen -ErrorAction SilentlyContinue)) { throw 'A native runtime endpoint is already owned.' }
    $next = New-Item -ItemType Directory -Path (Join-Path $RunDirectory $RuntimeAttempt)
    Copy-Item -LiteralPath $oldDll -Destination (Join-Path $next.FullName 'prior-harness.dll')
}
if (!$ContinueStoppedAttempt) {
$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $next.FullName 'runtime-settings.json') -Encoding utf8
foreach ($name in 'verified-adjacent-chest-entry.json','verified-farm-chest-entry.json') {
    # Historical access-tile receipts; storage still checks the fresh native graph.
    Copy-Item -LiteralPath (Join-Path $prior $name) -Destination (Join-Path $next.FullName $name)
}
[ordered]@{previous_session=(Join-Path $prior 'live-session.json');verified_save=$VerifiedSavePath;verified_save_sha256=(Get-FileHash -LiteralPath $VerifiedSavePath).Hash;main_save_sha256=$expected[0].sha256;old_harness_sha256=(Get-FileHash -LiteralPath $oldDll).Hash;replacement_harness=$newDll;replacement_harness_sha256=(Get-FileHash -LiteralPath $newDll).Hash;from_stopped_saved_run=[bool]$FromStoppedSavedRun;stopped_run_evidence=$StoppedRunEvidencePath} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $next.FullName 'restart-intent.json') -Encoding utf8
if (!$FromStoppedSavedRun) {
foreach ($role in 'product','backend','game') {
    $row = $session.processes | Where-Object role -ceq $role
    Stop-Process -Id $row.id
    Wait-Process -Id $row.id -Timeout 10 -ErrorAction SilentlyContinue
}
}
}
foreach ($copyAttempt in 1..10) {
    try { Copy-Item -LiteralPath $newDll -Destination $oldDll -Force; break }
    catch [IO.IOException] { if ($copyAttempt -eq 10) { throw }; Start-Sleep -Milliseconds 250 }
}
if ((Get-FileHash -LiteralPath $oldDll).Hash -cne (Get-FileHash -LiteralPath $newDll).Hash) { throw 'Harness copy did not verify.' }
foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process') }
$backend = Start-Process -FilePath $dotnet -ArgumentList @($startup.backend.path) -WorkingDirectory (Split-Path -Parent $startup.backend.path) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $next.FullName 'backend.stdout.log') -RedirectStandardError (Join-Path $next.FullName 'backend.stderr.log')
[Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $settings.STARDEWAI_PRODUCT_EXECUTOR_URL, 'Process')
$product = Start-Process -FilePath $dotnet -ArgumentList @($startup.product.path) -WorkingDirectory (Split-Path -Parent $startup.product.path) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $next.FullName 'product.stdout.log') -RedirectStandardError (Join-Path $next.FullName 'product.stderr.log')
[Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $settings.ASPNETCORE_URLS, 'Process')
$game = Start-Process -FilePath $gameExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $next.FullName 'game.stdout.log') -RedirectStandardError (Join-Path $next.FullName 'game.stderr.log')
$processes = @(foreach ($entry in @(@{role='game';process=$game},@{role='backend';process=$backend},@{role='product';process=$product})) {
    [ordered]@{role=$entry.role;id=$entry.process.Id;exe=$entry.process.Path;started=$entry.process.StartTime.ToUniversalTime().ToString('o')}
})
$newSession = [ordered]@{run_id=$session.run_id;save_root=$session.save_root;slot=$session.slot;processes=$processes}
$newSession | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $next.FullName 'live-session.json') -Encoding utf8
[ordered]@{started=(Get-Date).ToString('o');source_save=$main;source_main_sha256=$expected[0].sha256;prior_native_attempt=$prior;backend=$startup.backend;product=$startup.product;harness=@{path=$oldDll;sha256=(Get-FileHash -LiteralPath $oldDll).Hash};bridge=$startup.bridge;processes=$processes} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $next.FullName 'startup.json') -Encoding utf8
$loaded = $null
foreach ($attempt in 1..30) {
    if ($game.HasExited -or $backend.HasExited -or $product.HasExited) { throw 'A recorded native service exited during startup; inspect its retained log.' }
    try {
        $loadedBody = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 3).Content
        $loaded = $loadedBody | ConvertFrom-Json
        if ($loaded.save_id.value -ceq $saved.save_id -and $loaded.player_id.value -ceq $saved.player_id) { break }
    } catch { $loaded = $null }
    Start-Sleep -Seconds 1
}
if (!$loaded -or $loaded.state.time.total_days.value -ne $saved.total_day -or $loaded.state.time.time.value -ne 600 -or
    $loaded.state.player.money.value -ne $saved.cash -or (Get-FileHash -LiteralPath $main).Hash -cne $expected[0].sha256) {
    throw 'New recorded services have not verified the same actual saved morning; inspect them without restarting.'
}
[IO.File]::WriteAllText((Join-Path $next.FullName 'initial-readback.json'), $loadedBody)
[ordered]@{same_actual_save_verified=$true;run_id=$session.run_id;save_id=$loaded.save_id.value;player_id=$loaded.player_id.value;total_day=$loaded.state.time.total_days.value;cash=$loaded.state.player.money.value;state_hash=$loaded.state_hash;game_tick=$loaded.game_tick;source_main_sha256=$expected[0].sha256;harness_sha256=(Get-FileHash -LiteralPath $oldDll).Hash;formal_training_authorized=$false} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $next.FullName 'verified-readback.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $next.FullName 'verified-readback.json')
