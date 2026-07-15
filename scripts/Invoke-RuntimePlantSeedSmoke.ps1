param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-plant-seed-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-plant-seed-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $SeedId = "472",
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") { return $response }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $plantingReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "current_location" -and
                $snapshot.state.current_location.PSObject.Properties.Name -contains "planting_context") {
                $plantingReadable = $snapshot.state.current_location.planting_context.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);planting_context_readable=$plantingReadable"
            if ($saveReadable -and $plantingReadable) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready planting snapshot. Last status: $lastStatus"
}

function Find-PlantingResult {
    param($Snapshot, [int] $X, [int] $Y, [string] $Seed)
    $context = $Snapshot.state.current_location.planting_context.value
    if ($null -eq $context -or $null -eq $context.hoe_dirt_tiles) { return $null }
    foreach ($tile in @($context.hoe_dirt_tiles)) {
        if ([int]$tile.tile_x -ne $X -or [int]$tile.tile_y -ne $Y) { continue }
        foreach ($result in @($tile.seed_results)) {
            if ([string]$result.seed_id -eq $Seed) {
                return [pscustomobject]@{ tile = $tile; result = $result }
            }
        }
    }
    return $null
}

function Select-SeedIdForSnapshot {
    param($Snapshot)
    if (-not [string]::IsNullOrWhiteSpace($SeedId)) { return $SeedId }
    $context = $Snapshot.state.current_location.planting_context.value
    $season = [string]$context.season
    $daysRemaining = 28
    if ($null -ne $Snapshot.state.time -and $null -ne $Snapshot.state.time.day_of_month) {
        $daysRemaining = 28 - [int]$Snapshot.state.time.day_of_month.value
    }

    $catalog = $Snapshot.state.farm.crop_catalog.value
    $candidate = @($catalog | Where-Object {
        $_.seasons -contains $season -and
        [int]$_.grow_days -gt 0 -and
        [int]$_.grow_days -le $daysRemaining
    } | Sort-Object @{ Expression = { [int]$_.grow_days } }, seed_id | Select-Object -First 1)
    if ($candidate.Count -eq 0) {
        throw "No transparent crop_catalog seed can mature in current season '$season' with $daysRemaining days remaining."
    }

    return [string]$candidate[0].seed_id
}

function Wait-AllowedPlantingSnapshot {
    param([string] $Url, [int] $TimeoutSeconds, [int] $X, [int] $Y, [string] $Seed)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    $lastSnapshot = $null
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url -TimeoutSeconds 5
        $lastSnapshot = $snapshot
        $match = Find-PlantingResult -Snapshot $snapshot -X $X -Y $Y -Seed $Seed
        if ($null -ne $match -and $match.result.hard_rule_allows_planting -eq $true) {
            return $snapshot
        }

        if ($null -eq $match) {
            $last = "target planting result missing"
        }
        else {
            $last = ($match | ConvertTo-Json -Depth 16)
        }

        Start-Sleep -Milliseconds 500
    }

    return [pscustomobject]@{ timed_out = $true; last = $last; snapshot = $lastSnapshot }
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
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
    $selectedSeedId = Select-SeedIdForSnapshot -Snapshot $initialSnapshot

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-plant-seed-smoke"
        queue_item_id = "runtime-plant-seed-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_plant_seed_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        seed_id = $selectedSeedId
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    $beforePlantSnapshot = Wait-AllowedPlantingSnapshot -Url $snapshotUrl -TimeoutSeconds 30 -X $TargetTileX -Y $TargetTileY -Seed $selectedSeedId
    if ($beforePlantSnapshot.timed_out -eq $true) {
        if ($null -ne $beforePlantSnapshot.snapshot) {
            Write-JsonFile (Join-Path $runDirectory "snapshot-before-plant-rejected.json") $beforePlantSnapshot.snapshot
        }
        throw "Timed out waiting for transparent allowed planting context for $TargetTileX,$TargetTileY seed $selectedSeedId. Last: $($beforePlantSnapshot.last)"
    }
    Write-JsonFile (Join-Path $runDirectory "snapshot-before-plant.json") $beforePlantSnapshot
    $plantingResult = Find-PlantingResult -Snapshot $beforePlantSnapshot -X $TargetTileX -Y $TargetTileY -Seed $selectedSeedId
    if ($null -eq $plantingResult -or $plantingResult.result.hard_rule_allows_planting -ne $true) {
        throw "Fixture did not produce transparent allowed planting context for $TargetTileX,$TargetTileY seed $selectedSeedId."
    }

    $plantRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-plant-seed-smoke"
        queue_item_id = "runtime-plant-seed-smoke.plant"
        before_state_hash = $beforePlantSnapshot.state_hash
        option_id = "executor.plant_seed"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        seed_id = $selectedSeedId
    }
    $plantResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $plantRequest -TimeoutSeconds 120
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30

    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $setupResult.primitive_verification_status -eq "verified" -and $plantResult.status -eq "applied" -and $plantResult.primitive_verification_status -eq "verified") { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        seed_id = $selectedSeedId
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        planting_context_allowed_before = [bool]$plantingResult.result.hard_rule_allows_planting
        plant_status = $plantResult.status
        plant_verification = $plantResult.primitive_verification_status
        plant_reasons = @($plantResult.primitive_verification_reasons)
        plant_block_reasons = @($plantResult.block_reasons)
        bridge_state_hash_before = $beforePlantSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        executor_health = $executorHealth
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "plant-request.json") $plantRequest
    Write-JsonFile (Join-Path $runDirectory "plant-result.json") $plantResult
    Write-JsonFile (Join-Path $runDirectory "snapshot-after-plant.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 48
    if ($summary.status -ne "passed") { throw "Plant seed smoke failed." }
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
