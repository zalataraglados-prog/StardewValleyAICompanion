[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-statue-blessing-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-statue-blessing",
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

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) -TimeoutSec $TimeoutSeconds
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "schema=$($snapshot.schema_version);save_id=$($snapshot.save_id.status)"
        } catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full snapshot. Last status: $lastStatus"
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-statue-blessing"
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

function Close-InitialMenus($Snapshot) {
    $current = $Snapshot
    for ($pass = 0; $pass -lt 8 -and $current.state.menus.active_menu.value.is_open; $pass++) {
        $close = Invoke-JsonPost $executeUrl (New-Request $current "executor.close_menu" "$RunId.initial-close.$pass")
        if ($close.status -ne "applied") { throw "Initial menu close failed: $(@($close.block_reasons) -join ',')" }
        Start-Sleep -Seconds 1
        $current = Wait-Snapshot $snapshotUrl 30
    }
    if ($current.state.menus.active_menu.value.is_open) { throw "Initial menu did not close." }
    return $current
}

function Invoke-BlessingClaim($Snapshot) {
    $projection = $Snapshot.state.current_location.statue_blessing.value
    if ($projection.status -ne "ready") { throw "Statue blessing projection is not ready: $($projection.status)" }
    $statue = @($projection.statues | Where-Object { $_.has_available_adjacent_stand }) | Select-Object -First 1
    if ($null -eq $statue) { throw "No exact reachable Statue of Blessings." }
    $stand = @($statue.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1

    $request = New-Request $Snapshot "rewards.claim_statue_blessing" "$RunId.claim"
    $request.statue_blessing_id = [int]$projection.blessing_id
    $request.statue_blessing_buff_id = [string]$projection.buff_id
    $request.statue_blessing_effect_kind = [string]$projection.blessing.kind
    $request.statue_blessing_exact_effect = [string]$projection.blessing.exact_effect
    $request.statue_blessing_days_played = [int]$projection.days_played
    $request.statue_blessing_random_upper_bound_exclusive = [int]$projection.random_upper_bound_exclusive
    $request.location_id = [string]$projection.location_id
    $request.target_tile_x = [int]$statue.tile_x
    $request.target_tile_y = [int]$statue.tile_y
    $request.stand_tile_x = [int]$stand.tile_x
    $request.stand_tile_y = [int]$stand.tile_y
    $request.target_runtime_type = [string]$statue.target_runtime_type
    $request.qualified_item_id = [string]$projection.qualified_item_id
    $request.interaction_kind = "location_object"
    $request.expected_action_type = "StatueOfBlessings"
    $request.native_contract = [string]$projection.native_contract
    $request.max_movement_tiles = 512
    return Invoke-JsonPost $executeUrl $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}

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

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
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
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $initial = Close-InitialMenus (Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds)
    Write-Json (Join-Path $runDirectory "initial-snapshot.json") $initial

    $fixture = Invoke-JsonPost $executeUrl (New-Request $initial "debug.setup_statue_blessing" "$RunId.fixture")
    Write-Json (Join-Path $runDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied") { throw "Statue blessing fixture failed: $(@($fixture.block_reasons) -join ',')" }
    Start-Sleep -Seconds 1
    $ready = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "ready-snapshot.json") $ready
    $expectedBuff = [string]$ready.state.current_location.statue_blessing.value.buff_id
    $result = Invoke-BlessingClaim $ready
    Write-Json (Join-Path $runDirectory "claim-result.json") $result
    Start-Sleep -Seconds 1
    $after = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "after-snapshot.json") $after
    $observedBuffs = @($after.state.current_location.statue_blessing.value.active_blessing_buffs | ForEach-Object { [string]$_.buff_id })
    $passed = $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        $after.state.current_location.statue_blessing.value.has_been_blessed_today -and
        $observedBuffs.Count -eq 1 -and $observedBuffs[0] -eq $expectedBuff
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_statue_blessing_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        expected_buff = $expectedBuff
        observed_buffs = $observedBuffs
        blessing_id = [int]$ready.state.current_location.statue_blessing.value.blessing_id
        effect_kind = [string]$ready.state.current_location.statue_blessing.value.blessing.kind
        random_upper_bound_exclusive = [int]$ready.state.current_location.statue_blessing.value.random_upper_bound_exclusive
        execution_status = $result.status
        verification = $result.primitive_verification_status
        has_been_blessed_today = [bool]$after.state.current_location.statue_blessing.value.has_been_blessed_today
        loaded_mod_allowlist = $loadedModAllowlist
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 8
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
