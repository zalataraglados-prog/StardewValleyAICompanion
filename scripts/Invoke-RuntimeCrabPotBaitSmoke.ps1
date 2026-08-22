param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-crab-pot-bait-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.crab_pot_placement.status -in @("available", "derived") -and
                $snapshot.state.current_location.objects.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for crab-pot bait snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-crab-pot-bait"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "SMAPI executable not found: $smapi" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Crab-pot bait smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-crab-pot-bait\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru

    $initial = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $placementFixtureRequest = New-Request $initial "debug.setup_crab_pot_placement_target" "$RunId.placement-fixture"
    $placementFixture = Invoke-Post $executeUrl $placementFixtureRequest
    Write-Json (Join-Path $artifactDirectory "placement-fixture-result.json") $placementFixture
    if ($placementFixture.status -ne "applied" -or $placementFixture.primitive_verification_status -ne "verified") {
        throw "Crab-pot placement fixture failed: $(@($placementFixture.block_reasons) -join ',')"
    }
    $TargetTileX = [int]$placementFixture.target_tile_x
    $TargetTileY = [int]$placementFixture.target_tile_y

    $beforePlacement = Wait-World $snapshotUrl 60
    $placement = $beforePlacement.state.player.crab_pot_placement.value
    $potInventory = @($placement.rows | Where-Object {
        [string]$_.qualified_item_id -eq "(O)710" -and [int]$_.stack -gt 0
    }) | Select-Object -First 1
    if ($null -eq $potInventory) { throw "Transparent crab-pot placement inventory projection is incomplete." }
    $placeRequest = New-Request $beforePlacement "executor.place_crab_pot" "$RunId.place"
    $placeRequest["location_id"] = [string]$beforePlacement.state.player.location_id.value
    $placeRequest["inventory_slot_index"] = [int]$potInventory.inventory_slot_index
    $placeRequest["qualified_item_id"] = "(O)710"
    $placeRequest["native_contract"] = [string]$placement.native_runtime_contract
    $placeRequest["max_movement_tiles"] = 512
    $placeResult = Invoke-Post $executeUrl $placeRequest
    Write-Json (Join-Path $artifactDirectory "placement-result.json") $placeResult
    if ($placeResult.status -ne "applied" -or $placeResult.primitive_verification_status -ne "verified") {
        throw "Crab-pot placement prerequisite failed: $(@($placeResult.block_reasons) -join ',')"
    }

    $baitIds = @("(O)685", "(O)DeluxeBait", "(O)774", "(O)908", "(O)SpecificBait")
    $caseResults = @()
    foreach ($baitId in $baitIds) {
        $fixtureSnapshot = Wait-World $snapshotUrl 60
        $fixtureRequest = New-Request $fixtureSnapshot "debug.setup_crab_pot_bait_target" "$RunId.fixture.$baitId"
        $fixtureRequest["qualified_item_id"] = $baitId
        $fixtureRequest["quantity"] = 2
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        Write-Json (Join-Path $artifactDirectory ("fixture-" + ($baitId -replace '[^A-Za-z0-9]', '_') + ".json")) $fixture
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Crab-pot bait fixture rejected ${baitId}: $(@($fixture.block_reasons) -join ',')"
        }

        $before = Wait-World $snapshotUrl 60
        $pot = @($before.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and [int]$_.tile_y -eq $TargetTileY -and
            [string]$_.type -eq "StardewValley.Objects.CrabPot"
        }) | Select-Object -First 1
        if ($null -eq $pot -or [string]$pot.crab_pot_bait_load_status -ne "ready") {
            throw "Transparent CrabPot bait projection is not ready for $baitId."
        }
        $row = @($pot.crab_pot_bait_load_inventory_rows | Where-Object {
            [string]$_.qualified_item_id -eq $baitId -and [bool]$_.native_probe_accepts
        }) | Select-Object -First 1
        if ($null -eq $row) { throw "Transparent native-accepted bait row missing for $baitId." }

        $applyRequest = New-Request $before "executor.load_crab_pot_bait" "$RunId.apply.$baitId"
        $applyRequest["location_id"] = [string]$before.state.player.location_id.value
        $applyRequest["inventory_slot_index"] = [int]$row.inventory_slot_index
        $applyRequest["expected_stack_before"] = [int]$row.stack
        $applyRequest["qualified_item_id"] = [string]$row.qualified_item_id
        $applyRequest["expected_container_bait_qualified_item_id"] = [string]$row.expected_container_bait_qualified_item_id
        $applyRequest["expected_container_bait_unit_state_sha256"] = [string]$row.expected_container_bait_unit_state_sha256
        $applyRequest["expected_container_owner_player_id_before"] = [long]$pot.crab_pot_owner_player_id_before_bait
        $applyRequest["expected_container_owner_player_id_after"] = [long]$pot.crab_pot_expected_owner_player_id_after_bait
        $applyRequest["bait_runtime_type"] = [string]$row.runtime_type
        $applyRequest["bait_quality"] = [int]$row.quality
        $applyRequest["target_runtime_type"] = [string]$pot.type
        $applyRequest["native_contract"] = [string]$pot.crab_pot_bait_load_native_contract
        $applyRequest["max_movement_tiles"] = 512
        $result = Invoke-Post $executeUrl $applyRequest
        Write-Json (Join-Path $artifactDirectory ("result-" + ($baitId -replace '[^A-Za-z0-9]', '_') + ".json")) $result

        $after = Wait-World $snapshotUrl 60
        Write-Json (Join-Path $artifactDirectory ("snapshot-after-" + ($baitId -replace '[^A-Za-z0-9]', '_') + ".json")) $after
        $afterPot = @($after.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and [int]$_.tile_y -eq $TargetTileY -and
            [string]$_.type -eq "StardewValley.Objects.CrabPot"
        }) | Select-Object -First 1
        $transparentVerified = $null -ne $afterPot -and
            [string]$afterPot.crab_pot_bait_qualified_item_id -eq [string]$row.expected_container_bait_qualified_item_id -and
            [string]$afterPot.crab_pot_bait_unit_state_sha256 -eq [string]$row.expected_container_bait_unit_state_sha256 -and
            [long]$afterPot.crab_pot_owner_id -eq [long]$pot.crab_pot_expected_owner_player_id_after_bait -and
            -not [bool]$afterPot.crab_pot_ready_for_harvest
        $casePassed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and $transparentVerified
        $caseResults += [ordered]@{
            bait_qualified_item_id = $baitId
            runtime_type = [string]$row.runtime_type
            quality = [int]$row.quality
            unit_state_sha256 = [string]$row.expected_container_bait_unit_state_sha256
            execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status
            transparent_post_state_verified = $transparentVerified
            status = if ($casePassed) { "passed" } else { "failed" }
        }
        if (-not $casePassed) { throw "Runtime crab-pot bait case failed for $baitId." }
    }

    $passed = @($caseResults | Where-Object { $_.status -ne "passed" }).Count -eq 0 -and $caseResults.Count -eq $baitIds.Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_crab_pot_bait_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        target_tile = "$TargetTileX,$TargetTileY"
        native_contract = "GameLocation.checkAction->CrabPot.performObjectDropInAction(Category=-21,probe:false,owner=current_player)->Farmer.reduceActiveItemByOne"
        case_count = $caseResults.Count
        cases = $caseResults
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Runtime crab-pot bait smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) {
        Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
    }
}
