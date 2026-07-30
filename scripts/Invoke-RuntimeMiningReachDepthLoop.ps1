param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [int] $StartMineLevel = 96,
    [int] $TargetMineLevel = 98,
    [int] $MaximumFloorSteps = 96,
    [int] $LatestExitTime = 2400,
    [int] $MinimumReserveHealth = 1,
    [int] $MinimumReserveEnergy = 0,
    [string] $RunId = ("runtime-mining-reach-depth-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-mining-reach-depth",
    [switch] $AcquireSkullKey,
    [switch] $AllowStaircaseConsumption,
    [switch] $VisibleGame,
    [switch] $KeepProcessesRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_attempted"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec 3
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
    throw "Health endpoint did not become ready: $Url; last_error=$lastError"
}

function Wait-MiningSnapshot {
    param([int] $TimeoutSeconds = 60)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_attempted"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8765/api/v1/snapshot?profile=mining" -Headers @{ Accept = "application/json" } -TimeoutSec 10
            if ($snapshot.completeness -eq "complete" -and
                $snapshot.state.mining.completeness.value.status -eq "complete" -and
                $null -ne $snapshot.state.mining.current_mine.value.mine_level) {
                return $snapshot
            }
            $lastError = "mining_snapshot_not_ready"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Mining snapshot did not become ready; last_error=$lastError"
}

function Read-MineLevel {
    param($Snapshot)
    if ($null -eq $Snapshot.state.mining.current_mine.value.mine_level) {
        return $null
    }
    return [int]$Snapshot.state.mining.current_mine.value.mine_level
}

function Read-HasSkullKey {
    param($Snapshot)
    return [bool]$Snapshot.state.player.has_skull_key.value
}

function Read-ExecutionPrimitive {
    param($Execution)
    $steps = @($Execution.step_results)
    if ($steps.Count -gt 0) {
        return $steps[-1]
    }
    return $Execution
}

if ($AcquireSkullKey) {
    if ($StartMineLevel -lt 1 -or $StartMineLevel -gt 120) {
        throw "AcquireSkullKey StartMineLevel must be between 1 and 120."
    }
    if ($TargetMineLevel -ne 120) {
        throw "AcquireSkullKey requires TargetMineLevel 120."
    }
} else {
    if ($StartMineLevel -lt 1 -or $StartMineLevel -gt 119) {
        throw "StartMineLevel must be between 1 and 119."
    }
    if ($TargetMineLevel -le $StartMineLevel -or $TargetMineLevel -gt 120) {
        throw "TargetMineLevel must be greater than StartMineLevel and at most 120."
    }
}
if ($MaximumFloorSteps -lt 2) {
    throw "MaximumFloorSteps must be at least 2."
}

