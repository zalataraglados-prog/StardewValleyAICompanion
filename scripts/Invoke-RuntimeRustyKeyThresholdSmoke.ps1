[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "CrabPotAudit_442159967",
    [string] $RunId = ("rusty-key-threshold-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-rusty-key-threshold",
    [int] $BackendPort = 5132,
    [switch] $NoBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeRustyKeyEvidence.ps1")
$runRoot = Join-Path (Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputDirectory) $RunId
if (Test-Path -LiteralPath $runRoot) { throw "Run already exists: $runRoot" }
$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$sourceSave = Assert-RuntimeSourceSaveSlot `
    -Path (Join-Path (Join-Path $RuntimeRoot "saves") $SaveSlot)
$sourceHash = Get-DirectoryContentHash -Path $sourceSave
$isolatedSaves = Join-Path $runRoot "isolated-saves"
$isolatedMods = Join-Path $runRoot "smapi-mods"
$backendUrl = "http://127.0.0.1:$BackendPort"
$executorUrl = "http://127.0.0.1:8767"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$backendDll = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
Assert-RuntimePortsUnused -Ports @($BackendPort, 8765, 8767)
if (Get-Process -Name StardewModdingAPI -ErrorAction SilentlyContinue) {
    throw "A game is running; refusing to attach or replace its Mods."
}
if (-not $NoBuild) {
    foreach ($project in @(
        "src\StardewAI.Backend\StardewAI.Backend.csproj",
        "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj"
    )) {
        & dotnet build (Join-Path $ProjectRoot $project) -c Release `
            --no-restore --nologo -m:1 /nodeReuse:false
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
}
foreach ($deploy in @("TransparentBridge", "RuntimeTestHarness")) {
    & (Join-Path $PSScriptRoot "Deploy-${deploy}ToRuntime.ps1") `
        -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
        -NoBuild:$NoBuild | Out-Null
}
New-Item -ItemType Directory -Path $isolatedSaves -Force | Out-Null
New-Item -ItemType Directory -Path $isolatedMods -Force | Out-Null
Copy-Item -LiteralPath $sourceSave -Destination (Join-Path $isolatedSaves $SaveSlot) -Recurse
if ((Get-DirectoryContentHash -Path (Join-Path $isolatedSaves $SaveSlot)) -ne $sourceHash) {
    throw "Copied save does not match its source."
}
foreach ($name in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory "Mods\$name") `
        -Destination (Join-Path $isolatedMods $name) -Recurse
}
$modIdentity = @(Get-RuntimeExactModAllowlist -ModsRoot $isolatedMods `
    -ExpectedUniqueIds ([ordered]@{
        "StardewAI.TransparentBridge" = "StardewAI.TransparentBridge"
        "StardewAI.RuntimeTestHarness" = "StardewAI.RuntimeTestHarness"
    }))
$binaryIdentity = [ordered]@{}
foreach ($path in @($backendDll, $loopDll)) {
    $binaryIdentity[$path] = Get-RuntimeFileSha256 -Path $path
}
$savedEnvironment = Save-RuntimeProcessEnvironment -Names @(
    "ASPNETCORE_URLS", "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "STARDEWAI_SUPPRESS_LOCAL_RENDER",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "CLIENT_FPS"
)
$processes = [Collections.Generic.List[object]]::new()
$steps = [Collections.Generic.List[object]]::new()
$transitions = [Collections.Generic.List[object]]::new()
try {
    $env:ASPNETCORE_URLS = $backendUrl
    $env:STARDEWAI_TEST_SAVES = $isolatedSaves
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSaves
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runRoot
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:CLIENT_FPS = "0"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $processes.Add((Start-RuntimeEvidenceProcess -FilePath dotnet `
        -ArgumentList @($backendDll) -WorkingDirectory (Split-Path -Parent $backendDll) `
        -StandardOutputPath (Join-Path $runRoot "backend.stdout.log") `
        -StandardErrorPath (Join-Path $runRoot "backend.stderr.log")))
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null
    $processes.Add((Start-RuntimeEvidenceProcess `
        -FilePath (Join-Path $gameDirectory "StardewModdingAPI.exe") `
        -ArgumentList @("--mods-path", ('"' + $isolatedMods + '"')) `
        -WorkingDirectory $gameDirectory `
        -StandardOutputPath (Join-Path $runRoot "game.stdout.log") `
        -StandardErrorPath (Join-Path $runRoot "game.stderr.log")))
    Wait-Json -Url "$executorUrl/health" -TimeoutSeconds 120 | Out-Null
    $initial = (Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 180).Value
    if ([int](Read-StateValue $initial "time" "time") -lt 900) {
        $timeFixture = Invoke-JsonPost -Url "$executorUrl/api/v1/training/execute" -Body ([ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId; queue_id = "$RunId.fixture"; queue_item_id = "$RunId.fixture.time"
            before_state_hash = $initial.state_hash; option_id = "debug.advance_time_to"
            execution_mode = "training_singleplayer"; actor = "training_farmer.main"
            save_isolation_path = $isolatedSaves; request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O"); target_time = 900
        })
        Write-Utf8Text -Path (Join-Path $runRoot "fixture-time-result.json") -Value $timeFixture.Raw
        if ($timeFixture.Value.status -ne "applied" -or $timeFixture.Value.primitive_verification_status -ne "verified") {
            throw "Opening-hour boundary fixture failed."
        }
        $initial = (Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 60).Value
    }
    $edge = @($initial.state.locations.route_graph.value.edges | Where-Object {
        $_.from_location -eq "Town" -and $_.target_location -eq "ArchaeologyHouse" -and $_.resolved
    } | Sort-Object from_y, from_x | Select-Object -First 1)
    if ($edge.Count -ne 1) { throw "Native Town-to-museum connector is missing." }
    # All direct state setup ends here; every measured transition below is native.
    $fixture = Invoke-JsonPost -Url "$executorUrl/api/v1/training/execute" -Body ([ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId; queue_id = "$RunId.fixture"; queue_item_id = "$RunId.fixture.59"
        before_state_hash = $initial.state_hash; option_id = "debug.setup_museum_donation"
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"
        save_isolation_path = $isolatedSaves; request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        location_id = "Town"; target_tile_x = [int]$edge[0].from_x; target_tile_y = [int]$edge[0].from_y
        inventory_slot_index = 11; expected_donated_count_before = 59; field_guide_quest_present_before = $false
    })
    Write-Utf8Text -Path (Join-Path $runRoot "fixture-result.json") -Value $fixture.Raw
    if ($fixture.Value.status -ne "applied" -or $fixture.Value.primitive_verification_status -ne "verified") {
        throw "59-item museum boundary fixture failed."
    }
    $context = New-RuntimeQueueContext -LoopDll $loopDll -ArtifactDirectory $runRoot `
        -BackendUrl $backendUrl -SnapshotUrl $snapshotUrl -SnapshotProfile full `
        -ExecutorRoot $executorUrl -ProductUrl $executorUrl -RunId $RunId -IsolatedSavesPath $isolatedSaves
    $capture = Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 60
    Assert-RustyKeySnapshot -Snapshot $capture.Value -DonatedCount 59 -RewardApplied $false -KeyReceived $false
    $startingDay = [int](Read-StateValue $capture.Value "time" "total_days")
    for ($index = 0; $index -lt 20; $index++) {
        $snapshot = $capture.Value
        $museum = Read-StateValue $snapshot "world_progress" "museum"
        $story = Read-StateValue $snapshot "player" "story_event"
        if ($museum.rusty_key_event_seen -eq $true) { break }
        $option = if ($story.active -eq $true) { "story.advance_event" }
            elseif ($museum.donated_count -eq 59) { "museum.donate_items" }
            elseif ((Read-StateValue $snapshot "time" "total_days") -eq $startingDay) { "recovery.stabilize_day" }
            else { "exploration.visit_location" }
        $parameters = if ($option -eq "recovery.stabilize_day") {
            @([ordered]@{ name = "control_plane.native_save_boundary"; value = "true" })
        } else { @() }
        $sourcePath = Join-Path $runRoot ("input-{0:D2}.json" -f $index)
        Save-RuntimeSnapshotAndIngest -Path $sourcePath -Capture $capture `
            -BackendUrl $backendUrl -SnapshotProfile full
        $prediction = Invoke-RuntimeRanking -BackendUrl $backendUrl `
            -GoalId "daily.closed_loop" -SnapshotStateHash $snapshot.state_hash `
            -OutputPath (Join-Path $runRoot ("ranking-{0:D2}.json" -f $index)) `
            -OptionId $option -Parameters $parameters
        $candidates = @($prediction.ranked_event_candidates | Where-Object { $_.available -eq $true })
        if ($option -eq "museum.donate_items") {
            $candidates = @($candidates | Where-Object { $_.qualified_item_id -eq "(O)96" -and $_.slot_index -eq 11 })
        } elseif ($option -eq "story.advance_event") {
            $candidates = @($candidates | Where-Object {
                $_.candidate_id -eq "story-event:66:advance_story_event_automatic:continue"
            })
        } elseif ($option -eq "recovery.stabilize_day") {
            $candidates = @($candidates | Where-Object { $_.candidate_id -eq "recovery:native_save_boundary" })
        } elseif ($option -eq "exploration.visit_location") {
            $candidates = @($candidates | Where-Object {
                $_.kind -eq "route_connector_tile" -and
                @($_.parameters | Where-Object {
                    $_.name -eq "expected_target_location" -and $_.value -eq "Farm"
                }).Count -eq 1
            })
        }
        if ($candidates.Count -ne 1) { throw "Expected one exact $option candidate; found $($candidates.Count)." }
        $candidate = $candidates[0]
        $step = Invoke-RuntimeDailyPlanStep -Context $context -Name ("step-{0:D2}" -f $index) `
            -OptionId $option -CandidateKind $candidate.kind -CandidateId $candidate.candidate_id `
            -CandidateParameters @($parameters | ForEach-Object { "$($_.name)=$($_.value)" }) `
            -SourceSnapshotPath $sourcePath -AllowVerifiedContinuation
        foreach ($receipt in @($step.Execution.step_results)) { $steps.Add($receipt) }
        $transitions.Add([ordered]@{
            option_id = $option; candidate_id = $candidate.candidate_id
            before_snapshot_path = $step.BeforePath; queue_path = $step.QueuePath
            execution_path = $step.ExecutionPath; after_snapshot_path = $step.AfterPath
        })
        $capture = Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 60
        Write-Utf8Text -Path (Join-Path $runRoot ("observed-{0:D2}.json" -f $index)) -Value $capture.Raw
        if ($option -eq "museum.donate_items") {
            Assert-RustyKeySnapshot -Snapshot $capture.Value -DonatedCount 60 -RewardApplied $true -KeyReceived $false
        }
        Write-Host "Rusty Key step $index verified: $option;location=$($capture.Value.state.player.location_id.value)"
    }
    Assert-RustyKeySnapshot -Snapshot $capture.Value -DonatedCount 60 -RewardApplied $true -KeyReceived $true
    Assert-RustyKeyNativeSteps -Steps $steps.ToArray()
    $night = Wait-Json -Url "$executorUrl/api/v1/training/end-of-night-transaction" -TimeoutSeconds 15
    Write-JsonFile -Path (Join-Path $runRoot "native-night.json") -Value $night
    if ($night.status -ne "complete" -or $night.saw_native_save_commit -ne $true -or
        $night.saw_expected_new_day -ne $true -or $night.saw_safe_new_day_ready_state -ne $true -or
        $night.debug_negative_fixture_injected -ne $false -or
        $night.safe_next_day_planning_option_id -ne "exploration.visit_location" -or
        $night.completed_total_days -ne ($startingDay + 1) -or
        (Read-StateValue $capture.Value "time" "total_days") -ne ($startingDay + 1)) {
        throw "Rusty Key chain did not include exactly one normally committed night."
    }
    $sourceAfter = Get-DirectoryContentHash -Path $sourceSave
    if ($sourceAfter -ne $sourceHash) { throw "Original isolated baseline save changed." }
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_rusty_key_threshold_smoke.v1"
        status = "passed"; run_id = $RunId; project_head = [string](& git -C $ProjectRoot rev-parse HEAD)
        fixture_boundary = "59_donations_town_inventory_slot_11_item_96"
        evidence_scope = "shared_native_threshold_reward_key_event_capability"
        proves_acquisition_of_60_items = $false; proves_full_criterion = $false
        formal_training_started = $false
        source_save_preserved = $true; source_save_sha256 = $sourceHash
        candidate_binary_sha256 = $binaryIdentity; isolated_mods = $modIdentity
        native_start_total_days = $startingDay; native_end_total_days = $startingDay + 1
        actual_rusty_key_received = $true; native_event_66_completed = $true
        native_step_count = $steps.Count; transitions = $transitions.ToArray()
    }
    Write-JsonFile -Path (Join-Path $runRoot "summary.json") -Value $summary
    $summary | ConvertTo-Json -Depth 20
}
catch {
    Write-JsonFile -Path (Join-Path $runRoot "failure.json") -Value ([ordered]@{
        status = "failed"; run_id = $RunId; reason = $_.Exception.Message
        script_stack = $_.ScriptStackTrace; formal_training_started = $false
    })
    throw
}
finally {
    Stop-RuntimeEvidenceProcesses -Processes $processes.ToArray()
    Restore-RuntimeProcessEnvironment -Values $savedEnvironment
}
