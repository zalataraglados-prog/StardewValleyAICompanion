function Copy-RuntimeSnapshotArtifact {
    param(
        [Parameter(Mandatory)] [string] $SourcePath,
        [Parameter(Mandatory)] [string] $DestinationPath
    )
    Write-Utf8Text -Path $DestinationPath -Value (
        Get-Content -LiteralPath $SourcePath -Raw)
    return [IO.Path]::GetFullPath($DestinationPath)
}

function New-RuntimeTerminalQueueTransition {
    param(
        [Parameter(Mandatory)] [string] $TransitionId,
        [Parameter(Mandatory)] $Artifacts,
        [Parameter(Mandatory)] [string] $RunId
    )
    $selectedCandidateId = [string]$Artifacts.Execution.selected_candidate_id
    if ([string]::IsNullOrWhiteSpace($selectedCandidateId)) {
        throw "Runtime terminal transition has no selected candidate identity."
    }
    return [ordered]@{
        transition_id = $TransitionId
        queue_path = $Artifacts.QueuePath
        before_snapshot_path = $Artifacts.BeforePath
        execution_receipt_path = $Artifacts.ExecutionPath
        after_snapshot_path = $Artifacts.AfterPath
        run_id = $RunId
        executor_version = "runtime_test_harness_executor.v1"
        selected_candidate_id = $selectedCandidateId
    }
}

function Invoke-RuntimeBoundSingleAction {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] [string] $OptionId,
        [Parameter(Mandatory)] [string] $CandidateId,
        [Parameter(Mandatory)] [object[]] $Parameters,
        [Parameter(Mandatory)] [string] $Name
    )
    if ([string]::IsNullOrWhiteSpace($OptionId) -or
        [string]::IsNullOrWhiteSpace($CandidateId)) {
        throw "Bound runtime action identity is incomplete."
    }
    $source = Import-RuntimeSnapshotFile -Path $SourceSnapshotPath `
        -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile
    $effectiveParameters = [Collections.Generic.List[object]]::new()
    foreach ($parameter in @($Parameters)) {
        if ([string]::IsNullOrWhiteSpace([string]$parameter.name)) {
            throw "Bound runtime action contains an unnamed parameter."
        }
        $effectiveParameters.Add([ordered]@{
            name = [string]$parameter.name
            value = [string]$parameter.value
        })
    }
    if (@($effectiveParameters | Where-Object {
            [string]$_.name -eq "precondition"
        }).Count -eq 0) {
        $effectiveParameters.Add([ordered]@{
            name = "precondition"
            value = "candidate_id:$CandidateId"
        })
    }
    $modelOutput = [ordered]@{
        schema_version = "small_model_action.v1"
        model_output_id = "model-output:$CandidateId"
        source_model = "authoritative_crop_terminal_continuation.v1"
        state_hash = [string]$source.Value.state_hash
        goal_id = "daily.closed_loop"
        execution_mode = "training_singleplayer"
        actor = [ordered]@{
            actor_id = "training_farmer.main"
            actor_type = "training_farmer"
            control_surface = "training_sandbox"
        }
        actions = @([ordered]@{
            action_id = "$CandidateId:action"
            option_id = $OptionId
            rationale = "authoritative_crop_terminal_continuation"
            parameters = @($effectiveParameters)
        })
    }
    $compileRoot = Join-Path $Context.ArtifactDirectory "$Name-compile"
    $modelOutputPath = Join-Path $compileRoot "model-output.json"
    $queuePath = Join-Path $compileRoot "compiled-queue.json"
    Write-JsonFile -Path $modelOutputPath -Value $modelOutput
    $compiled = Invoke-JsonPost `
        -Url "$BackendUrl/api/v1/small-model/action-queue/compile" `
        -Body $modelOutput
    Write-Utf8Text -Path $queuePath -Value $compiled.Raw
    $items = @($compiled.Value.items)
    if ([string]$compiled.Value.schema_version -ne "action_queue.v1" -or
        [string]$compiled.Value.status -ne "pending" -or
        $items.Count -ne 1 -or
        [string]$items[0].option_id -ne $OptionId -or
        [string]$items[0].status -ne "pending") {
        $diagnostics = @($compiled.Value.compiler_diagnostics) -join " | "
        throw "Bound runtime action $Name did not compile exactly: $diagnostics"
    }
    $artifacts = Invoke-RuntimePrecompiledQueue -Context $Context `
        -Name $Name -QueuePath $queuePath -BeforePath $source.Path `
        -ExecutionTarget "runtime_harness"
    if ([string]$artifacts.Execution.selected_candidate_id -ne $CandidateId) {
        throw "Bound runtime action $Name execution candidate identity drifted."
    }
    return $artifacts
}

function Get-RuntimeCropRows {
    param([Parameter(Mandatory)] $Snapshot)
    return @(Read-StateValue $Snapshot "farm" "crops")
}

function ConvertTo-RuntimeCropSeedId {
    param([Parameter(Mandatory)] [string] $QualifiedItemId)
    if ($QualifiedItemId -match '^\(O\)(.+)$') {
        return $Matches[1]
    }
    if ([string]::IsNullOrWhiteSpace($QualifiedItemId)) {
        throw "Crop seed identity is empty."
    }
    return $QualifiedItemId
}

function Find-RuntimeCropRow {
    param(
        [Parameter(Mandatory)] $Snapshot,
        [Parameter(Mandatory)] [int] $TileX,
        [Parameter(Mandatory)] [int] $TileY
    )
    $matches = @(Get-RuntimeCropRows -Snapshot $Snapshot | Where-Object {
        [int]$_.tile_x -eq $TileX -and [int]$_.tile_y -eq $TileY
    })
    if ($matches.Count -ne 1) {
        throw "Expected one transparent crop row at Farm($TileX,$TileY); found $($matches.Count)."
    }
    return $matches[0]
}

function Get-RuntimeCropBatchTiles {
    param(
        [Parameter(Mandatory)] $Snapshot,
        [Parameter(Mandatory)] $Batch
    )
    $tiles = @($Batch.tiles)
    if ([string]$Batch.execution_condition -eq "always") {
        return $tiles
    }
    if ([string]$Batch.execution_condition -ne "crop_needs_watering" -or
        [string]$Batch.option_id -ne "executor.water_crop") {
        throw "Unsupported crop batch execution condition: $($Batch.execution_condition)"
    }
    return @($tiles | Where-Object {
        $row = Find-RuntimeCropRow -Snapshot $Snapshot `
            -TileX ([int]$_.tile_x) -TileY ([int]$_.tile_y)
        [bool]$row.needs_watering
    })
}

