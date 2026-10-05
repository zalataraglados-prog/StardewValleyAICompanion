[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $ArchivedFreshSaveRoot = "",
    [string] $SaveSlot = "ProofFarm_450250338",
    [string] $RunId = ("runtime-full-shipment-sap-prefix-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputRoot = "artifacts\runtime-full-shipment-sap-prefix",
    [string] $RequirementInventory =
        "experiments\local-data\output\authoritative-requirement-inventory-v1.json",
    [string] $AcquisitionLowering =
        "experiments\local-data\output\acquisition-route-option-lowering-v1.json",
    [string] $MasterAnglerWindows =
        "experiments\local-data\output\master-angler-stage-one-window-index-v1.json",
    [string] $RouteTimingCalibration =
        "I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json",
    [string] $GoalId = "grandpa.maximum_21",
    [string] $KnowledgeDictionaryVersion =
        "game-1.6.15-20260723T093543Z-linux-v24",
    [ValidateSet(
        "sap_prefix",
        "parsnip_harvest_sample",
        "berry_bush_harvest_sample",
        "ginger_harvest_sample",
        "tea_bush_harvest_sample",
        "wild_tree_seed_drop_sample",
        "wild_tree_seed_sample",
        "spring_onion_harvest_sample",
        "location_forage_spawn_sample",
        "fruit_tree_harvest_sample",
        "farm_animal_product_sample")]
    [string] $Scenario = "sap_prefix",
    [string] $ReplayAcquisitionQueue = "",
    [switch] $DownstreamSmokeOnly,
    [int] $BackendPort = 8798,
    [int] $ProductPort = 8768,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $SkipBuild,
    [switch] $UseExistingBackend,
    [switch] $UseExistingProduct
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}
if ([string]::IsNullOrWhiteSpace($ArchivedFreshSaveRoot)) {
    $ArchivedFreshSaveRoot = Join-Path $ProjectRoot `
        "artifacts\runtime-fresh-save\runtime-fresh-save-20260928-133156\fresh-save-runtime-fresh-save-20260928-133156\ProofFarm_450250338"
}

$sampleProofOnly = $Scenario -ne "sap_prefix"
$sampleRequirementId = switch ($Scenario) {
    "parsnip_harvest_sample" { "full_shipment:item:24" }
    "berry_bush_harvest_sample" { "full_shipment:item:296" }
    "ginger_harvest_sample" { "full_shipment:item:829" }
    "tea_bush_harvest_sample" { "full_shipment:item:815" }
    "wild_tree_seed_drop_sample" { "full_shipment:item:408" }
    "wild_tree_seed_sample" { "full_shipment:item:88" }
    "spring_onion_harvest_sample" { "full_shipment:item:399" }
    "location_forage_spawn_sample" { "full_shipment:item:16" }
    "fruit_tree_harvest_sample" { "full_shipment:item:638" }
    "farm_animal_product_sample" { "full_shipment:item:184" }
    default { "full_shipment:item:92" }
}
$sampleQualifiedItemId = switch ($Scenario) {
    "parsnip_harvest_sample" { "(O)24" }
    "berry_bush_harvest_sample" { "(O)296" }
    "ginger_harvest_sample" { "(O)829" }
    "tea_bush_harvest_sample" { "(O)815" }
    "wild_tree_seed_drop_sample" { "(O)408" }
    "wild_tree_seed_sample" { "(O)88" }
    "spring_onion_harvest_sample" { "(O)399" }
    "location_forage_spawn_sample" { "(O)16" }
    "fruit_tree_harvest_sample" { "(O)638" }
    "farm_animal_product_sample" { "(O)184" }
    default { "(O)92" }
}
$sampleRankingOptionId = switch ($Scenario) {
    "parsnip_harvest_sample" { "farm.maintain_crops" }
    "berry_bush_harvest_sample" { "foraging.harvest_bushes" }
    "ginger_harvest_sample" { "foraging.harvest_ginger" }
    "tea_bush_harvest_sample" { "foraging.harvest_bushes" }
    "wild_tree_seed_drop_sample" { "foraging.harvest_tree_product" }
    "wild_tree_seed_sample" { "foraging.harvest_tree_product" }
    "spring_onion_harvest_sample" { "foraging.harvest_spring_onions" }
    "location_forage_spawn_sample" { "foraging.collect_spawned_objects" }
    "fruit_tree_harvest_sample" { "foraging.harvest_fruit_tree" }
    "farm_animal_product_sample" { "farm.collect_animal_products" }
    default { "foraging.chop_wild_tree" }
}
$cropFixture = switch ($Scenario) {
    "parsnip_harvest_sample" {
        [pscustomobject][ordered]@{
            Slug = "parsnip"
            RuleKey = ""
            SeedId = "472"
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    "spring_onion_harvest_sample" {
        [pscustomobject][ordered]@{
            Slug = "spring-onion"
            RuleKey = "spring_onion"
            SeedId = ""
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    default { $null }
}
$forageFixture = switch ($Scenario) {
    "berry_bush_harvest_sample" {
        [pscustomobject][ordered]@{
            Slug = "berry-bush"
            RuleKey = "bush"
            BushProfile = "berry_standard"
            GingerProfile = ""
            WildTreeProfile = ""
            SpawnedObjectProfile = ""
            FruitTreeProfile = ""
            LocationId = "Farm"
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    "ginger_harvest_sample" {
        [pscustomobject][ordered]@{
            Slug = "ginger"
            RuleKey = "ginger"
            BushProfile = ""
            GingerProfile = "dry_standard"
            WildTreeProfile = ""
            SpawnedObjectProfile = ""
            FruitTreeProfile = ""
            LocationId = "Farm"
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    "tea_bush_harvest_sample" {
        [pscustomobject][ordered]@{
            Slug = "tea-bush"
            RuleKey = "bush"
            BushProfile = "tea_leaf"
            GingerProfile = ""
            WildTreeProfile = ""
            SpawnedObjectProfile = ""
            FruitTreeProfile = ""
            LocationId = "Farm"
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    "wild_tree_seed_drop_sample" {
        [pscustomobject][ordered]@{
            Slug = "wild-tree-seed-drop"
            RuleKey = "wild_tree"
            BushProfile = ""
            GingerProfile = ""
            WildTreeProfile = "fall_hazelnut"
            SpawnedObjectProfile = ""
            FruitTreeProfile = ""
            LocationId = "Farm"
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    "wild_tree_seed_sample" {
        [pscustomobject][ordered]@{
            Slug = "wild-tree-seed"
            RuleKey = "wild_tree"
            BushProfile = ""
            GingerProfile = ""
            WildTreeProfile = "island_palm"
            SpawnedObjectProfile = ""
            FruitTreeProfile = ""
            LocationId = "IslandSouth"
            TargetTileX = 20
            TargetTileY = 20
        }
    }
    "location_forage_spawn_sample" {
        [pscustomobject][ordered]@{
            Slug = "location-forage-spawn"
            RuleKey = "spawned_object"
            BushProfile = ""
            GingerProfile = ""
            WildTreeProfile = ""
            SpawnedObjectProfile = "ordinary"
            FruitTreeProfile = ""
            LocationId = "Forest"
            TargetTileX = 40
            TargetTileY = 20
        }
    }
    "fruit_tree_harvest_sample" {
        [pscustomobject][ordered]@{
            Slug = "fruit-tree"
            RuleKey = "fruit_tree"
            BushProfile = ""
            GingerProfile = ""
            WildTreeProfile = ""
            SpawnedObjectProfile = ""
            FruitTreeProfile = "single_normal"
            LocationId = "Farm"
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    default { $null }
}
$animalFixture = switch ($Scenario) {
    "farm_animal_product_sample" {
        [pscustomobject][ordered]@{
            Slug = "farm-animal-product"
            RequiredToolKind = "Milk Pail"
            QualifiedItemId = "(O)184"
            ExpectedOutputQuality = 2
            ExpectedAnimalCrackerMultiplier = 1
            TargetTileX = 64
            TargetTileY = 15
        }
    }
    default { $null }
}
$acquisitionRootLocationId = if ($null -ne $forageFixture) {
    [string]$forageFixture.LocationId
}
else {
    "Farm"
}

function Resolve-InputPath {
    param([string] $Path)
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
}

function Write-Utf8Text {
    param([string] $Path, [string] $Value)
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function Write-JsonFile {
    param([string] $Path, $Value)
    Write-Utf8Text -Path $Path -Value (
        $Value | ConvertTo-Json -Depth 96)
}

function Get-DirectoryContentHash {
    param([string] $Path)
    $root = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $rows = @(Get-ChildItem -LiteralPath $root -File -Recurse |
        Sort-Object FullName | ForEach-Object {
            if (-not $_.FullName.StartsWith(
                    $root + '\',
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "File escaped hash root: $($_.FullName)"
            }
            $relative = $_.FullName.Substring($root.Length + 1)
            $hash = (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash.ToLowerInvariant()
            "$relative`t$($_.Length)`t$hash"
        })
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $rows))
        return [BitConverter]::ToString(
            $algorithm.ComputeHash($bytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Read-StateValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Wait-Json {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -TimeoutSec 15
            if ($response.StatusCode -eq 200) {
                return $response.Content | ConvertFrom-Json
            }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Get-FreshSnapshot {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $snapshotUrl `
                -TimeoutSec 45
            $snapshot = $response.Content | ConvertFrom-Json
            $location = [string](Read-StateValue `
                $snapshot "player" "location_id")
            if ($response.StatusCode -eq 200 -and
                -not [string]::IsNullOrWhiteSpace($location)) {
                return [pscustomobject]@{
                    Raw = $response.Content
                    Value = $snapshot
                }
            }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a fresh world snapshot. Last error: $lastError"
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 240)
    $json = if ($Body -is [string]) {
        $Body
    }
    else {
        $Body | ConvertTo-Json -Depth 96
    }
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri $Url `
            -ContentType "application/json; charset=utf-8" -Body $json `
            -TimeoutSec $TimeoutSeconds
    }
    catch {
        $detail = ""
        if ($null -ne $_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = [IO.StreamReader]::new($stream)
                try { $detail = $reader.ReadToEnd() }
                finally { $reader.Dispose() }
            }
        }
        throw "POST $Url failed: $($_.Exception.Message) $detail"
    }
    return [pscustomobject]@{
        Raw = $response.Content
        Value = $response.Content | ConvertFrom-Json
    }
}

function Invoke-Bootstrap {
    param([string[]] $Arguments)
    & dotnet $script:bootstrapDll @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Goal-conditioned bootstrap failed: $($Arguments[0])"
    }
}

function Save-SnapshotAndIngest {
    param([string] $Path, $Capture)
    Write-Utf8Text -Path $Path -Value $Capture.Raw
    $ingest = Invoke-JsonPost `
        -Url "$backendUrl/api/v1/snapshots?profile=full" `
        -Body $Capture.Raw
    if (-not [bool]$ingest.Value.accepted -or
        [string]$ingest.Value.state_hash -ne
            [string]$Capture.Value.state_hash) {
        throw "Backend rejected or changed snapshot identity for $Path."
    }
}

function Invoke-Ranking {
    param(
        [string] $SnapshotStateHash,
        [string] $OutputPath,
        [string] $OptionId,
        [object[]] $Parameters = @()
    )
    [object[]]$candidates = @()
    if ($Parameters.Count -gt 0) {
        $candidates = @([ordered]@{
            option_id = $OptionId
            parameters = $Parameters
            explicit_confirmation_granted = $false
            invocation_source = 0
            actor_is_host = $true
            ownership_authorized = $true
            adapter_id = "vanilla_native"
        })
    }
    [string[]]$candidateOptionIds = if ($candidates.Count -eq 0) {
        @($OptionId)
    }
    else { @() }
    $request = [ordered]@{
        goal_id = $GoalId
        execution_mode = "training_singleplayer"
        state_hash = $SnapshotStateHash
        candidate_option_ids = $candidateOptionIds
        candidates = $candidates
        include_blocked_options = $false
        training_report = [ordered]@{}
        policy_checkpoint_path = $null
        require_structured_policy = $false
    }
    $response = Invoke-JsonPost `
        -Url "$backendUrl/api/v1/planner/baseline/rank-options" `
        -Body $request
    Write-Utf8Text -Path $OutputPath -Value $response.Raw
    if ([string]$response.Value.schema_version -ne
        "availability_policy_prediction.v1") {
        throw "Unexpected ranking schema for $OptionId."
    }
    return $response.Value
}

function Invoke-DailyPlanStep {
    param(
        [string] $Name,
        [string] $OptionId,
        [string] $CandidateKind,
        [string] $CandidateId,
        [string[]] $CandidateParameters = @()
    )
    $stepRoot = Join-Path $artifactDirectory $Name
    $capture = Get-FreshSnapshot -TimeoutSeconds 60
    $sourcePath = Join-Path $stepRoot "source-snapshot.json"
    Write-Utf8Text -Path $sourcePath -Value $capture.Raw
    $arguments = [Collections.Generic.List[string]]::new()
    foreach ($value in @(
        $loopDll,
        "--root", $stepRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $executorRoot,
        "--snapshot-file", $sourcePath,
        "--no-manifest",
        "--skip-training",
        "--run-id", $RunId,
        "--save-isolation-path", $isolatedSavesPath,
        "--iterations", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", $OptionId,
        "--daily-plan-candidate-kind", $CandidateKind,
        "--daily-plan-candidate-id", $CandidateId,
        "--emit-queue-execution-receipt",
        "--after-snapshot-wait-ms", "1000",
        "--continue-after-blocked-queue-items"
    )) { $arguments.Add([string]$value) }
    foreach ($parameter in $CandidateParameters) {
        $arguments.Add("--daily-plan-candidate-parameter")
        $arguments.Add($parameter)
    }
    $stdout = & dotnet $arguments
    $stdout | Set-Content -LiteralPath (Join-Path $stepRoot "loop.stdout.log") `
        -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Daily plan step $Name failed with exit $LASTEXITCODE."
    }
    return Read-LoopArtifacts -LoopRoot $stepRoot
}

function Invoke-PrecompiledQueue {
    param([string] $Name, [string] $QueuePath, [string] $BeforePath)
    $loopRoot = Join-Path $artifactDirectory $Name
    $arguments = @(
        $loopDll,
        "--root", $loopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--snapshot-file", $BeforePath,
        "--executor-url", $productUrl,
        "--use-product-executor",
        "--no-manifest",
        "--skip-training",
        "--run-id", $RunId,
        "--save-isolation-path", $isolatedSavesPath,
        "--max-attempts", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--precompiled-queue", $QueuePath,
        "--sleep-ms", "0",
        "--after-snapshot-wait-ms", "1000"
    )
    $stdout = & dotnet $arguments
    $stdout | Set-Content `
        -LiteralPath (Join-Path $loopRoot "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Precompiled queue $Name failed with exit $LASTEXITCODE."
    }
    return Read-LoopArtifacts -LoopRoot $loopRoot
}

function Invoke-TeacherPreferenceQueue {
    param([string] $Name, [string] $PreferencePath, [string] $BeforePath)
    $loopRoot = Join-Path $artifactDirectory $Name
    $arguments = @(
        $loopDll,
        "--root", $loopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--snapshot-file", $BeforePath,
        "--executor-url", $productUrl,
        "--use-product-executor",
        "--no-manifest",
        "--skip-training",
        "--run-id", $RunId,
        "--save-isolation-path", $isolatedSavesPath,
        "--max-attempts", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--teacher-preference", $PreferencePath,
        "--sleep-ms", "0",
        "--after-snapshot-wait-ms", "1000"
    )
    $stdout = & dotnet $arguments
    $stdout | Set-Content `
        -LiteralPath (Join-Path $loopRoot "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Teacher preference queue $Name failed with exit $LASTEXITCODE."
    }
    return Read-LoopArtifacts -LoopRoot $loopRoot
}

function Read-LoopArtifacts {
    param([string] $LoopRoot)
    $root = Join-Path $LoopRoot "runs\$RunId\live-snapshots"
    $queuePath = Join-Path $root "compiled-queue-0001.json"
    $beforePath = Join-Path $root "before-snapshot-0001.json"
    $executionPath = Join-Path $root "execution-0001.json"
    $afterPath = Join-Path $root "after-snapshot-0001.json"
    foreach ($path in @($queuePath, $beforePath, $executionPath, $afterPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing loop artifact: $path"
        }
    }
    $execution = Get-Content -LiteralPath $executionPath -Raw |
        ConvertFrom-Json
    if ([string]$execution.status -ne "applied" -or
        -not [bool]$execution.after_snapshot_fresh) {
        throw "Loop execution was not applied with a fresh after snapshot."
    }
    return [pscustomobject]@{
        QueuePath = $queuePath
        BeforePath = $beforePath
        ExecutionPath = $executionPath
        AfterPath = $afterPath
        Queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
        Before = Get-Content -LiteralPath $beforePath -Raw | ConvertFrom-Json
        Execution = $execution
        After = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
    }
}

function Assert-FullShipmentState {
    param(
        $Snapshot,
        [int] $Shipped,
        [int] $Missing,
        [int] $SapInBin,
        [string] $Phase
    )
    $progress = Read-StateValue `
        $Snapshot "world_progress" "full_shipment_progress"
    $row = @($progress.items | Where-Object {
        [string]$_.qualified_item_id -eq "(O)92"
    })
    $bins = @(Read-StateValue $Snapshot "farm" "shipping_bins")
    $binCount = [int](@($bins | ForEach-Object {
        @($_.contents | Where-Object {
            [string]$_.qualified_item_id -eq "(O)92"
        } | ForEach-Object { [int]$_.count } |
            Measure-Object -Sum).Sum
    } | Measure-Object -Sum).Sum)
    if ($row.Count -ne 1 -or
        [int]$progress.eligible_item_count -ne 154 -or
        [int]$progress.shipped_eligible_item_count -ne $Shipped -or
        [int]$progress.missing_item_count -ne $Missing -or
        $binCount -ne $SapInBin) {
        throw "$Phase Full Shipment mismatch: shipped=" +
            "$($progress.shipped_eligible_item_count), missing=" +
            "$($progress.missing_item_count), sap_bin=$binCount."
    }
}

$requirementInventoryPath = Resolve-InputPath $RequirementInventory
$acquisitionLoweringPath = Resolve-InputPath $AcquisitionLowering
$masterAnglerWindowsPath = Resolve-InputPath $MasterAnglerWindows
$routeTimingPath = Resolve-InputPath $RouteTimingCalibration
$archivedSavePath = Resolve-InputPath $ArchivedFreshSaveRoot
foreach ($path in @(
    $requirementInventoryPath,
    $acquisitionLoweringPath,
    $masterAnglerWindowsPath,
    $routeTimingPath,
    $archivedSavePath
)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required input is missing: $path"
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$backendUrl = "http://127.0.0.1:$BackendPort"
$productUrl = "http://127.0.0.1:$ProductPort"
$executorRoot = "http://127.0.0.1:8767"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) {
    throw "SMAPI executable is missing: $smapi"
}
$requiredUnusedPorts = @(8765, 8767)
if (-not $UseExistingBackend) {
    $requiredUnusedPorts += $BackendPort
}
if (-not $UseExistingProduct) {
    $requiredUnusedPorts += $ProductPort
}
foreach ($port in $requiredUnusedPorts) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port `
            -ErrorAction SilentlyContinue)) {
        throw "Sap prefix smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" `
        -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path (Resolve-InputPath $OutputRoot) $RunId
$planningDirectory = Join-Path $artifactDirectory "planning"
$isolatedSavesPath = Join-Path $artifactDirectory "isolated-saves"
$isolatedSavePath = Join-Path $isolatedSavesPath $SaveSlot
$isolatedModsPath = Join-Path $artifactDirectory "smapi-mods"
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
$productJournalRoot = Join-Path $artifactDirectory "product-journal"
foreach ($path in @(
    $planningDirectory,
    $isolatedSavesPath,
    $isolatedModsPath,
    $trainingOutputDirectory,
    $productJournalRoot
)) { New-Item -ItemType Directory -Force -Path $path | Out-Null }

$sourceSaveHashBefore = Get-DirectoryContentHash -Path $archivedSavePath
Copy-Item -LiteralPath $archivedSavePath -Destination $isolatedSavePath `
    -Recurse

if ($SkipBuild) {
    & (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
        -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild |
        Out-Null
    & (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
        -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild |
        Out-Null
}
else {
    & (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
        -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
    & (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
        -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
}
foreach ($modName in @(
    "StardewAI.TransparentBridge",
    "StardewAI.RuntimeTestHarness"
)) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory "Mods\$modName") `
        -Destination (Join-Path $isolatedModsPath $modName) -Recurse
}

if (-not $SkipBuild) {
    foreach ($project in @(
        "src\StardewAI.Backend\StardewAI.Backend.csproj",
        "tools\StardewAI.ProductExecutor\StardewAI.ProductExecutor.csproj",
        "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj",
        "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
    )) {
        & dotnet build (Join-Path $ProjectRoot $project) `
            -c Release --no-restore --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Release build failed: $project" }
    }
}
$backendDll = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$productDll = Join-Path $ProjectRoot `
    "tools\StardewAI.ProductExecutor\bin\Release\net8.0\StardewAI.ProductExecutor.dll"
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$script:bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER",
    "STARDEWAI_PRODUCT_EXECUTOR_URL",
    "STARDEWAI_NATIVE_EXECUTOR_URL",
    "STARDEWAI_BRIDGE_SNAPSHOT_URL",
    "STARDEWAI_PRODUCT_JOURNAL_ROOT",
    "STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT",
    "STARDEWAI_PRODUCT_RUN_ID",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH",
    "ASPNETCORE_URLS"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

$backend = $null
$product = $null
$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $isolatedSavesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSavesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL = "1"
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $isolatedModsPath

    if (-not $UseExistingBackend) {
        $env:ASPNETCORE_URLS = $backendUrl
        $backend = Start-Process dotnet -ArgumentList @($backendDll) `
            -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $artifactDirectory `
                "backend.stdout.log") `
            -RedirectStandardError (Join-Path $artifactDirectory `
                "backend.stderr.log") -PassThru
    }
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $env:STARDEWAI_PRODUCT_EXECUTOR_URL = $productUrl
    $env:STARDEWAI_NATIVE_EXECUTOR_URL = $executorRoot
    $env:STARDEWAI_BRIDGE_SNAPSHOT_URL = $snapshotUrl
    $env:STARDEWAI_PRODUCT_JOURNAL_ROOT = $productJournalRoot
    $env:STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT = $isolatedSavesPath
    $env:STARDEWAI_PRODUCT_RUN_ID = $RunId
    if (-not $UseExistingProduct) {
        $product = Start-Process dotnet -ArgumentList @($productDll) `
            -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $artifactDirectory `
                "product.stdout.log") `
            -RedirectStandardError (Join-Path $artifactDirectory `
                "product.stderr.log") -PassThru
    }
    $productHealth = Wait-Json -Url "$productUrl/health" -TimeoutSeconds 60
    if ([string]$productHealth.status -ne "ready") {
        throw "Product executor did not become ready."
    }

    $game = Start-Process -FilePath $smapi -WorkingDirectory $gameDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "game.stderr.log") -PassThru
    Wait-Json -Url "$executorRoot/health" `
        -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
    $inside = Get-FreshSnapshot -TimeoutSeconds $StartupTimeoutSeconds
    if ([string](Read-StateValue $inside.Value "player" "location_id") -ne
        "FarmHouse") {
        throw "Archived fresh save did not start in FarmHouse."
    }

    $connectors = Read-StateValue `
        $inside.Value "locations" "route_connectors"
    $exit = @($connectors.connectors | Where-Object {
        [string]$_.target_location -eq "Farm" -and [bool]$_.resolved
    } | Select-Object -First 1)[0]
    if ($null -eq $exit) {
        throw "Fresh FarmHouse has no resolved native Farm connector."
    }
    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.setup"
        queue_item_id = "$RunId.setup.exit_house"
        before_state_hash = [string]$inside.Value.state_hash
        option_id = "executor.traverse_connector"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $isolatedSavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = [int]$exit.tile_x
        target_tile_y = [int]$exit.tile_y
        connector_kind = [string]$exit.kind
        expected_target_location = "Farm"
        expected_arrival_tile_x = [int]$exit.target_x
        expected_arrival_tile_y = [int]$exit.target_y
        max_movement_tiles = 64
    }
    $setupResult = Invoke-JsonPost `
        -Url "$executorRoot/api/v1/training/execute" -Body $setupRequest
    Write-JsonFile -Path (Join-Path $artifactDirectory `
        "setup-exit-request.json") -Value $setupRequest
    Write-Utf8Text -Path (Join-Path $artifactDirectory `
        "setup-exit-result.json") -Value $setupResult.Raw
    if ([string]$setupResult.Value.status -ne "applied" -or
        [string]$setupResult.Value.primitive_verification_status -ne
            "verified") {
        throw "Native fresh-save FarmHouse exit failed."
    }

    if ($null -ne $cropFixture) {
        $fixtureSlug = [string]$cropFixture.Slug
        $cropSetupSource = Get-FreshSnapshot -TimeoutSeconds 60
        $cropSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.fixture"
            queue_item_id = "$RunId.fixture.ready_$($fixtureSlug.Replace('-', '_'))"
            before_state_hash = [string]$cropSetupSource.Value.state_hash
            option_id = "debug.setup_harvest_crop_target"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $isolatedSavesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$cropFixture.TargetTileX
            target_tile_y = [int]$cropFixture.TargetTileY
            rule_key = [string]$cropFixture.RuleKey
            seed_id = [string]$cropFixture.SeedId
            debug_fill_inventory = $false
        }
        $cropSetupResult = Invoke-JsonPost `
            -Url "$executorRoot/api/v1/training/execute" `
            -Body $cropSetupRequest
        Write-JsonFile -Path (Join-Path $artifactDirectory `
            "fixture-ready-$fixtureSlug-request.json") -Value $cropSetupRequest
        Write-Utf8Text -Path (Join-Path $artifactDirectory `
            "fixture-ready-$fixtureSlug-result.json") -Value $cropSetupResult.Raw
        if ([string]$cropSetupResult.Value.status -ne "applied" -or
            [string]$cropSetupResult.Value.primitive_verification_status -ne
                "verified") {
            throw "Ready $fixtureSlug proof fixture setup failed."
        }
    }
    elseif ($null -ne $forageFixture) {
        $fixtureSlug = [string]$forageFixture.Slug
        $forageSetupSource = Get-FreshSnapshot -TimeoutSeconds 60
        $forageSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.fixture"
            queue_item_id = "$RunId.fixture.ready_$($fixtureSlug.Replace('-', '_'))"
            before_state_hash = [string]$forageSetupSource.Value.state_hash
            option_id = "debug.setup_forage_source_fixture"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $isolatedSavesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            location_id = [string]$forageFixture.LocationId
            target_tile_x = [int]$forageFixture.TargetTileX
            target_tile_y = [int]$forageFixture.TargetTileY
            rule_key = [string]$forageFixture.RuleKey
            fixture_bush_profile = [string]$forageFixture.BushProfile
            fixture_ginger_profile = [string]$forageFixture.GingerProfile
            fixture_fruit_tree_profile = [string]$forageFixture.FruitTreeProfile
            fixture_wild_tree_product_profile = [string]$forageFixture.WildTreeProfile
            fixture_garbage_can_profile = ""
            fixture_spawned_object_profile = [string]$forageFixture.SpawnedObjectProfile
            debug_fill_inventory = $false
        }
        $forageSetupResult = Invoke-JsonPost `
            -Url "$executorRoot/api/v1/training/execute" `
            -Body $forageSetupRequest
        Write-JsonFile -Path (Join-Path $artifactDirectory `
            "fixture-ready-$fixtureSlug-request.json") `
            -Value $forageSetupRequest
        Write-Utf8Text -Path (Join-Path $artifactDirectory `
            "fixture-ready-$fixtureSlug-result.json") `
            -Value $forageSetupResult.Raw
        if ([string]$forageSetupResult.Value.status -ne "applied" -or
            [string]$forageSetupResult.Value.primitive_verification_status -ne
                "verified") {
            throw "Ready $fixtureSlug proof fixture setup failed."
        }
    }
    elseif ($null -ne $animalFixture) {
        $fixtureSlug = [string]$animalFixture.Slug
        $animalSetupSource = Get-FreshSnapshot -TimeoutSeconds 60
        $animalSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.fixture"
            queue_item_id = "$RunId.fixture.ready_$($fixtureSlug.Replace('-', '_'))"
            before_state_hash = [string]$animalSetupSource.Value.state_hash
            option_id = "debug.setup_animal_product_target"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $isolatedSavesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$animalFixture.TargetTileX
            target_tile_y = [int]$animalFixture.TargetTileY
            required_tool_kind = [string]$animalFixture.RequiredToolKind
            qualified_item_id = [string]$animalFixture.QualifiedItemId
            expected_output_quality = [int]$animalFixture.ExpectedOutputQuality
            expected_animal_cracker_multiplier =
                [int]$animalFixture.ExpectedAnimalCrackerMultiplier
        }
        $animalSetupResult = Invoke-JsonPost `
            -Url "$executorRoot/api/v1/training/execute" `
            -Body $animalSetupRequest
        Write-JsonFile -Path (Join-Path $artifactDirectory `
            "fixture-ready-$fixtureSlug-request.json") `
            -Value $animalSetupRequest
        Write-Utf8Text -Path (Join-Path $artifactDirectory `
            "fixture-ready-$fixtureSlug-result.json") `
            -Value $animalSetupResult.Raw
        if ([string]$animalSetupResult.Value.status -ne "applied" -or
            [string]$animalSetupResult.Value.primitive_verification_status -ne
                "verified") {
            throw "Ready $fixtureSlug proof fixture setup failed."
        }
    }

    $initial = Get-FreshSnapshot -TimeoutSeconds 60
    $initialSnapshotPath = Join-Path $artifactDirectory `
        "recurrence-initial-snapshot.json"
    Save-SnapshotAndIngest -Path $initialSnapshotPath -Capture $initial
    $initialTotalDay = [int](Read-StateValue `
        $initial.Value "time" "total_days")
    if ([string](Read-StateValue $initial.Value "player" "location_id") -ne
            $acquisitionRootLocationId) {
        throw "Acquisition root is not on the expected native " +
            "$acquisitionRootLocationId map."
    }
    if (-not $sampleProofOnly -and $initialTotalDay -ne 0) {
        throw "Recurrence root is not native Spring 1 Farm state."
    }
    Assert-FullShipmentState -Snapshot $initial.Value `
        -Shipped 0 -Missing 154 -SapInBin 0 -Phase "initial"

    if ([string]::IsNullOrWhiteSpace($ReplayAcquisitionQueue)) {
    $rankingPath = Join-Path $planningDirectory "ranking-acquisition.json"
    Invoke-Ranking -SnapshotStateHash $initial.Value.state_hash `
        -OutputPath $rankingPath -OptionId $sampleRankingOptionId |
        Out-Null
    $ledgerResponse = Invoke-WebRequest -UseBasicParsing -Uri (
        "$backendUrl/api/v1/strategy/commitments/latest?stateHash=" +
        [Uri]::EscapeDataString([string]$initial.Value.state_hash)) `
        -TimeoutSec 30
    $strategyLedgerPath = Join-Path $planningDirectory `
        "strategy-ledger-before-commit.json"
    Write-Utf8Text -Path $strategyLedgerPath -Value $ledgerResponse.Content
    $strategyLedger = $ledgerResponse.Content | ConvertFrom-Json

    $calendarResolutionPath = Join-Path $planningDirectory `
        "calendar-resolution.json"
    Invoke-Bootstrap @(
        "build-current-acquisition-route-calendar-resolution",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--master-angler-windows", $masterAnglerWindowsPath,
        "--snapshot", $initialSnapshotPath,
        "--output", $calendarResolutionPath
    )
    $targetCalendarPath = Join-Path $planningDirectory `
        "target-date-calendar.json"
    Invoke-Bootstrap @(
        "build-current-acquisition-route-target-date-calendar",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--master-angler-windows", $masterAnglerWindowsPath,
        "--calendar-resolution", $calendarResolutionPath,
        "--snapshot", $initialSnapshotPath,
        "--target-total-day", ([string]$initialTotalDay),
        "--output", $targetCalendarPath
    )

    $axis = [ordered]@{}
    foreach ($row in @(
        @("unlock", "build-acquisition-route-target-date-unlock-state"),
        @("festival", "build-acquisition-route-target-date-festival-state"),
        @("location", "build-acquisition-route-target-date-location-route"),
        @("facility", "build-acquisition-route-target-date-facility-capacity"),
        @("resource", "build-acquisition-route-target-date-resource-inputs"),
        @("currency", "build-acquisition-route-target-date-currency-budget"),
        @("reservation", "build-acquisition-route-target-date-inventory-reservation"),
        @("processing", "build-acquisition-route-target-date-processing-lead-time")
    )) {
        $axis[$row[0]] = Join-Path $planningDirectory `
            ("target-date-" + $row[0] + ".json")
        $arguments = @(
            $row[1],
            "--requirement-inventory", $requirementInventoryPath,
            "--acquisition-lowering", $acquisitionLoweringPath,
            "--master-angler-windows", $masterAnglerWindowsPath,
            "--calendar-resolution", $calendarResolutionPath,
            "--target-date-calendar", $targetCalendarPath,
            "--snapshot", $initialSnapshotPath,
            "--output", $axis[$row[0]]
        )
        foreach ($prior in @(
            "unlock", "festival", "location", "facility", "resource",
            "currency", "reservation", "processing"
        )) {
            if ($axis.Contains($prior) -and $prior -ne $row[0]) {
                $arguments += @("--target-date-$prior", $axis[$prior])
            }
        }
        if ($row[0] -in @(
                "location", "facility", "resource", "currency",
                "reservation", "processing")) {
            $arguments += @("--route-timing-calibration", $routeTimingPath)
        }
        if ($row[0] -in @("reservation", "processing")) {
            $arguments += @("--strategy-ledger", $strategyLedgerPath)
        }
        Invoke-Bootstrap $arguments
    }

    $forecastPath = Join-Path $planningDirectory `
        "fishing-forecast-manifest.json"
    Write-JsonFile -Path $forecastPath -Value ([ordered]@{
        schema_version = "fishing_forecast_snapshot_manifest.v1"
        snapshots = @()
    })
    $axis["fishing-probability"] = Join-Path $planningDirectory `
        "target-date-fishing-probability.json"
    Invoke-Bootstrap @(
        "build-acquisition-route-target-date-fishing-probability",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--master-angler-windows", $masterAnglerWindowsPath,
        "--calendar-resolution", $calendarResolutionPath,
        "--target-date-calendar", $targetCalendarPath,
        "--target-date-unlock", $axis["unlock"],
        "--target-date-festival", $axis["festival"],
        "--target-date-location", $axis["location"],
        "--target-date-facility", $axis["facility"],
        "--target-date-resource", $axis["resource"],
        "--target-date-currency", $axis["currency"],
        "--target-date-reservation", $axis["reservation"],
        "--target-date-processing", $axis["processing"],
        "--strategy-ledger", $strategyLedgerPath,
        "--snapshot", $initialSnapshotPath,
        "--route-timing-calibration", $routeTimingPath,
        "--fishing-forecast-manifest", $forecastPath,
        "--output", $axis["fishing-probability"]
    )
    $axis["stochastic-retry"] = Join-Path $planningDirectory `
        "target-date-stochastic-retry.json"
    Invoke-Bootstrap @(
        "build-acquisition-route-target-date-stochastic-retry-budget",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--master-angler-windows", $masterAnglerWindowsPath,
        "--calendar-resolution", $calendarResolutionPath,
        "--target-date-calendar", $targetCalendarPath,
        "--target-date-unlock", $axis["unlock"],
        "--target-date-festival", $axis["festival"],
        "--target-date-location", $axis["location"],
        "--target-date-facility", $axis["facility"],
        "--target-date-resource", $axis["resource"],
        "--target-date-currency", $axis["currency"],
        "--target-date-reservation", $axis["reservation"],
        "--target-date-processing", $axis["processing"],
        "--target-date-fishing-probability", $axis["fishing-probability"],
        "--fishing-forecast-manifest", $forecastPath,
        "--strategy-ledger", $strategyLedgerPath,
        "--snapshot", $initialSnapshotPath,
        "--route-timing-calibration", $routeTimingPath,
        "--output", $axis["stochastic-retry"]
    )
    $axis["daily-time-energy"] = Join-Path $planningDirectory `
        "target-date-daily-time-energy.json"
    Invoke-Bootstrap @(
        "build-acquisition-route-target-date-daily-time-energy-budget",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--master-angler-windows", $masterAnglerWindowsPath,
        "--calendar-resolution", $calendarResolutionPath,
        "--target-date-calendar", $targetCalendarPath,
        "--target-date-unlock", $axis["unlock"],
        "--target-date-festival", $axis["festival"],
        "--target-date-location", $axis["location"],
        "--target-date-facility", $axis["facility"],
        "--target-date-resource", $axis["resource"],
        "--target-date-currency", $axis["currency"],
        "--target-date-reservation", $axis["reservation"],
        "--target-date-processing", $axis["processing"],
        "--target-date-fishing-probability", $axis["fishing-probability"],
        "--target-date-stochastic-retry", $axis["stochastic-retry"],
        "--fishing-forecast-manifest", $forecastPath,
        "--strategy-ledger", $strategyLedgerPath,
        "--snapshot", $initialSnapshotPath,
        "--route-timing-calibration", $routeTimingPath,
        "--output", $axis["daily-time-energy"]
    )
    $axis["opportunity-cost"] = Join-Path $planningDirectory `
        "target-date-opportunity-cost.json"
    Invoke-Bootstrap @(
        "build-acquisition-route-target-date-opportunity-cost",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--master-angler-windows", $masterAnglerWindowsPath,
        "--calendar-resolution", $calendarResolutionPath,
        "--target-date-calendar", $targetCalendarPath,
        "--target-date-unlock", $axis["unlock"],
        "--target-date-festival", $axis["festival"],
        "--target-date-location", $axis["location"],
        "--target-date-facility", $axis["facility"],
        "--target-date-resource", $axis["resource"],
        "--target-date-currency", $axis["currency"],
        "--target-date-reservation", $axis["reservation"],
        "--target-date-processing", $axis["processing"],
        "--target-date-fishing-probability", $axis["fishing-probability"],
        "--target-date-stochastic-retry", $axis["stochastic-retry"],
        "--target-date-daily-time-energy", $axis["daily-time-energy"],
        "--fishing-forecast-manifest", $forecastPath,
        "--strategy-ledger", $strategyLedgerPath,
        "--snapshot", $initialSnapshotPath,
        "--route-timing-calibration", $routeTimingPath,
        "--output", $axis["opportunity-cost"]
    )

    $preferenceRequestPath = Join-Path $planningDirectory `
        "portfolio-preference-request.json"
    Write-JsonFile -Path $preferenceRequestPath -Value ([ordered]@{
        schema_version =
            "acquisition_route_portfolio_teacher_preference_request.v1"
        request_id = "$RunId.sample"
        goal_id = $GoalId
        snapshot_state_hash = [string]$initial.Value.state_hash
        expected_ledger_revision = [int]$strategyLedger.revision
        scoped_requirements = @([ordered]@{
            requirement_set_id = "full_shipment"
            requirement_id = $sampleRequirementId
        })
    })
    $teacherPreferencePath = Join-Path $planningDirectory `
        "portfolio-teacher-preference.json"
    $proposalPath = Join-Path $planningDirectory `
        "portfolio-selected-proposal.json"
    $admissionPath = Join-Path $planningDirectory `
        "portfolio-selected-admission.json"

    $portfolioCommon = @(
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--master-angler-windows", $masterAnglerWindowsPath,
        "--calendar-resolution", $calendarResolutionPath,
        "--target-date-calendar", $targetCalendarPath,
        "--target-date-unlock", $axis["unlock"],
        "--target-date-festival", $axis["festival"],
        "--target-date-location", $axis["location"],
        "--target-date-facility", $axis["facility"],
        "--target-date-resource", $axis["resource"],
        "--target-date-currency", $axis["currency"],
        "--target-date-reservation", $axis["reservation"],
        "--target-date-processing", $axis["processing"],
        "--target-date-fishing-probability", $axis["fishing-probability"],
        "--target-date-stochastic-retry", $axis["stochastic-retry"],
        "--target-date-daily-time-energy", $axis["daily-time-energy"],
        "--target-date-opportunity-cost", $axis["opportunity-cost"],
        "--fishing-forecast-manifest", $forecastPath,
        "--strategy-ledger", $strategyLedgerPath,
        "--snapshot", $initialSnapshotPath,
        "--route-timing-calibration", $routeTimingPath
    )
    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-teacher-preference"
    ) + $portfolioCommon + @(
        "--preference-request", $preferenceRequestPath,
        "--selected-proposal-output", $proposalPath,
        "--selected-admission-output", $admissionPath,
        "--output", $teacherPreferencePath
    ))
    $proposal = Get-Content -LiteralPath $proposalPath -Raw |
        ConvertFrom-Json
    $routeIds = @($proposal.selected_route_occurrence_ids)
    if ($routeIds.Count -ne 1 -or
        -not [string]$routeIds[0].StartsWith(
            "full_shipment:${sampleRequirementId}:",
            [StringComparison]::Ordinal)) {
        throw "Teacher did not select exactly one requested authoritative route."
    }
    $routeOccurrenceId = [string]$routeIds[0]

    $admission = Get-Content -LiteralPath $admissionPath -Raw |
        ConvertFrom-Json
    $commitResultResponse = Invoke-JsonPost -Url (
        "$backendUrl/api/v1/strategy/commitments/" +
        "reservation-portfolios/commit") `
        -Body $admission.atomic_commit_request
    $commitResultPath = Join-Path $planningDirectory `
        "portfolio-commit-result.json"
    $committedLedgerPath = Join-Path $planningDirectory `
        "committed-strategy-ledger.json"
    Write-Utf8Text -Path $commitResultPath -Value $commitResultResponse.Raw
    Write-JsonFile -Path $committedLedgerPath `
        -Value $commitResultResponse.Value.ledger
    $commitReceiptPath = Join-Path $planningDirectory `
        "portfolio-commit-receipt.json"
    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-commit-receipt"
    ) + $portfolioCommon + @(
        "--proposal", $proposalPath,
        "--portfolio-admission", $admissionPath,
        "--committed-ledger", $committedLedgerPath,
        "--commit-result", $commitResultPath,
        "--output", $commitReceiptPath
    ))

    $queuePath = Join-Path $planningDirectory "action-queue.json"
    $dispatchPath = Join-Path $planningDirectory `
        "dispatch-compilation.json"
    $executionCommon = $portfolioCommon + @(
        "--portfolio-proposal", $proposalPath,
        "--portfolio-admission", $admissionPath,
        "--portfolio-preference-request", $preferenceRequestPath,
        "--portfolio-teacher-preference", $teacherPreferencePath,
        "--portfolio-commit-receipt", $commitReceiptPath,
        "--committed-strategy-ledger", $committedLedgerPath,
        "--portfolio-commit-result", $commitResultPath,
        "--route-occurrence-id", $routeOccurrenceId
    )
    Invoke-Bootstrap (@("compile-acquisition-route-dispatch") +
        $executionCommon + @(
            "--ranking", $rankingPath,
            "--queue-output", $queuePath,
            "--output", $dispatchPath
        ))
    $bindingPath = Join-Path $planningDirectory "execution-binding.json"
    Invoke-Bootstrap (@("build-acquisition-route-execution-binding") +
        $executionCommon + @(
            "--action-queue", $queuePath,
            "--output", $bindingPath
        ))
    }
    else {
        if (-not $DownstreamSmokeOnly) {
            throw "ReplayAcquisitionQueue requires DownstreamSmokeOnly."
        }
        $replayQueueSource = Resolve-InputPath $ReplayAcquisitionQueue
        if (-not (Test-Path -LiteralPath $replayQueueSource -PathType Leaf)) {
            throw "Replay acquisition queue does not exist: $replayQueueSource"
        }
        $replayQueue = Get-Content -LiteralPath $replayQueueSource -Raw |
            ConvertFrom-Json
        $replayQueue.state_hash = [string]$initial.Value.state_hash
        foreach ($item in @($replayQueue.items)) {
            $item.normalized_command.state_hash =
                [string]$initial.Value.state_hash
        }
        $queuePath = Join-Path $planningDirectory `
            "rebound-acquisition-action-queue.json"
        Write-JsonFile -Path $queuePath -Value $replayQueue
        $routeOccurrenceId = "downstream_smoke_replayed_acquisition"
    }

    $acquisition = Invoke-PrecompiledQueue `
        -Name "01-acquire-sample" -QueuePath $queuePath `
        -BeforePath $initialSnapshotPath
    $acquiredInventory = @((Read-StateValue `
        $acquisition.After "player" "inventory") | Where-Object {
            [string]$_.qualified_item_id -eq $sampleQualifiedItemId -and
            [int]$_.stack -gt 0
        })
    if ($acquiredInventory.Count -eq 0) {
        throw "Native acquisition produced no requested transparent inventory."
    }
    Assert-FullShipmentState -Snapshot $acquisition.After `
        -Shipped 0 -Missing 154 -SapInBin 0 -Phase "post-acquisition"

    if ([string]::IsNullOrWhiteSpace($ReplayAcquisitionQueue)) {
    $freshReceiptPath = Join-Path $planningDirectory `
        "fresh-terminal-receipt.json"
    Invoke-Bootstrap (@("build-acquisition-route-fresh-terminal-receipt") +
        $executionCommon + @(
            "--action-queue", $queuePath,
            "--execution-binding", $bindingPath,
            "--execution-receipt", $acquisition.ExecutionPath,
            "--after-snapshot", $acquisition.AfterPath,
            "--run-id", $RunId,
            "--executor-version", "product_executor.v1",
            "--output", $freshReceiptPath
        ))
    $afterAcquisitionRaw = Get-Content -LiteralPath $acquisition.AfterPath -Raw
    Invoke-JsonPost -Url "$backendUrl/api/v1/snapshots?profile=full" `
        -Body $afterAcquisitionRaw | Out-Null
    $settlementRequestPath = Join-Path $planningDirectory `
        "settlement-request.json"
    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-settlement-request-from-verified-artifacts"
    ) + $executionCommon + @(
        "--action-queue", $queuePath,
        "--execution-binding", $bindingPath,
        "--execution-receipt", $acquisition.ExecutionPath,
        "--after-snapshot", $acquisition.AfterPath,
        "--fresh-terminal-receipt", $freshReceiptPath,
        "--run-id", $RunId,
        "--executor-version", "product_executor.v1",
        "--output", $settlementRequestPath
    ))
    $settlementRequest = Get-Content -LiteralPath $settlementRequestPath `
        -Raw | ConvertFrom-Json
    $settlementResponse = Invoke-JsonPost -Url (
        "$backendUrl/api/v1/strategy/commitments/" +
        "reservation-portfolios/settle-completed-route") `
        -Body $settlementRequest
    $settlementResultPath = Join-Path $planningDirectory `
        "settlement-result.json"
    $settledLedgerPath = Join-Path $planningDirectory `
        "settled-strategy-ledger.json"
    Write-Utf8Text -Path $settlementResultPath -Value $settlementResponse.Raw
    Write-JsonFile -Path $settledLedgerPath `
        -Value $settlementResponse.Value.ledger
    $settlementReceiptPath = Join-Path $planningDirectory `
        "settlement-receipt.json"
    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-settlement-receipt-from-verified-artifacts"
    ) + $executionCommon + @(
        "--action-queue", $queuePath,
        "--execution-binding", $bindingPath,
        "--execution-receipt", $acquisition.ExecutionPath,
        "--after-snapshot", $acquisition.AfterPath,
        "--fresh-terminal-receipt", $freshReceiptPath,
        "--run-id", $RunId,
        "--executor-version", "product_executor.v1",
        "--settlement-request", $settlementRequestPath,
        "--settlement-result", $settlementResultPath,
        "--settled-ledger", $settledLedgerPath,
        "--output", $settlementReceiptPath
    ))
    $checkpointPath = Join-Path $planningDirectory `
        "rollout-checkpoint.json"
    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-rollout-checkpoint-from-verified-artifacts"
    ) + $executionCommon + @(
        "--action-queue", $queuePath,
        "--execution-binding", $bindingPath,
        "--execution-receipt", $acquisition.ExecutionPath,
        "--after-snapshot", $acquisition.AfterPath,
        "--fresh-terminal-receipt", $freshReceiptPath,
        "--run-id", $RunId,
        "--executor-version", "product_executor.v1",
        "--settlement-request", $settlementRequestPath,
        "--settlement-result", $settlementResultPath,
        "--settled-ledger", $settledLedgerPath,
        "--settlement-receipt", $settlementReceiptPath,
        "--output", $checkpointPath
    ))

    $executionInputs = [ordered]@{
        requirement_inventory_path = $requirementInventoryPath
        acquisition_lowering_path = $acquisitionLoweringPath
        master_angler_windows_path = $masterAnglerWindowsPath
        calendar_resolution_path = $calendarResolutionPath
        target_date_calendar_path = $targetCalendarPath
        target_date_unlock_path = $axis["unlock"]
        target_date_festival_path = $axis["festival"]
        target_date_location_path = $axis["location"]
        target_date_facility_path = $axis["facility"]
        target_date_resource_path = $axis["resource"]
        target_date_currency_path = $axis["currency"]
        target_date_reservation_path = $axis["reservation"]
        target_date_processing_path = $axis["processing"]
        target_date_fishing_probability_path = $axis["fishing-probability"]
        target_date_stochastic_retry_path = $axis["stochastic-retry"]
        target_date_daily_time_energy_path = $axis["daily-time-energy"]
        target_date_opportunity_cost_path = $axis["opportunity-cost"]
        fishing_forecast_manifest_path = $forecastPath
        strategy_ledger_path = $strategyLedgerPath
        before_snapshot_path = $initialSnapshotPath
        route_timing_calibration_path = $routeTimingPath
        portfolio_proposal_path = $proposalPath
        portfolio_admission_path = $admissionPath
        portfolio_preference_request_path = $preferenceRequestPath
        portfolio_teacher_preference_path = $teacherPreferencePath
        portfolio_commit_receipt_path = $commitReceiptPath
        committed_strategy_ledger_path = $committedLedgerPath
        portfolio_commit_result_path = $commitResultPath
        action_queue_path = $queuePath
        route_occurrence_id = $routeOccurrenceId
    }
    $rolloutManifestPath = Join-Path $planningDirectory `
        "rollout-proof-manifest.json"
    Write-JsonFile -Path $rolloutManifestPath -Value ([ordered]@{
        schema_version =
            "acquisition_route_portfolio_rollout_proof_manifest.v1"
        initial_checkpoint_proof = [ordered]@{
            execution_inputs = $executionInputs
            execution_binding_path = $bindingPath
            execution_receipt_path = $acquisition.ExecutionPath
            after_snapshot_path = $acquisition.AfterPath
            fresh_terminal_receipt_path = $freshReceiptPath
            run_id = $RunId
            executor_version = "product_executor.v1"
            settlement_request_path = $settlementRequestPath
            settlement_result_path = $settlementResultPath
            settled_ledger_path = $settledLedgerPath
            settlement_receipt_path = $settlementReceiptPath
        }
        initial_checkpoint_path = $checkpointPath
        continuation_transitions = @()
        formal_training_authorized = $false
    })
    $manifestCheck = Get-Content -LiteralPath $rolloutManifestPath -Raw |
        ConvertFrom-Json
    foreach ($name in @(
        "requirement_inventory_path",
        "acquisition_lowering_path",
        "master_angler_windows_path",
        "calendar_resolution_path",
        "target_date_calendar_path",
        "target_date_unlock_path",
        "target_date_festival_path",
        "target_date_location_path",
        "target_date_facility_path",
        "target_date_resource_path",
        "target_date_currency_path",
        "target_date_reservation_path",
        "target_date_processing_path",
        "target_date_fishing_probability_path",
        "target_date_stochastic_retry_path",
        "target_date_daily_time_energy_path",
        "target_date_opportunity_cost_path",
        "fishing_forecast_manifest_path",
        "strategy_ledger_path",
        "before_snapshot_path",
        "route_timing_calibration_path",
        "portfolio_proposal_path",
        "portfolio_admission_path",
        "portfolio_preference_request_path",
        "portfolio_teacher_preference_path",
        "portfolio_commit_receipt_path",
        "committed_strategy_ledger_path",
        "portfolio_commit_result_path",
        "action_queue_path",
        "route_occurrence_id"
    )) {
        $property = $manifestCheck.initial_checkpoint_proof.execution_inputs.
            PSObject.Properties[$name]
        if ($null -eq $property -or
            [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            throw "Rollout proof manifest execution input is missing: $name"
        }
    }
    $rolloutReceiptPath = Join-Path $planningDirectory `
        "rollout-proof-receipt.json"
    Invoke-Bootstrap @(
        "build-acquisition-route-portfolio-rollout-proof-receipt",
        "--rollout-proof-manifest", $rolloutManifestPath,
        "--output", $rolloutReceiptPath
    )

    if ($sampleProofOnly) {
        $rolloutReceipt = Get-Content -LiteralPath $rolloutReceiptPath -Raw |
            ConvertFrom-Json
        if (-not [bool]$rolloutReceipt.proof_chain_verified -or
            -not [bool]$rolloutReceipt.portfolio_completion_verified -or
            [bool]$rolloutReceipt.formal_training_authorized) {
            throw "Acquisition sample rollout proof is incomplete."
        }
        $sourceSaveHashAfter = Get-DirectoryContentHash -Path $archivedSavePath
        if ($sourceSaveHashAfter -ne $sourceSaveHashBefore) {
            throw "Archived fresh save changed during isolated acquisition sample."
        }
        $summary = [ordered]@{
            schema_version =
                "stardewai.runtime_full_shipment_acquisition_sample.v1"
            status = "passed"
            scenario = $Scenario
            run_id = $RunId
            save_slot = $SaveSlot
            archived_source_preserved = $true
            archived_source_sha256 = $sourceSaveHashBefore
            fixture_excluded_from_proof_root = $true
            initial_state_hash = [string]$initial.Value.state_hash
            requirement_id = $sampleRequirementId
            qualified_item_id = $sampleQualifiedItemId
            acquisition_route_occurrence_id = $routeOccurrenceId
            acquired_stack = [int]$acquiredInventory[0].stack
            rollout_id = [string]$rolloutReceipt.rollout_id
            rollout_proof_manifest_path = $rolloutManifestPath
            rollout_proof_receipt_path = $rolloutReceiptPath
            isolated_save_root = $isolatedSavesPath
        }
        Write-JsonFile -Path (Join-Path $artifactDirectory "summary.json") `
            -Value $summary
        $summary | ConvertTo-Json -Depth 24
        return
    }
    }
    else {
        $rolloutManifestPath = ""
        $rolloutReceiptPath = ""
    }

    $depositDirectory = Join-Path $artifactDirectory "deposit"
    New-Item -ItemType Directory -Force -Path $depositDirectory | Out-Null
    $depositBeforePath = Join-Path $depositDirectory "before-snapshot.json"
    Copy-Item -LiteralPath $acquisition.AfterPath `
        -Destination $depositBeforePath
    $depositRankingPath = Join-Path $depositDirectory "ranking.json"
    $depositParameters = @([ordered]@{
        name = "continuation.qualified_item_id"
        value = "(O)92"
    })
    Invoke-Ranking `
        -SnapshotStateHash $acquisition.After.state_hash `
        -OutputPath $depositRankingPath `
        -OptionId "economy.ship_items" `
        -Parameters $depositParameters | Out-Null
    $intentsPath = Join-Path $depositDirectory `
        "master-angler-target-date-intents.json"
    Invoke-Bootstrap @(
        "build-master-angler-target-date-intents",
        "--windows", $masterAnglerWindowsPath,
        "--snapshot", $depositBeforePath,
        "--timing-calibration", $routeTimingPath,
        "--output", $intentsPath
    )
    $depositPreferencePath = Join-Path $depositDirectory `
        "teacher-preference.json"
    Invoke-Bootstrap @(
        "build-current-stage-one-collection-teacher-preference",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--ranking", $depositRankingPath,
        "--snapshot", $depositBeforePath,
        "--master-angler-target-date-intents", $intentsPath,
        "--output", $depositPreferencePath
    )
    $depositPreference = Get-Content -LiteralPath $depositPreferencePath `
        -Raw | ConvertFrom-Json
    $selectedDepositCandidateId =
        [string]$depositPreference.selected_candidate.candidate_id
    $depositRanking = Get-Content -LiteralPath $depositRankingPath -Raw |
        ConvertFrom-Json
    $selectedDepositRanking = @(
        $depositRanking.ranked_event_candidates | Where-Object {
            [string]$_.candidate_id -eq $selectedDepositCandidateId
        })
    $selectedDepositCredits = @(
        $depositPreference.selected_candidate.requirement_credits)
    if ($selectedDepositRanking.Count -ne 1 -or
        [string]$selectedDepositRanking[0].qualified_item_id -ne "(O)92" -or
        @($selectedDepositCredits | Where-Object {
            [string]$_.requirement_set_id -eq "full_shipment" -and
            [string]$_.requirement_id -eq "full_shipment:item:92" -and
            [string]$_.qualified_item_id -eq "(O)92"
        }).Count -ne 1) {
        throw "Deposit Teacher selected a non-Sap item."
    }
    $deposit = Invoke-TeacherPreferenceQueue `
        -Name "02-deposit-sap" `
        -PreferencePath $depositPreferencePath `
        -BeforePath $depositBeforePath
    Assert-FullShipmentState -Snapshot $deposit.After `
        -Shipped 0 -Missing 154 -SapInBin 1 -Phase "post-deposit"
    $depositReceiptPath = Join-Path $depositDirectory `
        "teacher-receipt.json"
    Invoke-Bootstrap @(
        "build-current-stage-one-collection-teacher-receipt",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--ranking", $depositRankingPath,
        "--before-snapshot", $depositBeforePath,
        "--master-angler-target-date-intents", $intentsPath,
        "--preference", $depositPreferencePath,
        "--execution-receipt", $deposit.ExecutionPath,
        "--after-snapshot", $deposit.AfterPath,
        "--trajectory-id", "$RunId.deposit.sap",
        "--run-id", $RunId,
        "--knowledge-dictionary-version", $KnowledgeDictionaryVersion,
        "--executor-version", "product_executor.v1",
        "--output", $depositReceiptPath
    )

    $recoveryParameters = @("control_plane.native_save_boundary=true")
    $returnHome = Invoke-DailyPlanStep `
        -Name "03-return-home" `
        -OptionId "recovery.stabilize_day" `
        -CandidateKind "recovery_sleep_immediately" `
        -CandidateId "recovery:native_save_boundary" `
        -CandidateParameters $recoveryParameters
    if ([string](Read-StateValue `
            $returnHome.After "player" "location_id") -ne "FarmHouse") {
        throw "Native recovery did not return to FarmHouse."
    }
    $sleep = Invoke-DailyPlanStep `
        -Name "04-native-settlement" `
        -OptionId "recovery.stabilize_day" `
        -CandidateKind "recovery_sleep_immediately" `
        -CandidateId "recovery:native_save_boundary" `
        -CandidateParameters $recoveryParameters
    Assert-FullShipmentState -Snapshot $sleep.After `
        -Shipped 1 -Missing 153 -SapInBin 0 -Phase "post-settlement"
    if ([int](Read-StateValue $sleep.After "time" "total_days") -ne 1) {
        throw "Native Sap settlement did not advance exactly to total day 1."
    }

    $fullShipmentSettlementPath = Join-Path $artifactDirectory `
        "full-shipment-settlement-receipt.json"
    Invoke-Bootstrap @(
        "build-full-shipment-settlement-receipt",
        "--requirement-inventory", $requirementInventoryPath,
        "--acquisition-lowering", $acquisitionLoweringPath,
        "--queue", $sleep.QueuePath,
        "--before-snapshot", $sleep.BeforePath,
        "--execution-receipt", $sleep.ExecutionPath,
        "--after-snapshot", $sleep.AfterPath,
        "--run-id", $RunId,
        "--executor-version", "runtime_test_harness_executor.v1",
        "--selected-candidate-id", "recovery:native_save_boundary",
        "--expected-qualified-item-id", "(O)92",
        "--output", $fullShipmentSettlementPath
    )

    if ($DownstreamSmokeOnly) {
        $sourceSaveHashAfter = Get-DirectoryContentHash -Path $archivedSavePath
        if ($sourceSaveHashAfter -ne $sourceSaveHashBefore) {
            throw "Archived fresh save changed during downstream smoke."
        }
        $downstreamSummary = [ordered]@{
            schema_version =
                "stardewai.runtime_full_shipment_sap_downstream_smoke.v1"
            status = "passed"
            run_id = $RunId
            formal_training_authorized = $false
            replayed_acquisition_queue = $true
            acquired_sap_stack = [int]$acquiredInventory[0].stack
            shipped_item_count = 1
            remaining_item_count = 153
            final_total_day = 1
            settlement_receipt_path = $fullShipmentSettlementPath
            isolated_save_root = $isolatedSavesPath
        }
        Write-JsonFile -Path (Join-Path $artifactDirectory "summary.json") `
            -Value $downstreamSummary
        $downstreamSummary | ConvertTo-Json -Depth 24
        return
    }

    $recurrenceManifestPath = Join-Path $artifactDirectory `
        "full-shipment-recurrence-prefix-manifest.json"
    Write-JsonFile -Path $recurrenceManifestPath -Value ([ordered]@{
        schema_version = "full_shipment_recurrence_proof_manifest.v1"
        requirement_inventory_path = $requirementInventoryPath
        acquisition_lowering_path = $acquisitionLoweringPath
        initial_snapshot_path = $initialSnapshotPath
        iterations = @([ordered]@{
            requirement_id = "full_shipment:item:92"
            qualified_item_id = "(O)92"
            acquisition_rollout_proof_manifest_path = $rolloutManifestPath
            acquisition_rollout_proof_receipt_path = $rolloutReceiptPath
            acquisition_after_snapshot_path = $acquisition.AfterPath
            deposit = [ordered]@{
                ranking_path = $depositRankingPath
                before_snapshot_path = $depositBeforePath
                master_angler_target_date_intents_path = $intentsPath
                preference_path = $depositPreferencePath
                execution_receipt_path = $deposit.ExecutionPath
                after_snapshot_path = $deposit.AfterPath
                teacher_receipt_path = $depositReceiptPath
                trajectory_id = "$RunId.deposit.sap"
                run_id = $RunId
                knowledge_dictionary_version = $KnowledgeDictionaryVersion
                executor_version = "product_executor.v1"
            }
            settlement = [ordered]@{
                settlement_kind = "ordinary"
                recovery_transitions = @([ordered]@{
                    queue_path = $returnHome.QueuePath
                    before_snapshot_path = $returnHome.BeforePath
                    execution_receipt_path = $returnHome.ExecutionPath
                    after_snapshot_path = $returnHome.AfterPath
                    run_id = $RunId
                    executor_version =
                        "runtime_test_harness_executor.v1"
                    selected_candidate_id =
                        "recovery:native_save_boundary"
                })
                queue_path = $sleep.QueuePath
                before_snapshot_path = $sleep.BeforePath
                execution_receipt_path = $sleep.ExecutionPath
                after_snapshot_path = $sleep.AfterPath
                settlement_receipt_path = $fullShipmentSettlementPath
                run_id = $RunId
                executor_version = "runtime_test_harness_executor.v1"
                selected_candidate_id = "recovery:native_save_boundary"
            }
        })
        formal_training_authorized = $false
    })
    $prefixCheckpointPath = Join-Path $artifactDirectory `
        "full-shipment-recurrence-prefix-checkpoint.json"
    Invoke-Bootstrap @(
        "build-full-shipment-recurrence-prefix-checkpoint",
        "--manifest", $recurrenceManifestPath,
        "--output", $prefixCheckpointPath
    )
    $prefix = Get-Content -LiteralPath $prefixCheckpointPath -Raw |
        ConvertFrom-Json
    if (-not [bool]$prefix.prefix_proof_verified -or
        [int]$prefix.verified_iteration_count -ne 1 -or
        [int]$prefix.remaining_item_count -ne 153 -or
        -not [bool]$prefix.ready_for_next_iteration -or
        [bool]$prefix.complete -or
        [bool]$prefix.achievement_34_verified) {
        throw "Full Shipment Sap prefix checkpoint did not prove exact 1/154."
    }

    $sourceSaveHashAfter = Get-DirectoryContentHash -Path $archivedSavePath
    if ($sourceSaveHashAfter -ne $sourceSaveHashBefore) {
        throw "Archived fresh save changed during isolated Sap prefix smoke."
    }
    $summary = [ordered]@{
        schema_version =
            "stardewai.runtime_full_shipment_sap_prefix_smoke.v1"
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        archived_source_preserved = $true
        archived_source_sha256 = $sourceSaveHashBefore
        initial_state_hash = [string]$initial.Value.state_hash
        acquisition_route_occurrence_id = $routeOccurrenceId
        acquired_sap_stack = [int]$acquiredInventory[0].stack
        verified_iteration_count = [int]$prefix.verified_iteration_count
        remaining_item_count = [int]$prefix.remaining_item_count
        final_total_day = [int]$prefix.final_total_day
        ready_for_next_iteration =
            [bool]$prefix.ready_for_next_iteration
        complete = [bool]$prefix.complete
        achievement_34_verified =
            [bool]$prefix.achievement_34_verified
        prefix_checkpoint_path = $prefixCheckpointPath
        recurrence_manifest_path = $recurrenceManifestPath
        acquisition_rollout_receipt_path = $rolloutReceiptPath
        isolated_save_root = $isolatedSavesPath
    }
    Write-JsonFile -Path (Join-Path $artifactDirectory "summary.json") `
        -Value $summary
    $summary | ConvertTo-Json -Depth 24
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    foreach ($process in @($game, $product, $backend)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(10000) | Out-Null
        }
    }
}
