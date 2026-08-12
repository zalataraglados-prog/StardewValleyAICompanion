param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-quest-terminal-daily-plan-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 5132,
    [string[]] $CaseName = @(),
    [switch] $VisibleGame,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPostRaw([string] $Url, [string] $Json, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body $Json -TimeoutSec $TimeoutSeconds
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($null -ne $value) { return $value }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([scriptblock] $Predicate, [string] $Description, [int] $TimeoutSeconds = 120) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $lastStatus = "save=$($snapshot.save_id.status);location=$($snapshot.state.player.location_id.value)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.player.location_id.status -in @("available", "derived") -and
                (& $Predicate $snapshot)) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    if ($null -ne $snapshot -and -not [string]::IsNullOrWhiteSpace($caseDirectory)) {
        Write-Json (Join-Path $caseDirectory "wait-timeout-snapshot.json") $snapshot
    }
    throw "Timed out waiting for $Description. Last status: $lastStatus"
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Get-CandidateParameter($Candidate, [string] $Name) {
    @($Candidate.parameters | Where-Object { [string]$_.name -eq $Name } |
        Select-Object -First 1).value
}

function New-ExecutionRequest(
    [string] $CaseRunId,
    [string] $QueueItemId,
    [string] $StateHash,
    [string] $FixtureKind,
    [string] $QuestId,
    [string] $QuestKey) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $CaseRunId
        queue_id = "runtime-quest-terminal-matrix"
        queue_item_id = $QueueItemId
        before_state_hash = $StateHash
        option_id = "debug.setup_quest_terminal_fixture"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $isolatedSavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        quest_interaction_kind = $FixtureKind
        quest_id = $QuestId
        quest_key = $QuestKey
    }
}

function Find-OrdinaryQuest($Snapshot, [string] $QuestId) {
    @($Snapshot.state.quests.active_quests.value | Where-Object {
        [string]$_.id -eq $QuestId -and [string]$_.runtime_type -eq "ItemDeliveryQuest"
    }) | Select-Object -First 1
}

function Find-CraftingQuest($Snapshot, [string] $QuestId) {
    @($Snapshot.state.quests.active_quests.value | Where-Object {
        [string]$_.id -eq $QuestId -and [string]$_.runtime_type -eq "CraftingQuest" -and
        -not [bool]$_.completed
    }) | Select-Object -First 1
}

function Find-BuildingQuest($Snapshot, [string] $QuestId) {
    @($Snapshot.state.quests.active_quests.value | Where-Object {
        [string]$_.id -eq $QuestId -and [string]$_.runtime_type -eq "HaveBuildingQuest" -and
        -not [bool]$_.completed
    }) | Select-Object -First 1
}

function Find-DonateObjective($Snapshot, [string] $QuestKey, [string] $RequiredTagPrefix) {
    $order = @($Snapshot.state.quests.special_orders.value | Where-Object {
        [string]$_.quest_key -eq $QuestKey
    }) | Select-Object -First 1
    if ($null -eq $order) { return $null }
    @($order.objectives | Where-Object {
        if ([string]$_.runtime_type -ne "DonateObjective") { return $false }
        if ([string]::IsNullOrWhiteSpace($RequiredTagPrefix)) { return $true }
        return @($_.per_type_fields.acceptable_context_tag_sets | Where-Object {
            [string]$_ -like ($RequiredTagPrefix + "*")
        }).Count -gt 0
    }) |
        Select-Object -First 1
}

