param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-panning-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
        try { $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5; if ($null -ne $value) { return $value } }
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
            $ready = $snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.current_location.panning.status -in @("available", "derived")
            $lastStatus = "save=$($snapshot.save_id.status);panning=$($snapshot.state.current_location.panning.status)"
            if ($ready) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready panning snapshot. Last status: $lastStatus"
}

function Wait-ExactPanningSnapshot([string] $Url, [int] $TargetX, [int] $TargetY, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot $Url 10
        $pan = $snapshot.state.current_location.panning.value
        $lastStatus = "status=$($pan.status);point=$($pan.ore_pan_point_x),$($pan.ore_pan_point_y)"
        if ($pan.status -eq "exact" -and $pan.ore_pan_point_active -and [int]$pan.ore_pan_point_x -eq $TargetX -and [int]$pan.ore_pan_point_y -eq $TargetY) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for exact panning projection at $TargetX,$TargetY. Last status: $lastStatus"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-panning-smoke";
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId;
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath;
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-PanCase([int] $UpgradeLevel, [int] $Offset) {
    $initial = Wait-WorldSnapshot $snapshotUrl 30
    $caseName = "upgrade-$UpgradeLevel"
    $targetX = $TargetTileX + $Offset
    $setup = New-BaseRequest $initial "debug.setup_pan_ore_spot" ("setup-" + $caseName)
    $setup.target_tile_x = $targetX; $setup.target_tile_y = $TargetTileY; $setup.pan_upgrade_level = $UpgradeLevel
    $setupResult = Invoke-JsonPost $executorUrl $setup
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($caseName + "-setup-result.json")) -Encoding utf8
    if ($setupResult.status -ne "applied") {
        throw "Pan fixture setup failed for $caseName. reasons=$(@($setupResult.block_reasons) -join ','); observed=$($setupResult.observed_effect)"
    }
    $before = Wait-ExactPanningSnapshot $snapshotUrl $targetX $TargetTileY 30
    $pan = $before.state.current_location.panning.value
    if ($pan.status -ne "exact" -or -not $pan.ore_pan_point_active) {
        throw "Transparent Pan projection was not exact for $caseName. status=$($pan.status)"
    }

    $collect = New-BaseRequest $before "executor.pan_ore_spot" ("collect-" + $caseName)
    $collect.location_id = [string]$pan.location_id
    $collect.target_tile_x = [int]$pan.ore_pan_point_x; $collect.target_tile_y = [int]$pan.ore_pan_point_y
    $collect.stand_tile_x = [int]$before.state.player.tile_x.value; $collect.stand_tile_y = [int]$before.state.player.tile_y.value
    $collect.target_runtime_type = "StardewValley.Tools.Pan"; $collect.required_tool_kind = "Pan"
    $collect.tool_slot_index = [int]$pan.pan_tool_slot_index; $collect.pan_upgrade_level = [int]$pan.pan_upgrade_level
    $collect.pan_enchantments_json = [string]$pan.pan_enchantments_json
    $collect.click_pixel_x = [int]$pan.click_pixel_x; $collect.click_pixel_y = [int]$pan.click_pixel_y
    $collect.expected_output_items_json = [string]$pan.expected_output_items_json
    $collect.expected_stat_increments_json = [string]$pan.expected_receipt_stat_increments_json
    $collect.expected_times_panned_before = [int]$pan.times_panned_before; $collect.expected_times_panned_after = [int]$pan.times_panned_after
    $collect.expected_mining_experience_before = [int]$pan.mining_experience_before
    $collect.expected_mining_experience_delta = [int]$pan.mining_experience_delta
    $collect.expected_mining_experience_after = [int]$pan.mining_experience_after
    $collect.expected_foraging_experience_before = [int]$pan.foraging_experience_before
    $collect.expected_foraging_experience_delta = [int]$pan.foraging_experience_delta
    $collect.expected_foraging_experience_after = [int]$pan.foraging_experience_after
    $collect.post_use_ore_pan_point_status = [string]$pan.post_use_ore_pan_point_status
    $collect.post_use_respawn_attempts = [int]$pan.post_use_respawn_attempts; $collect.max_movement_tiles = 512
    $result = Invoke-JsonPost $executorUrl $collect
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($caseName + "-before-snapshot.json")) -Encoding utf8
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($caseName + "-result.json")) -Encoding utf8
    return [ordered]@{
        upgrade_level = $UpgradeLevel; setup_status = $setupResult.status; collect_status = $result.status;
        verification = $result.primitive_verification_status; reasons = @($result.primitive_verification_reasons);
        block_reasons = @($result.block_reasons); observed_effect = $result.observed_effect
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-panning-smoke\" + $RunId); New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$savedEnvironment = @{}; foreach ($name in @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-WorldSnapshot $snapshotUrl 120 | Out-Null
    $cases = @(); $cases += Invoke-PanCase 1 0; $cases += Invoke-PanCase 2 2
    $passed = @($cases | Where-Object { $_.setup_status -eq "applied" -and $_.collect_status -eq "applied" -and $_.verification -eq "verified" }).Count -eq 2
    $summary = [ordered]@{ status = if ($passed) { "passed" } else { "failed" }; run_id = $RunId; save_slot = $SaveSlot; cases = $cases }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Runtime panning smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
