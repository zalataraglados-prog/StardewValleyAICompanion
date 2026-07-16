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
    [switch] $BreakOneContainer,
    [switch] $CombatOneMonster,
    [switch] $ManualCombatMovement,
    [switch] $MiningCalibrationLoadout,
    [switch] $VisibleGame,
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

function Get-ReachableAdjacentDistance {
    param($Collision, [int] $StartX, [int] $StartY, [int] $TargetX, [int] $TargetY)
    if ($Collision.encoding -ne "row_major_strings_1_blocked_0_passable") { return $null }
    $width = [int]$Collision.width
    $height = [int]$Collision.height
    $rows = @($Collision.blocked_rows)
    $goals = @{}
    foreach ($delta in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
        $x = $TargetX + $delta[0]
        $y = $TargetY + $delta[1]
        if ($x -ge 0 -and $x -lt $width -and $y -ge 0 -and $y -lt $height -and $rows[$y][$x] -eq '0') {
            $goals["$x,$y"] = $true
        }
    }
    if ($goals.Count -eq 0) { return $null }

    $queue = New-Object 'System.Collections.Generic.Queue[object]'
    $queue.Enqueue([pscustomobject]@{ X = $StartX; Y = $StartY; Distance = 0 })
    $visited = @{ "$StartX,$StartY" = $true }
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if ($goals.ContainsKey("$($current.X),$($current.Y)")) { return [int]$current.Distance }
        foreach ($delta in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
            $x = [int]$current.X + $delta[0]
            $y = [int]$current.Y + $delta[1]
            $key = "$x,$y"
            if ($x -lt 0 -or $x -ge $width -or $y -lt 0 -or $y -ge $height -or $visited.ContainsKey($key) -or $rows[$y][$x] -ne '0') { continue }
            $visited[$key] = $true
            $queue.Enqueue([pscustomobject]@{ X = $x; Y = $y; Distance = [int]$current.Distance + 1 })
        }
    }
    return $null
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

    foreach ($field in @("current_mine", "tiles", "objects", "resource_clumps", "monsters", "floor_objectives", "player_resources", "completeness")) {
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
    foreach ($monster in @($mining.monsters.value)) {
        foreach ($property in @("runtime_identity", "runtime_type", "health", "resilience", "miss_chance", "is_invincible", "invincible_countdown_ms", "is_glider", "ignore_damage_line_of_sight")) {
            if ($null -eq $monster.$property) { throw "Mining monster row omitted combat field '$property'." }
        }
    }
    foreach ($weapon in @($mining.player_resources.value.weapon_slots)) {
        foreach ($property in @("weapon_type", "is_scythe", "min_damage", "max_damage", "speed", "precision", "area_of_effect", "knockback", "critical_chance", "critical_multiplier", "enchantments")) {
            if ($null -eq $weapon.$property) { throw "Mining weapon row omitted combat field '$property'." }
        }
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
    STARDEWAI_COMBAT_MANUAL_MOVEMENT = $env:STARDEWAI_COMBAT_MANUAL_MOVEMENT
    STARDEWAI_MINING_CALIBRATION_LOADOUT = $env:STARDEWAI_MINING_CALIBRATION_LOADOUT
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
    $env:STARDEWAI_COMBAT_MANUAL_MOVEMENT = if ($ManualCombatMovement) { "1" } else { "0" }
    $env:STARDEWAI_MINING_CALIBRATION_LOADOUT = if ($MiningCalibrationLoadout) { "1" } else { "0" }
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $gameWindowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle $gameWindowStyle -PassThru
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

    $containerResult = $null
    $containerTarget = $null
    $containerRemoved = $null
    if ($BreakOneContainer) {
        for ($clearanceAttempt = 0; $clearanceAttempt -lt 4 -and $null -eq $containerTarget; $clearanceAttempt++) {
            $playerX = [int]$snapshot.state.mining.tiles.value.player_tile.tile_x
            $playerY = [int]$snapshot.state.mining.tiles.value.player_tile.tile_y
            $collision = $snapshot.state.mining.tiles.value.collision_context
            $allContainers = @($snapshot.state.mining.objects.value | Where-Object { $_.is_container })
            $containerCandidates = @($allContainers | ForEach-Object {
                $distance = Get-ReachableAdjacentDistance -Collision $collision -StartX $playerX -StartY $playerY -TargetX ([int]$_.tile_x) -TargetY ([int]$_.tile_y)
                if ($null -ne $distance) { [pscustomobject]@{ Container = $_; Distance = [int]$distance } }
            })
            $containerCandidate = @($containerCandidates | Sort-Object Distance, @{ Expression = { [int]$_.Container.health_or_hits_remaining } }, @{ Expression = { [int]$_.Container.tile_y } }, @{ Expression = { [int]$_.Container.tile_x } }) | Select-Object -First 1
            if ($null -ne $containerCandidate) {
                $containerTarget = $containerCandidate.Container
                break
            }

            $stoneCandidates = @($snapshot.state.mining.objects.value | Where-Object { $_.is_breakable_stone } | ForEach-Object {
                $distance = Get-ReachableAdjacentDistance -Collision $collision -StartX $playerX -StartY $playerY -TargetX ([int]$_.tile_x) -TargetY ([int]$_.tile_y)
                if ($null -ne $distance) {
                    $stone = $_
                    $containerDistance = @($allContainers | ForEach-Object { [Math]::Abs([int]$_.tile_x - [int]$stone.tile_x) + [Math]::Abs([int]$_.tile_y - [int]$stone.tile_y) } | Measure-Object -Minimum).Minimum
                    [pscustomobject]@{ Stone = $stone; Distance = [int]$distance; ApproachCost = [int]$distance + [int]$containerDistance }
                }
            })
            $clearanceStone = @($stoneCandidates | Sort-Object ApproachCost, Distance, @{ Expression = { [int]$_.Stone.best_pickaxe_hits_remaining } }, @{ Expression = { [int]$_.Stone.tile_y } }, @{ Expression = { [int]$_.Stone.tile_x } }) | Select-Object -First 1
            if ($null -eq $clearanceStone) { break }
            $stone = $clearanceStone.Stone
            $clearanceRequest = [ordered]@{
                schema_version = "training_execution_request.v1"
                run_id = $RunId
                queue_id = "runtime-mining-snapshot-smoke"
                queue_item_id = "runtime-mining-snapshot-smoke.container-clearance-$clearanceAttempt"
                before_state_hash = $snapshot.state_hash
                option_id = "executor.mine_stone"
                execution_mode = "training_singleplayer"
                actor = "training_farmer.main"
                save_isolation_path = $savesPath
                request_nonce = [guid]::NewGuid().ToString("N")
                created_at = [DateTimeOffset]::UtcNow.ToString("O")
                target_tile_x = [int]$stone.tile_x
                target_tile_y = [int]$stone.tile_y
                max_crops = [Math]::Max(2, [int]$stone.best_pickaxe_hits_remaining + 2)
                max_movement_tiles = 512
            }
            $clearanceResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $clearanceRequest -TimeoutSeconds 150
            Write-JsonFile (Join-Path $runDirectory "container-clearance-$clearanceAttempt-result.json") $clearanceResult
            if ($clearanceResult.status -ne "applied" -or $clearanceResult.primitive_verification_status -ne "verified") {
                throw "Container route clearance failed: status=$($clearanceResult.status); reasons=$(@($clearanceResult.block_reasons) -join ',')"
            }
            $snapshot = Wait-MiningSnapshot -Url $miningSnapshotUrl -ExpectedMineLevel $MineLevel -TimeoutSeconds 30
        }
        if ($null -eq $containerTarget) {
            $fixtureRequest = [ordered]@{
                schema_version = "training_execution_request.v1"
                run_id = $RunId
                queue_id = "runtime-mining-snapshot-smoke"
                queue_item_id = "runtime-mining-snapshot-smoke.setup-break-container"
                before_state_hash = $snapshot.state_hash
                option_id = "debug.setup_breakable_container"
                execution_mode = "training_singleplayer"
                actor = "training_farmer.main"
                save_isolation_path = $savesPath
                request_nonce = [guid]::NewGuid().ToString("N")
                created_at = [DateTimeOffset]::UtcNow.ToString("O")
            }
            $fixtureResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $fixtureRequest -TimeoutSeconds 60
            Write-JsonFile (Join-Path $runDirectory "setup-break-container-result.json") $fixtureResult
            if ($fixtureResult.status -ne "applied" -or $fixtureResult.primitive_verification_status -ne "verified") {
                throw "Break-container fixture setup failed: status=$($fixtureResult.status); reasons=$(@($fixtureResult.block_reasons) -join ',')"
            }
            $fixtureDeadline = (Get-Date).AddSeconds(10)
            while ((Get-Date) -lt $fixtureDeadline -and $null -eq $containerTarget) {
                Start-Sleep -Milliseconds 500
                $snapshot = Wait-MiningSnapshot -Url $miningSnapshotUrl -ExpectedMineLevel $MineLevel -TimeoutSeconds 30
                $containerTarget = @($snapshot.state.mining.objects.value | Where-Object {
                    [int]$_.tile_x -eq [int]$fixtureResult.target_tile_x -and [int]$_.tile_y -eq [int]$fixtureResult.target_tile_y -and $_.is_container
                }) | Select-Object -First 1
            }
        }
        if ($null -eq $containerTarget) { throw "No collision-reachable breakable container was available for the native smoke." }

        $containerRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-mining-snapshot-smoke"
            queue_item_id = "runtime-mining-snapshot-smoke.break-container"
            before_state_hash = $snapshot.state_hash
            option_id = "executor.break_container"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$containerTarget.tile_x
            target_tile_y = [int]$containerTarget.tile_y
            max_crops = [Math]::Max(4, [int]$containerTarget.health_or_hits_remaining + 2)
            max_movement_tiles = 512
            restore_slot_index = [int]$snapshot.state.mining.player_resources.value.selected_slot_index
        }
        $containerResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $containerRequest -TimeoutSeconds 150
        Write-JsonFile (Join-Path $runDirectory "break-container-result.json") $containerResult
        if ($containerResult.status -ne "applied" -or $containerResult.primitive_verification_status -ne "verified") {
            throw "Native break-container lifecycle failed: status=$($containerResult.status); reasons=$(@($containerResult.block_reasons) -join ',')"
        }
        $afterContainerDeadline = (Get-Date).AddSeconds(10)
        do {
            Start-Sleep -Milliseconds 500
            $afterContainerSnapshot = Wait-MiningSnapshot -Url $miningSnapshotUrl -ExpectedMineLevel $MineLevel -TimeoutSeconds 30
            $containerRemoved = -not @($afterContainerSnapshot.state.mining.objects.value | Where-Object {
                [int]$_.tile_x -eq [int]$containerTarget.tile_x -and [int]$_.tile_y -eq [int]$containerTarget.tile_y -and $_.is_container
            }).Count
        } while (-not $containerRemoved -and (Get-Date) -lt $afterContainerDeadline)
        Write-JsonFile (Join-Path $runDirectory "after-break-container-snapshot.json") $afterContainerSnapshot
        if (-not $containerRemoved) { throw "Native break-container lifecycle reported success but the transparent container row remained." }
        $snapshot = $afterContainerSnapshot
    }

    $combatResult = $null
    $combatTarget = $null
    $combatTargetRemoved = $null
    if ($CombatOneMonster) {
        $playerX = [int]$snapshot.state.mining.tiles.value.player_tile.tile_x
        $playerY = [int]$snapshot.state.mining.tiles.value.player_tile.tile_y
        $collision = $snapshot.state.mining.tiles.value.collision_context
        $combatCandidates = @($snapshot.state.mining.monsters.value | Where-Object {
            $_.melee_damage_semantics.can_defeat_with_available_melee_weapon -ne $false -and
            @($_.melee_attack_projections | Where-Object { $_.duration_status -eq "exact_active_melee_phase_excluding_movement" }).Count -gt 0
        } | ForEach-Object {
            $distance = Get-ReachableAdjacentDistance -Collision $collision -StartX $playerX -StartY $playerY -TargetX ([int]$_.tile_x) -TargetY ([int]$_.tile_y)
            if ($null -ne $distance) { [pscustomobject]@{ Monster = $_; Distance = [int]$distance } }
        })
        $combatCandidate = @($combatCandidates | Sort-Object Distance, @{ Expression = { [int]$_.Monster.health } }, @{ Expression = { [string]$_.Monster.runtime_identity } }) | Select-Object -First 1
        $combatTarget = if ($null -ne $combatCandidate) { $combatCandidate.Monster } else { $null }
        if ($null -eq $combatTarget) { throw "No collision-reachable monster was available for the native combat smoke." }
        if (@($snapshot.state.mining.player_resources.value.weapon_slots | Where-Object { -not $_.is_scythe }).Count -eq 0) {
            throw "No non-scythe melee weapon was available for the native combat smoke."
        }
        $combatProjection = @($combatTarget.melee_attack_projections | Where-Object {
            $_.duration_status -eq "exact_active_melee_phase_excluding_movement"
        } | Sort-Object @{ Expression = { [double]$_.expected_active_damage_duration_ms } }, @{ Expression = { [int]$_.slot_index } }) | Select-Object -First 1
        if ($null -eq $combatProjection) { throw "Selected combat target had no complete melee projection." }

        $combatRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-mining-snapshot-smoke"
            queue_item_id = "runtime-mining-snapshot-smoke.combat-monster"
            before_state_hash = $snapshot.state_hash
            option_id = "executor.combat_monster"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$combatTarget.tile_x
            target_tile_y = [int]$combatTarget.tile_y
            target_runtime_identity = [string]$combatTarget.runtime_identity
            target_runtime_type = [string]$combatTarget.runtime_type
            target_name = [string]$combatTarget.name
            max_attacks = 64
            max_movement_tiles = 512
            combat_weapon_slot_index = [int]$combatProjection.slot_index
            required_weapon_enchantment_runtime_type = [string]$combatTarget.melee_damage_semantics.required_weapon_enchantment_runtime_type
        }
        # The executor's hard combat ceiling is 120 seconds; keep transport timeout later so its typed failure is retained.
        $combatResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $combatRequest -TimeoutSeconds 150
        Write-JsonFile (Join-Path $runDirectory "combat-monster-result.json") $combatResult
        if ($combatResult.status -ne "applied" -or $combatResult.primitive_verification_status -ne "verified" -or -not $combatResult.combat_target_defeated) {
            throw "Native combat lifecycle failed: status=$($combatResult.status); reasons=$(@($combatResult.block_reasons) -join ',')"
        }
        if ([int]$combatResult.combat_attack_count -le 0 -or [int]$combatResult.combat_hit_count -le 0) {
            throw "Native combat lifecycle did not record positive attack and hit counts."
        }
        $combatHealth = @($combatResult.combat_target_health_sequence)
        if ($combatHealth.Count -lt 2 -or [int]$combatHealth[-1] -gt 0) {
            throw "Native combat lifecycle did not record a terminal defeated-health observation."
        }

        Start-Sleep -Milliseconds 500
        $afterCombatSnapshot = Wait-MiningSnapshot -Url $miningSnapshotUrl -ExpectedMineLevel $MineLevel -TimeoutSeconds 30
        $combatTargetRemoved = $null -eq (@($afterCombatSnapshot.state.mining.monsters.value | Where-Object {
            [string]$_.runtime_identity -eq [string]$combatTarget.runtime_identity
        }) | Select-Object -First 1)
        Write-JsonFile (Join-Path $runDirectory "after-combat-snapshot.json") $afterCombatSnapshot
        if (-not $combatTargetRemoved) { throw "Native combat reported defeat but the transparent monster row remained." }
        $snapshot = $afterCombatSnapshot
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
        break_one_container_requested = [bool]$BreakOneContainer
        break_container_target = if ($null -ne $containerTarget) { "$($containerTarget.tile_x),$($containerTarget.tile_y)" } else { "" }
        break_container_health_before = if ($null -ne $containerTarget) { [int]$containerTarget.health_or_hits_remaining } else { $null }
        break_container_status = if ($null -ne $containerResult) { [string]$containerResult.status } else { "not_requested" }
        break_container_verification = if ($null -ne $containerResult) { [string]$containerResult.primitive_verification_status } else { "not_requested" }
        break_container_observed_effect = if ($null -ne $containerResult) { [string]$containerResult.observed_effect } else { "" }
        break_container_removed = $containerRemoved
        combat_one_monster_requested = [bool]$CombatOneMonster
        combat_target_identity = if ($null -ne $combatTarget) { [string]$combatTarget.runtime_identity } else { "" }
        combat_target_type = if ($null -ne $combatTarget) { [string]$combatTarget.runtime_type } else { "" }
        combat_target_name = if ($null -ne $combatTarget) { [string]$combatTarget.name } else { "" }
        combat_target_health_before = if ($null -ne $combatTarget) { [int]$combatTarget.health } else { $null }
        combat_projected_weapon_slot = if ($null -ne $combatProjection) { [int]$combatProjection.slot_index } else { $null }
        combat_projected_expected_attacks = if ($null -ne $combatProjection) { [double]$combatProjection.expected_attacks_to_defeat } else { $null }
        combat_projected_active_duration_ms = if ($null -ne $combatProjection) { [double]$combatProjection.expected_active_damage_duration_ms } else { $null }
        combat_status = if ($null -ne $combatResult) { [string]$combatResult.status } else { "not_requested" }
        combat_verification = if ($null -ne $combatResult) { [string]$combatResult.primitive_verification_status } else { "not_requested" }
        combat_attack_count = if ($null -ne $combatResult) { [int]$combatResult.combat_attack_count } else { $null }
        combat_hit_count = if ($null -ne $combatResult) { [int]$combatResult.combat_hit_count } else { $null }
        combat_target_health_sequence = if ($null -ne $combatResult) { @($combatResult.combat_target_health_sequence) } else { @() }
        combat_player_health_sequence = if ($null -ne $combatResult) { @($combatResult.combat_player_health_sequence) } else { @() }
        combat_damage_taken = if ($null -ne $combatResult) { [int]$combatResult.combat_damage_taken } else { $null }
        combat_target_removed = $combatTargetRemoved
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
