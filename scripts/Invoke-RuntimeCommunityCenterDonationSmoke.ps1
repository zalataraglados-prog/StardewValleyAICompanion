param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-community-center-donation-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string[]] $CaseNames = @("ordinary", "complete_bundle", "complete_area", "complete_bulletin_area", "complete_all_areas"),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 240
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try { $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5; if ($null -ne $result) { return $result } }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.world_progress.community_center.status -in @("available", "derived")) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready Community Center snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-community-center-donation"
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Convert-ArrayJson($Values) {
    ConvertTo-Json -InputObject @($Values) -Compress
}

function Setup-CommunityCenterFixture([string] $CaseName) {
    $snapshot = Wait-WorldSnapshot 30
    $request = New-BaseRequest $snapshot "debug.setup_community_center_donation" ("setup-" + $CaseName)
    $request.community_center_fixture_case = $CaseName
    $request.inventory_slot_index = 11
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Community Center fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $bundleReason = @($result.primitive_verification_reasons) | Where-Object { $_ -like "bundle=*" } | Select-Object -First 1
    $ingredientReason = @($result.primitive_verification_reasons) | Where-Object { $_ -like "ingredient=*" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($bundleReason) -or [string]::IsNullOrWhiteSpace($ingredientReason)) {
        throw "Fixture did not report its dynamic bundle target."
    }
    Start-Sleep -Milliseconds 750
    [ordered]@{
        snapshot = Wait-WorldSnapshot 30
        bundle_id = [int]($bundleReason.Substring("bundle=".Length))
        ingredient_index = [int]($ingredientReason.Substring("ingredient=".Length))
    }
}

