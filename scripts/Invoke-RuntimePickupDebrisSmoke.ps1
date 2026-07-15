param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-pickup-debris-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-pickup-debris-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $QualifiedItemId = "(O)388",
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
                $snapshot.state.farm.PSObject.Properties.Name -contains "debris") {
                $farmReadable = $snapshot.state.farm.debris.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);farm_debris_readable=$farmReadable"
            if ($saveReadable -and $farmReadable) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready farm debris snapshot. Last status: $lastStatus"
}

function Find-DebrisAtTile {
    param($Snapshot, [int] $X, [int] $Y, [string] $ItemId)
    if ($null -eq $Snapshot.state.farm.debris.value) { return $null }
    foreach ($debris in @($Snapshot.state.farm.debris.value)) {
        $qualified = [string]$debris.qualified_item_id
        if (-not [string]::IsNullOrWhiteSpace($ItemId) -and $qualified -ne $ItemId) { continue }
        foreach ($chunk in @($debris.chunks)) {
            if ([int]$chunk.tile_x -eq $X -and [int]$chunk.tile_y -eq $Y) { return $debris }
        }
    }
    return $null
}

function Find-DebrisForItem {
    param($Snapshot, [string] $ItemId)
    if ($null -eq $Snapshot.state.farm.debris.value) { return $null }
    foreach ($debris in @($Snapshot.state.farm.debris.value)) {
        $qualified = [string]$debris.qualified_item_id
        if ([string]::IsNullOrWhiteSpace($ItemId) -or $qualified -eq $ItemId) { return $debris }
    }
    return $null
}

function Get-FirstDebrisTile {
    param($Debris)
    foreach ($chunk in @($Debris.chunks)) {
        return [pscustomobject]@{ x = [int]$chunk.tile_x; y = [int]$chunk.tile_y }
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
        queue_id = "runtime-pickup-debris-smoke"
        queue_item_id = "runtime-pickup-debris-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_debris_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        qualified_item_id = $QualifiedItemId
        quantity = 1
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Start-Sleep -Milliseconds 500
    $beforePickupSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $targetDebris = Find-DebrisForItem -Snapshot $beforePickupSnapshot -ItemId $QualifiedItemId
    $debrisTile = if ($null -ne $targetDebris) { Get-FirstDebrisTile -Debris $targetDebris } else { $null }
    if ($null -eq $targetDebris) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-pickup-rejected.json") $beforePickupSnapshot
        throw "Fixture did not produce debris for $QualifiedItemId."
    }
    if ($null -eq $debrisTile) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-pickup-rejected.json") $beforePickupSnapshot
        throw "Fixture debris has no transparent chunk tile."
    }

    $pickupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-pickup-debris-smoke"
        queue_item_id = "runtime-pickup-debris-smoke.pickup"
        before_state_hash = $beforePickupSnapshot.state_hash
        option_id = "executor.pickup_debris"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $debrisTile.x
        target_tile_y = $debrisTile.y
        debris_index = [int]$targetDebris.debris_index
        qualified_item_id = $QualifiedItemId
    }
    $pickupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $pickupRequest -TimeoutSeconds 120
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $afterDebris = Find-DebrisAtTile -Snapshot $afterSnapshot -X $debrisTile.x -Y $debrisTile.y -ItemId $QualifiedItemId
    $beforeDebrisCount = Count-Debris -Snapshot $beforePickupSnapshot
    $afterDebrisCount = Count-Debris -Snapshot $afterSnapshot

    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $setupResult.primitive_verification_status -eq "verified" -and $pickupResult.status -eq "applied" -and $pickupResult.primitive_verification_status -eq "verified" -and $null -eq $afterDebris -and $afterDebrisCount -lt $beforeDebrisCount) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        transparent_debris_tile = "$($debrisTile.x),$($debrisTile.y)"
        qualified_item_id = $QualifiedItemId
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        debris_present_before = $null -ne $targetDebris
        pickup_status = $pickupResult.status
        pickup_verification = $pickupResult.primitive_verification_status
        pickup_reasons = @($pickupResult.primitive_verification_reasons)
        pickup_block_reasons = @($pickupResult.block_reasons)
        debris_present_after = $null -ne $afterDebris
        debris_count_before = $beforeDebrisCount
        debris_count_after = $afterDebrisCount
        debris_count_decreased = $afterDebrisCount -lt $beforeDebrisCount
        bridge_state_hash_before = $beforePickupSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        state_hash_changed = $beforePickupSnapshot.state_hash -ne $afterSnapshot.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "pickup-result.json") $pickupResult
    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "before-pickup-snapshot.json") $beforePickupSnapshot
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") { throw "Runtime pickup debris smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
