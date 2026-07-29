param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId =
        ("runtime-mining-resource-clump-smoke-" +
         (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-mining-resource-clump-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $MineLevel = 5,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value |
        ConvertTo-Json -Depth 64 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod `
        -Method Post `
        -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $json `
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
            if (
                $snapshot.save_id.status -in @("available", "derived")
            ) {
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

function Find-MiningResourceClump {
    param($Snapshot, [int] $X, [int] $Y, [int] $ParentSheetIndex)
    $Snapshot.state.mining.resource_clumps.value |
        Where-Object {
            [int]$_.tile_x -eq $X -and
            [int]$_.tile_y -eq $Y -and
            [int]$_.parent_sheet_index -eq $ParentSheetIndex
        } |
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
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
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
    $mineSetupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-mining-resource-clump-smoke"
        queue_item_id = "runtime-mining-resource-clump-smoke.mine.setup"
        before_state_hash = $snapshot.state_hash
        option_id = "debug.setup_mining_floor"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        mine_level = $MineLevel
    }
    $mineSetupResult = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $mineSetupRequest
    if (
        $mineSetupResult.status -ne "applied" -or
        $mineSetupResult.primitive_verification_status -ne "verified"
    ) {
        throw "Mine fixture failed: " +
            (@($mineSetupResult.block_reasons) -join ",")
    }
    $snapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds 30

    $cases = @(
        [ordered]@{ name = "quarry_boulder"; parent_sheet_index = 148 },
        [ordered]@{ name = "meteorite"; parent_sheet_index = 622 },
        [ordered]@{ name = "boulder"; parent_sheet_index = 672 },
        [ordered]@{ name = "mine_rock_1"; parent_sheet_index = 752 },
        [ordered]@{ name = "mine_rock_2"; parent_sheet_index = 754 },
        [ordered]@{ name = "mine_rock_3"; parent_sheet_index = 756 },
        [ordered]@{ name = "mine_rock_4"; parent_sheet_index = 758 }
    )
    $caseResults = @()
    foreach ($case in $cases) {
        $caseName = [string]$case.name
        $setupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-mining-resource-clump-smoke"
            queue_item_id =
                "runtime-mining-resource-clump-smoke.$caseName.setup"
            before_state_hash = $snapshot.state_hash
            option_id = "debug.setup_mining_resource_clump"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            resource_clump_parent_sheet_index =
                [int]$case.parent_sheet_index
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

        Start-Sleep -Milliseconds 400
        $before = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        $targetTileX = [int]$setupResult.target_tile_x
        $targetTileY = [int]$setupResult.target_tile_y
        $clump = Find-MiningResourceClump `
            -Snapshot $before `
            -X $targetTileX `
            -Y $targetTileY `
            -ParentSheetIndex ([int]$case.parent_sheet_index)
        if ($null -eq $clump) {
            throw "$caseName fixture is absent from mining.resource_clumps"
        }
        if ([string]$clump.executor_status -ne
            "native_executor_available") {
            throw "$caseName is not executable: " +
                [string]$clump.executor_status
        }

        $standTileX = [int]$before.state.player.tile_x.value
        $standTileY = [int]$before.state.player.tile_y.value
        $hitTile = @(
            [pscustomobject]@{ x = $standTileX; y = $standTileY - 1 },
            [pscustomobject]@{ x = $standTileX; y = $standTileY + 1 },
            [pscustomobject]@{ x = $standTileX - 1; y = $standTileY },
            [pscustomobject]@{ x = $standTileX + 1; y = $standTileY }
        ) | Where-Object {
            $_.x -ge [int]$clump.tile_x -and
            $_.x -lt ([int]$clump.tile_x + [int]$clump.width) -and
            $_.y -ge [int]$clump.tile_y -and
            $_.y -lt ([int]$clump.tile_y + [int]$clump.height)
        } | Select-Object -First 1
        if ($null -eq $hitTile) {
            throw "$caseName has no adjacent footprint hit tile"
        }
        $breakRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-mining-resource-clump-smoke"
            queue_item_id =
                "runtime-mining-resource-clump-smoke.$caseName.break"
            before_state_hash = $before.state_hash
            option_id = "executor.break_resource_clump"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$hitTile.x
            target_tile_y = [int]$hitTile.y
            stand_tile_x = $standTileX
            stand_tile_y = $standTileY
            resource_clump_tile_x = [int]$clump.tile_x
            resource_clump_tile_y = [int]$clump.tile_y
            resource_clump_width = [int]$clump.width
            resource_clump_height = [int]$clump.height
            resource_clump_parent_sheet_index =
                [int]$clump.parent_sheet_index
            tool_slot_index =
                [int]$clump.selected_tool_slot_index
            required_tool_kind = "pickaxe"
            target_runtime_type = [string]$clump.runtime_type
            expected_output_items_json =
                [string]$clump.expected_core_output_items_json
            max_crops =
                [Math]::Max(
                    1,
                    [int]$clump.expected_hits_remaining + 1)
            max_movement_tiles = 16
        }
        $breakResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $breakRequest
        Start-Sleep -Milliseconds 500
        $after = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        $afterClump = Find-MiningResourceClump `
            -Snapshot $after `
            -X $targetTileX `
            -Y $targetTileY `
            -ParentSheetIndex ([int]$case.parent_sheet_index)
        $passed =
            $breakResult.status -eq "applied" -and
            $breakResult.primitive_verification_status -eq "verified" -and
            $null -eq $afterClump -and
            [int]$breakResult.tool_use_count -eq
                [int]$clump.expected_hits_remaining
        $caseResult = [ordered]@{
            name = $caseName
            parent_sheet_index = [int]$case.parent_sheet_index
            setup_status = $setupResult.status
            break_status = $breakResult.status
            break_verification =
                $breakResult.primitive_verification_status
            break_block_reasons = @($breakResult.block_reasons)
            native_swings = $breakResult.tool_use_count
            tool_base_upgrade =
                [int]$clump.selected_tool_upgrade_level
            tool_additional_power =
                [int]$clump.selected_tool_additional_power
            tool_effective_upgrade =
                [int]$clump.selected_tool_effective_upgrade_level
            projected_damage_per_hit = [double]$clump.damage_per_hit
            projected_hits = [int]$clump.expected_hits_remaining
            expected_core_output_items_json =
                [string]$clump.expected_core_output_items_json
            present_after = $null -ne $afterClump
            status = if ($passed) { "passed" } else { "failed" }
        }
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-break.json") `
            -Value $breakResult
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-before.json") `
            -Value $before
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-after.json") `
            -Value $after
        $caseResults += [pscustomobject]$caseResult
        $snapshot = $after
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
        mine_level = $MineLevel
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
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") {
        throw "Runtime mining resource-clump smoke failed. See $runDirectory"
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
