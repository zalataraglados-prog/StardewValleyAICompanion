param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-pet-care-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $PetTileX = 64,
    [int] $PetTileY = 15,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 180
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

function Wait-WorldSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.farm.pets.status -in @("available", "derived") -and
                $snapshot.state.farm.pet_bowls.status -in @("available", "derived")) {
                return $snapshot
            }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready pet snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-pet-care-smoke";
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId;
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath;
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Setup-PetFixture([int] $Friendship, [bool] $GiftTrigger, [string] $InteractionKind) {
    $snapshot = Wait-WorldSnapshot 30
    $request = New-BaseRequest $snapshot "debug.setup_pet_care_target" ("setup-" + $InteractionKind + "-" + $Friendship)
    $request.target_tile_x = $PetTileX; $request.target_tile_y = $PetTileY
    $request.expected_friendship_before = $Friendship
    $request.pet_gift_trigger_expected = $GiftTrigger
    $request.interaction_kind = $InteractionKind
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Pet fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    Start-Sleep -Milliseconds 750
    return Wait-WorldSnapshot 30
}

function Invoke-PetInteractionCase([string] $CaseName, [int] $Friendship, [bool] $GiftTrigger) {
    $before = Setup-PetFixture $Friendship $GiftTrigger "pet_interact"
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $pet = @($before.state.farm.pets.value) | Where-Object { $_.name -eq "EvdPet" } | Select-Object -First 1
    if ($null -eq $pet -or $pet.action_status -ne "ready") {
        throw "Transparent pet fixture was not ready for $CaseName."
    }

    $request = New-BaseRequest $before "executor.pet_interact" ("interact-" + $CaseName)
    $request.location_id = [string]$pet.location_id; $request.target_location = [string]$pet.location_id
    $request.target_tile_x = [int]$pet.tile_x; $request.target_tile_y = [int]$pet.tile_y
    $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
    $request.target_runtime_type = [string]$pet.runtime_type; $request.target_runtime_identity = [string]$pet.pet_id
    $request.target_name = [string]$pet.name; $request.safe_slot_index = [int]$pet.safe_slot_index
    $request.expected_friendship_before = [int]$pet.friendship_toward_farmer
    $request.expected_friendship_after = [int]$pet.friendship_after_daily_interaction
    $request.expected_last_pet_day_before_missing = ($null -eq $pet.last_pet_day_for_player)
    if ($null -ne $pet.last_pet_day_for_player) { $request.expected_last_pet_day_before = [int]$pet.last_pet_day_for_player }
    $request.expected_last_pet_day_after = [int]$pet.current_total_days
    $request.expected_times_pet_before = [int]$pet.times_pet_before; $request.expected_times_pet_after = [int]$pet.times_pet_after_daily_interaction
    $request.expected_granted_friendship_before = [bool]$pet.granted_friendship_for_pet
    $request.expected_granted_friendship_after = [bool]$pet.granted_friendship_after_daily_interaction
    $request.expected_pet_love_mail_before = [bool]$pet.pet_love_mail_before
    $request.expected_pet_love_mail_after = [bool]$pet.pet_love_mail_after_daily_interaction
    $request.expected_marnie_pet_adoption_mail_before_or_pending = [bool]$pet.marnie_pet_adoption_mail_before_or_pending
    $request.expected_marnie_pet_adoption_mail_after_or_pending = [bool]$pet.marnie_pet_adoption_mail_after_daily_interaction
    $request.pet_gift_trigger_expected = [bool]$pet.gift_trigger_will_succeed
    $request.pet_gift_selection_status = [string]$pet.gift_selection_status
    $request.max_movement_tiles = 512
    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    [ordered]@{
        case = $CaseName; status = $result.status; verification = $result.primitive_verification_status
        friendship_before = $result.pet_friendship_before; friendship_after = $result.pet_friendship_after
        times_pet_before = $result.pet_times_pet_before; times_pet_after = $result.pet_times_pet_after
        gift_trigger_expected = $result.pet_gift_trigger_expected
        gift_debris_before = $result.pet_gift_debris_count_before; gift_debris_after = $result.pet_gift_debris_count_after
        reasons = @($result.primitive_verification_reasons); block_reasons = @($result.block_reasons)
    }
}

