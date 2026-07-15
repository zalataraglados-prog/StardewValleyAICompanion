param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-machine-input-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-machine-input-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $QualifiedItemId = "(O)262",
    [switch] $RequireTransparentLoadableInput,
    [switch] $RequireNativePredictedOutput,
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
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $farmReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "farm" -and
                $snapshot.state.farm.PSObject.Properties.Name -contains "machines") {
                $farmReadable = $snapshot.state.farm.machines.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);farm_machines_readable=$farmReadable"
            if ($saveReadable -and $farmReadable) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready farm machines snapshot. Last status: $lastStatus"
}

function Find-MachineAtTile {
    param($Snapshot, [int] $X, [int] $Y)
    if ($null -eq $Snapshot.state.farm.machines.value) { return $null }
    foreach ($machine in @($Snapshot.state.farm.machines.value)) {
        if ([int]$machine.tile_x -eq $X -and [int]$machine.tile_y -eq $Y) { return $machine }
    }
    return $null
}

function Find-LoadableInput {
    param($Machine, [string] $ItemId)
    if ($null -eq $Machine -or $null -eq $Machine.loadable_inputs) { return $null }
    foreach ($input in @($Machine.loadable_inputs)) {
        if ([string]::IsNullOrWhiteSpace($ItemId) -or [string]$input.qualified_item_id -eq $ItemId) { return $input }
    }
    return $null
}

function Read-InputSlotIndexFromSetup {
    param($SetupResult)
    $texts = @()
    if ($null -ne $SetupResult.primitive_verification_reasons) { $texts += @($SetupResult.primitive_verification_reasons) }
    if ($null -ne $SetupResult.observed_effect) { $texts += [string]$SetupResult.observed_effect }
    foreach ($text in $texts) {
        if ([string]$text -match "input_slot_index=(-?\d+)") {
            return [int]$Matches[1]
        }
    }
    return -1
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=machine"
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
    Start-Sleep -Seconds 20
    $initialSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-input-smoke"
        queue_item_id = "runtime-machine-input-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_machine_input_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        qualified_item_id = $QualifiedItemId
        quantity = 2
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Start-Sleep -Milliseconds 500
    $beforeLoadSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $targetMachine = Find-MachineAtTile -Snapshot $beforeLoadSnapshot -X $TargetTileX -Y $TargetTileY
    $input = Find-LoadableInput -Machine $targetMachine -ItemId $QualifiedItemId
    $inputSlotIndex = Read-InputSlotIndexFromSetup -SetupResult $setupResult
    if ($null -eq $targetMachine -or $inputSlotIndex -lt 0) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-load-rejected.json") $beforeLoadSnapshot
        throw "Fixture did not produce machine target or setup input slot for $QualifiedItemId at $TargetTileX,$TargetTileY."
    }
    if ($RequireTransparentLoadableInput -and $null -eq $input) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-load-rejected.json") $beforeLoadSnapshot
        throw "Transparent machine loadable_inputs did not include $QualifiedItemId at $TargetTileX,$TargetTileY."
    }
    $predictedOutput = $input.predicted_output
    $predictedOutputStatus = if ($null -ne $predictedOutput) { [string]$predictedOutput.status } else { "" }
    $predictedOutputSource = if ($null -ne $predictedOutput) { [string]$predictedOutput.source } else { "" }
    $predictedOutputSalePrice = if ($null -ne $predictedOutput -and $null -ne $predictedOutput.sale_price) { [int]$predictedOutput.sale_price } else { 0 }
    $predictedOutputItemId = if ($null -ne $predictedOutput -and $null -ne $predictedOutput.item) { [string]$predictedOutput.item.qualified_item_id } else { "" }
    $predictedOutputRuleId = if ($null -ne $predictedOutput) { [string]$predictedOutput.matched_rule_id } else { "" }
    $predictedOutputEffectiveMinutes = if ($null -ne $predictedOutput -and $null -ne $predictedOutput.effective_minutes_until_ready) { [int]$predictedOutput.effective_minutes_until_ready } else { 0 }
    if ($RequireNativePredictedOutput -and
        ($predictedOutputStatus -ne "available" -or
            $predictedOutputSource -ne "MachineDataUtility.GetOutputItem(probe:true)" -or
            [string]::IsNullOrWhiteSpace($predictedOutputItemId) -or
            $predictedOutputSalePrice -le 0)) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-load-rejected.json") $beforeLoadSnapshot
        throw "Native machine predicted_output was not transparently available for $QualifiedItemId at $TargetTileX,$TargetTileY."
    }

    $loadRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-input-smoke"
        queue_item_id = "runtime-machine-input-smoke.load"
        before_state_hash = $beforeLoadSnapshot.state_hash
        option_id = "executor.load_machine_input"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        input_slot_index = $inputSlotIndex
        qualified_item_id = $QualifiedItemId
    }
    $loadResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $loadRequest -TimeoutSeconds 120
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $afterMachine = Find-MachineAtTile -Snapshot $afterSnapshot -X $TargetTileX -Y $TargetTileY
    $afterMinutes = if ($null -ne $afterMachine) { [int]$afterMachine.minutes_until_ready } else { -999 }
    $afterReady = if ($null -ne $afterMachine) { [bool]$afterMachine.ready_for_harvest } else { $false }
    $afterHeld = if ($null -ne $afterMachine -and $null -ne $afterMachine.held_item) { [string]$afterMachine.held_item.qualified_item_id } else { "" }

    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $setupResult.primitive_verification_status -eq "verified" -and $loadResult.status -eq "applied" -and $loadResult.primitive_verification_status -eq "verified" -and ($afterMinutes -gt 0 -or $afterReady -or -not [string]::IsNullOrWhiteSpace($afterHeld))) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        qualified_item_id = $QualifiedItemId
        input_slot_index = $inputSlotIndex
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        machine_present_before = $null -ne $targetMachine
        loadable_input_before = $null -ne $input
        transparent_loadable_input_required = [bool]$RequireTransparentLoadableInput
        native_predicted_output_status = $predictedOutputStatus
        native_predicted_output_source = $predictedOutputSource
        native_predicted_output_item_id = $predictedOutputItemId
        native_predicted_output_sale_price = $predictedOutputSalePrice
        native_predicted_output_rule_id = $predictedOutputRuleId
        native_predicted_output_effective_minutes = $predictedOutputEffectiveMinutes
        native_predicted_output_required = [bool]$RequireNativePredictedOutput
        load_status = $loadResult.status
        load_verification = $loadResult.primitive_verification_status
        load_reasons = @($loadResult.primitive_verification_reasons)
        load_block_reasons = @($loadResult.block_reasons)
        machine_minutes_after = $afterMinutes
        machine_ready_after = $afterReady
        machine_held_after = $afterHeld
        bridge_state_hash_before = $beforeLoadSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        state_hash_changed = $beforeLoadSnapshot.state_hash -ne $afterSnapshot.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "load-result.json") $loadResult
    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "before-load-snapshot.json") $beforeLoadSnapshot
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") { throw "Runtime machine input smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
