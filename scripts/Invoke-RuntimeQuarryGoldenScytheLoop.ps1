param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [int] $MaximumSteps = 160,
    [int] $StartupTimeoutSeconds = 120,
    [int] $LatestExitTime = 2400,
    [int] $MinimumReserveHealth = 1,
    [int] $MinimumReserveEnergy = 0,
    [string] $RunId = ("runtime-quarry-golden-scythe-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-quarry-golden-scythe",
    [switch] $VisibleGame,
    [switch] $KeepProcessesRunning
)

$ErrorActionPreference = "Stop"
$recoverableReplanReasons = @(
    "combat_disengaged_transit_target",
    "combat_target_not_found_or_moved",
    "mine_stone_target_not_breakable_stone"
)

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") {
                return $response
            }
            $lastError = "status=$($response.status)"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Read-FieldValue {
    param($Snapshot, [string] $Section, [string] $Field)
    if ($null -eq $Snapshot.state.$Section.$Field) {
        return $null
    }
    return $Snapshot.state.$Section.$Field.value
}

function Read-PlayerLocation {
    param($Snapshot)
    return [string](Read-FieldValue -Snapshot $Snapshot -Section "player" -Field "location_id")
}

function Read-PlayerTile {
    param($Snapshot)
    return [ordered]@{
        x = [int](Read-FieldValue -Snapshot $Snapshot -Section "player" -Field "tile_x")
        y = [int](Read-FieldValue -Snapshot $Snapshot -Section "player" -Field "tile_y")
    }
}

function Read-MiningField {
    param($Snapshot, [string] $Field)
    return Read-FieldValue -Snapshot $Snapshot -Section "mining" -Field $Field
}

function Read-QuarryState {
    param($Snapshot)
    $mine = Read-MiningField -Snapshot $Snapshot -Field "current_mine"
    $objectives = Read-MiningField -Snapshot $Snapshot -Field "floor_objectives"
    $resources = Read-MiningField -Snapshot $Snapshot -Field "player_resources"
    $tiles = Read-MiningField -Snapshot $Snapshot -Field "tiles"
    return [ordered]@{
        level = if ($null -eq $mine.mine_level) { $null } else { [int]$mine.mine_level }
        kind = [string]$mine.mine_kind
        claimed = [bool]$objectives.golden_scythe_claimed
        inventory_count = [int]$resources.golden_scythe_inventory_count
        altar_count = @($tiles.golden_scythe_altars).Count
    }
}

function Wait-WorldSnapshot {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=route"
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastError = "save_id=$($snapshot.save_id.status)"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for isolated save. Last error: $lastError"
}

function Wait-QuarrySnapshot {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=mining"
            $quarry = Read-QuarryState $snapshot
            if ($snapshot.state.mining.completeness.value.status -eq "complete" -and
                $quarry.level -eq 77377 -and
                $quarry.kind -eq "quarry_mine") {
                return $snapshot
            }
            $lastError = "quarry_snapshot_not_ready"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for a complete Quarry Mine snapshot. Last error: $lastError"
}

