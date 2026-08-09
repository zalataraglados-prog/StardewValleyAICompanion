param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-apply-fertilizer-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-apply-fertilizer-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [switch] $IndoorPot,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 3
            if ($response.status -eq "ok") { return $response }
        }
        catch {}
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url."
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.current_location.planting_context.status -in @("available", "derived")) {
                return $snapshot
            }
        }
        catch {}
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a world-ready planting snapshot."
}

function Find-FertilizerTarget {
    param($Snapshot, [int] $X, [int] $Y)
    foreach ($tile in @($Snapshot.state.current_location.planting_context.value.hoe_dirt_tiles)) {
        if ([int]$tile.tile_x -ne $X -or [int]$tile.tile_y -ne $Y) { continue }
        $fertilizer = @($tile.fertilizer_results | Where-Object {
            $_.hard_rule_allows_application -eq $true -and
            -not [string]::IsNullOrWhiteSpace([string]$_.qualified_item_id) -and
            $null -ne $_.slot_index
        }) | Select-Object -First 1
        if ($null -ne $fertilizer) {
            return [pscustomobject]@{ tile = $tile; fertilizer = $fertilizer }
        }
    }
    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
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

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $initialSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId
        queue_id = "runtime-apply-fertilizer-smoke"; queue_item_id = "runtime-apply-fertilizer-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = if ($IndoorPot) { "debug.setup_indoor_pot_fertilizer_target" } else { "debug.setup_fertilizer_target" }
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX; target_tile_y = $TargetTileY
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    Start-Sleep -Milliseconds 500
    $beforeSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $target = Find-FertilizerTarget -Snapshot $beforeSnapshot -X $TargetTileX -Y $TargetTileY
    if ($null -eq $target) { throw "Fixture exposed no runtime-legal fertilizer candidate." }

    $applyRequest = [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId
        queue_id = "runtime-apply-fertilizer-smoke"; queue_item_id = "runtime-apply-fertilizer-smoke.apply"
        before_state_hash = $beforeSnapshot.state_hash; option_id = "executor.apply_fertilizer"
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX; target_tile_y = $TargetTileY
        location_id = [string]$beforeSnapshot.state.player.location_id.value
        qualified_item_id = [string]$target.fertilizer.qualified_item_id
        slot_index = [int]$target.fertilizer.slot_index
        max_movement_tiles = 512
    }
    $applyResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $applyRequest
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $afterTile = @($afterSnapshot.state.current_location.planting_context.value.hoe_dirt_tiles | Where-Object {
        [int]$_.tile_x -eq $TargetTileX -and [int]$_.tile_y -eq $TargetTileY
    }) | Select-Object -First 1
    $verifiedAfter = $null -ne $afterTile -and
        [string]$afterTile.fertilizer_id -eq [string]$target.fertilizer.qualified_item_id
    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $applyResult.status -eq "applied" -and
            $applyResult.primitive_verification_status -eq "verified" -and $verifiedAfter) { "passed" } else { "failed" }
        run_id = $RunId; save_slot = $SaveSlot; target_tile = "$TargetTileX,$TargetTileY"
        target_kind = if ($IndoorPot) { "indoor_pot" } else { "terrain_hoe_dirt" }
        fertilizer_qualified_item_id = [string]$target.fertilizer.qualified_item_id
        fertilizer_slot_index = [int]$target.fertilizer.slot_index
        transparent_rule_allowed_before = [bool]$target.fertilizer.hard_rule_allows_application
        setup_status = $setupResult.status; apply_status = $applyResult.status
        apply_verification = $applyResult.primitive_verification_status
        apply_reasons = @($applyResult.primitive_verification_reasons)
        apply_block_reasons = @($applyResult.block_reasons)
        transparent_fertilizer_id_after = if ($null -eq $afterTile) { "" } else { [string]$afterTile.fertilizer_id }
        bridge_state_hash_before = $beforeSnapshot.state_hash; bridge_state_hash_after = $afterSnapshot.state_hash
        executor_health = $executorHealth; kept_game_running = [bool]$KeepGameRunning
    }
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "snapshot-before-apply.json") $beforeSnapshot
    Write-JsonFile (Join-Path $runDirectory "apply-request.json") $applyRequest
    Write-JsonFile (Join-Path $runDirectory "apply-result.json") $applyResult
    Write-JsonFile (Join-Path $runDirectory "snapshot-after-apply.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 48
    if ($summary.status -ne "passed") { throw "Apply fertilizer smoke failed. See $runDirectory" }
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) { Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue }
        else { Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value }
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