function Invoke-PetBowlSettlementCase {
    $before = Setup-PetFixture 994 $false "pet_bowl"
    $sourceDay = [int]$before.state.time.total_days.value
    $bowl = @($before.state.farm.pet_bowls.value) | Where-Object { $_.action_status -eq "ready" -and $_.assigned_pet_name -eq "EvdPet" } | Select-Object -First 1
    if ($null -eq $bowl) { throw "Transparent pet bowl fixture was not ready." }
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "pet-bowl-before-snapshot.json") -Encoding utf8

    $request = New-BaseRequest $before "executor.fill_pet_bowl" "fill-pet-bowl"
    $request.location_id = [string]$bowl.location_id; $request.target_location = [string]$bowl.location_id
    $request.target_tile_x = [int]$bowl.action_tile_x; $request.target_tile_y = [int]$bowl.action_tile_y
    $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
    $request.target_runtime_type = [string]$bowl.runtime_type; $request.target_runtime_identity = [string]$bowl.assigned_pet_id
    $request.tool_slot_index = [int]$bowl.watering_can_slot_index; $request.watering_can_slot_index = [int]$bowl.watering_can_slot_index
    $request.required_tool_kind = "Watering Can"
    $request.expected_water_before = [int]$bowl.watering_can_water_left; $request.expected_water_after = [int]$bowl.expected_watering_can_water_after
    $request.expected_watering_can_bottomless = [bool]$bowl.watering_can_bottomless
    $request.expected_bowl_watered_before = [bool]$bowl.watered; $request.expected_bowl_watered_after = $true
    $request.expected_friendship_before = [int]$bowl.friendship_before_next_day
    $request.expected_next_day_friendship_after = [int]$bowl.friendship_after_fill_and_next_day_update
    $request.expected_pet_love_mail_before = [bool]$bowl.pet_love_mail_before
    $request.expected_next_day_pet_love_mail = [bool]$bowl.pet_love_mail_after_fill_and_next_day_update
    $request.expected_marnie_pet_adoption_mail_before_or_pending = [bool]$bowl.marnie_pet_adoption_mail_before_or_pending
    $request.expected_next_day_marnie_pet_adoption_mail = [bool]$bowl.marnie_pet_adoption_mail_after_fill_and_next_day_update
    $request.delayed_settlement = [string]$bowl.delayed_settlement; $request.max_movement_tiles = 512
    $fillResult = Invoke-JsonPost $executorUrl $request
    $fillResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "pet-bowl-fill-result.json") -Encoding utf8
    if ($fillResult.status -ne "applied" -or $fillResult.pet_next_day_settlement_status -ne "pending_Pet.dayUpdate" -or
        [string]::IsNullOrWhiteSpace([string]$fillResult.pet_bowl_pending_receipt_path)) {
        throw "Pet bowl immediate result did not create a pending settlement receipt."
    }

    $prepareSnapshot = Wait-WorldSnapshot 30
    $prepare = New-BaseRequest $prepareSnapshot "debug.prepare_pet_bowl_sleep" "prepare-pet-bowl-sleep"
    $prepareResult = Invoke-JsonPost $executorUrl $prepare
    if ($prepareResult.status -ne "applied") { throw "Could not prepare native sleep for pet bowl settlement." }
    $sleepSnapshot = Wait-WorldSnapshot 30
    $sleep = New-BaseRequest $sleepSnapshot "executor.sleep" "sleep-for-pet-bowl-settlement"
    $sleepResult = Invoke-JsonPost $executorUrl $sleep
    $sleepResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "pet-bowl-sleep-result.json") -Encoding utf8
    if ($sleepResult.status -ne "applied" -or $sleepResult.primitive_verification_status -ne "verified") {
        throw "Native sleep did not complete for pet bowl settlement."
    }

    $receiptPath = [string]$fillResult.pet_bowl_pending_receipt_path
    $deadline = (Get-Date).AddSeconds(30); $receipt = $null
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $receiptPath) {
            $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
            if ($receipt.status -ne "pending") { break }
        }
        Start-Sleep -Milliseconds 500
    }
    if ($null -eq $receipt -or $receipt.status -ne "completed") {
        throw "Pet bowl delayed receipt did not settle exactly: $receiptPath"
    }
    $after = Wait-WorldSnapshot 30
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "pet-bowl-after-snapshot.json") -Encoding utf8
    [ordered]@{
        case = "pet-bowl-native-next-day"; fill_status = $fillResult.status; fill_verification = $fillResult.primitive_verification_status
        sleep_status = $sleepResult.status; receipt_status = $receipt.status; settlement_reason = $receipt.settlement_reason
        source_total_days = $sourceDay; settled_total_days = $receipt.settled_total_days
        friendship_before = $receipt.friendship_before; expected_friendship_after = $receipt.expected_friendship_after
        settled_friendship = $receipt.settled_friendship; settled_bowl_watered = $receipt.settled_bowl_watered
        settled_pet_love_mail = $receipt.settled_pet_love_mail
        settled_marnie_adoption_mail_or_pending = $receipt.settled_marnie_adoption_mail_or_pending
        receipt_path = $receiptPath
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach to an existing runtime." }
}
$existingSmapi = Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue
if ($null -ne $existingSmapi) { throw "StardewModdingAPI process already running (PID $($existingSmapi.Id)). Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-pet-care-smoke\" + $RunId)
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

    $normal = Invoke-PetInteractionCase "pet-interaction-normal" 500 $false
    $maximumGift = Invoke-PetInteractionCase "pet-interaction-max-gift" 1000 $true
    $bowl = Invoke-PetBowlSettlementCase
    $passed = $normal.status -eq "applied" -and $normal.verification -eq "verified" -and
        $normal.friendship_after -eq 512 -and $maximumGift.status -eq "applied" -and
        $maximumGift.verification -eq "verified" -and $maximumGift.friendship_after -eq 1000 -and
        $maximumGift.times_pet_after -eq ($maximumGift.times_pet_before + 1) -and $maximumGift.gift_trigger_expected -eq $true -and
        $bowl.receipt_status -eq "completed" -and $bowl.settled_friendship -eq 1000 -and $bowl.settled_bowl_watered -eq $false
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }; evidence_id = "EVD-223"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = 3; passed_case_count = if ($passed) { 3 } else { 0 }; cases = @($normal, $maximumGift, $bowl)
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if (-not $passed) { throw "Runtime pet-care smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
