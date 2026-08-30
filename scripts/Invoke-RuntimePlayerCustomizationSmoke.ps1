[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-player-customization-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 300,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}
function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 240) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}
function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Wait-Snapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $snapshotUrl 30
            if ($snapshot.save_id.status -in @("available", "derived")) { return $snapshot }
            $lastStatus = "save=$($snapshot.save_id.status)"
        } catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for player customization snapshot. Last status: $lastStatus"
}
function Wait-CustomizationReady([string] $Mode, [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.player.customization
        $branch = if ($Mode -eq "wizard_shrine") { $field.value.wizard_shrine } else { $field.value.desert_makeover }
        if ($field.status -in @("available", "derived") -and
            $field.value.projection_status -eq "complete_locked_base_1.6.15" -and
            $branch.service_status -eq "ready") { return $snapshot }
        Start-Sleep -Milliseconds 300
    }
    throw "Player customization fixture did not become ready for $Mode."
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-player-customization"
        queue_item_id = $ItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Invoke-Setup($Snapshot, [string] $Mode) {
    $setup = New-Request $Snapshot "debug.setup_player_customization" "$RunId.setup.$Mode"
    $setup.customization_mode = $Mode
    $result = Invoke-JsonPost $executeUrl $setup
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Player customization fixture setup failed for $Mode`: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    Start-Sleep -Seconds 1
}
function Add-CommonCustomizationFields($Request, $Snapshot, $Branch, $Target, [string] $Mode) {
    $customization = $Snapshot.state.player.customization.value
    $Request.location_id = [string]$Branch.location_id
    $Request.target_location = [string]$Branch.location_id
    $Request.target_tile_x = [int]$Target.tile_x
    $Request.target_tile_y = [int]$Target.tile_y
    $Request.stand_tile_x = if ($Mode -eq "wizard_shrine") { [int]$Snapshot.state.player.tile_x.value } else { [int]$Target.tile_x }
    $Request.stand_tile_y = if ($Mode -eq "wizard_shrine") { [int]$Snapshot.state.player.tile_y.value } else { [int]$Target.tile_y }
    $Request.customization_mode = $Mode
    $Request.customization_reason = "isolated EVD-314 native smoke"
    $Request.confirm_customization = $true
    $Request.customization_projection_fingerprint = [string]$customization.projection_fingerprint
    $Request.customization_action_raw = [string]$Target.action_raw
    $Request.customization_action_token = [string]$Target.action_token
    $Request.native_contract = [string]$customization.native_contract
    $Request.max_movement_tiles = 512
}
function New-WizardRequest($Snapshot, [string] $Suffix, [int[]] $Sliders) {
    $customization = $Snapshot.state.player.customization.value
    $branch = $customization.wizard_shrine
    $target = @($branch.action_tiles) | Select-Object -First 1
    $current = $customization.current
    $hair = @($branch.hair_style_ids | Where-Object { [int]$_ -ne [int]$current.hair_style_id }) | Select-Object -First 1
    if ($null -eq $hair) { throw "No alternate native hairstyle exists." }
    $request = New-Request $Snapshot "executor.customize_player" "$RunId.wizard.$Suffix"
    Add-CommonCustomizationFields $request $Snapshot $branch $target "wizard_shrine"
    $request.customization_name = "Evd314$Suffix"
    $request.customization_favorite_thing = "Parsnip$Suffix"
    $request.customization_gender = [string]$current.gender
    $request.customization_skin_index = ([int]$current.skin_index + 1) % 24
    $request.customization_hair_style_id = [int]$hair
    $request.customization_accessory_index = if ([int]$current.accessory_index -eq -1) { 0 } else { -1 }
    $request.customization_eye_hue = $Sliders[0]
    $request.customization_eye_saturation = $Sliders[1]
    $request.customization_eye_value = $Sliders[2]
    $request.customization_hair_hue = $Sliders[3]
    $request.customization_hair_saturation = $Sliders[4]
    $request.customization_hair_value = $Sliders[5]
    $request.customization_price_gold = [int]$branch.price_gold
    $request.customization_money_before = [int]$branch.money_before
    $request.expected_menu_type_after = "CharacterCustomization"
    $request.expected_menu_kind = "wizard"
    return $request
}
function Get-OutfitPart($Branch, [string] $Slot) {
    @($Branch.expected_parts | Where-Object { $_.slot -eq $Slot }) | Select-Object -First 1
}
function New-DesertRequest($Snapshot) {
    $customization = $Snapshot.state.player.customization.value
    $branch = $customization.desert_makeover
    $target = @($branch.touch_tiles) | Select-Object -First 1
    $request = New-Request $Snapshot "executor.customize_player" "$RunId.desert"
    Add-CommonCustomizationFields $request $Snapshot $branch $target "desert_makeover"
    $request.customization_stylist_name = [string]$branch.stylist_name
    $request.customization_passive_festival_day = [int]$branch.passive_festival_day
    $request.customization_free_inventory_slots = [int]$branch.free_inventory_slots
    $request.customization_equipped_item_count = [int]$branch.equipped_item_count
    $request.customization_expected_outfit_index = [int]$branch.expected_outfit_index
    $request.customization_uses_player_seed = [bool]$branch.uses_player_seed
    $request.customization_special_laurel_outfit = [bool]$branch.special_laurel_outfit
    foreach ($slot in @("hat", "shirt", "pants")) {
        $part = Get-OutfitPart $branch $slot
        $request["customization_expected_${slot}_qid"] = if ($null -eq $part) { "" } else { [string]$part.qualified_item_id }
        $request["customization_expected_${slot}_color"] = if ($null -eq $part) { "" } else { [string]$part.color }
    }
    $request.expected_menu_type_after = "none"
    $request.expected_menu_kind = "desert_makeover_event"
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-player-customization\" + $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
if (-not (Test-Path -LiteralPath (Join-Path $savesPath $SaveSlot) -PathType Container)) { throw "Isolated save not found: $SaveSlot" }
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$loadedModAllowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in $loadedModAllowlist) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_DEDICATED_HOST_MODE", "STARDEWAI_DEDICATED_HOST_RUN_ID", "STARDEWAI_DEDICATED_HOST_ACTOR_ID",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_DEDICATED_HOST_MODE = "1"
    $env:STARDEWAI_DEDICATED_HOST_RUN_ID = $RunId
    $env:STARDEWAI_DEDICATED_HOST_ACTOR_ID = "ai_host.main"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $initial = Wait-Snapshot $StartupTimeoutSeconds

    Invoke-Setup $initial "wizard_shrine"
    $wizardSnapshot = Wait-CustomizationReady "wizard_shrine"
    $wizardResult = Invoke-JsonPost $executeUrl (New-WizardRequest $wizardSnapshot "A" @(20, 30, 40, 50, 60, 70))
    $wizardPassed = $wizardResult.status -eq "applied" -and $wizardResult.primitive_verification_status -eq "verified"

    Invoke-Setup (Wait-Snapshot 30) "wizard_shrine"
    $boundarySnapshot = Wait-CustomizationReady "wizard_shrine"
    $boundaryResult = Invoke-JsonPost $executeUrl (New-WizardRequest $boundarySnapshot "B" @(100, 100, 100, 100, 100, 100))
    $boundaryPassed = $boundaryResult.status -eq "applied" -and $boundaryResult.primitive_verification_status -eq "verified"

    Invoke-Setup (Wait-Snapshot 30) "wizard_shrine"
    $forgedSnapshot = Wait-CustomizationReady "wizard_shrine"
    $forgedRequest = New-WizardRequest $forgedSnapshot "Forged" @(10, 20, 30, 40, 50, 60)
    $forgedRequest.customization_money_before = [int]$forgedRequest.customization_money_before + 1
    $forgedResult = Invoke-JsonPost $executeUrl $forgedRequest
    $forgedPassed = $forgedResult.status -eq "blocked" -and
        @($forgedResult.block_reasons) -contains "player_customization_wizard_endpoint_price_or_target_domain_drifted"

    Invoke-Setup (Wait-Snapshot 30) "desert_makeover"
    $desertSnapshot = Wait-CustomizationReady "desert_makeover"
    $desertResult = Invoke-JsonPost $executeUrl (New-DesertRequest $desertSnapshot)
    $desertPassed = $desertResult.status -eq "applied" -and $desertResult.primitive_verification_status -eq "verified"

    $cases = @(
        [ordered]@{ case = "wizard_all_editable_fields_native_controls"; passed = $wizardPassed; result = $wizardResult },
        [ordered]@{ case = "wizard_color_slider_upper_boundary_100"; passed = $boundaryPassed; result = $boundaryResult },
        [ordered]@{ case = "forged_wizard_money_binding_rejected"; passed = $forgedPassed; result = $forgedResult },
        [ordered]@{ case = "desert_makeover_native_touch_event_receipt"; passed = $desertPassed; result = $desertResult }
    )
    $finalSnapshot = Wait-Snapshot 30
    Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_player_customization_smoke.v1"
        evidence_id = "EVD-314"
        run_id = $RunId
        status = if ($passedCount -eq 4) { "passed" } else { "failed" }
        expected_case_count = 4
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{ run_id = $RunId; status = $summary.status; passed = "$passedCount/4"; artifact = $runDirectory } | ConvertTo-Json -Depth 4
    if ($passedCount -ne 4) { throw "Runtime player customization smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
