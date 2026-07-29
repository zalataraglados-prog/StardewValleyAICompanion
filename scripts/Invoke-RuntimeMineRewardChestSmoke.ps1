param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId =
        ("runtime-mine-reward-chest-smoke-" +
         (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-mine-reward-chest-smoke",
    [string] $CaseName = "",
    [int] $StartupTimeoutSeconds = 120,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value |
        ConvertTo-Json -Depth 64 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Write-MineRewardSnapshotEvidence {
    param([string] $Path, $Snapshot)
    $evidence = [ordered]@{
        schema_version = $Snapshot.schema_version
        state_hash = $Snapshot.state_hash
        game_tick = $Snapshot.game_tick
        real_timestamp = $Snapshot.real_timestamp
        save_id = $Snapshot.save_id
        player = [ordered]@{
            location_id = $Snapshot.state.player.location_id
            tile_x = $Snapshot.state.player.tile_x
            tile_y = $Snapshot.state.player.tile_y
            active_menu = $Snapshot.state.player.active_menu
            max_stamina = $Snapshot.state.player.max_stamina
        }
        mining = [ordered]@{
            current_mine = $Snapshot.state.mining.current_mine
            tiles = $Snapshot.state.mining.tiles
            reward_chests = $Snapshot.state.mining.reward_chests
            player_resources = $Snapshot.state.mining.player_resources
        }
    }
    Write-JsonFile -Path $Path -Value $evidence
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod `
        -Method Post `
        -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    Invoke-RestMethod `
        -Method Get `
        -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") {
                return $response
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
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
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            if ($snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "save=$($snapshot.save_id.status)"
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world snapshot. Last status: $lastStatus"
}

function Clear-TransientMenus {
    param(
        $Snapshot,
        [string] $RunId,
        [string] $SavePath,
        [string] $CaseName,
        [string] $RunDirectory,
        [string] $SnapshotUrl
    )
    $current = $Snapshot
    for ($attempt = 1; $attempt -le 16; $attempt++) {
        $menuType = [string]$current.state.player.active_menu.value
        if (
            [string]::IsNullOrWhiteSpace($menuType) -or
            $menuType -eq "none"
        ) {
            return $current
        }
        $request = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-mine-reward-chest-smoke"
            queue_item_id =
                "runtime-mine-reward-chest-smoke.$CaseName." +
                "close-menu-$attempt"
            before_state_hash = $current.state_hash
            option_id = "executor.close_menu"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $SavePath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            max_crops = 512
            social_continuation_dialogue_recovery = $true
        }
        $result = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $request `
            -TimeoutSeconds 60
        Write-JsonFile `
            -Path (Join-Path $RunDirectory (
                "$CaseName-close-menu-$attempt.json")) `
            -Value $result
        if (
            $result.status -ne "applied" -or
            $result.primitive_verification_status -ne "verified"
        ) {
            throw "$CaseName could not clear transient menu " +
                "$menuType`: $(@($result.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 250
        $current = Wait-WorldSnapshot `
            -Url $SnapshotUrl `
            -TimeoutSeconds 30
    }
    throw "$CaseName transient menu did not settle after 16 advances"
}

function Get-ReadyMineRewardChests {
    param($Snapshot, [string] $RewardBranch)
    @($Snapshot.state.mining.reward_chests.value |
        Where-Object {
            [string]$_.status -eq "ready" -and
            -not [bool]$_.contains_skull_key -and
            [string]$_.reward_branch -eq $RewardBranch
        } |
        Sort-Object tile_y,tile_x)
}

function Find-RewardChestStand {
    param($Snapshot, $Chest)
    $grid = $Snapshot.state.mining.tiles.value.collision_context
    $playerX = [int]$Snapshot.state.player.tile_x.value
    $playerY = [int]$Snapshot.state.player.tile_y.value
    @(
        [pscustomobject]@{
            x = [int]$Chest.tile_x
            y = [int]$Chest.tile_y - 1
        },
        [pscustomobject]@{
            x = [int]$Chest.tile_x
            y = [int]$Chest.tile_y + 1
        },
        [pscustomobject]@{
            x = [int]$Chest.tile_x - 1
            y = [int]$Chest.tile_y
        },
        [pscustomobject]@{
            x = [int]$Chest.tile_x + 1
            y = [int]$Chest.tile_y
        }
    ) |
        Where-Object {
            $_.x -ge 0 -and $_.x -lt [int]$grid.width -and
            $_.y -ge 0 -and $_.y -lt [int]$grid.height -and
            ([string]$grid.blocked_rows[$_.y])[$_.x] -eq "0"
        } |
        Sort-Object @{
            Expression = {
                [Math]::Abs($_.x - $playerX) +
                    [Math]::Abs($_.y - $playerY)
            }
        },y,x |
        Select-Object -First 1
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path (
    Join-Path $ProjectRoot $OutputDirectory
) $RunId
New-Item -ItemType Directory -Force -Path $runDirectory |
    Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot |
    Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot |
    Out-Null

$smokeModsPath = Join-Path (
    Join-Path $RuntimeRoot "smoke-mods"
) $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath |
    Out-Null
foreach ($modName in @(
    "StardewAI.TransparentBridge",
    "StardewAI.RuntimeTestHarness"
)) {
    $sourceMod = Join-Path (
        Join-Path $runtimeGameDir "Mods"
    ) $modName
    $targetMod = Join-Path $smokeModsPath $modName
    if (-not (Test-Path -LiteralPath $sourceMod -PathType Container)) {
        throw "Required smoke mod is missing: $sourceMod"
    }
    New-Item -ItemType Directory -Force -Path $targetMod |
        Out-Null
    Copy-Item `
        -Path (Join-Path $sourceMod "*") `
        -Destination $targetMod `
        -Recurse `
        -Force
}

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH =
        $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID =
        $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_RESET_MINE_REWARD_CHEST_FIXTURE =
        $env:STARDEWAI_RESET_MINE_REWARD_CHEST_FIXTURE
    STARDEWAI_SKIP_SKULL_CAVERN_SHAFT_FIXTURE =
        $env:STARDEWAI_SKIP_SKULL_CAVERN_SHAFT_FIXTURE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    SMAPI_MODS_PATH = $env:SMAPI_MODS_PATH
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_RESET_MINE_REWARD_CHEST_FIXTURE = "1"
    $env:STARDEWAI_SKIP_SKULL_CAVERN_SHAFT_FIXTURE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $process = Start-Process `
        -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden `
        -PassThru
    $executorHealth = Wait-JsonHealth `
        -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds 30
    $snapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    $cases = @(
        [ordered]@{
            name = "ordinary_floor_20"
            setup_option = "debug.setup_mining_floor"
            mine_level = 20
            reward_branch = "ordinary_fixed_reward"
            expected_chest_count = 1
            expected_stardrop = $false
        },
        [ordered]@{
            name = "ordinary_floor_100_stardrop"
            setup_option = "debug.setup_mining_floor"
            mine_level = 100
            reward_branch = "ordinary_fixed_stardrop"
            expected_chest_count = 1
            expected_stardrop = $true
        },
        [ordered]@{
            name = "skull_cavern_forced_multi"
            setup_option = "debug.setup_skull_cavern_shaft"
            mine_level = 320
            reward_branch = "skull_cavern_forced_treasure"
            expected_chest_count = 2
            expected_stardrop = $false
        }
    )
    if (-not [string]::IsNullOrWhiteSpace($CaseName)) {
        $cases = @($cases | Where-Object { $_.name -eq $CaseName })
        if ($cases.Count -ne 1) {
            throw "Unknown mine reward-chest smoke case: $CaseName"
        }
    }
    $caseResults = @()
    foreach ($case in $cases) {
        $caseName = [string]$case.name
        $setupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-mine-reward-chest-smoke"
            queue_item_id =
                "runtime-mine-reward-chest-smoke.$caseName.setup"
            before_state_hash = $snapshot.state_hash
            option_id = [string]$case.setup_option
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            mine_level = [int]$case.mine_level
        }
        $setupResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $setupRequest
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-setup.json") `
            -Value $setupResult
        if (
            $setupResult.status -ne "applied" -or
            $setupResult.primitive_verification_status -ne "verified"
        ) {
            throw "$caseName fixture failed: " +
                (@($setupResult.block_reasons) -join ",")
        }

        Start-Sleep -Milliseconds 500
        $snapshot = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        Write-MineRewardSnapshotEvidence `
            -Path (Join-Path $runDirectory "$caseName-setup-snapshot.json") `
            -Snapshot $snapshot
        $snapshot = Clear-TransientMenus `
            -Snapshot $snapshot `
            -RunId $RunId `
            -SavePath $savesPath `
            -CaseName $caseName `
            -RunDirectory $runDirectory `
            -SnapshotUrl $snapshotUrl
        $ready = @(
            Get-ReadyMineRewardChests `
                -Snapshot $snapshot `
                -RewardBranch ([string]$case.reward_branch)
        )
        if ($ready.Count -ne [int]$case.expected_chest_count) {
            $observedRows = @(
                $snapshot.state.mining.reward_chests.value |
                    ForEach-Object {
                        "$($_.tile_x),$($_.tile_y):" +
                            "$($_.reward_branch):$($_.status):" +
                            "$($_.item.qualified_item_id)"
                    }
            ) -join ";"
            throw "$caseName expected $($case.expected_chest_count) " +
                "ready chests but observed $($ready.Count); " +
                "all reward rows=$observedRows"
        }

        $claims = @()
        foreach ($initialChest in $ready) {
            $before = Wait-WorldSnapshot `
                -Url $snapshotUrl `
                -TimeoutSeconds 30
            $chest = @(
                Get-ReadyMineRewardChests `
                    -Snapshot $before `
                    -RewardBranch ([string]$case.reward_branch) |
                    Where-Object {
                        [int]$_.tile_x -eq [int]$initialChest.tile_x -and
                        [int]$_.tile_y -eq [int]$initialChest.tile_y
                    }
            ) | Select-Object -First 1
            if ($null -eq $chest) {
                throw "$caseName chest disappeared before claim"
            }
            $stand = Find-RewardChestStand `
                -Snapshot $before `
                -Chest $chest
            if ($null -eq $stand) {
                throw "$caseName chest has no collision-safe stand"
            }

            $claimId = "$caseName-$([int]$chest.tile_x)-" +
                "$([int]$chest.tile_y)"
            $claimRequest = [ordered]@{
                schema_version = "training_execution_request.v1"
                run_id = $RunId
                queue_id = "runtime-mine-reward-chest-smoke"
                queue_item_id =
                    "runtime-mine-reward-chest-smoke.$claimId.claim"
                before_state_hash = $before.state_hash
                option_id = "executor.claim_mine_reward_chest"
                execution_mode = "training_singleplayer"
                actor = "training_farmer.main"
                save_isolation_path = $savesPath
                request_nonce = [guid]::NewGuid().ToString("N")
                created_at = [DateTimeOffset]::UtcNow.ToString("O")
                target_tile_x = [int]$chest.tile_x
                target_tile_y = [int]$chest.tile_y
                stand_tile_x = [int]$stand.x
                stand_tile_y = [int]$stand.y
                target_runtime_type = [string]$chest.runtime_type
                interaction_kind = "overlay_object"
                expected_action_type = "MineRewardChest"
                reward_branch = [string]$chest.reward_branch
                qualified_item_id =
                    [string]$chest.item.qualified_item_id
                quantity = [int]$chest.item.quantity
                expected_output_quality = [int]$chest.item.quality
                expected_output_items_json =
                    [string]$chest.expected_output_items_json
                expected_skill_id = "luck"
                expected_skill_experience_delta =
                    [int]$chest.expected_luck_experience_delta
                native_gain_experience_call_amount =
                    [int]$chest.native_gain_experience_call_amount
                expected_stardrop_max_stamina_delta =
                    [int]$chest.expected_stardrop_max_stamina_delta
                max_movement_tiles = 512
            }
            $claimResult = Invoke-JsonPost `
                -Url "http://127.0.0.1:8767/api/v1/training/execute" `
                -Body $claimRequest `
                -TimeoutSeconds 180
            Start-Sleep -Milliseconds 500
            $after = Wait-WorldSnapshot `
                -Url $snapshotUrl `
                -TimeoutSeconds 30
            $stillPresent = @(
                $after.state.mining.reward_chests.value |
                    Where-Object {
                        [int]$_.tile_x -eq [int]$chest.tile_x -and
                        [int]$_.tile_y -eq [int]$chest.tile_y
                    }
            ).Count -gt 0
            $maxStaminaFact = @(
                $claimResult.changed_facts |
                    Where-Object {
                        [string]$_.path -eq "player.max_stamina"
                    } |
                    Select-Object -First 1
            )
            $maxStaminaDelta =
                if ($maxStaminaFact.Count -eq 1) {
                    [int]$maxStaminaFact[0].after -
                        [int]$maxStaminaFact[0].before
                }
                else {
                    $null
                }
            $passed =
                $claimResult.status -eq "applied" -and
                $claimResult.primitive_verification_status -eq
                    "verified" -and
                -not $stillPresent -and
                (
                    -not [bool]$case.expected_stardrop -or
                    $maxStaminaDelta -eq 34
                )
            $claimSummary = [ordered]@{
                tile = "$([int]$chest.tile_x),$([int]$chest.tile_y)"
                reward_branch = [string]$chest.reward_branch
                qualified_item_id =
                    [string]$chest.item.qualified_item_id
                quantity = [int]$chest.item.quantity
                claim_status = $claimResult.status
                verification =
                    $claimResult.primitive_verification_status
                block_reasons = @($claimResult.block_reasons)
                actual_ticks = $claimResult.actual_ticks
                present_after = $stillPresent
                max_stamina_delta = $maxStaminaDelta
                status = if ($passed) { "passed" } else { "failed" }
            }
            Write-MineRewardSnapshotEvidence `
                -Path (Join-Path $runDirectory "$claimId-before.json") `
                -Snapshot $before
            Write-JsonFile `
                -Path (Join-Path $runDirectory "$claimId-claim.json") `
                -Value $claimResult
            Write-MineRewardSnapshotEvidence `
                -Path (Join-Path $runDirectory "$claimId-after.json") `
                -Snapshot $after
            $claims += [pscustomobject]$claimSummary
            if (-not $passed) {
                throw "$claimId claim failed: status=" +
                    "$($claimResult.status); verification=" +
                    "$($claimResult.primitive_verification_status); " +
                    "reasons=$(@($claimResult.block_reasons) -join ',')"
            }
            $snapshot = $after
        }

        $casePassed = @(
            $claims |
                Where-Object { $_.status -ne "passed" }
        ).Count -eq 0
        $caseResults += [pscustomobject][ordered]@{
            name = $caseName
            mine_level = [int]$case.mine_level
            reward_branch = [string]$case.reward_branch
            expected_chest_count = [int]$case.expected_chest_count
            observed_chest_count = $claims.Count
            claims = $claims
            status = if ($casePassed) { "passed" } else { "failed" }
        }
    }

    $summary = [ordered]@{
        status = if (
            @($caseResults |
                Where-Object { $_.status -ne "passed" }).Count -eq 0
        ) {
            "passed"
        } else {
            "failed"
        }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        smoke_mods_path = $smokeModsPath
        loaded_mod_allowlist = @(
            "StardewAI.TransparentBridge",
            "StardewAI.RuntimeTestHarness"
        )
        cases = $caseResults
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }
    Write-JsonFile `
        -Path (Join-Path $runDirectory "summary.json") `
        -Value $summary
    $summary | ConvertTo-Json -Depth 16
    if ($summary.status -ne "passed") {
        throw "Runtime mine reward-chest smoke failed. See $runDirectory"
    }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (
        -not $KeepGameRunning -and
        $null -ne $process -and
        -not $process.HasExited
    ) {
        Stop-Process `
            -Id $process.Id `
            -Force `
            -ErrorAction SilentlyContinue
    }
}
