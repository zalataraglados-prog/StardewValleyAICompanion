param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][int]$ExpectedTotalDay,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$session = Get-Content -LiteralPath (Join-Path $runtime 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($row in $session.processes) {
    $process = Get-Process -Id $row.id
    if ($process.Path -cne $row.exe -or $process.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($row.started).UtcDateTime) {
        throw 'Original isolation process identity changed.'
    }
}
if ($settings.STARDEWAI_SAVE_ISOLATION_PATH -cne $session.save_root -or
    $settings.STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT -cne $session.save_root) { throw 'Save isolation binding changed.' }
$body = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
$before = $body | ConvertFrom-Json
if ($before.save_id.value -cne 'ProofFarm' -or $before.player_id.value -cne '319842636613009663' -or
    $before.state.time.total_days.value -ne $ExpectedTotalDay -or $before.state.player.location_id.value -cne 'FarmHouse') {
    throw 'Native save requires the expected same-player day in FarmHouse.'
}
$proof = New-Item -ItemType Directory -Path (Join-Path $RunDirectory $Attempt)
[IO.File]::WriteAllText((Join-Path $proof.FullName 'before.json'), $body)
$slot = Join-Path $session.save_root $session.slot
$beforeMain = (Get-FileHash -LiteralPath (Join-Path $slot $session.slot) -Algorithm SHA256).Hash
$template = Join-Path $RunDirectory 'summer04-native-save-20261123\native-boundary-arguments.json'
$arguments = [string[]](Get-Content -LiteralPath $template -Raw | ConvertFrom-Json)
foreach ($binding in @{
    '--run-id' = $session.run_id
    '--save-isolation-path' = $session.save_root
    '--save-slot' = $session.slot
    '--backend-url' = $settings.ASPNETCORE_URLS
    '--bridge-snapshot-url' = $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL
    '--executor-url' = $settings.STARDEWAI_PRODUCT_EXECUTOR_URL
}.GetEnumerator()) {
    $index = [Array]::IndexOf($arguments, $binding.Key)
    if ($index -lt 0 -or $arguments[$index + 1] -cne $binding.Value) { throw 'Retained native-save command binding changed.' }
}
$arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $proof.FullName 'native-boundary-arguments.json') -Encoding utf8
foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process') }
$dotnet = ($session.processes | Where-Object role -eq 'backend').exe
& $dotnet @arguments *> (Join-Path $proof.FullName 'native-boundary.log')
$code = $LASTEXITCODE
$result = Get-Content -LiteralPath (Join-Path $proof.FullName 'native-boundary.log') -Raw | ConvertFrom-Json
if ($code -ne 0 -or $result.status -cne 'ok' -or !$result.native_save_boundary_verified) { throw 'Original native save verifier did not accept the boundary.' }
$afterBody = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
$after = $afterBody | ConvertFrom-Json
[IO.File]::WriteAllText((Join-Path $proof.FullName 'after.json'), $afterBody)
if ($after.save_id.value -cne $before.save_id.value -or $after.player_id.value -cne $before.player_id.value -or
    $after.state.time.total_days.value -ne ($ExpectedTotalDay + 1) -or $after.state.time.time.value -ne 600) { throw 'Native save did not reach the same-player next morning.' }
$names = @($session.slot, ($session.slot + '_old'), 'SaveGameInfo', 'SaveGameInfo_old')
$files = @(Get-ChildItem -LiteralPath $slot -File)
if ($files.Count -ne 4 -or @($files | Where-Object { $_.Name -cnotin $names -or $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
    throw 'Native save slot no longer contains exactly four ordinary expected files.'
}
if ((Get-FileHash -LiteralPath (Join-Path $slot ($session.slot + '_old')) -Algorithm SHA256).Hash -cne $beforeMain) { throw 'Native save lineage differs from the prior actual main file.' }
$archive = New-Item -ItemType Directory -Path (Join-Path $proof.FullName 'sealed-save-slot')
$hashes = @(foreach ($file in $files) {
    $target = Join-Path $archive.FullName $file.Name
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $hash) { throw 'Sealed native save differs from its original file.' }
    [ordered]@{name=$file.Name;sha256=$hash}
})
[xml]$xml = Get-Content -LiteralPath (Join-Path $archive.FullName $session.slot) -Raw
if ($xml.SaveGame.player.farmName -cne $after.save_id.value -or
    $xml.SaveGame.player.UniqueMultiplayerID -cne $after.player_id.value -or
    [int]$xml.SaveGame.player.money -ne $after.state.player.money.value -or
    [int]$xml.SaveGame.dayOfMonth -ne $after.state.time.day.value -or
    [int]$xml.SaveGame.year -ne $after.state.time.year.value -or
    $xml.SaveGame.currentSeason -cne $after.state.time.season.value) { throw 'Independent saved XML differs from the actual next-morning player/date/balance.' }
$verified = [ordered]@{
    run_id=$session.run_id;save_id=$after.save_id.value;player_id=$after.player_id.value
    total_day=$after.state.time.total_days.value;season=$after.state.time.season.value;day_of_month=$after.state.time.day.value
    time=$after.state.time.time.value;cash=$after.state.player.money.value;health=$after.state.player.health.value
    deepest_mine=$after.state.player.deepest_mine_level.value;before_state_hash=$before.state_hash;after_state_hash=$after.state_hash
    native_save_boundary_verified=$true;independent_saved_xml_verified=$true;previous_main_sha256=$beforeMain;files=$hashes
    formal_training_authorized=$false
}
$verified | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $proof.FullName 'verified.json') -Encoding utf8
$verified | ConvertTo-Json -Depth 5