function Invoke-CommunityCenterCase([string] $CaseName) {
    $setup = Setup-CommunityCenterFixture $CaseName
    $before = $setup.snapshot
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $progress = $before.state.world_progress.community_center.value
    $bundle = @($progress.bundle_rows) | Where-Object { [int]$_.bundle_id -eq [int]$setup.bundle_id } | Select-Object -First 1
    $candidate = @($bundle.donation_candidates) | Where-Object {
        [int]$_.inventory_slot_index -eq 11 -and [int]$_.ingredient_index -eq [int]$setup.ingredient_index
    } | Select-Object -First 1
    if ($null -eq $bundle -or $null -eq $candidate -or $candidate.action_status -ne "ready") {
        throw "Transparent Community Center candidate was not ready for $CaseName."
    }

    $request = New-BaseRequest $before "executor.donate_community_center_item" ("donate-" + $CaseName)
    $request.location_id = "CommunityCenter"; $request.target_location = "CommunityCenter"
    $request.target_tile_x = [int]$bundle.interaction_tile_x; $request.target_tile_y = [int]$bundle.interaction_tile_y
    $request.community_center_note_tile_x = [int]$bundle.note_tile_x; $request.community_center_note_tile_y = [int]$bundle.note_tile_y
    $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
    $request.bundle_data_key = [string]$bundle.bundle_data_key; $request.bundle_id = [int]$bundle.bundle_id
    $request.bundle_area_id = [int]$bundle.area_id; $request.bundle_area_name = [string]$bundle.area_name
    $request.bundle_ingredient_index = [int]$candidate.ingredient_index; $request.inventory_slot_index = [int]$candidate.inventory_slot_index
    $request.item_id = [string]$candidate.item_id; $request.qualified_item_id = [string]$candidate.qualified_item_id
    $request.target_runtime_type = [string]$candidate.runtime_type; $request.expected_item_quality = [int]$candidate.quality
    $request.required_stack = [int]$candidate.required_stack; $request.expected_stack_before = [int]$candidate.stack_before
    $request.expected_stack_after = [int]$candidate.stack_after; $request.inventory_item_total_before = [int]$candidate.inventory_item_total_before
    $request.inventory_item_total_after = [int]$candidate.inventory_item_total_after; $request.bundle_required_slot_count = [int]$bundle.required_slot_count
    $request.expected_bundle_completed_count_before = [int]$candidate.completed_ingredient_count_before
    $request.expected_bundle_completed_count_after = [int]$candidate.completed_ingredient_count_after
    $request.expected_bundle_complete_after = [bool]$candidate.completes_bundle
    $request.expected_bundle_reward_available_after = [bool]$candidate.expected_bundle_reward_available_after
    $request.expected_complete_bundle_count_after = [int]$candidate.expected_complete_bundle_count_after
    $request.completes_area = [bool]$candidate.completes_area; $request.expected_area_complete_after = [bool]$candidate.expected_area_complete_after
    $request.area_completion_mail_id = [string]$bundle.area_completion_mail_id
    $request.expected_area_completion_mail_pending_after = [bool]$candidate.expected_area_completion_mail_pending_after
    $request.expected_bulletin_thank_you_pending_after = [bool]$candidate.expected_bulletin_thank_you_pending_after
    $request.expected_all_areas_complete_after = [bool]$candidate.expected_all_areas_complete_after
    $request.newly_appearing_note_area_ids_json = Convert-ArrayJson $candidate.newly_appearing_note_area_ids
    $request.route_state = [string]$progress.route_state
    $request.native_contract = "CommunityCenter.checkBundle_then_JunimoNoteMenu.receiveLeftClick_bundle_inventory_and_ingredient_slot_then_exitThisMenu"
    $request.max_movement_tiles = 512

    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    $after = Wait-WorldSnapshot 30
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-after-snapshot.json")) -Encoding utf8
    $afterProgress = $after.state.world_progress.community_center.value
    $afterBundle = @($afterProgress.bundle_rows) | Where-Object { [int]$_.bundle_id -eq [int]$bundle.bundle_id } | Select-Object -First 1
    $afterIngredient = @($afterBundle.ingredients) | Where-Object { [int]$_.ingredient_index -eq [int]$candidate.ingredient_index } | Select-Object -First 1
    $areaMailPending = @($afterProgress.pending_area_mail_flags) -contains [string]$bundle.area_completion_mail_id
    $allAreasComplete = @($afterProgress.areas_complete | Where-Object { -not [bool]$_ }).Count -eq 0
    $newNotesMatch = $true
    foreach ($areaId in @($candidate.newly_appearing_note_area_ids)) {
        if (-not (@($afterProgress.bundle_rows) | Where-Object { [int]$_.area_id -eq [int]$areaId -and [bool]$_.note_appears } | Select-Object -First 1)) {
            $newNotesMatch = $false
        }
    }
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified_native_junimo_note_menu_lifecycle" -and
        [bool]$afterIngredient.completed -and [int]$afterBundle.completed_ingredient_count -eq [int]$candidate.completed_ingredient_count_after -and
        [bool]$afterBundle.reward_available -eq [bool]$candidate.expected_bundle_reward_available_after -and
        [int]$afterProgress.complete_bundle_count -eq [int]$candidate.expected_complete_bundle_count_after -and
        [bool]$afterBundle.area_complete -eq [bool]$candidate.expected_area_complete_after -and
        $areaMailPending -eq [bool]$candidate.expected_area_completion_mail_pending_after -and
        $allAreasComplete -eq [bool]$candidate.expected_all_areas_complete_after -and $newNotesMatch
    [ordered]@{
        case = $CaseName; passed = $passed; status = $result.status; verification = $result.primitive_verification_status
        bundle_id = [int]$bundle.bundle_id; area_id = [int]$bundle.area_id; ingredient_index = [int]$candidate.ingredient_index
        completes_bundle = [bool]$candidate.completes_bundle; completes_area = [bool]$candidate.completes_area
        all_areas_complete_after = $allAreasComplete; newly_appearing_note_areas = @($candidate.newly_appearing_note_area_ids)
        block_reasons = @($result.block_reasons)
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." } }
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-community-center-donation\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")
$savedEnvironment = @{}; foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-WorldSnapshot 120 | Out-Null
    $selectedCaseNames = @($CaseNames)
    $caseResults = @($selectedCaseNames | ForEach-Object { Invoke-CommunityCenterCase $_ })
    $passedCount = @($caseResults | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $selectedCaseNames.Count) { "passed" } else { "failed" }; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = $selectedCaseNames.Count; passed_case_count = $passedCount; cases = $caseResults
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne $selectedCaseNames.Count) { throw "Runtime Community Center donation smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
