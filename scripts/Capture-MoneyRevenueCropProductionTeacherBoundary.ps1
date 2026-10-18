[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SourceSaveSlotPath,
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $CaptureId =
        ("money-crop-teacher-boundary-" +
            (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputRoot =
        "E:\StardewAI-TestLab\artifacts\money-crop-teacher-boundaries",
    [ValidateRange(1, 64)]
    [int] $MaximumTileCount = 8,
    [ValidateRange(1, 8)]
    [int] $MaxQueueItemCount = 8,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $SkipBuild,
    [switch] $SkipDeploy
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

if ([string]::IsNullOrWhiteSpace($CaptureId) -or
    [IO.Path]::GetFileName($CaptureId) -ne $CaptureId -or
    $CaptureId.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "Money crop Teacher capture ID must be one safe path segment."
}

$sourceSave = Assert-RuntimeSourceSaveSlot -Path $SourceSaveSlotPath
$sourceSlotName = Split-Path -Leaf $sourceSave
$sourceSaveSha256Before = Get-DirectoryContentHash -Path $sourceSave
$outputBase = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputRoot
$runDirectory = Join-Path $outputBase $CaptureId
if (Test-Path -LiteralPath $runDirectory) {
    throw "Money crop Teacher capture directory already exists: $runDirectory"
}

$runtimeGameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiPath = Join-Path $runtimeGameDirectory "StardewModdingAPI.exe"
if (-not (Test-Path -LiteralPath $smapiPath -PathType Leaf)) {
    throw "Runtime SMAPI is missing: $smapiPath"
}
Assert-RuntimePortsUnused -Ports @(8765, 8767) `
    -OperationName "Money crop Teacher boundary capture"

$isolatedSaves = Join-Path $runDirectory "isolated-saves"
$isolatedSlot = Join-Path $isolatedSaves $sourceSlotName
$isolatedMods = Join-Path $runDirectory "smapi-mods"
$evidenceDirectory = Join-Path $runDirectory "evidence"
$layoutDirectory = Join-Path $evidenceDirectory "farm-layout"
$sourceSnapshotPath = Join-Path $evidenceDirectory "source-snapshot.json"
$probePath = Join-Path $evidenceDirectory "farm-layout-probe.json"
$manifestPath = Join-Path $runDirectory "capture-manifest.json"
$gameStdout = Join-Path $runDirectory "game.stdout.log"
$gameStderr = Join-Path $runDirectory "game.stderr.log"

New-Item -ItemType Directory -Force -Path $isolatedSaves | Out-Null
New-Item -ItemType Directory -Force -Path $isolatedMods | Out-Null
New-Item -ItemType Directory -Force -Path $layoutDirectory | Out-Null
Copy-Item -LiteralPath $sourceSave -Destination $isolatedSlot -Recurse
$isolatedSaveSha256Before = Get-DirectoryContentHash -Path $isolatedSlot
if ($isolatedSaveSha256Before -ne $sourceSaveSha256Before) {
    throw "Money crop Teacher isolated save does not equal its source."
}

if (-not $SkipDeploy) {
    $deployArguments = @{
        ProjectRoot = $ProjectRoot
        RuntimeRoot = $RuntimeRoot
    }
    if ($SkipBuild) { $deployArguments.NoBuild = $true }
    & (Join-Path $ProjectRoot `
        "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
        @deployArguments | Out-Null
    & (Join-Path $ProjectRoot `
        "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
        @deployArguments | Out-Null
}
foreach ($modName in @(
    "StardewAI.TransparentBridge",
    "StardewAI.RuntimeTestHarness"
)) {
    $sourceMod = Join-Path $runtimeGameDirectory "Mods\$modName"
    if (-not (Test-Path -LiteralPath $sourceMod -PathType Container)) {
        throw "Required Money crop capture Mod is missing: $sourceMod"
    }
    Copy-Item -LiteralPath $sourceMod `
        -Destination (Join-Path $isolatedMods $modName) -Recurse
}
$runtimeModAllowlist = @(Get-RuntimeExactModAllowlist `
    -ModsRoot $isolatedMods `
    -OperationName "Money crop Teacher boundary capture" `
    -ExpectedUniqueIds ([ordered]@{
        "StardewAI.RuntimeTestHarness" = "StardewAI.RuntimeTestHarness"
        "StardewAI.TransparentBridge" = "StardewAI.TransparentBridge"
    }))
$isolatedModsSha256Before = Get-DirectoryContentHash -Path $isolatedMods

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER",
    "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH")
$savedEnvironment = Save-RuntimeProcessEnvironment -Names $environmentNames
$gameProcess = $null
$layoutPaths = @()
$layoutBindings = @()
$mapWidth = 0
$mapHeight = 0
$sourceStateHash = ""
$sourceTotalDay = -1
$sourceTime = -1

try {
    $env:STARDEWAI_TEST_SAVES = $isolatedSaves
    $env:STARDEWAI_TEST_SLOT = $sourceSlotName
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSaves
    $env:STARDEWAI_TRAINING_RUN_ID = $CaptureId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $isolatedMods

    $gameProcess = Start-RuntimeEvidenceProcess `
        -FilePath $smapiPath `
        -WorkingDirectory $runtimeGameDirectory `
        -StandardOutputPath $gameStdout `
        -StandardErrorPath $gameStderr
    Wait-Json -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds $StartupTimeoutSeconds | Out-Null

    $sourceCapture = Get-FreshRuntimeSnapshot `
        -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=true" `
        -TimeoutSeconds $StartupTimeoutSeconds
    Write-Utf8Text -Path $sourceSnapshotPath -Value $sourceCapture.Raw
    $sourceStateHash = [string]$sourceCapture.Value.state_hash
    $sourceTotalDay = [int](Read-StateValue `
        $sourceCapture.Value "time" "total_days")
    $sourceTime = [int](Read-StateValue `
        $sourceCapture.Value "time" "time")

    $probeCapture = Get-FreshRuntimeSnapshot `
        -Url ("http://127.0.0.1:8765/api/v1/snapshot?" +
            "profile=farm_layout&min_x=0&min_y=0&max_x=0&max_y=0&fresh=true") `
        -TimeoutSeconds $StartupTimeoutSeconds
    Write-Utf8Text -Path $probePath -Value $probeCapture.Raw
    $probeContext = Read-StateValue `
        $probeCapture.Value "farm" "site_preparation_context"
    $mapWidth = [int]$probeContext.map_width
    $mapHeight = [int]$probeContext.map_height
    if ($mapWidth -lt 1 -or $mapWidth -gt 512 -or
        $mapHeight -lt 1 -or $mapHeight -gt 512) {
        throw "Money crop Teacher Farm dimensions are invalid: $mapWidth x $mapHeight"
    }

    $sequence = 0
    for ($minY = 0; $minY -lt $mapHeight; $minY += 32) {
        $maxY = [Math]::Min($mapHeight - 1, $minY + 31)
        for ($minX = 0; $minX -lt $mapWidth; $minX += 32) {
            $maxX = [Math]::Min($mapWidth - 1, $minX + 31)
            $sequence++
            $url = "http://127.0.0.1:8765/api/v1/snapshot?" +
                "profile=farm_layout&min_x=$minX&min_y=$minY" +
                "&max_x=$maxX&max_y=$maxY&fresh=true"
            $capture = Get-FreshRuntimeSnapshot `
                -Url $url -TimeoutSeconds $StartupTimeoutSeconds
            $path = Join-Path $layoutDirectory (
                "layout-{0:D3}-x{1}-y{2}-x{3}-y{4}.json" -f `
                    $sequence, $minX, $minY, $maxX, $maxY)
            Write-Utf8Text -Path $path -Value $capture.Raw
            $context = Read-StateValue `
                $capture.Value "farm" "site_preparation_context"
            if ([int]$context.map_width -ne $mapWidth -or
                [int]$context.map_height -ne $mapHeight -or
                [int](Read-StateValue $capture.Value "time" "total_days") -ne
                    $sourceTotalDay -or
                [int](Read-StateValue $capture.Value "time" "time") -ne
                    $sourceTime) {
                throw "Money crop Teacher Farm layout boundary drifted during capture."
            }
            $fullPath = [IO.Path]::GetFullPath($path)
            $layoutPaths += $fullPath
            $layoutBindings += [ordered]@{
                path = $fullPath
                sha256 = Get-RuntimeFileSha256 -Path $fullPath
                min_x = $minX
                min_y = $minY
                max_x = $maxX
                max_y = $maxY
                tile_count = ($maxX - $minX + 1) *
                    ($maxY - $minY + 1)
            }
        }
    }
}
finally {
    Stop-RuntimeEvidenceProcesses -Processes @($gameProcess)
    Restore-RuntimeProcessEnvironment -Values $savedEnvironment
}

$sourceSaveSha256After = Get-DirectoryContentHash -Path $sourceSave
$isolatedSaveSha256After = Get-DirectoryContentHash -Path $isolatedSlot
$isolatedModsSha256After = Get-DirectoryContentHash -Path $isolatedMods
if ($sourceSaveSha256After -ne $sourceSaveSha256Before) {
    throw "Money crop Teacher source save changed during capture."
}
if ($isolatedSaveSha256After -ne $isolatedSaveSha256Before) {
    throw "Money crop Teacher capture unexpectedly changed its isolated save."
}
if ($isolatedModsSha256After -ne $isolatedModsSha256Before) {
    throw "Money crop Teacher capture Mods changed during capture."
}
if ($layoutPaths.Count -eq 0) {
    throw "Money crop Teacher capture produced no Farm layout snapshots."
}

$teacherArguments = @{
    SnapshotPath = $sourceSnapshotPath
    FarmLayoutSnapshotPaths = $layoutPaths
    ProjectRoot = $ProjectRoot
    DecisionId = "$CaptureId.decision"
    MaximumTileCount = $MaximumTileCount
    MaxQueueItemCount = $MaxQueueItemCount
    OutputId = "decision"
    OutputRoot = (Join-Path $runDirectory "teacher-decisions")
}
if ($SkipBuild) { $teacherArguments.SkipBuild = $true }
$teacherResult = & (Join-Path $ProjectRoot `
    "scripts\Initialize-MoneyRevenueCropProductionTeacherDecision.ps1") `
    @teacherArguments | Out-String | ConvertFrom-Json

$inputResult = & (Join-Path $ProjectRoot `
    "scripts\Initialize-MoneyRevenueCropNativeProduction.ps1") `
    -SourceSaveSlotPath $sourceSave `
    -DecisionRequestPath ([string]$teacherResult.decision_request_path) `
    -ProjectRoot $ProjectRoot `
    -InputId "input" `
    -OutputRoot (Join-Path $runDirectory "production-inputs") `
    -SkipBuild | Out-String | ConvertFrom-Json

$manifest = [ordered]@{
    schema_version = "money_crop_teacher_boundary_capture_manifest.v1"
    status = "teacher_decision_and_production_input_ready"
    capture_id = $CaptureId
    source_save_slot_path = $sourceSave
    source_save_tree_sha256 = $sourceSaveSha256Before
    isolated_save_tree_sha256 = $isolatedSaveSha256Before
    isolated_mods_root = [IO.Path]::GetFullPath($isolatedMods)
    isolated_mods_sha256 = $isolatedModsSha256Before
    runtime_mod_allowlist = $runtimeModAllowlist
    source_snapshot_path = [IO.Path]::GetFullPath($sourceSnapshotPath)
    source_snapshot_sha256 =
        Get-RuntimeFileSha256 -Path $sourceSnapshotPath
    source_state_hash = $sourceStateHash
    source_total_day = $sourceTotalDay
    source_time = $sourceTime
    farm_map_width = $mapWidth
    farm_map_height = $mapHeight
    farm_layout_snapshot_count = $layoutPaths.Count
    farm_layout_snapshots = @($layoutBindings)
    teacher_request_path = [string]$teacherResult.teacher_request_path
    teacher_request_sha256 =
        [string]$teacherResult.teacher_request_sha256
    decision_request_path = [string]$teacherResult.decision_request_path
    decision_request_sha256 =
        [string]$teacherResult.decision_request_sha256
    production_input_manifest_path = [string]$inputResult.manifest_path
    formal_product_training_authorized = $false
}
Write-JsonFile -Path $manifestPath -Value $manifest

[ordered]@{
    status = [string]$manifest.status
    capture_manifest_path = $manifestPath
    source_state_hash = $sourceStateHash
    source_total_day = $sourceTotalDay
    source_time = $sourceTime
    farm_map_width = $mapWidth
    farm_map_height = $mapHeight
    farm_layout_snapshot_count = $layoutPaths.Count
    selected_seed_id = [string]$teacherResult.selected_seed_id
    selected_tile_count = [int]$teacherResult.selected_tile_count
    production_input_manifest_path =
        [string]$inputResult.manifest_path
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