function Clear-TransientMenus {
    param($Snapshot, [string] $Phase)
    $current = $Snapshot
    for ($attempt = 1; $attempt -le 16; $attempt++) {
        if (-not [bool]$current.state.menus.active_menu.value.is_open) {
            return $current
        }

        $request = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-quarry-golden-scythe"
            queue_item_id = "runtime-quarry-golden-scythe.$Phase.close-menu-$attempt"
            before_state_hash = $current.state_hash
            option_id = "executor.close_menu"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $runtimeSaves
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            social_continuation_dialogue_recovery = $true
        }
        $result = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $request `
            -TimeoutSeconds 60
        Write-JsonFile `
            (Join-Path $runDirectory ("$Phase-close-menu-$attempt.json")) `
            $result
        if ($result.status -ne "applied" -or
            $result.primitive_verification_status -ne "verified") {
            throw "$Phase could not close transient menu: $(@($result.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 250
        $current = Wait-WorldSnapshot -TimeoutSeconds 30
    }
    throw "$Phase transient menu did not settle after 16 native advances."
}

function Read-ExecutionPrimitive {
    param($Execution)
    $steps = @($Execution.step_results)
    if ($steps.Count -gt 0) {
        return $steps[-1]
    }
    return $Execution
}

if ($MaximumSteps -lt 2) {
    throw "MaximumSteps must be at least 2."
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$sourceSaves = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
$trainingRoot = Join-Path $runDirectory "training"
$backendLog = Join-Path $runDirectory "backend.log"
$backendErrorLog = Join-Path $runDirectory "backend-error.log"
$backendDll = Join-Path $ProjectRoot "src\StardewAI.Backend\bin\Debug\net8.0\StardewAI.Backend.dll"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Debug\net8.0\StardewAI.LiveTrainingLoop.dll"
$gameProcess = $null
$backendProcess = $null
$stepSummaries = New-Object System.Collections.Generic.List[object]
$claimVerified = $false
$exitVerified = $false

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $sourceSaves -PathType Container)) {
    throw "Isolated saves path not found: $sourceSaves"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $sourceSaves -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $runtimeSaves"
    }
    $SaveSlot = $slot.Name
}

$occupiedPorts = Get-NetTCPConnection -State Listen -LocalPort 5108, 8765, 8767 -ErrorAction SilentlyContinue
if (@($occupiedPorts).Count -gt 0) {
    throw "Required runtime ports are occupied: $(@($occupiedPorts.LocalPort | Sort-Object -Unique) -join ',')"
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
$sourceSaveSlot = Join-Path $sourceSaves $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSaveSlot -PathType Container)) {
    throw "Source save slot not found: $sourceSaveSlot"
}
$runtimeSaves = Join-Path $runDirectory "isolated-saves"
$runtimeSaveSlot = Join-Path $runtimeSaves $SaveSlot
New-Item -ItemType Directory -Force -Path $runtimeSaveSlot | Out-Null
Get-ChildItem -LiteralPath $sourceSaveSlot -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtimeSaveSlot $_.Name) -Force
}
$oldSave = Join-Path $runtimeSaveSlot ($SaveSlot + "_old")
$currentSave = Join-Path $runtimeSaveSlot $SaveSlot
$oldSaveInfo = Join-Path $runtimeSaveSlot "SaveGameInfo_old"
$currentSaveInfo = Join-Path $runtimeSaveSlot "SaveGameInfo"
$usedOldSaveSnapshot = Test-Path -LiteralPath $oldSave -PathType Leaf
if ($usedOldSaveSnapshot) {
    Copy-Item -LiteralPath $oldSave -Destination $currentSave -Force
    if (Test-Path -LiteralPath $oldSaveInfo -PathType Leaf) {
        Copy-Item -LiteralPath $oldSaveInfo -Destination $currentSaveInfo -Force
    }
}

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_MINING_CALIBRATION_LOADOUT = $env:STARDEWAI_MINING_CALIBRATION_LOADOUT
    STARDEWAI_QUARRY_RESET_GOLDEN_SCYTHE = $env:STARDEWAI_QUARRY_RESET_GOLDEN_SCYTHE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    SMAPI_MODS_PATH = $env:SMAPI_MODS_PATH
}

try {
    & dotnet build (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj") --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Backend build failed with exit code $LASTEXITCODE."
    }
    & dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "LiveTrainingLoop build failed with exit code $LASTEXITCODE."
    }
    & (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
    & (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

    New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
    foreach ($modName in @(
        "StardewAI.TransparentBridge",
        "StardewAI.RuntimeTestHarness"
    )) {
        $sourceMod = Join-Path (Join-Path $runtimeGameDir "Mods") $modName
        $targetMod = Join-Path $smokeModsPath $modName
        if (-not (Test-Path -LiteralPath $sourceMod -PathType Container)) {
            throw "Required smoke mod is missing: $sourceMod"
        }
        New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
        Copy-Item `
            -Path (Join-Path $sourceMod "*") `
            -Destination $targetMod `
            -Recurse `
            -Force
    }

    $env:STARDEWAI_TEST_SAVES = $runtimeSaves
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $runtimeSaves
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_MINING_CALIBRATION_LOADOUT = "1"
    $env:STARDEWAI_QUARRY_RESET_GOLDEN_SCYTHE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $gameStart = @{
        FilePath = $smapiExe
        WorkingDirectory = $runtimeGameDir
        PassThru = $true
    }
    if (-not $VisibleGame) {
        $gameStart.WindowStyle = "Hidden"
    }
    $gameProcess = Start-Process @gameStart
    Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30 | Out-Null
    Start-Sleep -Seconds 20
    $worldSnapshot = Wait-WorldSnapshot -TimeoutSeconds $StartupTimeoutSeconds
    $worldSnapshot = Clear-TransientMenus -Snapshot $worldSnapshot -Phase "pre-setup"

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-quarry-golden-scythe"
        queue_item_id = "runtime-quarry-golden-scythe.setup"
        before_state_hash = $worldSnapshot.state_hash
        option_id = "debug.setup_quarry_mine"
        mine_level = 77377
        save_isolation_path = $runtimeSaves
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Quarry fixture failed: status=$($setupResult.status); reasons=$(@($setupResult.block_reasons) -join ',')"
    }

    $postSetupWorld = Wait-WorldSnapshot -TimeoutSeconds $StartupTimeoutSeconds
    Clear-TransientMenus -Snapshot $postSetupWorld -Phase "post-setup" | Out-Null
    $beforeSetupSnapshot = Wait-QuarrySnapshot -TimeoutSeconds $StartupTimeoutSeconds
    $initialQuarry = Read-QuarryState $beforeSetupSnapshot
    Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") $beforeSetupSnapshot
    if ($initialQuarry.claimed -or $initialQuarry.inventory_count -ne 0 -or $initialQuarry.altar_count -lt 1) {
        throw "Quarry fixture did not expose one unclaimed Golden Scythe altar with zero reward items."
    }

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @($backendDll, "--urls", "http://127.0.0.1:5108") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendLog `
        -RedirectStandardError $backendErrorLog `
        -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:5108/health" -TimeoutSeconds 60 | Out-Null

    for ($step = 1; $step -le $MaximumSteps; $step++) {
        $before = Wait-QuarrySnapshot -TimeoutSeconds $StartupTimeoutSeconds
        $beforeQuarry = Read-QuarryState $before
        $artifactRunId = $RunId + "-step-" + $step.ToString("D4")
        $arguments = @(
            $loopDll,
            "--root", $trainingRoot,
            "--backend-url", "http://127.0.0.1:5108",
            "--bridge-snapshot-url", "http://127.0.0.1:8765/api/v1/snapshot?profile=mining",
            "--executor-url", "http://127.0.0.1:8767",
            "--executor-timeout-seconds", "600",
            "--no-manifest",
            "--run-id", $RunId,
            "--artifact-run-id", $artifactRunId,
            "--save-isolation-path", $runtimeSaves,
            "--iterations", "1",
            "--required-verified-actions", "1",
            "--skip-training",
            "--sleep-ms", "0",
            "--max-crops", "64",
            "--use-daily-plan",
            "--daily-plan-max-candidates", "1",
            "--daily-plan-candidate-options", "mining.acquire_golden_scythe",
            "--daily-plan-candidate-kind", "mining_acquire_golden_scythe_plan_envelope",
            "--daily-plan-candidate-id", "mining:acquire_golden_scythe",
            "--daily-plan-candidate-parameter", "latest_exit_time=$LatestExitTime",
            "--daily-plan-candidate-parameter", "minimum_reserve_health=$MinimumReserveHealth",
            "--daily-plan-candidate-parameter", "minimum_reserve_energy=$MinimumReserveEnergy",
            "--max-queue-item-attempts", "1"
        )
        $loopOutputPath = Join-Path $runDirectory ("loop-step-" + $step.ToString("D4") + ".json")
        & dotnet @arguments | Set-Content -LiteralPath $loopOutputPath -Encoding utf8
        $loopExitCode = $LASTEXITCODE

        $snapshotDirectory = Join-Path $trainingRoot (Join-Path "runs" (Join-Path $artifactRunId "live-snapshots"))
        $dailyPlanPath = Join-Path $snapshotDirectory "daily-plan-response-0001.json"
        $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
        $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
        if (-not (Test-Path -LiteralPath $dailyPlanPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $queuePath -PathType Leaf)) {
            throw "Step $step did not write DailyPlan and compiled-queue artifacts; loop_exit_code=$loopExitCode"
        }
        if (-not (Test-Path -LiteralPath $executionPath -PathType Leaf)) {
            throw "Step $step did not write execution-0001.json; loop_exit_code=$loopExitCode"
        }

        $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
        $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
        $acceptedCandidate = @($dailyPlan.plan.candidate_audit | Where-Object {
            $_.candidate_id -eq "mining:acquire_golden_scythe" -and
            $_.kind -eq "mining_acquire_golden_scythe_plan_envelope" -and
            $_.decision -eq "accepted"
        }) | Select-Object -First 1
        if ($null -eq $acceptedCandidate) {
            throw "Step $step did not accept the exact Golden Scythe DailyPlan candidate."
        }
        if ([string]$queue.status -ne "pending" -or @($queue.items).Count -ne 1) {
            throw "Step $step did not compile one pending rolling primitive."
        }

        $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
        $primitive = Read-ExecutionPrimitive $execution
        $afterPath = [string]$execution.after_snapshot_path
        if ([string]::IsNullOrWhiteSpace($afterPath) -or -not (Test-Path -LiteralPath $afterPath -PathType Leaf)) {
            throw "Step $step did not write a readable after snapshot."
        }
        $after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
        $afterLocation = Read-PlayerLocation $after
        $afterTile = Read-PlayerTile $after
        $optionId = [string]$primitive.option_id
        $primitiveStatus = [string]$primitive.status
        $verification = [string]$primitive.primitive_verification_status
        $blockReasons = @($primitive.block_reasons)
        $isRecoverableReplan = (
            $primitiveStatus -eq "blocked" -and
            $verification -eq "blocked" -and
            $blockReasons.Count -gt 0 -and
            @($blockReasons | Where-Object {
                $_ -notin $recoverableReplanReasons
            }).Count -eq 0 -and
            [bool]$execution.after_snapshot_fresh -and
            [bool]$execution.state_hash_changed
        )
        $expectedOptions = @(
            "executor.move_to_tile",
            "executor.mine_stone",
            "executor.break_container",
            "executor.break_resource_clump",
            "executor.combat_monster",
            "executor.shoot_monster",
            "executor.place_bomb",
            "executor.pickup_debris",
            "executor.consume_food",
            "executor.interact",
            "executor.exit_mine"
        )
        $afterQuarry = if ($afterLocation -eq "Mine") { $null } else { Read-QuarryState $after }

        $stepSummary = [ordered]@{
            step = $step
            before_claimed = $beforeQuarry.claimed
            before_inventory_count = $beforeQuarry.inventory_count
            after_claimed = if ($null -eq $afterQuarry) { $beforeQuarry.claimed } else { $afterQuarry.claimed }
            after_inventory_count = if ($null -eq $afterQuarry) { $beforeQuarry.inventory_count } else { $afterQuarry.inventory_count }
            after_location = $afterLocation
            after_tile_x = $afterTile.x
            after_tile_y = $afterTile.y
            state_hash_before = [string]$before.state_hash
            state_hash_after = [string]$after.state_hash
            state_hash_changed = [bool]$execution.state_hash_changed
            after_snapshot_fresh = [bool]$execution.after_snapshot_fresh
            option_id = $optionId
            primitive_kind = [string]$primitive.primitive_kind
            status = $primitiveStatus
            verification = $verification
            block_reasons = $blockReasons
            replan_required = $isRecoverableReplan
            actual_ticks = $primitive.actual_ticks
            daily_plan_path = $dailyPlanPath
            queue_path = $queuePath
            execution_path = $executionPath
            after_snapshot_path = $afterPath
        }
        $stepSummaries.Add([pscustomobject]$stepSummary)
        Write-JsonFile (Join-Path $runDirectory ("step-summary-" + $step.ToString("D4") + ".json")) $stepSummary

        if ($optionId -notin $expectedOptions) {
            throw "Step $step selected an unexpected cross-family option: $optionId"
        }
        if ([string]$queue.items[0].option_id -ne $optionId) {
            throw "Step $step compiled queue option $($queue.items[0].option_id) but executed $optionId."
        }
        if (-not [bool]$execution.after_snapshot_fresh) {
            throw "Step $step produced a stale after snapshot."
        }
        if ($isRecoverableReplan) {
            continue
        }
        if ($loopExitCode -ne 0 -or $primitiveStatus -ne "applied" -or $verification -ne "verified") {
            throw "Step $step failed: option=$optionId status=$primitiveStatus verification=$verification reasons=$($blockReasons -join ',')"
        }

        if ($optionId -eq "executor.interact") {
            if ($beforeQuarry.claimed -or $null -eq $afterQuarry -or -not $afterQuarry.claimed -or $afterQuarry.inventory_count -le $beforeQuarry.inventory_count) {
                throw "Step $step did not verify the native Golden Scythe claim."
            }
            $claimVerified = $true
            continue
        }

        if ($optionId -eq "executor.exit_mine") {
            if (-not ($claimVerified -or $beforeQuarry.claimed)) {
                throw "Step $step attempted to exit before the Golden Scythe claim was verified."
            }
            if ($afterLocation -ne "Mine" -or $afterTile.x -ne 67 -or $afterTile.y -ne 10) {
                throw "Step $step did not verify the Quarry Mine native exit to Mine(67,10)."
            }
            $exitVerified = $true
            break
        }

        if ($null -eq $afterQuarry -or $afterQuarry.level -ne 77377 -or $afterQuarry.kind -ne "quarry_mine") {
            throw "Step $step left the Quarry Mine before the verified terminal exit."
        }
    }

    if (-not $claimVerified -or -not $exitVerified) {
        throw "MaximumSteps exhausted before the Golden Scythe claim and native Quarry Mine exit were both verified."
    }

    $datasetPath = Join-Path $trainingRoot "datasets\live-training-feature-rows.jsonl"
    $datasetRows = if (Test-Path -LiteralPath $datasetPath -PathType Leaf) {
        @(Get-Content -LiteralPath $datasetPath).Count
    }
    else {
        0
    }
    $stepSummaryArray = $stepSummaries.ToArray()
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        golden_scythe_claim_verified = $claimVerified
        quarry_exit_verified = $exitVerified
        executed_step_count = $stepSummaryArray.Count
        verified_step_count = @($stepSummaryArray | Where-Object { $_.status -eq "applied" -and $_.verification -eq "verified" }).Count
        primitive_counts = @($stepSummaryArray | Group-Object option_id | ForEach-Object {
            [ordered]@{ option_id = $_.Name; count = $_.Count }
        })
        all_after_snapshots_fresh = @($stepSummaryArray | Where-Object { -not $_.after_snapshot_fresh }).Count -eq 0
        all_state_hashes_changed = @($stepSummaryArray | Where-Object { -not $_.state_hash_changed }).Count -eq 0
        dataset_path = $datasetPath
        dataset_rows = $datasetRows
        source_save_slot = $sourceSaveSlot
        isolated_save_slot = $runtimeSaveSlot
        used_old_save_snapshot = $usedOldSaveSnapshot
        smoke_mods_path = $smokeModsPath
        loaded_mod_allowlist = @(
            "StardewAI.TransparentBridge",
            "StardewAI.RuntimeTestHarness"
        )
        game_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
        steps = @($stepSummaryArray)
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 24
}
finally {
    if (-not $KeepProcessesRunning) {
        if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
            Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
            Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }

    foreach ($name in $previousEnv.Keys) {
        if ($null -eq $previousEnv[$name]) {
            Remove-Item -Path ("Env:" + $name) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("Env:" + $name) -Value $previousEnv[$name]
        }
    }
}