function New-RuntimeCropTransition {
    param(
        [Parameter(Mandatory)] [string] $TransitionKind,
        [Parameter(Mandatory)] [int] $TotalDay,
        [Parameter(Mandatory)] $Artifacts,
        [Parameter(Mandatory)] [string] $RunId
    )
    $selectedCandidateId = [string]$Artifacts.Execution.selected_candidate_id
    if ([string]::IsNullOrWhiteSpace($selectedCandidateId)) {
        throw "Runtime crop transition has no selected candidate identity."
    }
    return [ordered]@{
        transition_kind = $TransitionKind
        total_day = $TotalDay
        queue_path = $Artifacts.QueuePath
        before_snapshot_path = $Artifacts.BeforePath
        execution_receipt_path = $Artifacts.ExecutionPath
        after_snapshot_path = $Artifacts.AfterPath
        run_id = $RunId
        executor_version = "runtime_test_harness_executor.v1"
        selected_candidate_id = $selectedCandidateId
    }
}

function Invoke-RuntimeCropSitePreparationAction {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] $Step,
        [Parameter(Mandatory)] [int] $TotalDay,
        [Parameter(Mandatory)] [string] $Name
    )
    $source = Import-RuntimeSnapshotFile -Path $SourceSnapshotPath `
        -BackendUrl $BackendUrl -SnapshotProfile "farm_layout"
    $sourceDay = [int](Read-StateValue $source.Value "time" "total_days")
    $sourceLocation = [string](Read-StateValue `
        $source.Value "player" "location_id")
    $optionId = [string]$Step.action_option_id
    $candidateId = [string]$Step.candidate_id
    if ($sourceDay -ne $TotalDay -or $sourceLocation -ne "Farm" -or
        $optionId -notin @(
            "executor.clear_obstacle",
            "executor.break_farm_resource_clump",
            "executor.till_soil") -or
        [string]::IsNullOrWhiteSpace($candidateId)) {
        throw "Crop site-preparation action $Name has an invalid source or binding."
    }

    $parameters = [Collections.Generic.List[object]]::new()
    foreach ($parameter in @($Step.parameters)) {
        if ([string]$parameter.name -eq "precondition") {
            throw "Crop site-preparation candidate supplied an unexpected precondition."
        }
        $parameters.Add([ordered]@{
            name = [string]$parameter.name
            value = [string]$parameter.value
        })
    }
    $parameters.Add([ordered]@{
        name = "precondition"
        value = "candidate_id:$candidateId"
    })
    $modelOutput = [ordered]@{
        schema_version = "small_model_action.v1"
        model_output_id = "model-output:site-preparation:$candidateId"
        source_model = "authoritative_crop_site_preparation_plan.v1"
        state_hash = [string]$source.Value.state_hash
        goal_id = "daily.closed_loop"
        execution_mode = "training_singleplayer"
        actor = [ordered]@{
            actor_id = "training_farmer.main"
            actor_type = "training_farmer"
            control_surface = "training_sandbox"
        }
        actions = @([ordered]@{
            action_id = "site-preparation:$candidateId"
            option_id = $optionId
            rationale = "authoritative_crop_site_preparation_plan"
            parameters = @($parameters)
        })
    }
    $compileRoot = Join-Path $Context.ArtifactDirectory "$Name-compile"
    $modelOutputPath = Join-Path $compileRoot "model-output.json"
    $queuePath = Join-Path $compileRoot "compiled-queue.json"
    Write-JsonFile -Path $modelOutputPath -Value $modelOutput
    $compiled = Invoke-JsonPost `
        -Url "$BackendUrl/api/v1/small-model/action-queue/compile" `
        -Body $modelOutput
    Write-Utf8Text -Path $queuePath -Value $compiled.Raw
    $items = @($compiled.Value.items)
    if ([string]$compiled.Value.schema_version -ne "action_queue.v1" -or
        [string]$compiled.Value.status -ne "pending" -or
        $items.Count -ne 1 -or
        [string]$items[0].option_id -ne $optionId -or
        [string]$items[0].status -ne "pending") {
        $diagnostics = @($compiled.Value.compiler_diagnostics) -join " | "
        throw "Crop site-preparation action $Name did not compile exactly: $diagnostics"
    }

    $artifacts = Invoke-RuntimePrecompiledQueue -Context $Context `
        -Name $Name -QueuePath $queuePath -BeforePath $source.Path `
        -BeforeSnapshotProfile "farm_layout" `
        -ExecutionTarget "runtime_harness"
    if ([string]$artifacts.Execution.selected_candidate_id -ne
        $candidateId) {
        throw "Crop site-preparation action $Name candidate identity drifted."
    }
    return $artifacts
}

function Invoke-RuntimeCropSitePreparation {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotRootUrl,
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $ProductionPlanPath,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] $LayoutIntent,
        [Parameter(Mandatory)] [int] $TotalDay,
        [Parameter(Mandatory)] [string] $OutputDirectory
    )
    $tiles = @($LayoutIntent.tiles)
    if ($tiles.Count -eq 0) {
        throw "Crop site-preparation layout contains no tiles."
    }
    $minX = [int](($tiles | Measure-Object tile_x -Minimum).Minimum)
    $maxX = [int](($tiles | Measure-Object tile_x -Maximum).Maximum)
    $minY = [int](($tiles | Measure-Object tile_y -Minimum).Minimum)
    $maxY = [int](($tiles | Measure-Object tile_y -Maximum).Maximum)
    if ($maxX - $minX + 1 -gt 32 -or $maxY - $minY + 1 -gt 32) {
        throw "Crop site-preparation layout exceeds one bounded farm projection."
    }

    $commitmentId = [string]$LayoutIntent.commitment_id
    $safeId = $commitmentId -replace '[^a-zA-Z0-9._-]', '_'
    $planPaths = [Collections.Generic.List[string]]::new()
    $executions = [Collections.Generic.List[object]]::new()
    $currentFullPath = [IO.Path]::GetFullPath($SourceSnapshotPath)
    $maxIterations = 2 * $tiles.Count + 1
    for ($iteration = 1; $iteration -le $maxIterations; $iteration++) {
        $iterationName = "site-preparation-$safeId-$($iteration.ToString('D3'))"
        $layoutUrl = "$SnapshotRootUrl/api/v1/snapshot?profile=farm_layout" +
            "&min_x=$minX&min_y=$minY&max_x=$maxX&max_y=$maxY&fresh=true"
        $capture = Get-FreshRuntimeSnapshot -Url $layoutUrl -TimeoutSeconds 60
        $layoutSnapshotPath = Join-Path $OutputDirectory `
            "$iterationName-snapshot.json"
        Save-RuntimeSnapshotAndIngest -Path $layoutSnapshotPath `
            -Capture $capture -BackendUrl $BackendUrl `
            -SnapshotProfile "farm_layout"
        $observedDay = [int](Read-StateValue `
            $capture.Value "time" "total_days")
        $observedLocation = [string](Read-StateValue `
            $capture.Value "player" "location_id")
        if ($observedDay -ne $TotalDay -or $observedLocation -ne "Farm") {
            throw "Crop site-preparation snapshot left its Farm/day boundary."
        }

        $requestPath = Join-Path $OutputDirectory `
            "$iterationName-request.json"
        $planPath = Join-Path $OutputDirectory "$iterationName-plan.json"
        Write-JsonFile -Path $requestPath -Value ([ordered]@{
            schema_version =
                "money_revenue_crop_site_preparation_plan_request.v1"
            crop_production_plan_path = $ProductionPlanPath
            commitment_id = $commitmentId
            preparation_snapshot_path = $layoutSnapshotPath
            location_id = "Farm"
            tiles = $tiles
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
            "build-money-revenue-crop-site-preparation-plan",
            "--request", $requestPath,
            "--output", $planPath)
        $planPaths.Add($planPath)
        $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
        if ([string]$plan.schema_version -ne
                "money_revenue_crop_site_preparation_plan.v1" -or
            [string]$plan.commitment_id -ne $commitmentId -or
            [int]$plan.preparation_total_day -ne $TotalDay -or
            [int]$plan.tile_count -ne $tiles.Count -or
            [int]$plan.blocked_count -ne 0 -or
            -not [bool]$plan.site_preparation_plan_ready -or
            [bool]$plan.formal_product_training_authorized) {
            $reasons = @($plan.steps | ForEach-Object {
                @($_.block_reasons)
            }) -join ","
            throw "Crop site preparation is blocked for ${commitmentId}: $reasons"
        }

        $pending = @($plan.steps | Where-Object {
            [string]$_.plan_status -ne "already_prepared"
        })
        if ($pending.Count -eq 0) {
            return [pscustomobject]@{
                SourcePath = $currentFullPath
                PlanPaths = @($planPaths)
                Executions = @($executions)
            }
        }

        $step = $pending[0]
        if ([string]$step.plan_status -notin @(
                "direct_till_ready",
                "clear_then_fresh_snapshot_rebind")) {
            throw "Crop site-preparation plan returned an unsupported ready status."
        }
        $execution = Invoke-RuntimeCropSitePreparationAction `
            -Context $Context -BackendUrl $BackendUrl `
            -SourceSnapshotPath $layoutSnapshotPath -Step $step `
            -TotalDay $TotalDay -Name $iterationName
        $executions.Add($execution)
        $currentFullPath = $execution.AfterPath
    }
    throw "Crop site preparation exceeded its bounded action count for $commitmentId."
}

