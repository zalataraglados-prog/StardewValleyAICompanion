param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-animal-product-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($null -ne $result) { return $result }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and $snapshot.state.farm.animals.status -in @("available", "derived")) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready animal snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-animal-product-smoke";
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId;
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath;
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-AnimalCase([string] $ToolName, [string] $OutputId, [int] $Quality) {
    $initial = Wait-WorldSnapshot $snapshotUrl 30
    $caseName = $ToolName.Replace(" ", "-").ToLowerInvariant()
    $setup = New-BaseRequest $initial "debug.setup_animal_product_target" ("setup-" + $caseName)
    $setup.target_tile_x = $TargetTileX; $setup.target_tile_y = $TargetTileY
    $setup.required_tool_kind = $ToolName; $setup.qualified_item_id = $OutputId
    $setup.expected_output_quality = $Quality; $setup.expected_animal_cracker_multiplier = 1
    $setupResult = Invoke-JsonPost $executorUrl $setup
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($caseName + "-setup-result.json")) -Encoding utf8
    Start-Sleep -Milliseconds 750
    $before = Wait-WorldSnapshot $snapshotUrl 30
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($caseName + "-before-snapshot.json")) -Encoding utf8
    $animal = @($before.state.farm.animals.value) | Where-Object {
        $_.location_id -eq "Farm" -and [int]$_.tile_x -eq $TargetTileX -and [int]$_.tile_y -eq $TargetTileY -and $_.harvest_tool -eq $ToolName
    } | Select-Object -First 1
    if ($null -eq $animal -or $animal.harvest_status -ne "ready") {
        $animalRows = @($before.state.farm.animals.value) | ConvertTo-Json -Depth 32 -Compress
        throw "Transparent $ToolName fixture was not ready. setup=$($setupResult.status); animals=$animalRows"
    }

    $collect = New-BaseRequest $before "executor.collect_animal_product" ("collect-" + $caseName)
    $collect.location_id = [string]$animal.location_id
    $collect.target_tile_x = [int]$animal.tile_x; $collect.target_tile_y = [int]$animal.tile_y
    $collect.stand_tile_x = [int]$before.state.player.tile_x.value; $collect.stand_tile_y = [int]$before.state.player.tile_y.value
    $collect.target_runtime_type = [string]$animal.runtime_type; $collect.target_runtime_identity = [string]$animal.animal_id
    $collect.target_name = [string]$animal.name; $collect.required_tool_kind = [string]$animal.harvest_tool
    $collect.tool_slot_index = [int]$animal.harvest_tool_slot_index; $collect.qualified_item_id = [string]$animal.harvest_output_qualified_item_id
    $collect.quantity = [int]$animal.harvest_output_quantity; $collect.expected_output_quality = [int]$animal.harvest_output_quality
    $collect.expected_output_items_json = [string]$animal.harvest_expected_output_items_json
    $collect.expected_animal_cracker_multiplier = if ($animal.has_eaten_animal_cracker) { 2 } else { 1 }
    $collect.expected_skill_id = "farming"; $collect.expected_skill_experience_delta = 5; $collect.expected_energy_delta = -4
    $collect.expected_friendship_before = [int]$animal.friendship_toward_farmer
    $collect.expected_friendship_after = [int]$animal.friendship_after_harvest
    $collect.expected_stat_increments_json = [string]$animal.harvest_stat_increments_json; $collect.max_movement_tiles = 512
    $result = Invoke-JsonPost $executorUrl $collect
    $caseSummary = [ordered]@{
        tool = $ToolName; setup_status = $setupResult.status; collect_status = $result.status;
        verification = $result.primitive_verification_status; reasons = @($result.primitive_verification_reasons);
        block_reasons = @($result.block_reasons); observed_effect = $result.observed_effect
    }
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($caseName + "-result.json")) -Encoding utf8
    return $caseSummary
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-animal-product-smoke\" + $RunId); New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$savedEnvironment = @{}; foreach ($name in @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-WorldSnapshot $snapshotUrl 120 | Out-Null
    $cases = @(); $cases += Invoke-AnimalCase "Milk Pail" "(O)184" 2; $cases += Invoke-AnimalCase "Shears" "(O)440" 1
    $passed = @($cases | Where-Object { $_.setup_status -eq "applied" -and $_.collect_status -eq "applied" -and $_.verification -eq "verified" }).Count -eq 2
    $summary = [ordered]@{ status = if ($passed) { "passed" } else { "failed" }; run_id = $RunId; save_slot = $SaveSlot; cases = $cases }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Runtime animal-product smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
