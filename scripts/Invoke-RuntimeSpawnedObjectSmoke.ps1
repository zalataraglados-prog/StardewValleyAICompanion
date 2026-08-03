param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-spawned-object-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($null -ne $value) { return $value }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            $saveReady = $snapshot.save_id.status -in @("available", "derived")
            $playerReady = $snapshot.state.player.location_id.status -in @("available", "derived")
            $objectsReady = $snapshot.state.current_location.objects.status -in @("available", "derived")
            $lastStatus = "save=$saveReady;player=$playerReady;objects=$objectsReady"
            if ($saveReady -and $playerReady -and $objectsReady) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for loaded world snapshot. Last status: $lastStatus"
}

function Wait-SpawnedObjectSnapshot([string] $Location, [int] $TargetX, [int] $TargetY, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            $actualLocation = [string]$snapshot.state.player.location_id.value
            $row = @($snapshot.state.current_location.objects.value) | Where-Object {
                [int]$_.tile_x -eq $TargetX -and [int]$_.tile_y -eq $TargetY -and $_.is_spawned_object -eq $true
            } | Select-Object -First 1
            $lastStatus = "location=$actualLocation;row=$($null -ne $row);status=$($row.spawned_object_pickup_status)"
            if ($actualLocation -eq $Location -and $null -ne $row) {
                return [ordered]@{ snapshot = $snapshot; row = $row }
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for spawned object at $Location $TargetX,$TargetY. Last status: $lastStatus"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-spawned-object-smoke"
        queue_item_id = $QueueItemId
        before_state_hash = $Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-SpawnedObjectCase([string] $Profile, [int] $Index) {
    $initial = Wait-WorldSnapshot 30
    $setup = New-BaseRequest $initial "debug.setup_forage_source_fixture" "setup-$Profile"
    $setup.location_id = "Forest"
    $setup.target_tile_x = 40 + $Index
    $setup.target_tile_y = 20
    $setup.rule_key = "spawned_object"
    $setup.fixture_spawned_object_profile = $Profile
    $setupResult = Invoke-JsonPost $executorUrl $setup
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "$Profile-setup-result.json") -Encoding utf8
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Spawned-object fixture $Profile failed: $(@($setupResult.block_reasons) -join ',')"
    }

    $location = [string]$setupResult.target_location
    $targetX = [int]$setupResult.target_tile_x
    $targetY = [int]$setupResult.target_tile_y
    $ready = Wait-SpawnedObjectSnapshot $location $targetX $targetY 30
    $snapshot = $ready.snapshot
    $row = $ready.row
    $snapshot | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "$Profile-before-snapshot.json") -Encoding utf8
    if ([string]$row.spawned_object_pickup_status -ne "ready" -or [string]$row.harvest_experience_status -ne "exact") {
        throw "Spawned-object projection $Profile was not exact and ready: $($row.spawned_object_pickup_status)/$($row.harvest_experience_status)"
    }

    $execute = New-BaseRequest $snapshot "executor.collect_spawned_object" "collect-$Profile"
    $execute.target_location = $location
    $execute.target_tile_x = $targetX
    $execute.target_tile_y = $targetY
    $execute.stand_tile_x = [int]$snapshot.state.player.tile_x.value
    $execute.stand_tile_y = [int]$snapshot.state.player.tile_y.value
    $execute.qualified_item_id = [string]$row.qualified_item_id
    $execute.quantity = [int]$row.projected_total_quantity
    $execute.expected_output_quality = [int]$row.projected_harvest_quality
    $execute.expected_foraging_experience_delta = [int]$row.foraging_experience_on_success_min
    $execute.expected_farming_experience_delta = [int]$row.farming_experience_on_success_min
    $execute.max_movement_tiles = 16
    $result = Invoke-JsonPost $executorUrl $execute
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "$Profile-result.json") -Encoding utf8
    return [ordered]@{
        profile = $Profile
        location = $location
        target_tile = "$targetX,$targetY"
        projection_status = [string]$row.spawned_object_pickup_status
        quantity = [int]$row.projected_total_quantity
        quality = [int]$row.projected_harvest_quality
        foraging_experience = [int]$row.foraging_experience_on_success_min
        farming_experience = [int]$row.farming_experience_on_success_min
        execution_status = [string]$result.status
        verification = [string]$result.primitive_verification_status
        block_reasons = @($result.block_reasons)
        observed_effect = [string]$result.observed_effect
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-spawned-object-smoke\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $sourceMod = Join-Path (Join-Path $gameDir "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS", "SMAPI_MODS_PATH"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null
    Wait-WorldSnapshot 120 | Out-Null

    $profiles = @("ordinary", "botanist", "gatherer_duplicate", "special_724519", "farm_interior")
    $cases = @()
    for ($index = 0; $index -lt $profiles.Count; $index++) {
        $cases += Invoke-SpawnedObjectCase $profiles[$index] $index
    }
    $passed = @($cases | Where-Object { $_.execution_status -eq "applied" -and $_.verification -eq "verified" }).Count -eq $profiles.Count
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        loaded_mod_allowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
        cases = $cases
    }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Runtime spawned-object smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