function Invoke-RuntimeCropActionBatch {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] $Batch,
        [Parameter(Mandatory)] [int] $TotalDay,
        [Parameter(Mandatory)] [string] $Name
    )
    $source = Import-RuntimeSnapshotFile -Path $SourceSnapshotPath `
        -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile
    $sourceDay = [int](Read-StateValue $source.Value "time" "total_days")
    $sourceLocation = [string](Read-StateValue `
        $source.Value "player" "location_id")
    if ($sourceDay -ne $TotalDay -or
        $sourceLocation -ne [string]$Batch.location_id) {
        throw "Crop batch $Name source boundary drifted: day=$sourceDay location=$sourceLocation."
    }

    $tiles = @(Get-RuntimeCropBatchTiles `
        -Snapshot $source.Value -Batch $Batch)
    if ($tiles.Count -eq 0) {
        return [pscustomobject]@{
            Skipped = $true
            SkipReason = "transparent_crop_needs_watering_false"
            SourcePath = $source.Path
            Artifacts = $null
        }
    }

    $candidateId = "crop:${TotalDay}:$($Batch.commitment_id):$($Batch.batch_index)"
    $actions = @($tiles | ForEach-Object {
        $tileX = [int]$_.tile_x
        $tileY = [int]$_.tile_y
        $parameters = [Collections.Generic.List[object]]::new()
        foreach ($parameter in @(
            [ordered]@{ name = "target_tile_x"; value = [string]$tileX },
            [ordered]@{ name = "target_tile_y"; value = [string]$tileY },
            [ordered]@{ name = "target_location"; value = [string]$Batch.location_id },
            [ordered]@{ name = "action_commitment_id"; value = [string]$Batch.commitment_id },
            [ordered]@{ name = "precondition"; value = "candidate_id:$candidateId" }
        )) { $parameters.Add($parameter) }

        if ([string]$Batch.option_id -eq "executor.plant_seed") {
            $parameters.Add([ordered]@{
                name = "seed_id"
                value = ConvertTo-RuntimeCropSeedId `
                    -QualifiedItemId ([string]$Batch.seed_qualified_item_id)
            })
        }
        elseif ([string]$Batch.option_id -eq "executor.harvest_crop") {
            $crop = Find-RuntimeCropRow -Snapshot $source.Value `
                -TileX $tileX -TileY $tileY
            if (-not [bool]$crop.ready_for_harvest -or
                [string]::IsNullOrWhiteSpace([string]$crop.harvest_method)) {
                throw "Scheduled harvest is not transparently ready at Farm($tileX,$tileY)."
            }
            $parameters.Add([ordered]@{
                name = "harvest_method"
                value = [string]$crop.harvest_method
            })
            if (-not [string]::IsNullOrWhiteSpace(
                    [string]$crop.harvest_item_qualified_id)) {
                $parameters.Add([ordered]@{
                    name = "harvest_item_qualified_id"
                    value = [string]$crop.harvest_item_qualified_id
                })
            }
            if (-not [string]::IsNullOrWhiteSpace(
                    [string]$crop.harvest_item_projection_status)) {
                $parameters.Add([ordered]@{
                    name = "harvest_item_projection_status"
                    value = [string]$crop.harvest_item_projection_status
                })
            }
        }
        elseif ([string]$Batch.option_id -ne "executor.water_crop") {
            throw "Unsupported native crop action: $($Batch.option_id)"
        }

        [ordered]@{
            action_id = "${candidateId}:${tileX}:${tileY}"
            option_id = [string]$Batch.option_id
            rationale = "authoritative_crop_native_run_plan"
            parameters = @($parameters)
        }
    })
    $modelOutput = [ordered]@{
        schema_version = "small_model_action.v1"
        model_output_id = "model-output:$candidateId"
        source_model = "authoritative_crop_native_run_plan.v1"
        state_hash = [string]$source.Value.state_hash
        goal_id = "daily.closed_loop"
        execution_mode = "training_singleplayer"
        actor = [ordered]@{
            actor_id = "training_farmer.main"
            actor_type = "training_farmer"
            control_surface = "training_sandbox"
        }
        actions = $actions
    }
    $compileRoot = Join-Path $Context.ArtifactDirectory "$Name-compile"
    $modelOutputPath = Join-Path $compileRoot "model-output.json"
    $queuePath = Join-Path $compileRoot "compiled-queue.json"
    Write-JsonFile -Path $modelOutputPath -Value $modelOutput
    $compiled = Invoke-JsonPost `
        -Url "$BackendUrl/api/v1/small-model/action-queue/compile" `
        -Body $modelOutput
    Write-Utf8Text -Path $queuePath -Value $compiled.Raw
    $queueItems = @($compiled.Value.items)
    if ([string]$compiled.Value.schema_version -ne "action_queue.v1" -or
        [string]$compiled.Value.status -ne "pending" -or
        $queueItems.Count -ne $tiles.Count -or
        @($queueItems | Where-Object {
            [string]$_.option_id -ne [string]$Batch.option_id -or
            [string]$_.status -ne "pending"
        }).Count -ne 0) {
        $diagnostics = @($compiled.Value.compiler_diagnostics) -join " | "
        throw "Crop batch $Name did not compile exactly: $diagnostics"
    }

    $artifacts = Invoke-RuntimePrecompiledQueue -Context $Context `
        -Name $Name -QueuePath $queuePath -BeforePath $source.Path `
        -ExecutionTarget "runtime_harness"
    if ([string]$artifacts.Execution.selected_candidate_id -ne $candidateId) {
        throw "Crop batch $Name execution candidate identity drifted."
    }
    return [pscustomobject]@{
        Skipped = $false
        SkipReason = ""
        SourcePath = $artifacts.AfterPath
        Artifacts = $artifacts
    }
}

function Invoke-RuntimeCropRoute {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] [string] $TargetLocation,
        [Parameter(Mandatory)] [int] $TotalDay,
        [Parameter(Mandatory)] [string] $Name,
        [int] $MaxConnectorCount = 16
    )
    $sourcePath = [IO.Path]::GetFullPath($SourceSnapshotPath)
    $artifacts = [Collections.Generic.List[object]]::new()
    for ($step = 1; $step -le $MaxConnectorCount; $step++) {
        $source = Import-RuntimeSnapshotFile -Path $sourcePath `
            -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile
        $sourceDay = [int](Read-StateValue $source.Value "time" "total_days")
        $sourceLocation = [string](Read-StateValue `
            $source.Value "player" "location_id")
        if ($sourceDay -ne $TotalDay) {
            throw "Route $Name crossed its declared day before execution."
        }
        if ($sourceLocation -eq $TargetLocation) {
            return [pscustomobject]@{
                SourcePath = $source.Path
                Artifacts = @($artifacts)
            }
        }

        $candidateId = "route:${TotalDay}:${Name}:${step}"
        $plan = [ordered]@{
            schema_version = "small_model_plan.v1"
            plan_id = "plan:$candidateId"
            source_model = "authoritative_crop_native_route.v1"
            state_hash = [string]$source.Value.state_hash
            goal_id = "daily.closed_loop"
            execution_mode = "training_singleplayer"
            actor = [ordered]@{
                actor_id = "training_farmer.main"
                actor_type = "training_farmer"
                control_surface = "training_sandbox"
            }
            plan_type = "mechanical_plan"
            steps = @([ordered]@{
                step_id = "${candidateId}:move"
                kind = "move_to_tile"
                target_location = $TargetLocation
                target_tile_x = 0
                target_tile_y = 0
                estimated_minutes = 1
                preconditions = @("candidate_id:$candidateId", "world_ready")
                expected_effects = @("player.location_id=$TargetLocation")
                safety_constraints = @("transparent_route_graph_only")
                failure_policy = @("refresh_snapshot_and_replan")
                parameters = @()
            })
            candidate_audit = @()
        }
        $stepName = "$Name-$($step.ToString('D2'))"
        $compileRoot = Join-Path $Context.ArtifactDirectory `
            "$stepName-compile"
        $planPath = Join-Path $compileRoot "model-plan.json"
        $queuePath = Join-Path $compileRoot "compiled-queue.json"
        Write-JsonFile -Path $planPath -Value $plan
        $compiled = Invoke-JsonPost `
            -Url "$BackendUrl/api/v1/small-model/plan/action-queue/compile" `
            -Body $plan
        Write-Utf8Text -Path $queuePath -Value $compiled.Raw
        $items = @($compiled.Value.items)
        if ([string]$compiled.Value.status -ne "pending" -or
            $items.Count -ne 1 -or
            [string]$items[0].option_id -ne "executor.traverse_connector" -or
            [string]$items[0].status -ne "pending") {
            $diagnostics = @($compiled.Value.compiler_diagnostics) -join " | "
            throw "Route $Name did not compile one transparent connector: $diagnostics"
        }
        $execution = Invoke-RuntimePrecompiledQueue -Context $Context `
            -Name $stepName -QueuePath $queuePath -BeforePath $source.Path `
            -ExecutionTarget "runtime_harness"
        if ([string]$execution.Execution.selected_candidate_id -ne
            $candidateId) {
            throw "Route $Name execution candidate identity drifted."
        }
        $afterLocation = [string](Read-StateValue `
            $execution.After "player" "location_id")
        $afterDay = [int](Read-StateValue `
            $execution.After "time" "total_days")
        if ($afterDay -ne $TotalDay -or $afterLocation -eq $sourceLocation) {
            throw "Route $Name connector made no same-day location progress."
        }
        $artifacts.Add($execution)
        $sourcePath = $execution.AfterPath
    }
    throw "Route $Name exceeded $MaxConnectorCount transparent connectors."
}

function Get-RuntimeCropHomeLocation {
    param([Parameter(Mandatory)] $Snapshot)
    $context = Read-StateValue $Snapshot "current_location" "home_context"
    $location = [string]$context.home_location_id
    if ([string]::IsNullOrWhiteSpace($location)) {
        throw "Transparent home location is unavailable."
    }
    return $location
}
