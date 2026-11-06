[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string] $RuntimeRoot = 'E:\StardewValleyAICompanion-runtime',
    [Parameter(Mandatory)] [string] $SaveSlot,
    [Parameter(Mandatory)] [string] $BasisSnapshot,
    [Parameter(Mandatory)] [string] $TimingCalibration,
    [Parameter(Mandatory)] [ValidateSet(5, 10)] [int] $TargetCount,
    [ValidateRange(0, 28)] [int] $DecisionWinterDayYearTwo = 0,
    [ValidateSet(1110)] [int] $DeclaredFixtureTime = 1110,
    [string] $ArtifactRoot = 'I:\StardewAITraining\friendship-native-20261106',
    [string] $RunId = ('friendship-threshold-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [int] $BackendPort = 5159,
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
if ($RunId -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,100}$' -or
    $SaveSlot -ne [IO.Path]::GetFileName($SaveSlot) -or $SaveSlot -in @('', '.', '..')) {
    throw 'Run and slot names must be single safe path components.'
}
$runRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactRoot)) $RunId
if (Test-Path -LiteralPath $runRoot) { throw "Run already exists: $runRoot" }
$gameDirectory = Join-Path $RuntimeRoot 'Stardew Valley'
$eventAsset = Assert-RuntimeFileIdentity -Path (Join-Path $gameDirectory 'Content/Data/Events/JoshHouse.xnb') `
    -ExpectedSha256 '2d9d465c4b204eb11f246315e2653fa08083e8b315191c93ec5c2ce16ca2ba3f' -Label 'Pinned local story fixture asset'
$sourceSave = Assert-RuntimeSourceSaveSlot -Path (Join-Path (Join-Path $RuntimeRoot 'saves') $SaveSlot)
$sourceHash = Get-DirectoryContentHash -Path $sourceSave
Assert-RuntimePortsUnused -Ports @($BackendPort, 8765, 8767)
if (Get-Process -Name @('StardewModdingAPI', 'Stardew Valley') -ErrorAction SilentlyContinue) {
    throw 'A game is running; refusing to attach, replace Mods, or stop it.'
}
$bootstrapDll = Join-Path $ProjectRoot 'experiments/StardewAI.GoalConditionedBootstrap/bin/Release/net8.0/StardewAI.GoalConditionedBootstrap.dll'
$backendDll = Join-Path $ProjectRoot 'src/StardewAI.Backend/bin/Release/net8.0/StardewAI.Backend.dll'
$loopDll = Join-Path $ProjectRoot 'tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'
if (-not $NoBuild) {
    $projects = [ordered]@{
        'src/StardewAI.TransparentBridge/StardewAI.TransparentBridge.csproj' = 'Debug'
        'tools/StardewAI.RuntimeTestHarness/StardewAI.RuntimeTestHarness.csproj' = 'Debug'
        'src/StardewAI.Backend/StardewAI.Backend.csproj' = 'Release'
        'tools/StardewAI.LiveTrainingLoop/StardewAI.LiveTrainingLoop.csproj' = 'Release'
        'experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj' = 'Release'
    }
    foreach ($project in $projects.Keys) {
        & dotnet build (Join-Path $ProjectRoot $project) -c $projects[$project] --no-restore --nologo `
            -m:1 --disable-build-servers -p:UseSharedCompilation=false "-p:GamePath=$gameDirectory" `
            -p:EnableModDeploy=false -p:EnableModZip=false
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
}
foreach ($deploy in @('TransparentBridge', 'RuntimeTestHarness')) {
    & (Join-Path $PSScriptRoot "Deploy-${deploy}ToRuntime.ps1") -ProjectRoot $ProjectRoot `
        -RuntimeRoot $RuntimeRoot -GamePath $gameDirectory -NoBuild | Out-Null
}
$isolatedSaves = Join-Path $runRoot 'isolated-saves'
$isolatedMods = Join-Path $runRoot 'smapi-mods'
New-Item -ItemType Directory -Path $isolatedSaves, $isolatedMods -Force | Out-Null
$clone = Join-Path $isolatedSaves $SaveSlot
Copy-Item -LiteralPath $sourceSave -Destination $clone -Recurse
if ((Get-DirectoryContentHash -Path $clone) -cne $sourceHash) { throw 'Source clone hash differs.' }
foreach ($name in @('StardewAI.TransparentBridge', 'StardewAI.RuntimeTestHarness')) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory "Mods/$name") -Destination (Join-Path $isolatedMods $name) -Recurse
}
$mods = @(Get-RuntimeExactModAllowlist -ModsRoot $isolatedMods -ExpectedUniqueIds ([ordered]@{
    'StardewAI.TransparentBridge' = 'StardewAI.TransparentBridge'
    'StardewAI.RuntimeTestHarness' = 'StardewAI.RuntimeTestHarness'
}))
$boundaryPath = Join-Path $runRoot 'boundary.json'
$requestPath = Join-Path $runRoot 'boundary-request.json'
$boundaryRequest = [ordered]@{
    schema_version = 'friendship_terminal_save_boundary_request.v1'
    source_save_file = Join-Path $sourceSave $SaveSlot
    isolated_save_file = Join-Path $clone $SaveSlot
    isolation_root = $isolatedSaves
    basis_snapshot_path = [IO.Path]::GetFullPath($BasisSnapshot)
    target_qualifying_count = $TargetCount
    declared_local_prerequisite_only = $true
    declared_local_story_prerequisites = $true
    formal_product_training_authorized = $false
}
if ($DecisionWinterDayYearTwo -gt 0) { $boundaryRequest.decision_winter_day_year_two = $DecisionWinterDayYearTwo }
Write-JsonFile -Path $requestPath -Value $boundaryRequest
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    'prepare-friendship-terminal-save-boundary', '--request', $requestPath, '--output', $boundaryPath)
$binaryIdentity = [ordered]@{}
foreach ($path in @($bootstrapDll, $backendDll, $loopDll, (Join-Path $gameDirectory 'Stardew Valley.dll'), $eventAsset)) {
    $binaryIdentity[$path] = Get-RuntimeFileSha256 -Path $path
}
Write-JsonFile -Path (Join-Path $runRoot 'run-identity.json') -Value ([ordered]@{
    run_id = $RunId; project_head = [string](& git -C $ProjectRoot rev-parse HEAD)
    source_save_sha256 = $sourceHash; isolated_mods = $mods; binary_sha256 = $binaryIdentity
    formal_product_training_authorized = $false
})
$backendUrl = "http://127.0.0.1:$BackendPort"
$executorUrl = 'http://127.0.0.1:8767'
$snapshotUrl = 'http://127.0.0.1:8765/api/v1/snapshot?profile=social_future&fresh=true'
$environment = [ordered]@{
    ASPNETCORE_URLS = $backendUrl
    STARDEWAI_TEST_SAVES = $isolatedSaves
    STARDEWAI_TEST_SLOT = $SaveSlot
    STARDEWAI_TEST_AUTO_LOAD = 'true'
    STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSaves
    STARDEWAI_TRAINING_RUN_ID = $RunId
    STARDEWAI_TRAINING_MODE = '1'
    STARDEWAI_TRAINING_OUTPUT_DIR = (Join-Path $runRoot 'runtime-receipts')
    STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = 'true'
    STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS = 'false'
    STARDEWAI_SUPPRESS_LOCAL_RENDER = '1'
    SMAPI_MODS_PATH = $isolatedMods
    SDL_AUDIODRIVER = 'dummy'
    ALSOFT_DRIVERS = 'null'
}
$savedEnvironment = Save-RuntimeProcessEnvironment -Names @($environment.Keys)
$processes = [Collections.Generic.List[object]]::new()
$summary = [ordered]@{
    schema_version = 'friendship_threshold_native_smoke.v1'; run_id = $RunId; status = 'failed'
    target_qualifying_count = $TargetCount; boundary_path = $boundaryPath
    native_criterion_outcome_verified = $false; prior_friendship_acquisition_verified = $false
    formal_product_training_authorized = $false
}
if ($DecisionWinterDayYearTwo -gt 0) {
    $summary.declared_fixture_winter_day_year_two = $DecisionWinterDayYearTwo
    $summary.calendar_fixture_is_not_native_acquisition = $true
}
try {
    foreach ($name in $environment.Keys) { [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process') }
    $processes.Add((Start-RuntimeEvidenceProcess -FilePath dotnet -ArgumentList @($backendDll) `
        -WorkingDirectory (Split-Path -Parent $backendDll) -StandardOutputPath (Join-Path $runRoot 'backend.stdout.log') `
        -StandardErrorPath (Join-Path $runRoot 'backend.stderr.log')))
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null
    $processes.Add((Start-RuntimeEvidenceProcess -FilePath (Join-Path $gameDirectory 'StardewModdingAPI.exe') `
        -WorkingDirectory $gameDirectory -StandardOutputPath (Join-Path $runRoot 'game.stdout.log') `
        -StandardErrorPath (Join-Path $runRoot 'game.stderr.log')))
    Wait-Json -Url "$executorUrl/health" -TimeoutSeconds 180 | Out-Null
    $before = Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 180
    Write-Utf8Text -Path (Join-Path $runRoot 'loaded.json') -Value $before.Raw
    if ($DecisionWinterDayYearTwo -gt 0 -and (
        [int](Read-StateValue $before.Value 'time' 'year') -ne 2 -or
        (Read-StateValue $before.Value 'time' 'season') -cne 'winter' -or
        [int](Read-StateValue $before.Value 'time' 'day') -ne $DecisionWinterDayYearTwo)) {
        throw 'Native loaded date differs from the explicitly declared clone calendar fixture.'
    }
    if ((Read-StateValue $before.Value 'player' 'story_event').event_up) {
        throw 'Native load triggered a story event; do not skip or mark it seen for this measurement.'
    }
    if ([int](Read-StateValue $before.Value 'time' 'time') -lt $DeclaredFixtureTime) {
        $fixture = Invoke-JsonPost -Url "$executorUrl/api/v1/training/execute" -Body ([ordered]@{
            schema_version = 'training_execution_request.v1'
            run_id = $RunId; queue_id = "$RunId.fixture"; queue_item_id = "$RunId.fixture.time"
            before_state_hash = $before.Value.state_hash; option_id = 'debug.advance_time_to'
            execution_mode = 'training_singleplayer'; actor = 'training_farmer.main'
            save_isolation_path = $isolatedSaves; request_nonce = [guid]::NewGuid().ToString('N')
            created_at = [DateTimeOffset]::UtcNow.ToString('O'); target_time = 1030
        })
        Write-Utf8Text -Path (Join-Path $runRoot 'fixture-time-result.json') -Value $fixture.Raw
        if ($fixture.Value.status -cne 'applied' -or $fixture.Value.primitive_verification_status -cne 'verified') {
            throw 'Declared opening-time fixture failed.'
        }
        $before = Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 60
        # The existing debug fixture changes only the clock. Native waiting must
        # cross 10:40 so Evelyn reaches a stationary native contact window,
        # instead of her earlier square_3_1_0 wandering behavior.
        $settle = Invoke-JsonPost -Url "$executorUrl/api/v1/training/execute" -Body ([ordered]@{
            schema_version = 'training_execution_request.v1'
            run_id = $RunId; queue_id = "$RunId.fixture"; queue_item_id = "$RunId.fixture.schedule"
            before_state_hash = $before.Value.state_hash; option_id = 'executor.wait_ticks'
            execution_mode = 'training_singleplayer'; actor = 'training_farmer.main'
            save_isolation_path = $isolatedSaves; request_nonce = [guid]::NewGuid().ToString('N')
            created_at = [DateTimeOffset]::UtcNow.ToString('O')
            wait_ticks = 600; wait_max_slices = 3; wait_until_time = $DeclaredFixtureTime
        })
        Write-Utf8Text -Path (Join-Path $runRoot 'fixture-schedule-result.json') -Value $settle.Raw
        if ($settle.Value.status -cne 'applied' -or $settle.Value.primitive_verification_status -cne 'verified') {
            throw 'Native schedule-settling fixture failed.'
        }
        $before = Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 60
    }
    $beforePath = Join-Path $runRoot 'before.json'
    Write-Utf8Text -Path $beforePath -Value $before.Raw
    $progress = Read-StateValue $before.Value 'npcs' 'grandpa_friendship_progress'
    if ((Read-StateValue $before.Value 'player' 'location_id') -cne 'JoshHouse' -or
        (Read-StateValue $before.Value 'player' 'tile_x') -ne 15 -or
        (Read-StateValue $before.Value 'player' 'tile_y') -ne 22 -or
        (Read-StateValue $before.Value 'player' 'story_event').event_up -or
        [int]$progress.qualifying_count -ne ($TargetCount - 1)) {
        throw 'Loaded native location or qualifying count differs from the declared boundary.'
    }
    $preferencePath = Join-Path $runRoot 'preference.json'
    $verifiedPath = Join-Path $runRoot 'preference-verified.json'
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        'build-current-friendship-teacher-preference', '--snapshot', $beforePath, '--calibration', $TimingCalibration,
        '--admission', 'independent_pairs', '--output', $preferencePath)
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        'verify-current-friendship-teacher-preference', '--preference', $preferencePath,
        '--admission', 'independent_pairs', '--output', $verifiedPath)
    if ((Get-RuntimeFileSha256 -Path $preferencePath) -cne (Get-RuntimeFileSha256 -Path $verifiedPath)) {
        throw 'Independent preference replay differs.'
    }
    $preference = Get-Content -LiteralPath $preferencePath -Raw | ConvertFrom-Json
    $selected = @($preference.candidates | Where-Object { $_.candidateId -ceq $preference.selectedCandidate.candidate_id })
    if ($selected.Count -ne 1 -or $selected[0].npcName -cnotin @('George', 'Evelyn') -or
        [int]$selected[0].friendshipPointsBefore -ne 1955 -or [int]$selected[0].expectedNewQualifiers -ne 1) {
        throw 'Unmodified Teacher did not select a declared local threshold target; retain diagnostics, do not narrow its pool.'
    }
    $queuePath = Join-Path $runRoot 'queue.json'
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        'compile-current-friendship-teacher-preference', '--preference', $preferencePath,
        '--admission', 'independent_pairs', '--plan-output', (Join-Path $runRoot 'plan.json'), '--output', $queuePath)
    $context = New-RuntimeQueueContext -LoopDll $loopDll -ArtifactDirectory $runRoot -BackendUrl $backendUrl `
        -SnapshotUrl $snapshotUrl -SnapshotProfile social_future -ExecutorRoot $executorUrl -ProductUrl $executorUrl `
        -RunId $RunId -IsolatedSavesPath $isolatedSaves
    $result = Invoke-RuntimePrecompiledQueue -Context $context -Name 'measure' -QueuePath $queuePath `
        -BeforePath $beforePath -BeforeSnapshotProfile social_future -ExecutionTarget runtime_harness
    $afterProgress = Read-StateValue $result.After 'npcs' 'grandpa_friendship_progress'
    $target = [string]$selected[0].npcName
    $nativeBefore = @($progress.eligible_villager_rows | Where-Object { $_.npc_name -ceq $target })
    $nativeAfter = @($afterProgress.eligible_villager_rows | Where-Object { $_.npc_name -ceq $target })
    if ($result.Execution.schema_version -cne 'queue_execution_receipt.v1' -or
        -not $result.Execution.selected_candidate_completed -or
        [int]$afterProgress.qualifying_count -ne $TargetCount -or $nativeBefore.Count -ne 1 -or $nativeAfter.Count -ne 1 -or
        [int]$nativeBefore[0].friendship_points -ne 1955 -or [int]$nativeAfter[0].friendship_points -lt 1975) {
        throw 'Native execution did not cross the exact qualifying-count boundary.'
    }
    $summary.status = 'native_boundary_crossed_pending_typed_receipt_join'
    $summary.native_criterion_outcome_verified = $true
    $summary.before_qualifying_count = [int]$progress.qualifying_count
    $summary.after_qualifying_count = [int]$afterProgress.qualifying_count
    $summary.npc_name = $target
    $summary.before_points = [int]$nativeBefore[0].friendship_points
    $summary.after_points = [int]$nativeAfter[0].friendship_points
    $summary.execution_path = $result.ExecutionPath
    $summary.before_path = $result.BeforePath
    $summary.after_path = $result.AfterPath
    $summary.preference_path = $preferencePath
    $summary.uncompared_candidate_count = @($preference.uncomparedCandidateIds).Count
    $summary.declared_fixture_time = $DeclaredFixtureTime
}
catch { $summary.error = $_.Exception.Message; throw }
finally {
    Stop-RuntimeEvidenceProcesses -Processes $processes.ToArray()
    Restore-RuntimeProcessEnvironment -Values $savedEnvironment
    $summary.source_save_unchanged = (Get-DirectoryContentHash -Path $sourceSave) -ceq $sourceHash
    Write-JsonFile -Path (Join-Path $runRoot 'summary.json') -Value $summary
    if (-not $summary.source_save_unchanged) { throw 'Original source save changed; reject this evidence.' }
}
$summary | ConvertTo-Json -Depth 5
