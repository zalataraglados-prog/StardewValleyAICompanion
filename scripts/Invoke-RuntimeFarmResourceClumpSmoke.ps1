param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId =
        ("runtime-farm-resource-clump-smoke-" +
         (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-farm-resource-clump-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
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
            $farmReadable =
                $snapshot.state.farm.resource_clumps.status -in
                    @("available", "derived")
            if (
                $snapshot.save_id.status -in @("available", "derived") -and
                $farmReadable
            ) {
                return $snapshot
            }
            $lastStatus =
                "save=$($snapshot.save_id.status);" +
                "farm_resource_clumps=$farmReadable"
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for farm snapshot. Last status: $lastStatus"
}

function Find-FarmResourceClump {
    param($Snapshot, [int] $X, [int] $Y, [string] $ClearKind)
    $Snapshot.state.farm.resource_clumps.value |
        Where-Object {
            [int]$_.tile_x -eq $X -and
            [int]$_.tile_y -eq $Y -and
            [string]$_.clear_kind -eq $ClearKind
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

    $cases = @(
        [ordered]@{
            clear_kind = "resource_stump"
            parent_sheet_index = 600
        },
        [ordered]@{
            clear_kind = "hollow_log"
            parent_sheet_index = 602
        }
    )
    $caseResults = @()
    foreach ($case in $cases) {
        $caseName = [string]$case.clear_kind
        $setupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-farm-resource-clump-smoke"
            queue_item_id =
                "runtime-farm-resource-clump-smoke.$caseName.setup"
            before_state_hash = $snapshot.state_hash
            option_id = "debug.setup_farm_resource_clump"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $TargetTileX
            target_tile_y = $TargetTileY
            resource_clump_parent_sheet_index =
                [int]$case.parent_sheet_index
        }
        $setupResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $setupRequest
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
        $clump = Find-FarmResourceClump `
            -Snapshot $before `
            -X $TargetTileX `
            -Y $TargetTileY `
            -ClearKind $caseName
        if ($null -eq $clump) {
            throw "$caseName fixture is absent from farm.resource_clumps"
        }
        if ([string]$clump.clear_obstacle_executor_status -ne "ready") {
            throw "$caseName is not executable: " +
                [string]$clump.clear_obstacle_executor_status
        }

        $standTileX = [int]$before.state.player.tile_x.value
        $standTileY = [int]$before.state.player.tile_y.value
        $breakRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-farm-resource-clump-smoke"
            queue_item_id =
                "runtime-farm-resource-clump-smoke.$caseName.break"
            before_state_hash = $before.state_hash
            option_id = "executor.break_farm_resource_clump"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $TargetTileX
            target_tile_y = $TargetTileY
            stand_tile_x = $standTileX
            stand_tile_y = $standTileY
            resource_clump_tile_x = [int]$clump.tile_x
            resource_clump_tile_y = [int]$clump.tile_y
            resource_clump_width = [int]$clump.width
            resource_clump_height = [int]$clump.height
            resource_clump_parent_sheet_index =
                [int]$clump.parent_sheet_index
            tool_slot_index = [int]$clump.tool_slot_index
            required_tool_kind = "axe"
            expected_foraging_experience_delta =
                [int]$clump.harvest_experience_on_success_min
            max_crops =
                [Math]::Max(
                    1,
                    [int]$clump.expected_tool_hits_to_clear + 1)
            max_movement_tiles = 16
        }
        $breakResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $breakRequest
        Start-Sleep -Milliseconds 500
        $after = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        $afterClump = Find-FarmResourceClump `
            -Snapshot $after `
            -X $TargetTileX `
            -Y $TargetTileY `
            -ClearKind $caseName
        $xpChange = @($breakResult.changed_facts |
            Where-Object {
                [string]$_.path -eq
                    "player.skills.foraging.experience"
            } |
            Select-Object -First 1)
        $xpDelta = if ($xpChange.Count -eq 1) {
            [int]$xpChange[0].after - [int]$xpChange[0].before
        } else {
            $null
        }
        $passed =
            $breakResult.status -eq "applied" -and
            $breakResult.primitive_verification_status -eq "verified" -and
            $null -eq $afterClump -and
            $xpDelta -eq 25
        $caseResult = [ordered]@{
            clear_kind = $caseName
            parent_sheet_index = [int]$case.parent_sheet_index
            setup_status = $setupResult.status
            break_status = $breakResult.status
            break_verification =
                $breakResult.primitive_verification_status
            break_block_reasons = @($breakResult.block_reasons)
            native_swings = $breakResult.tool_use_count
            tool_base_upgrade = [int]$clump.tool_upgrade_level
            tool_additional_power = [int]$clump.tool_additional_power
            tool_effective_upgrade =
                [int]$clump.tool_effective_upgrade_level
            projected_damage_per_hit = [double]$clump.damage_per_hit
            projected_hits = [int]$clump.expected_tool_hits_to_clear
            foraging_xp_delta = $xpDelta
            present_after = $null -ne $afterClump
            status = if ($passed) { "passed" } else { "failed" }
        }
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-setup.json") `
            -Value $setupResult
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
        target_tile = "$TargetTileX,$TargetTileY"
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
        throw "Runtime farm resource-clump smoke failed. See $runDirectory"
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
