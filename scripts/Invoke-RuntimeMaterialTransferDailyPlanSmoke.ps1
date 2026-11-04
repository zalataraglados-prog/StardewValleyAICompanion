[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string] $RuntimeRoot = 'E:\StardewValleyAICompanion-runtime',
    [string] $SaveSlot = '',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')]
    [string] $RunId = ('runtime-material-transfer-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [string] $OutputDirectory = 'artifacts/runtime-material-transfer-daily-plan-smoke',
    [int] $BackendPort = 5131,
    [int] $StartupTimeoutSeconds = 180,
    [int] $TargetTileX = -1,
    [int] $TargetTileY = -1,
    [ValidateRange(1,11)] [int] $TransferQuantity = 2,
    [ValidateSet('ordinary','sparse_source')] [string] $FixtureProfile = 'ordinary',
    [switch] $NoBuild,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeMaterialTransferSmoke.ps1')

if ($KeepGameRunning) { throw 'Isolated verification always releases its owned processes; KeepGameRunning is not supported.' }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { throw 'An explicit source SaveSlot is required; latest-save discovery is not safe.' }
if ($SaveSlot -match '[\\/:]|^\.\.?$') { throw 'SaveSlot must be a single source directory name.' }
if (($TargetTileX -lt 0) -xor ($TargetTileY -lt 0)) { throw 'TargetTileX and TargetTileY must both be supplied or omitted.' }
if ($FixtureProfile -eq 'sparse_source' -and ($TransferQuantity -lt 2 -or $TransferQuantity -gt 10)) {
    throw 'Sparse native verification requires a partial multi-unit transfer (2..10).'
}
$runRoot = Join-Path (Resolve-RuntimeEvidenceInputPath -ProjectRoot $ProjectRoot -Path $OutputDirectory) $RunId
if (Test-Path -LiteralPath $runRoot) { throw "Existing run cannot be replayed: $runRoot" }
$sourceSave = Assert-RuntimeSourceSaveSlot -Path (Join-Path (Join-Path $RuntimeRoot 'saves') $SaveSlot)
if ($runRoot.StartsWith($sourceSave + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Artifacts must not be nested under the source save.'
}
$sourceHash = Get-DirectoryContentHash -Path $sourceSave
$gameDirectory = Join-Path $RuntimeRoot 'Stardew Valley'
$smapi = Join-Path $gameDirectory 'StardewModdingAPI.exe'
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "Missing game executable: $smapi" }
Assert-RuntimePortsUnused -Ports @($BackendPort,8765,8767)
if (Get-Process -Name StardewModdingAPI -ErrorAction SilentlyContinue) { throw 'A game is already running; refusing to attach.' }
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($runRoot))
if ($drive.AvailableFreeSpace -lt 8GB) { throw 'Material transfer evidence disk floor reached.' }
New-Item -ItemType Directory -Path $runRoot | Out-Null
if (-not $NoBuild) {
    foreach ($project in @(
        @{path='src/StardewAI.Backend/StardewAI.Backend.csproj';configuration='Release'},
        @{path='tools/StardewAI.LiveTrainingLoop/StardewAI.LiveTrainingLoop.csproj';configuration='Release'},
        @{path='src/StardewAI.TransparentBridge/StardewAI.TransparentBridge.csproj';configuration='Debug'},
        @{path='tools/StardewAI.RuntimeTestHarness/StardewAI.RuntimeTestHarness.csproj';configuration='Debug'}
    )) {
        & dotnet build (Join-Path $ProjectRoot $project.path) -c $project.configuration --no-restore --nologo -m:1 /nodeReuse:false `
            "-p:GamePath=$gameDirectory" -p:UseSharedCompilation=false -p:EnableModDeploy=false -p:EnableModZip=false
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $($project.path)" }
    }
}
foreach ($name in @('TransparentBridge','RuntimeTestHarness')) {
    & (Join-Path $PSScriptRoot "Deploy-${name}ToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
        -GamePath $gameDirectory -NoBuild | Out-Null
}
$isolatedSaves = Join-Path $runRoot 'isolated-saves'
$isolatedMods = Join-Path $runRoot 'smapi-mods'
$ledgerDirectory = Join-Path $runRoot 'strategy-ledger'
New-Item -ItemType Directory -Path $isolatedSaves | Out-Null
New-Item -ItemType Directory -Path $isolatedMods | Out-Null
Copy-Item -LiteralPath $sourceSave -Destination (Join-Path $isolatedSaves $SaveSlot) -Recurse
if ((Get-DirectoryContentHash -Path (Join-Path $isolatedSaves $SaveSlot)) -cne $sourceHash) { throw 'Save copy does not match its source.' }
foreach ($name in @('StardewAI.TransparentBridge','StardewAI.RuntimeTestHarness')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $gameDirectory 'Mods') $name) -Destination (Join-Path $isolatedMods $name) -Recurse
}
$mods = @(Get-RuntimeExactModAllowlist -ModsRoot $isolatedMods -ExpectedUniqueIds ([ordered]@{
    'StardewAI.TransparentBridge'='StardewAI.TransparentBridge';'StardewAI.RuntimeTestHarness'='StardewAI.RuntimeTestHarness'
}))
$backendDll = Join-Path $ProjectRoot 'src/StardewAI.Backend/bin/Release/net8.0/StardewAI.Backend.dll'
$loopDll = Join-Path $ProjectRoot 'tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'
foreach ($entrypoint in @($backendDll,$loopDll)) {
    if (-not (Test-Path -LiteralPath $entrypoint -PathType Leaf)) { throw "Missing runtime entrypoint: $entrypoint" }
}
$sourceHead = [string](& git -C $ProjectRoot rev-parse HEAD)
if ($LASTEXITCODE -ne 0 -or @(& git -C $ProjectRoot status --porcelain).Count -ne 0) { throw 'Native verification requires a clean source revision.' }
$binaryHashes = [ordered]@{}
$versions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($directory in @((Split-Path -Parent $backendDll),(Split-Path -Parent $loopDll),
        (Join-Path $isolatedMods 'StardewAI.TransparentBridge'),(Join-Path $isolatedMods 'StardewAI.RuntimeTestHarness'))) {
    foreach ($dll in Get-ChildItem -LiteralPath $directory -File -Filter 'StardewAI*.dll') {
        $binaryHashes[$dll.FullName] = Get-RuntimeFileSha256 -Path $dll.FullName
        [void]$versions.Add([Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName).ProductVersion)
    }
}
if ($versions.Count -ne 1 -or @($versions)[0] -cnotmatch '^1\.0\.0\+([a-f0-9]{40})$') { throw 'Runtime components do not share one attributable source revision.' }
$runtimeHead = $Matches[1]
$sourceDiff = @(& git -C $ProjectRoot diff --name-only $runtimeHead $sourceHead -- src tools experiments `
    Directory.Build.props Directory.Build.targets Directory.Packages.props global.json NuGet.Config)
if ($LASTEXITCODE -ne 0 -or $sourceDiff.Count -ne 0) { throw 'Retained runtime bundle differs from current compiled source/configuration.' }
Write-JsonFile -Path (Join-Path $runRoot 'run-inputs.json') -Value ([ordered]@{
    schema_version='runtime_material_transfer_inputs.v1';script_head=$sourceHead;runtime_source_head=$runtimeHead
    source_clean=$true;compiled_source_diff=@();binary_sha256=$binaryHashes;isolated_mods=$mods
    source_save_sha256=$sourceHash;fixture_profile=$FixtureProfile;transfer_quantity=$TransferQuantity
    game_assembly_sha256=(Get-RuntimeFileSha256 -Path (Join-Path $gameDirectory 'Stardew Valley.dll'));formal_training_authorized=$false
})
$backendUrl = "http://127.0.0.1:$BackendPort"
$executorUrl = 'http://127.0.0.1:8767'
$snapshotUrl = 'http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1'
$savedEnvironment = Save-RuntimeProcessEnvironment -Names @(
    'ASPNETCORE_URLS','STARDEWAI_STRATEGY_LEDGER_DIR','STARDEWAI_TEST_SAVES','STARDEWAI_TEST_SLOT',
    'STARDEWAI_TEST_AUTO_LOAD','STARDEWAI_SAVE_ISOLATION_PATH','STARDEWAI_TRAINING_RUN_ID','STARDEWAI_TRAINING_MODE',
    'STARDEWAI_TRAINING_OUTPUT_DIR','STARDEWAI_SUPPRESS_LOCAL_RENDER','SDL_AUDIODRIVER','ALSOFT_DRIVERS','CLIENT_FPS'
)
$processes = [Collections.Generic.List[object]]::new()
try {
    $env:ASPNETCORE_URLS = $backendUrl
    $env:STARDEWAI_STRATEGY_LEDGER_DIR = $ledgerDirectory
    $env:STARDEWAI_TEST_SAVES = $isolatedSaves
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = 'true'
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSaves
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = '1'
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runRoot
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = '1'
    $env:CLIENT_FPS = '0'
    $env:SDL_AUDIODRIVER = 'dummy'
    $env:ALSOFT_DRIVERS = 'null'
    $processes.Add((Start-RuntimeEvidenceProcess -FilePath dotnet -ArgumentList @($backendDll) `
        -WorkingDirectory (Split-Path -Parent $backendDll) -StandardOutputPath (Join-Path $runRoot 'backend.stdout.log') `
        -StandardErrorPath (Join-Path $runRoot 'backend.stderr.log')))
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null
    $processes.Add((Start-RuntimeEvidenceProcess -FilePath $smapi -WorkingDirectory $gameDirectory `
        -ArgumentList @('--mods-path', ('"' + $isolatedMods + '"')) `
        -StandardOutputPath (Join-Path $runRoot 'game.stdout.log') -StandardErrorPath (Join-Path $runRoot 'game.stderr.log')))
    Wait-Json -Url "$executorUrl/health" -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
    $capture = Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $fixture = Invoke-JsonPost -Url "$executorUrl/api/v1/training/execute" -Body ([ordered]@{
        schema_version='training_execution_request.v1';run_id=$RunId;queue_id="$RunId.fixture";queue_item_id="$RunId.fixture.material_inventory"
        before_state_hash=$capture.Value.state_hash;option_id='debug.setup_material_transfer_target';execution_mode='training_singleplayer'
        actor='training_farmer.main';save_isolation_path=$isolatedSaves;request_nonce=[guid]::NewGuid().ToString('N')
        created_at=[DateTimeOffset]::UtcNow.ToString('O');target_tile_x=$TargetTileX;target_tile_y=$TargetTileY
        fixture_material_transfer_profile=$FixtureProfile
    })
    Write-Utf8Text -Path (Join-Path $runRoot 'setup-result.json') -Value $fixture.Raw
    if ($fixture.Value.status -cne 'applied' -or $fixture.Value.primitive_verification_status -cne 'verified') { throw 'Material transfer setup failed.' }
    if ($TargetTileX -lt 0) {
        if ($fixture.Value.observed_effect -cnotmatch 'target=(-?\d+),(-?\d+)') { throw 'Dynamic material transfer fixture did not report its target.' }
        $TargetTileX = [int]$Matches[1]
        $TargetTileY = [int]$Matches[2]
    }
    $capture = Get-FreshRuntimeSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $fixturePath = Join-Path $runRoot 'fixture-snapshot.json'
    Save-RuntimeSnapshotAndIngest -Path $fixturePath -Capture $capture -BackendUrl $backendUrl -SnapshotProfile full
    $context = New-RuntimeQueueContext -LoopDll $loopDll -ArtifactDirectory $runRoot -BackendUrl $backendUrl `
        -SnapshotUrl $snapshotUrl -SnapshotProfile full -ExecutorRoot $executorUrl -ProductUrl $executorUrl `
        -RunId $RunId -IsolatedSavesPath $isolatedSaves
    $graph = Read-StateValue $capture.Value 'farm' 'material_inventory_graph'
    $players = @($graph.inventory_nodes | Where-Object inventory_kind -CEQ 'player_inventory')
    $access = @($graph.access_points | Where-Object { $_.access_kind -ceq 'placed_chest' -and $_.location_id -ceq 'Farm' -and $_.tile_x -eq $TargetTileX -and $_.tile_y -eq $TargetTileY })
    if ($players.Count -ne 1 -or $access.Count -ne 1) { throw 'Fixture player/chest identity is not unique.' }
    $player = $players[0]
    $chest = Get-RuntimeMaterialNode $graph $access[0].node_id
    $slot = Get-RuntimeMaterialSlot $chest '(O)388' 4
    $ledgerResponse = Invoke-WebRequest -UseBasicParsing -TimeoutSec 30 `
        -Uri "$backendUrl/api/v1/strategy/commitments/latest?stateHash=$($capture.Value.state_hash)"
    $ledger = $ledgerResponse.Content | ConvertFrom-Json
    Write-Utf8Text -Path (Join-Path $runRoot 'initial-strategy-ledger.json') -Value $ledgerResponse.Content
    if ($ledger.revision -ne 0 -or $ledger.save_id -cne $capture.Value.save_id.value -or $ledger.player_id -cne $capture.Value.player_id.value) { throw 'Dedicated ledger did not start with this actor at revision zero.' }
    if ($FixtureProfile -eq 'sparse_source' -and ($slot.slot_index -ne 5 -or (Get-RuntimeMaterialSlot $chest '(O)390' 0).slot_index -ne 9)) { throw 'Sparse native fixture did not retain empty slots before measurement.' }
    # All fixture mutation ends here. Both transfers use the original candidate/compiler/native executor.
    $withdraw = Invoke-RuntimeMaterialTransferDirection -Context $context -Name 'chest-to-player' -Before $capture.Value -BeforePath $fixturePath `
        -SourceNodeId $chest.node_id -DestinationNodeId $player.node_id -SourceSlotIndex $slot.slot_index `
        -Quantity $TransferQuantity -ExpectedSourceStack $slot.stack
    $afterGraph = Read-StateValue $withdraw.After 'farm' 'material_inventory_graph'
    $returnSlot = Get-RuntimeMaterialSlot (Get-RuntimeMaterialNode $afterGraph $player.node_id) '(O)388' 4
    if ($FixtureProfile -eq 'sparse_source' -and $withdraw.Execution.material_transfer_source_slot_after -ne 0) { throw 'Native sparse source did not move from slot 5 to slot 0.' }
    $deposit = Invoke-RuntimeMaterialTransferDirection -Context $context -Name 'player-to-chest' -Before $withdraw.After -BeforePath $withdraw.AfterPath `
        -SourceNodeId $player.node_id -DestinationNodeId $chest.node_id -SourceSlotIndex $returnSlot.slot_index `
        -Quantity $TransferQuantity -ExpectedSourceStack $returnSlot.stack
    $finalGraph = Read-StateValue $deposit.After 'farm' 'material_inventory_graph'
    $finalChest = Get-RuntimeMaterialNode $finalGraph $chest.node_id
    if ((Get-RuntimeMaterialQuantity $finalChest '(O)388' 4) -ne 11 -or
        (Get-RuntimeMaterialQuantity (Get-RuntimeMaterialNode $finalGraph $player.node_id) '(O)388' 4) -ne (Get-RuntimeMaterialQuantity $player '(O)388' 4)) { throw 'Native round trip did not restore item quantities.' }
    if ($FixtureProfile -eq 'sparse_source' -and ((Get-RuntimeMaterialSlot $finalChest '(O)390' 0).slot_index -ne 1 -or (Get-RuntimeMaterialQuantity $finalChest '(O)390' 0) -ne 3)) { throw 'Unrelated sparse stock moved incorrectly.' }
    Assert-RuntimeMaterialStaleProjection -Context $context -After $deposit.After -ChestNodeId $chest.node_id `
        -PlayerNodeId $player.node_id -TargetTileX $TargetTileX -TargetTileY $TargetTileY
    if ((Get-DirectoryContentHash -Path $sourceSave) -cne $sourceHash) { throw 'Original source save changed.' }
    $summary = [ordered]@{
        schema_version='runtime_material_transfer_native_evidence.v2';status='passed';run_id=$RunId
        project_head=$sourceHead;runtime_source_head=$runtimeHead;fixture_profile=$FixtureProfile;save_slot=$SaveSlot
        high_level_option='inventory.transfer_item';native_executor='executor.transfer_material'
        debug_fixture_excluded_from_action_evidence=$true;directions_verified=2;native_menu_lifecycles_verified=2
        original_projector_and_slot_verifier_rebuilt=$true;round_trip_restored=$true;stale_projection_failure_closed=$true
        source_save_preserved=$true;source_save_sha256=$sourceHash;isolated_save_path=$isolatedSaves
        strategy_ledger_directory=$ledgerDirectory;isolated_mods=$mods;hidden=$true;silent=$true
        evidence_scope='native_transfer_slot_lineage_not_custody_relocation_or_criterion';formal_training_authorized=$false
        directions=@(@{label='chest-to-player';execution_path=$withdraw.ExecutionPath;queue_path=$withdraw.QueuePath;episode_path=$withdraw.EpisodePath},
            @{label='player-to-chest';execution_path=$deposit.ExecutionPath;queue_path=$deposit.QueuePath;episode_path=$deposit.EpisodePath})
    }
    Write-JsonFile -Path (Join-Path $runRoot 'summary.json') -Value $summary
    $summary | ConvertTo-Json -Depth 20
}
catch {
    Write-JsonFile -Path (Join-Path $runRoot 'failure.json') -Value @{status='failed';error=$_.Exception.Message;run_id=$RunId;formal_training_authorized=$false}
    throw
}
finally {
    Stop-RuntimeEvidenceProcesses -Processes $processes.ToArray()
    Restore-RuntimeProcessEnvironment -Values $savedEnvironment
}
