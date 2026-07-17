param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-crab-pot-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($null -ne $result) { return $result }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            $saveReady = $snapshot.save_id.status -in @("available", "derived")
            $objectsReady = $null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "current_location" -and
                $snapshot.state.current_location.objects.status -in @("available", "derived")
            $lastStatus = "save=$($snapshot.save_id.status);objects=$objectsReady"
            if ($saveReady -and $objectsReady) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    return [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-crab-pot-smoke"
        queue_item_id = $QueueItemId
        before_state_hash = $Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slot under $savesPath" }
    $SaveSlot = $slot.Name
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-crab-pot-smoke\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$savedEnvironment = @{}
foreach ($name in @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null
    $initial = Wait-WorldSnapshot $snapshotUrl 120

    $setup = New-BaseRequest $initial "debug.setup_crab_pot_target" "setup"
    $setup.target_tile_x = $TargetTileX
    $setup.target_tile_y = $TargetTileY
    $setup.qualified_item_id = "(O)372"
    $setup.quantity = 1
    $setupResult = Invoke-JsonPost $executorUrl $setup
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "setup-result.json") -Encoding utf8
    Start-Sleep -Milliseconds 750
    $before = Wait-WorldSnapshot $snapshotUrl 30
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "before-snapshot.json") -Encoding utf8
    $pot = @($before.state.current_location.objects.value) | Where-Object { [int]$_.tile_x -eq $TargetTileX -and [int]$_.tile_y -eq $TargetTileY } | Select-Object -First 1
    if ($null -eq $pot -or $pot.crab_pot_collect_status -ne "ready") {
        throw "Transparent crab-pot fixture was not ready."
    }

    $collect = New-BaseRequest $before "executor.collect_crab_pot" "collect"
    $collect.target_tile_x = $TargetTileX
    $collect.target_tile_y = $TargetTileY
    $collect.stand_tile_x = [int]$before.state.player.tile_x.value
    $collect.stand_tile_y = [int]$before.state.player.tile_y.value
    $collect.target_runtime_type = [string]$pot.type
    $collect.qualified_item_id = [string]$pot.crab_pot_output_qualified_item_id
    $collect.quantity = [int]$pot.crab_pot_output_stack_on_collect
    $collect.expected_output_items_json = [string]$pot.crab_pot_expected_output_items_json
    $collect.expected_skill_id = "fishing"
    $collect.expected_skill_experience_delta = [int]$pot.crab_pot_fishing_experience_on_success_min
    $collect.expected_container_bait_qualified_item_id = [string]$pot.crab_pot_bait_qualified_item_id
    $collect.expected_fish_collection_eligible = if ($pot.crab_pot_fish_collection_eligible) { 1 } else { 0 }
    $collect.expected_fish_caught_count_before = [int]$pot.crab_pot_fish_caught_count_before
    $collect.expected_fish_caught_count_after = [int]$pot.crab_pot_fish_caught_count_after
    $collect.expected_fish_caught_max_size_before = [int]$pot.crab_pot_fish_caught_max_size_before
    $collect.expected_catch_size_min = [int]$pot.crab_pot_catch_size_min
    $collect.expected_catch_size_max = [int]$pot.crab_pot_catch_size_max
    $collect.catch_size_projection_status = [string]$pot.crab_pot_catch_size_projection_status
    $collect.max_movement_tiles = 512
    $collectResult = Invoke-JsonPost $executorUrl $collect
    Start-Sleep -Milliseconds 500
    $after = Wait-WorldSnapshot $snapshotUrl 30

    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $collectResult.status -eq "applied" -and $collectResult.primitive_verification_status -eq "verified") { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        setup_status = $setupResult.status
        collect_status = $collectResult.status
        collect_verification = $collectResult.primitive_verification_status
        collect_reasons = @($collectResult.primitive_verification_reasons)
        collect_block_reasons = @($collectResult.block_reasons)
        observed_effect = $collectResult.observed_effect
    }
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "setup-result.json") -Encoding utf8
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "before-snapshot.json") -Encoding utf8
    $collectResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "collect-result.json") -Encoding utf8
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "after-snapshot.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 16
    if ($summary.status -ne "passed") { throw "Runtime crab-pot smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
