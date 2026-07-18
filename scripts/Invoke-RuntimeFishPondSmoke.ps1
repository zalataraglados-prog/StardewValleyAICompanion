param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-fish-pond-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
        try { $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5; if ($null -ne $value) { return $value } }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-FarmSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            $lastStatus = "save=$($snapshot.save_id.status);farm=$($snapshot.state.farm.buildings.status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and $snapshot.state.farm.buildings.status -in @("available", "derived")) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for farm snapshot. Last status: $lastStatus"
}

function Wait-FishPond([string] $Branch, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-FarmSnapshot 10
        $pondBuilding = @($snapshot.state.farm.buildings.value) | Where-Object {
            $null -ne $_.fish_pond -and $_.fish_pond.status -eq "exact" -and
            (($Branch -eq "output" -and $_.fish_pond.output_status -eq "ready") -or
             ($Branch -eq "request" -and $_.fish_pond.request_status -eq "ready"))
        } | Select-Object -Last 1
        $lastStatus = "pond_count=$(@($snapshot.state.farm.buildings.value | Where-Object { $_.fish_pond.status -eq 'exact' }).Count);branch=$Branch"
        if ($null -ne $pondBuilding) { return [ordered]@{ snapshot = $snapshot; building = $pondBuilding } }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for ready fish-pond $Branch projection. Last status: $lastStatus"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-fish-pond-smoke";
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId;
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath;
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Add-CommonPondFields($Request, $Building) {
    $pond = $Building.fish_pond
    $Request.target_tile_x = [int]$pond.preferred_target_tile_x; $Request.target_tile_y = [int]$pond.preferred_target_tile_y
    $Request.stand_tile_x = [int]$pond.preferred_stand_tile_x; $Request.stand_tile_y = [int]$pond.preferred_stand_tile_y
    $Request.building_tile_x = [int]$Building.tile_x; $Request.building_tile_y = [int]$Building.tile_y
    $Request.target_runtime_type = [string]$pond.runtime_type; $Request.fish_type_item_id = [string]$pond.fish_type_item_id
    $Request.expected_fish_count = [int]$pond.fish_count
    $Request.expected_maximum_occupants_before = [int]$pond.maximum_occupants
    $Request.expected_last_unlocked_population_gate_before = [int]$pond.last_unlocked_population_gate
    $Request.expected_days_since_spawn_before = [int]$pond.days_since_spawn
    $Request.expected_skill_id = "fishing"; $Request.max_movement_tiles = 512
}

function Invoke-OutputCase {
    $initial = Wait-FarmSnapshot 30
    $setup = New-BaseRequest $initial "debug.setup_fish_pond_output" "setup-output"
    $setup.target_tile_x = 64; $setup.target_tile_y = 18; $setup.fish_type_item_id = "(O)698"; $setup.qualified_item_id = "(O)812"; $setup.quantity = 1
    $setupResult = Invoke-JsonPost $executorUrl $setup
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "output-setup-result.json") -Encoding utf8
    if ($setupResult.status -ne "applied") { throw "Fish-pond output fixture failed: $(@($setupResult.block_reasons) -join ',')" }
    $ready = Wait-FishPond "output" 30; $snapshot = $ready.snapshot; $building = $ready.building; $pond = $building.fish_pond
    $collect = New-BaseRequest $snapshot "executor.collect_fish_pond_output" "collect-output"
    Add-CommonPondFields $collect $building
    $collect.safe_slot_index = [int]$pond.output_safe_slot_index; $collect.qualified_item_id = [string]$pond.output_qualified_item_id
    $collect.quantity = [int]$pond.output_stack; $collect.expected_output_items_json = [string]$pond.output_items_json
    $collect.expected_skill_experience_delta = [int]$pond.output_fishing_experience_delta
    $collect.native_receipt_callbacks_status = [string]$pond.output_receipt_callbacks_status
    $result = Invoke-JsonPost $executorUrl $collect
    $snapshot | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "output-before-snapshot.json") -Encoding utf8
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "output-result.json") -Encoding utf8
    return [ordered]@{ branch = "output"; setup_status = $setupResult.status; execution_status = $result.status; verification = $result.primitive_verification_status; block_reasons = @($result.block_reasons); observed_effect = $result.observed_effect }
}

function Invoke-RequestCase {
    $initial = Wait-FarmSnapshot 30
    $setup = New-BaseRequest $initial "debug.setup_fish_pond_request" "setup-request"
    $setup.target_tile_x = 72; $setup.target_tile_y = 18; $setup.fish_type_item_id = "(O)698"
    $setupResult = Invoke-JsonPost $executorUrl $setup
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "request-setup-result.json") -Encoding utf8
    if ($setupResult.status -ne "applied") { throw "Fish-pond request fixture failed: $(@($setupResult.block_reasons) -join ',')" }
    $ready = Wait-FishPond "request" 30; $snapshot = $ready.snapshot; $building = $ready.building; $pond = $building.fish_pond
    $complete = New-BaseRequest $snapshot "executor.complete_fish_pond_request" "complete-request"
    Add-CommonPondFields $complete $building
    $complete.qualified_item_id = [string]$pond.request_item_qualified_item_id; $complete.quantity = [int]$pond.request_item_count_remaining
    $complete.request_item_runtime_type = [string]$pond.request_item_runtime_type
    $complete.request_item_toolbar_slots_json = [string]$pond.request_item_toolbar_slots_json
    $complete.expected_skill_experience_delta = [int]$pond.request_fishing_experience_delta
    $complete.expected_maximum_occupants_after = [int]$pond.request_expected_maximum_occupants_after
    $complete.expected_last_unlocked_population_gate_after = [int]$pond.request_expected_last_unlocked_population_gate_after
    $complete.expected_days_since_spawn_after = [int]$pond.request_expected_days_since_spawn_after
    $complete.expected_needed_item_count_after = [int]$pond.request_expected_needed_item_count_after
    $complete.expected_has_completed_request_after = if ($pond.request_expected_has_completed_request_after) { 1 } else { 0 }
    $result = Invoke-JsonPost $executorUrl $complete
    $snapshot | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "request-before-snapshot.json") -Encoding utf8
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "request-result.json") -Encoding utf8
    return [ordered]@{ branch = "request"; setup_status = $setupResult.status; execution_status = $result.status; verification = $result.primitive_verification_status; block_reasons = @($result.block_reasons); observed_effect = $result.observed_effect }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-fish-pond-smoke\" + $RunId); New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$savedEnvironment = @{}; foreach ($name in @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-FarmSnapshot 120 | Out-Null
    $cases = @(); $cases += Invoke-OutputCase; $cases += Invoke-RequestCase
    $passed = @($cases | Where-Object { $_.setup_status -eq "applied" -and $_.execution_status -eq "applied" -and $_.verification -eq "verified" }).Count -eq 2
    $summary = [ordered]@{ status = if ($passed) { "passed" } else { "failed" }; run_id = $RunId; save_slot = $SaveSlot; cases = $cases }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Runtime fish-pond smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
