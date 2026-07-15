param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-giant-crop-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-giant-crop-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $GiantCropId = "276",
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
            $farmReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "farm" -and
                $snapshot.state.farm.PSObject.Properties.Name -contains "resource_clumps") {
                $farmReadable = $snapshot.state.farm.resource_clumps.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);farm_resource_clumps_readable=$farmReadable"
            if ($saveReadable -and $farmReadable) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready farm resource-clump snapshot. Last status: $lastStatus"
}

function Find-GiantCrop {
    param($Snapshot, [int] $X, [int] $Y)
    if ($null -eq $Snapshot.state.farm.resource_clumps.value) { return $null }
    foreach ($clump in @($Snapshot.state.farm.resource_clumps.value)) {
        $cx = [int]$clump.tile_x
        $cy = [int]$clump.tile_y
        $width = [Math]::Max(1, [int]$clump.width)
        $height = [Math]::Max(1, [int]$clump.height)
        if ($clump.is_giant_crop -eq $true -and
            $X -ge $cx -and $X -lt ($cx + $width) -and
            $Y -ge $cy -and $Y -lt ($cy + $height)) {
            return $clump
        }
    }
    return $null
}

function Count-Debris {
    param($Snapshot)
    if ($null -eq $Snapshot.state.farm.debris.value) { return 0 }
    return @($Snapshot.state.farm.debris.value).Count
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

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-giant-crop-smoke"
        queue_item_id = "runtime-giant-crop-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_giant_crop_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        giant_crop_id = $GiantCropId
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Start-Sleep -Milliseconds 500
    $beforeHarvestSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $beforeClump = Find-GiantCrop -Snapshot $beforeHarvestSnapshot -X $TargetTileX -Y $TargetTileY
    if ($null -eq $beforeClump) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-giant-harvest-rejected.json") $beforeHarvestSnapshot
        throw "Fixture did not produce giant crop resource clump at $TargetTileX,$TargetTileY."
    }

    $harvestRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-giant-crop-smoke"
        queue_item_id = "runtime-giant-crop-smoke.harvest"
        before_state_hash = $beforeHarvestSnapshot.state_hash
        option_id = "executor.harvest_giant_crop"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        giant_crop_id = $GiantCropId
    }
    $harvestResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $harvestRequest -TimeoutSeconds 120
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $afterClump = Find-GiantCrop -Snapshot $afterSnapshot -X $TargetTileX -Y $TargetTileY
    $beforeDebrisCount = Count-Debris -Snapshot $beforeHarvestSnapshot
    $afterDebrisCount = Count-Debris -Snapshot $afterSnapshot

    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $setupResult.primitive_verification_status -eq "verified" -and $harvestResult.status -eq "applied" -and $harvestResult.primitive_verification_status -eq "verified" -and $null -eq $afterClump -and $afterDebrisCount -gt $beforeDebrisCount) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        giant_crop_id = $GiantCropId
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        giant_crop_present_before = $null -ne $beforeClump
        harvest_status = $harvestResult.status
        harvest_verification = $harvestResult.primitive_verification_status
        harvest_reasons = @($harvestResult.primitive_verification_reasons)
        harvest_block_reasons = @($harvestResult.block_reasons)
        giant_crop_present_after = $null -ne $afterClump
        debris_count_before = $beforeDebrisCount
        debris_count_after = $afterDebrisCount
        debris_count_increased = $afterDebrisCount -gt $beforeDebrisCount
        bridge_state_hash_before = $beforeHarvestSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        state_hash_changed = $beforeHarvestSnapshot.state_hash -ne $afterSnapshot.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "harvest-result.json") $harvestResult
    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "before-harvest-snapshot.json") $beforeHarvestSnapshot
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") { throw "Runtime giant crop smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
