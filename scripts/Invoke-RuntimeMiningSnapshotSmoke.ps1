param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [int] $MineLevel = 99,
    [int] $MinimumBreakableStoneCount = 1,
    [int] $SampleCount = 5,
    [int] $MaximumSnapshotMilliseconds = 3000,
    [int] $StartupTimeoutSeconds = 120,
    [string] $RunId = ("runtime-mining-snapshot-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-mining-snapshot-smoke",
    [switch] $MineOneStone,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") { return $response }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $lastStatus = "save_id=$($snapshot.save_id.status)"
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a loaded isolated save. Last status: $lastStatus"
}

function Wait-MiningSnapshot {
    param([string] $Url, [int] $ExpectedMineLevel, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $mining = $snapshot.state.mining
            $level = if ($null -ne $mining) { [int]$mining.current_mine.value.mine_level } else { -1 }
            $lastStatus = "mining=$($mining.completeness.value.status);level=$level"
            if ($null -ne $mining -and $mining.completeness.value.status -eq "complete" -and $level -eq $ExpectedMineLevel) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for complete mining snapshot. Last status: $lastStatus"
}

function Assert-MiningSnapshot {
    param($Snapshot, [int] $ExpectedMineLevel, [int] $RequiredBreakableStoneCount)
    $mining = $Snapshot.state.mining
    if ($null -eq $mining) { throw "Snapshot omitted state.mining." }

    foreach ($field in @("current_mine", "tiles", "objects", "monsters", "floor_objectives", "player_resources", "completeness")) {
        if ($mining.$field.status -notin @("available", "derived")) {
            throw "Mining field '$field' is not readable: status=$($mining.$field.status); reason=$($mining.$field.reason)"
        }
    }
    if ([int]$mining.current_mine.value.mine_level -ne $ExpectedMineLevel) {
        throw "Unexpected mine level $($mining.current_mine.value.mine_level), expected $ExpectedMineLevel."
    }
    if ($mining.completeness.value.status -ne "complete" -or @($mining.completeness.value.unavailable_reasons).Count -ne 0) {
        throw "Mining completeness is not complete."
    }

    $collision = $mining.tiles.value.collision_context
    if ($collision.status -ne "available" -or $collision.encoding -ne "row_major_strings_1_blocked_0_passable") {
        throw "Mining collision context is unavailable or has an unexpected encoding."
    }
    $rows = @($collision.blocked_rows)
    if ($rows.Count -ne [int]$collision.height -or $rows.Count -eq 0) {
        throw "Mining collision row count does not match its declared height."
    }
    foreach ($row in $rows) {
        if ([string]$row -notmatch '^[01]+$' -or ([string]$row).Length -ne [int]$collision.width) {
            throw "Mining collision row width or encoding is invalid."
        }
    }

    $breakableStoneCount = 0
    foreach ($object in @($mining.objects.value)) {
        if ($object.is_breakable_stone -and ($null -eq $object.health_or_hits_remaining -or $null -eq $object.ladder_preview)) {
            throw "Breakable stone row omitted durability or ladder preview."
        }
        if ($object.is_breakable_stone) { $breakableStoneCount++ }
    }
    if ($breakableStoneCount -lt $RequiredBreakableStoneCount) {
        throw "Mining snapshot has $breakableStoneCount breakable stones, expected at least $RequiredBreakableStoneCount."
    }
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$worldSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=route"
$miningSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=mining"

if ($MineLevel -lt 1 -or $MineLevel -gt 120) { throw "MineLevel must be between 1 and 120 for the native isolated fixture." }
if ($SampleCount -lt 1) { throw "SampleCount must be positive." }
if ($MinimumBreakableStoneCount -lt 0) { throw "MinimumBreakableStoneCount cannot be negative." }
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $worldSnapshot = Wait-WorldSnapshot -Url $worldSnapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-mining-snapshot-smoke"
        queue_item_id = "runtime-mining-snapshot-smoke.setup"
        before_state_hash = $worldSnapshot.state_hash
        option_id = "debug.setup_mining_floor"
        mine_level = $MineLevel
        save_isolation_path = $savesPath
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Native isolated mine entry failed: status=$($setupResult.status); reasons=$(@($setupResult.block_reasons) -join ',')"
    }

    $snapshot = Wait-MiningSnapshot -Url $miningSnapshotUrl -ExpectedMineLevel $MineLevel -TimeoutSeconds $StartupTimeoutSeconds
    Assert-MiningSnapshot -Snapshot $snapshot -ExpectedMineLevel $MineLevel -RequiredBreakableStoneCount $MinimumBreakableStoneCount
    Write-JsonFile (Join-Path $runDirectory "mining-snapshot.json") $snapshot

    $mineStoneResult = $null
    $stoneTarget = $null
    $stoneRemoved = $null
    $nativeSwingCount = $null
    $healthSequence = ""
    if ($MineOneStone) {
        $playerX = [int]$snapshot.state.mining.tiles.value.player_tile.tile_x
        $playerY = [int]$snapshot.state.mining.tiles.value.player_tile.tile_y
        $stoneTarget = @($snapshot.state.mining.objects.value | Where-Object { $_.is_breakable_stone } | Sort-Object `
            @{ Expression = { [Math]::Abs([int]$_.tile_x - $playerX) + [Math]::Abs([int]$_.tile_y - $playerY) } }, `
            @{ Expression = { [int]$_.best_pickaxe_hits_remaining } }, `
            @{ Expression = { [int]$_.tile_y } }, `
            @{ Expression = { [int]$_.tile_x } }) | Select-Object -First 1
        if ($null -eq $stoneTarget) { throw "No breakable stone was available for the native lifecycle smoke." }

        $mineStoneRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-mining-snapshot-smoke"
            queue_item_id = "runtime-mining-snapshot-smoke.mine-stone"
            before_state_hash = $snapshot.state_hash
            option_id = "executor.mine_stone"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$stoneTarget.tile_x
            target_tile_y = [int]$stoneTarget.tile_y
            max_crops = [Math]::Min(64, [Math]::Max(2, [int]$stoneTarget.best_pickaxe_hits_remaining + 2))
            max_movement_tiles = 512
        }
        $mineStoneResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $mineStoneRequest
        Write-JsonFile (Join-Path $runDirectory "mine-stone-result.json") $mineStoneResult
        if ($mineStoneResult.status -ne "applied" -or $mineStoneResult.primitive_verification_status -ne "verified") {
            throw "Native mine stone lifecycle failed: status=$($mineStoneResult.status); reasons=$(@($mineStoneResult.block_reasons) -join ',')"
        }
        $swingMatch = [regex]::Match((@($mineStoneResult.primitive_verification_reasons) -join ";"), "(?:^|;)native_swing_count=(\d+)(?:;|$)")
        if (-not $swingMatch.Success -or [int]$swingMatch.Groups[1].Value -le 0) {
            throw "Native mine stone lifecycle did not record a positive native swing count."
        }
        $nativeSwingCount = [int]$swingMatch.Groups[1].Value
        $healthMatch = [regex]::Match([string]$mineStoneResult.observed_effect, "(?:^|;)health_sequence=([^;]+)")
        if (-not $healthMatch.Success -or $healthMatch.Groups[1].Value -notmatch "(?:^|,)0$") {
            throw "Native mine stone lifecycle did not record a terminal zero-health observation."
        }
        $healthSequence = $healthMatch.Groups[1].Value

        Start-Sleep -Milliseconds 500
        $afterMineSnapshot = Wait-MiningSnapshot -Url $miningSnapshotUrl -ExpectedMineLevel $MineLevel -TimeoutSeconds 30
        $stoneRemoved = $null -eq (@($afterMineSnapshot.state.mining.objects.value | Where-Object {
            [int]$_.tile_x -eq [int]$stoneTarget.tile_x -and [int]$_.tile_y -eq [int]$stoneTarget.tile_y
        }) | Select-Object -First 1)
        Write-JsonFile (Join-Path $runDirectory "after-mine-snapshot.json") $afterMineSnapshot
        if (-not $stoneRemoved) { throw "Native mine stone lifecycle reported success but the transparent object row remained." }
        $snapshot = $afterMineSnapshot
    }

    $latencies = @()
    $serializedBytes = @()
    for ($sample = 1; $sample -le $SampleCount; $sample++) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest -UseBasicParsing -Method Get -Uri $miningSnapshotUrl -Headers @{ "Accept" = "application/json" } -TimeoutSec 30
        $stopwatch.Stop()
        $sampleSnapshot = $response.Content | ConvertFrom-Json
        Assert-MiningSnapshot -Snapshot $sampleSnapshot -ExpectedMineLevel $MineLevel -RequiredBreakableStoneCount $MinimumBreakableStoneCount
        $latencies += [int]$stopwatch.ElapsedMilliseconds
        $serializedBytes += [System.Text.Encoding]::UTF8.GetByteCount([string]$response.Content)
        Start-Sleep -Milliseconds 150
    }

    $maximumLatency = ($latencies | Measure-Object -Maximum).Maximum
    $summary = [ordered]@{
        status = if ($maximumLatency -le $MaximumSnapshotMilliseconds) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        mine_level = $MineLevel
        mining_completeness = $snapshot.state.mining.completeness.value.status
        collision_width = $snapshot.state.mining.tiles.value.collision_context.width
        collision_height = $snapshot.state.mining.tiles.value.collision_context.height
        object_count = @($snapshot.state.mining.objects.value).Count
        breakable_stone_count = @($snapshot.state.mining.objects.value | Where-Object { $_.is_breakable_stone }).Count
        monster_count = @($snapshot.state.mining.monsters.value).Count
        mine_one_stone_requested = [bool]$MineOneStone
        mine_stone_target = if ($null -ne $stoneTarget) { "$($stoneTarget.tile_x),$($stoneTarget.tile_y)" } else { "" }
        mine_stone_health_before = if ($null -ne $stoneTarget) { [int]$stoneTarget.health_or_hits_remaining } else { $null }
        mine_stone_expected_swings = if ($null -ne $stoneTarget) { [int]$stoneTarget.best_pickaxe_hits_remaining } else { $null }
        mine_stone_status = if ($null -ne $mineStoneResult) { [string]$mineStoneResult.status } else { "not_requested" }
        mine_stone_verification = if ($null -ne $mineStoneResult) { [string]$mineStoneResult.primitive_verification_status } else { "not_requested" }
        mine_stone_actual_ticks = if ($null -ne $mineStoneResult) { [int]$mineStoneResult.actual_ticks } else { $null }
        mine_stone_native_swing_count = $nativeSwingCount
        mine_stone_health_sequence = $healthSequence
        mine_stone_removed = $stoneRemoved
        sample_count = $SampleCount
        snapshot_latency_ms = $latencies
        maximum_snapshot_latency_ms = $maximumLatency
        maximum_allowed_latency_ms = $MaximumSnapshotMilliseconds
        serialized_bytes = $serializedBytes
        executor_health = $executorHealth
        smapi_process_id = $gameProcess.Id
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") { throw "Mining snapshot latency exceeded $MaximumSnapshotMilliseconds ms. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