function Invoke-QuestTerminalCase($Case) {
    $caseRunId = $RunId
    $caseDirectory = Join-Path $artifactDirectory $Case.Name
    $caseLoopRoot = Join-Path $caseDirectory "loop"
    $isolatedSavesPath = Join-Path $caseDirectory "isolated-saves"
    $isolatedSaveSlot = Join-Path $isolatedSavesPath $SaveSlot
    $trainingOutputDirectory = Join-Path $caseDirectory "training-output"
    New-Item -ItemType Directory -Path $caseDirectory | Out-Null
    New-Item -ItemType Directory -Path $isolatedSavesPath | Out-Null
    New-Item -ItemType Directory -Path $trainingOutputDirectory | Out-Null
    Copy-Item -LiteralPath $sourceSaveSlot -Destination $isolatedSaveSlot -Recurse

    $env:STARDEWAI_TEST_SAVES = $isolatedSavesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSavesPath
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $script:isolatedSavesPath = $isolatedSavesPath
    $windowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $caseGameProcess = $null

    try {
        $caseGameProcess = Start-Process -FilePath $smapiExecutable `
            -WorkingDirectory $gameDirectory -WindowStyle $windowStyle -PassThru
        Wait-Json "$executorBaseUrl/health" 45 | Out-Null
        $initial = Wait-WorldSnapshot { param($snapshot) $true } "world-ready snapshot"
        $setup = Invoke-JsonPost $executorUrl (New-ExecutionRequest `
            $caseRunId "fixture.$($Case.Name)" ([string]$initial.state_hash) `
            $Case.FixtureKind $Case.QuestId $Case.QuestKey)
        Write-Json (Join-Path $caseDirectory "setup-result.json") $setup
        if ($setup.status -ne "applied" -or $setup.primitive_verification_status -ne "verified") {
            throw "Quest terminal fixture failed for $($Case.Name): $(@($setup.block_reasons) -join ',')"
        }

        $before = Wait-WorldSnapshot {
            param($snapshot)
            if ($Case.Name -eq "craft-item") {
                return $null -ne (Find-CraftingQuest $snapshot $Case.QuestId) -and
                    @($snapshot.state.player.quest_crafting.value.rows | Where-Object {
                        [string]$_.quest_id -eq $Case.QuestId -and
                        [string]$_.craft_candidate_status -eq "ready_for_native_personal_crafting_menu"
                    }).Count -gt 0
            }
            if ($Case.Name -eq "item-delivery") {
                return $null -ne (Find-OrdinaryQuest $snapshot $Case.QuestId)
            }
            if ($Case.Name -eq "building-construction") {
                return $null -ne (Find-BuildingQuest $snapshot $Case.QuestId) -and
                    @($snapshot.state.player.quest_building_construction.value.rows | Where-Object {
                        [string]$_.quest_id -eq $Case.QuestId -and
                        [string]$_.action_status -eq "ready_for_native_carpenter_menu"
                    }).Count -gt 0
            }
            if ($Case.Name -eq "building-construction-general") {
                return $null -eq (Find-BuildingQuest $snapshot $Case.QuestId) -and
                    @($snapshot.state.player.building_construction_catalog.value.rows | Where-Object {
                        [string]$_.building_type -eq "Coop" -and
                        [string]$_.placement_location_id -eq "Farm" -and
                        [string]$_.action_status -eq "ready_for_native_construction"
                    }).Count -gt 0
            }
            if ($Case.Name -eq "building-skin") {
                return @($snapshot.state.player.building_skin_catalog.value.rows | Where-Object {
                    [string]$_.building_location_id -eq "Farm" -and
                    [string]$_.building_type -eq "Pet Bowl" -and
                    [string]$_.target_skin_key -eq "Stone Pet Bowl" -and
                    [string]$_.current_skin_key -eq "__default__" -and
                    [string]$_.action_status -eq "ready_for_native_skin_change"
                }).Count -eq 1
            }
            if ($Case.Name -eq "building-paint") {
                return @($snapshot.state.player.building_paint_catalog.value.rows | Where-Object {
                    [string]$_.building_location_id -eq "Farm" -and
                    [string]$_.building_type -eq "Farmhouse" -and
                    [bool]$_.current_default -and
                    [string]$_.action_status -eq "ready_for_native_building_paint"
                }).Count -gt 0
            }
            $objective = Find-DonateObjective $snapshot $Case.QuestKey $Case.RequiredTagPrefix
            return $null -ne $objective -and [int]$objective.current_count -eq 0
        } "ready quest terminal fixture $($Case.Name)" $(if ($Case.Name -like "building-construction*") { 30 } else { 120 })
        $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"
        Write-Json $snapshotPath $before
        Invoke-JsonPostRaw "$backendUrl/api/v1/snapshots" `
            (Get-Content -LiteralPath $snapshotPath -Raw) | Out-Null

        if ($Case.Name -eq "building-paint") {
            $paintRow = @($before.state.player.building_paint_catalog.value.rows | Where-Object {
                [string]$_.building_location_id -eq "Farm" -and [string]$_.building_type -eq "Farmhouse" -and
                [bool]$_.current_default -and [string]$_.action_status -eq "ready_for_native_building_paint"
            }) | Select-Object -First 1
            $hues = @($paintRow.hue_mouse_reachable_values)
            $saturations = @($paintRow.saturation_mouse_reachable_values)
            $lightnesses = @($paintRow.lightness_mouse_reachable_values)
            $targetHue = [int]$hues[[math]::Floor($hues.Count / 2)]
            $targetSaturation = [int]$saturations[[math]::Floor($saturations.Count / 2)]
            $targetLightness = [int]$lightnesses[[math]::Floor($lightnesses.Count / 2)]
            $Case.IntentParameters = @(
                [pscustomobject]@{ name = "building_location_id"; value = [string]$paintRow.building_location_id },
                [pscustomobject]@{ name = "building_type"; value = [string]$paintRow.building_type },
                [pscustomobject]@{ name = "building_tile_x"; value = [string]$paintRow.building_tile_x },
                [pscustomobject]@{ name = "building_tile_y"; value = [string]$paintRow.building_tile_y },
                [pscustomobject]@{ name = "paint_region_id"; value = [string]$paintRow.paint_region_id },
                [pscustomobject]@{ name = "paint_target_mode"; value = "custom" },
                [pscustomobject]@{ name = "target_hue"; value = [string]$targetHue },
                [pscustomobject]@{ name = "target_saturation"; value = [string]$targetSaturation },
                [pscustomobject]@{ name = "target_lightness"; value = [string]$targetLightness },
                [pscustomobject]@{ name = "appearance_reason"; value = "explicit_test_appearance_choice" }
            )
            $Case | Add-Member -NotePropertyName PaintRegionId -NotePropertyValue ([string]$paintRow.paint_region_id) -Force
            $Case | Add-Member -NotePropertyName TargetHue -NotePropertyValue $targetHue -Force
            $Case | Add-Member -NotePropertyName TargetSaturation -NotePropertyValue $targetSaturation -Force
            $Case | Add-Member -NotePropertyName TargetLightness -NotePropertyValue $targetLightness -Force
        }

        $availabilityCandidate = [ordered]@{
            option_id = $Case.CandidateOptionId
            parameters = @($Case.IntentParameters)
            actor_is_host = $true
        }
        $availability = Invoke-JsonPost "$backendUrl/api/v1/planner/options/availability" ([ordered]@{
            state_hash = [string]$before.state_hash
            candidate_option_ids = @()
            candidates = @($availabilityCandidate)
            include_executor_calibration_options = $true
        })
        Write-Json (Join-Path $caseDirectory "availability.json") $availability
        $candidate = @($availability.options |
            Where-Object { $_.option_id -eq $Case.CandidateOptionId } |
            ForEach-Object { $_.event_candidates } |
            Where-Object {
                [bool]$_.available -and
                ([string]::IsNullOrWhiteSpace($Case.QuestCandidateId) -or
                 [string](Get-CandidateParameter $_ "quest_candidate_id") -eq $Case.QuestCandidateId) -and
                [string]$_.kind -eq $Case.CandidateKind
            }) | Select-Object -First 1
        if ($null -eq $candidate) {
            throw "Exact quest candidate unavailable for $($Case.Name)."
        }
        $candidateId = [string]$candidate.candidate_id

        $loopArguments = @(
            "run", "--no-restore", "--project",
            (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj"), "--",
            "--root", $caseLoopRoot,
            "--backend-url", $backendUrl,
            "--bridge-snapshot-url", $snapshotUrl,
            "--executor-url", $executorBaseUrl,
            "--snapshot-file", $snapshotPath,
            "--no-manifest",
            "--run-id", $caseRunId,
            "--save-isolation-path", $isolatedSavesPath,
            "--iterations", "1",
            "--train-every", "1",
            "--sleep-ms", "0",
            "--use-daily-plan",
            "--daily-plan-max-candidates", "1",
            "--daily-plan-candidate-options", $Case.CandidateOptionId,
            "--daily-plan-candidate-kind", $Case.CandidateKind,
            "--daily-plan-candidate-id", $candidateId,
            "--after-snapshot-wait-ms", "1000")
        foreach ($parameter in @($Case.IntentParameters)) {
            $loopArguments += @("--daily-plan-candidate-parameter", ([string]$parameter.name + "=" + [string]$parameter.value))
        }
        & dotnet @loopArguments | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "LiveTrainingLoop failed for $($Case.Name) with exit code $LASTEXITCODE"
        }

        $snapshotDirectory = Join-Path $caseLoopRoot `
            (Join-Path "runs" (Join-Path $caseRunId "live-snapshots"))
        $dailyPlanPath = Join-Path $snapshotDirectory "daily-plan-response-0001.json"
        $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
        $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
        $datasetPath = Join-Path $caseLoopRoot "datasets\live-training-feature-rows.jsonl"
        foreach ($requiredPath in @($dailyPlanPath, $queuePath, $executionPath)) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Required quest terminal artifact missing: $requiredPath"
            }
        }

        $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
        $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
        $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
        $queueItem = @($queue.items | Where-Object {
            $_.option_id -eq $Case.PrimitiveOptionId
        }) | Select-Object -First 1
        $stepResult = @($execution.step_results | Where-Object {
            $_.option_id -eq $Case.PrimitiveOptionId -and
            $_.queue_item_id -eq $queueItem.queue_item_id
        }) | Select-Object -First 1
        if ($null -eq $queueItem -or $null -eq $stepResult -or
            $stepResult.status -ne "applied" -or
            $stepResult.primitive_verification_status -ne "verified") {
            $firstResult = @($execution.step_results | Select-Object -First 1)
            throw "Quest terminal execution failed for $($Case.Name): terminal_status=$($stepResult.status); terminal_verification=$($stepResult.primitive_verification_status); terminal_reasons=$(@($stepResult.block_reasons) -join ','); first_option=$($firstResult.option_id); first_status=$($firstResult.status); first_reasons=$(@($firstResult.block_reasons) -join ',')"
        }
        if (-not (Test-Path -LiteralPath $datasetPath -PathType Leaf)) {
            throw "Verified quest terminal dataset artifact missing: $datasetPath"
        }

        $after = Wait-WorldSnapshot {
            param($snapshot)
            if ($Case.Name -eq "craft-item") {
                return $null -eq (Find-CraftingQuest $snapshot $Case.QuestId) -and
                    -not [bool]$snapshot.state.menus.active_menu.value.is_open
            }
            if ($Case.Name -eq "item-delivery") {
                return $null -eq (Find-OrdinaryQuest $snapshot $Case.QuestId)
            }
            if ($Case.Name -eq "building-construction") {
                return $null -ne (Find-BuildingQuest $snapshot $Case.QuestId) -and
                    @($snapshot.state.player.quest_building_construction.value.rows | Where-Object {
                        [string]$_.quest_id -eq $Case.QuestId -and
                        [string]$_.action_status -eq "construction_in_progress" -and
                        [int]$_.construction_days_left -eq 3
                    }).Count -gt 0
            }
            if ($Case.Name -eq "building-construction-general") {
                return @($snapshot.state.player.building_construction_catalog.value.rows | Where-Object {
                    [string]$_.building_type -eq "Coop" -and
                    [string]$_.placement_location_id -eq "Farm" -and
                    [int]$_.matching_under_construction_count -eq 1 -and
                    [int]$_.matching_under_construction[0].days_of_construction_left -eq 3 -and
                    [string]$_.action_status -eq "another_building_under_construction"
                }).Count -gt 0
            }
            if ($Case.Name -eq "building-skin") {
                return @($snapshot.state.player.building_skin_catalog.value.rows | Where-Object {
                    [string]$_.building_location_id -eq "Farm" -and
                    [string]$_.building_type -eq "Pet Bowl" -and
                    [string]$_.current_skin_key -eq "Stone Pet Bowl" -and
                    [bool]$_.current_paint_color_1_default -and
                    [bool]$_.current_paint_color_2_default -and
                    [bool]$_.current_paint_color_3_default
                }).Count -gt 0 -and -not [bool]$snapshot.state.menus.active_menu.value.is_open
            }
            if ($Case.Name -eq "building-paint") {
                $rows = @($snapshot.state.player.building_paint_catalog.value.rows | Where-Object {
                    [string]$_.building_location_id -eq "Farm" -and [string]$_.building_type -eq "Farmhouse"
                })
                $target = @($rows | Where-Object {
                    [string]$_.paint_region_id -eq $Case.PaintRegionId -and -not [bool]$_.current_default -and
                    [int]$_.current_hue -eq $Case.TargetHue -and [int]$_.current_saturation -eq $Case.TargetSaturation -and
                    [int]$_.current_lightness -eq $Case.TargetLightness
                })
                $siblings = @($rows | Where-Object { [string]$_.paint_region_id -ne $Case.PaintRegionId })
                return $target.Count -eq 1 -and @($siblings | Where-Object { -not [bool]$_.current_default }).Count -eq 0 -and
                    -not [bool]$snapshot.state.menus.active_menu.value.is_open
            }
            $objective = Find-DonateObjective $snapshot $Case.QuestKey $Case.RequiredTagPrefix
            return $null -ne $objective -and [int]$objective.current_count -eq 1 -and
                -not [bool]$snapshot.state.menus.active_menu.value.is_open
        } "verified quest terminal result $($Case.Name)"
        Write-Json (Join-Path $caseDirectory "after-snapshot.json") $after
        $datasetText = Get-Content -LiteralPath $datasetPath -Raw
        $passed = $null -ne $dailyPlan.plan -and
            [string]$queue.status -eq "pending" -and
            [string]$stepResult.status -eq "applied" -and
            [string]$stepResult.primitive_verification_status -eq "verified" -and
            $datasetText.Contains($Case.PrimitiveOptionId)
        [ordered]@{
            case = $Case.Name
            passed = $passed
            candidate_id = [string]$candidate.candidate_id
            candidate_kind = [string]$candidate.kind
            primitive_option_id = $Case.PrimitiveOptionId
            execution_status = [string]$stepResult.status
            verification_status = [string]$stepResult.primitive_verification_status
            verification_reasons = @($stepResult.primitive_verification_reasons)
            location_before = [string]$before.state.player.location_id.value
            location_after = [string]$after.state.player.location_id.value
            dataset_path = $datasetPath
            daily_plan_path = $dailyPlanPath
            queue_path = $queuePath
            execution_path = $executionPath
        }
    }
    finally {
        $keepThisGame = $KeepGameRunning -and $Case.Name -eq "drop-box"
        if (-not $keepThisGame -and $null -ne $caseGameProcess -and -not $caseGameProcess.HasExited) {
            Stop-Process -Id $caseGameProcess.Id -Force -ErrorAction SilentlyContinue
            Wait-Process -Id $caseGameProcess.Id -Timeout 15 -ErrorAction SilentlyContinue
        }
        if ($keepThisGame) { $script:gameProcess = $caseGameProcess }
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$sourceSavesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorBaseUrl = "http://127.0.0.1:8767"
$executorUrl = "$executorBaseUrl/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $sourceSavesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
$sourceSaveSlot = Join-Path $sourceSavesPath $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSaveSlot -PathType Container)) {
    throw "Source save slot not found: $sourceSaveSlot"
}
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-quest-terminal-daily-plan\" + $RunId)
if (Test-Path -LiteralPath $artifactDirectory) {
    throw "Artifact directory already exists: $artifactDirectory"
}
New-Item -ItemType Directory -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "ASPNETCORE_URLS")
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
$gameProcess = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl
    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $cases = @(
        [pscustomobject]@{
            Name = "craft-item"
            FixtureKind = "craft_item"
            QuestId = "stardewai.runtime.crafting"
            QuestKey = ""
            RequiredTagPrefix = ""
            QuestCandidateId = "quest:stardewai.runtime.crafting:CraftingQuest"
            CandidateKind = "craft_quest_item"
            PrimitiveOptionId = "executor.craft_quest_item"
            CandidateOptionId = "quest.advance"
            IntentParameters = @()
        },
        [pscustomobject]@{
            Name = "item-delivery"
            FixtureKind = "offer_item"
            QuestId = "stardewai.runtime.item_delivery"
            QuestKey = ""
            RequiredTagPrefix = ""
            QuestCandidateId = "quest:stardewai.runtime.item_delivery:ItemDeliveryQuest"
            CandidateKind = "quest_npc_interaction"
            PrimitiveOptionId = "executor.quest_npc_interact"
            CandidateOptionId = "quest.advance"
            IntentParameters = @()
        },
        [pscustomobject]@{
            Name = "building-construction"
            FixtureKind = "building_construction"
            QuestId = "stardewai.runtime.building"
            QuestKey = ""
            RequiredTagPrefix = ""
            QuestCandidateId = "quest:stardewai.runtime.building:HaveBuildingQuest"
            CandidateKind = "construct_quest_building"
            PrimitiveOptionId = "executor.construct_building"
            CandidateOptionId = "quest.advance"
            IntentParameters = @()
        },
        [pscustomobject]@{
            Name = "building-construction-general"
            FixtureKind = "building_construction_general"
            QuestId = ""
            QuestKey = ""
            RequiredTagPrefix = ""
            QuestCandidateId = ""
            CandidateKind = "construct_building"
            PrimitiveOptionId = "executor.construct_building"
            CandidateOptionId = "buildings.construct"
            IntentParameters = @(
                [pscustomobject]@{ name = "building_type"; value = "Coop" },
                [pscustomobject]@{ name = "placement_location_id"; value = "Farm" },
                [pscustomobject]@{ name = "construction_reason"; value = "animal_capacity" }
            )
        },
        [pscustomobject]@{
            Name = "drop-box"
            FixtureKind = "drop_box"
            QuestId = ""
            QuestKey = "Gunther"
            RequiredTagPrefix = ""
            QuestCandidateId = "special_order:Gunther"
            CandidateKind = "quest_drop_box_donation"
            PrimitiveOptionId = "executor.quest_drop_box_donate"
            CandidateOptionId = "quest.advance"
            IntentParameters = @()
        },
        [pscustomobject]@{
            Name = "building-skin"
            FixtureKind = "building_skin"
            QuestId = ""
            QuestKey = ""
            RequiredTagPrefix = ""
            QuestCandidateId = ""
            CandidateKind = "change_building_skin"
            PrimitiveOptionId = "executor.change_building_skin"
            CandidateOptionId = "buildings.change_skin"
            IntentParameters = @(
                [pscustomobject]@{ name = "building_location_id"; value = "Farm" },
                [pscustomobject]@{ name = "building_type"; value = "Pet Bowl" },
                [pscustomobject]@{ name = "building_tile_x"; value = "49" },
                [pscustomobject]@{ name = "building_tile_y"; value = "40" },
                [pscustomobject]@{ name = "target_skin_key"; value = "Stone Pet Bowl" },
                [pscustomobject]@{ name = "appearance_reason"; value = "explicit_test_appearance_choice" }
            )
        },
        [pscustomobject]@{
            Name = "building-paint"
            FixtureKind = "building_paint"
            QuestId = ""
            QuestKey = ""
            RequiredTagPrefix = ""
            QuestCandidateId = ""
            CandidateKind = "paint_building_region"
            PrimitiveOptionId = "executor.change_building_skin"
            CandidateOptionId = "buildings.paint"
            IntentParameters = @()
        },
        [pscustomobject]@{
            Name = "drop-box-preserved-parent-color"
            FixtureKind = "drop_box_color"
            QuestId = ""
            QuestKey = "QiChallenge12"
            RequiredTagPrefix = "color_red"
            QuestCandidateId = "special_order:QiChallenge12"
            CandidateKind = "quest_drop_box_donation"
            PrimitiveOptionId = "executor.quest_drop_box_donate"
            CandidateOptionId = "quest.advance"
            IntentParameters = @()
        }
    )
    if ($CaseName.Count -gt 0) {
        $cases = @($cases | Where-Object { $CaseName -contains $_.Name })
        if ($cases.Count -ne $CaseName.Count) {
            throw "One or more requested quest terminal cases are unknown: $($CaseName -join ',')"
        }
    }
    $caseResults = @($cases | ForEach-Object { Invoke-QuestTerminalCase $_ })
    $passedCount = @($caseResults | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $cases.Count) { "passed" } else { "failed" }
        run_id = $RunId
        source_save_slot = $sourceSaveSlot
        expected_case_count = $cases.Count
        passed_case_count = $passedCount
        cases = $caseResults
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if ($passedCount -ne $cases.Count) {
        throw "Runtime quest terminal matrix failed: $artifactDirectory"
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