$runtimeSaves = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$trainingRoot = Join-Path $runDirectory "training"
$backendLog = Join-Path $runDirectory "backend.log"
$backendErrorLog = Join-Path $runDirectory "backend-error.log"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Debug\net8.0\StardewAI.LiveTrainingLoop.dll"
$backendDll = Join-Path $ProjectRoot "src\StardewAI.Backend\bin\Debug\net8.0\StardewAI.Backend.dll"
$bootstrapRunId = $RunId + "-bootstrap"
$bootstrapOutputDirectory = Join-Path $OutputDirectory $RunId
$backendProcess = $null
$gameProcess = $null
$stepSummaries = New-Object System.Collections.Generic.List[object]
$visitedDepths = New-Object System.Collections.Generic.List[int]
$recoverableTransitCombatReasons = @(
    "combat_disengaged_transit_target",
    "combat_movement_budget_exceeded",
    "slingshot_disengaged_transit_target"
)
$objectiveReached = $false
$terminalExitVerified = $false
$skullKeyTransitionObserved = $false
$resourcePreservationPolicy = if ($AllowStaircaseConsumption) {
    "allow_staircase_consumption"
} else {
    "preserve_staircases"
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$occupiedPorts = Get-NetTCPConnection -State Listen -LocalPort 5108, 8765, 8767 -ErrorAction SilentlyContinue
if (@($occupiedPorts).Count -gt 0) {
    throw "Required runtime ports are already occupied: $(@($occupiedPorts.LocalPort | Sort-Object -Unique) -join ',')"
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

    $existingGameIds = @(Get-Process -Name StardewModdingAPI -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    & (Join-Path $ProjectRoot "scripts\Invoke-RuntimeMiningSnapshotSmoke.ps1") `
        -ProjectRoot $ProjectRoot `
        -RuntimeRoot $RuntimeRoot `
        -MineLevel $StartMineLevel `
        -MinimumBreakableStoneCount 0 `
        -SampleCount 1 `
        -MaximumSnapshotMilliseconds 5000 `
        -RunId $bootstrapRunId `
        -OutputDirectory $bootstrapOutputDirectory `
        -MiningCalibrationLoadout `
        -MiningStaircaseLoadout:$AllowStaircaseConsumption `
        -ResetSkullKeyFixture:$AcquireSkullKey `
        -VisibleGame:$VisibleGame `
        -KeepGameRunning | Out-Null

    $gameProcess = Get-Process -Name StardewModdingAPI -ErrorAction Stop |
        Where-Object { $_.Id -notin $existingGameIds -and $_.Path -like "$RuntimeRoot\*" } |
        Sort-Object StartTime -Descending |
        Select-Object -First 1
    if ($null -eq $gameProcess) {
        throw "Isolated SMAPI process was not found after bootstrap."
    }

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @($backendDll, "--urls", "http://127.0.0.1:5108") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendLog `
        -RedirectStandardError $backendErrorLog `
        -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:5108/health" | Out-Null

    for ($step = 1; $step -le $MaximumFloorSteps; $step++) {
        $before = Wait-MiningSnapshot
        $beforeLevel = Read-MineLevel $before
        $beforeHasSkullKey = Read-HasSkullKey $before
        if ($null -eq $beforeLevel) {
            throw "Current mine level was unavailable before step $step."
        }
        if ($visitedDepths.Count -eq 0 -or $visitedDepths[-1] -ne $beforeLevel) {
            $visitedDepths.Add($beforeLevel)
        }

        $stepArtifactRunId = $RunId + "-step-" + $step.ToString("D4")
        $arguments = @(
            $loopDll,
            "--root", $trainingRoot,
            "--backend-url", "http://127.0.0.1:5108",
            "--bridge-snapshot-url", "http://127.0.0.1:8765/api/v1/snapshot?profile=full",
            "--executor-url", "http://127.0.0.1:8767",
            "--no-manifest",
            "--run-id", $bootstrapRunId,
            "--artifact-run-id", $stepArtifactRunId,
            "--save-isolation-path", $runtimeSaves,
            "--iterations", "1",
            "--required-verified-actions", "1",
            "--train-every", "1",
            "--sleep-ms", "0",
            "--max-crops", "64",
            "--use-parameterized-action"
        )
        if ($AcquireSkullKey) {
            $arguments += @(
                "--action-option-id", "mining.obtain_skull_key",
                "--action-parameter", "target_depth=120",
                "--action-parameter", "required_terminal_interaction=skull_key_reward_chest",
                "--action-parameter", "required_postcondition=player.has_skull_key=true"
            )
        } else {
            $arguments += @(
                "--action-option-id", "mining.reach_depth",
                "--action-parameter", "target_depth=$TargetMineLevel"
            )
        }
        $arguments += @(
            "--action-parameter", "target_location_family=ordinary_mines",
            "--action-parameter", "latest_exit_time=$LatestExitTime",
            "--action-parameter", "minimum_reserve_health=$MinimumReserveHealth",
            "--action-parameter", "minimum_reserve_energy=$MinimumReserveEnergy",
            "--action-parameter", "resource_preservation_policy=$resourcePreservationPolicy",
            "--max-queue-item-attempts", "1"
        )
        & dotnet @arguments | Set-Content -LiteralPath (Join-Path $runDirectory ("loop-step-" + $step.ToString("D4") + ".json")) -Encoding utf8
        $loopExitCode = $LASTEXITCODE

        $stepSnapshotDirectory = Join-Path $trainingRoot (Join-Path "runs" (Join-Path $stepArtifactRunId "live-snapshots"))
        $executionPath = Join-Path $stepSnapshotDirectory "execution-0001.json"
        if (-not (Test-Path -LiteralPath $executionPath -PathType Leaf)) {
            throw "Step $step did not write an aggregate execution artifact; loop_exit_code=$loopExitCode"
        }
        $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
        $primitive = Read-ExecutionPrimitive $execution
        $afterPath = [string]$execution.after_snapshot_path
        if ([string]::IsNullOrWhiteSpace($afterPath) -or -not (Test-Path -LiteralPath $afterPath -PathType Leaf)) {
            throw "Step $step did not write a readable after snapshot."
        }
        $after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
        $afterLevel = Read-MineLevel $after
        $afterHasSkullKey = Read-HasSkullKey $after
        if (-not $beforeHasSkullKey -and $afterHasSkullKey) {
            $skullKeyTransitionObserved = $true
        }
        $primitiveStatus = [string]$primitive.status
        $verification = [string]$primitive.primitive_verification_status
        $optionId = [string]$primitive.option_id
        $blockReasons = @($primitive.block_reasons)
        $combatIntent = [string]$primitive.combat_intent
        $isTransitCombat = $combatIntent -in @(
            "transit_self_defense",
            "transit_route_clearance"
        )
        $isRecoverableReplan = (
            $isTransitCombat -and
            $primitiveStatus -eq "blocked" -and
            $verification -eq "blocked" -and
            $blockReasons.Count -gt 0 -and
            @($blockReasons | Where-Object {
                $_ -notin $recoverableTransitCombatReasons
            }).Count -eq 0 -and
            [bool]$execution.after_snapshot_fresh -and
            [bool]$execution.state_hash_changed
        )
        $stepSummary = [ordered]@{
            step = $step
            before_depth = $beforeLevel
            after_depth = $afterLevel
            has_skull_key_before = $beforeHasSkullKey
            has_skull_key_after = $afterHasSkullKey
            state_hash_before = [string]$before.state_hash
            state_hash_after = [string]$after.state_hash
            state_hash_changed = [bool]$execution.state_hash_changed
            after_snapshot_fresh = [bool]$execution.after_snapshot_fresh
            option_id = $optionId
            primitive_kind = [string]$primitive.primitive_kind
            status = $primitiveStatus
            verification = $verification
            block_reasons = $blockReasons
            combat_intent = $combatIntent
            replan_required = $isRecoverableReplan
            actual_ticks = $primitive.actual_ticks
            execution_path = $executionPath
            after_snapshot_path = $afterPath
        }
        $stepSummaries.Add([pscustomobject]$stepSummary)
        Write-JsonFile (Join-Path $runDirectory ("step-summary-" + $step.ToString("D4") + ".json")) $stepSummary

        if (-not [bool]$execution.after_snapshot_fresh) {
            throw "Step $step produced a stale after snapshot."
        }
        if ($isRecoverableReplan) {
            continue
        }
        if ($loopExitCode -ne 0 -or $primitiveStatus -ne "applied" -or $verification -ne "verified") {
            throw "Step $step failed: option=$optionId status=$primitiveStatus verification=$verification reasons=$($blockReasons -join ',')"
        }

        if ($AcquireSkullKey -and $beforeHasSkullKey) {
            if ($optionId -ne "executor.exit_mine") {
                throw "Skull Key was acquired but compiler selected '$optionId' instead of executor.exit_mine."
            }
            if (-not $skullKeyTransitionObserved) {
                throw "Skull Key exit was selected without observing the false-to-true postcondition transition."
            }
            $objectiveReached = $true
            $terminalExitVerified = $true
            break
        }

        if (-not $AcquireSkullKey -and $beforeLevel -ge $TargetMineLevel) {
            if ($optionId -ne "executor.exit_mine") {
                throw "Target depth was reached but compiler selected '$optionId' instead of executor.exit_mine."
            }
            $objectiveReached = $true
            $terminalExitVerified = $true
            break
        }

        if ($AcquireSkullKey -and $beforeLevel -ge 120 -and $optionId -eq "executor.exit_mine") {
            throw "Compiler attempted to exit floor 120 before player.has_skull_key became true."
        }

        if ($null -ne $afterLevel -and $afterLevel -gt $TargetMineLevel) {
            throw "Executor overshot target depth: target=$TargetMineLevel after=$afterLevel."
        }
    }

    if (-not $terminalExitVerified) {
        throw "MaximumFloorSteps exhausted before target depth and terminal exit were verified."
    }

    $datasetPath = Join-Path $trainingRoot "datasets\live-training-feature-rows.jsonl"
    $datasetRows = if (Test-Path -LiteralPath $datasetPath -PathType Leaf) {
        @(Get-Content -LiteralPath $datasetPath).Count
    } else {
        0
    }
    $stepSummaryArray = $stepSummaries.ToArray()
    $visitedDepthArray = $visitedDepths.ToArray()
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        start_depth = $StartMineLevel
        target_depth = $TargetMineLevel
        objective_reached = $objectiveReached
        terminal_exit_verified = $terminalExitVerified
        acquire_skull_key_mode = [bool]$AcquireSkullKey
        skull_key_transition_observed = $skullKeyTransitionObserved
        visited_depths = @($visitedDepthArray)
        distinct_depth_count = @($visitedDepthArray | Sort-Object -Unique).Count
        executed_step_count = $stepSummaryArray.Count
        verified_step_count = @($stepSummaryArray | Where-Object { $_.status -eq "applied" -and $_.verification -eq "verified" }).Count
        replan_step_count = @($stepSummaryArray | Where-Object { $_.replan_required }).Count
        primitive_counts = @($stepSummaryArray | Group-Object option_id | ForEach-Object {
            [ordered]@{ option_id = $_.Name; count = $_.Count }
        })
        all_after_snapshots_fresh = @($stepSummaryArray | Where-Object { -not $_.after_snapshot_fresh }).Count -eq 0
        all_state_hashes_changed = @($stepSummaryArray | Where-Object { -not $_.state_hash_changed }).Count -eq 0
        dataset_path = $datasetPath
        dataset_rows = $datasetRows
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
}
