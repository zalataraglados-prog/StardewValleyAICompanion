param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-machine-output-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-machine-output-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $QualifiedItemId = "(O)388",
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") { return $response }
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
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $farmReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "farm" -and
                $snapshot.state.farm.PSObject.Properties.Name -contains "machines") {
                $farmReadable = $snapshot.state.farm.machines.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);farm_machines_readable=$farmReadable"
            if ($saveReadable -and $farmReadable) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready farm machines snapshot. Last status: $lastStatus"
}

function Find-MachineAtTile {
    param($Snapshot, [int] $X, [int] $Y)
    if ($null -eq $Snapshot.state.farm.machines.value) { return $null }
    foreach ($machine in @($Snapshot.state.farm.machines.value)) {
        if ([int]$machine.tile_x -eq $X -and [int]$machine.tile_y -eq $Y) { return $machine }
    }
    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

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
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
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

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $initialSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    $cases = @(
        [ordered]@{
            name = "native_configured"
            use_native = $true
            override = $false
            raw = ""
            skill_profile = "zero"
        },
        [ordered]@{
            name = "no_configured_experience"
            use_native = $false
            override = $true
            raw = ""
            skill_profile = "zero"
        },
        [ordered]@{
            name = "multi_skill_sink_and_invalid"
            use_native = $false
            override = $true
            raw =
                "Farming 5 Mining 3 Luck 20 Invalid 7 " +
                "Fishing nope Combat -2"
            skill_profile = "zero"
        },
        [ordered]@{
            name = "mastery_threshold_order"
            use_native = $false
            override = $true
            raw = "Farming 5 Mining 3"
            skill_profile = "mastery_threshold_order"
        }
    )
    $caseResults = @()
    $snapshot = $initialSnapshot
    foreach ($case in $cases) {
        $caseName = [string]$case.name
        $setupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-machine-output-smoke"
            queue_item_id =
                "runtime-machine-output-smoke.$caseName.setup"
            before_state_hash = $snapshot.state_hash
            option_id = "debug.setup_machine_output_target"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $TargetTileX
            target_tile_y = $TargetTileY
            qualified_item_id = $QualifiedItemId
            quantity = 1
            fixture_machine_harvest_use_native_config =
                [bool]$case.use_native
            fixture_machine_harvest_experience_override =
                [bool]$case.override
            fixture_machine_harvest_experience_raw =
                [string]$case.raw
            fixture_machine_harvest_skill_profile =
                [string]$case.skill_profile
        }
        $setupResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $setupRequest `
            -TimeoutSeconds 120
        Write-JsonFile `
            (Join-Path $runDirectory "$caseName-setup.json") `
            $setupResult
        if (
            $setupResult.status -ne "applied" -or
            $setupResult.primitive_verification_status -ne "verified"
        ) {
            throw "$caseName fixture failed: " +
                (@($setupResult.block_reasons) -join ",")
        }

        Start-Sleep -Milliseconds 300
        $beforeCollectSnapshot = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        $targetMachine = Find-MachineAtTile `
            -Snapshot $beforeCollectSnapshot `
            -X $TargetTileX `
            -Y $TargetTileY
        if ($null -eq $targetMachine) {
            throw "$caseName did not project the fixture machine"
        }

        $collectRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-machine-output-smoke"
            queue_item_id =
                "runtime-machine-output-smoke.$caseName.collect"
            before_state_hash = $beforeCollectSnapshot.state_hash
            option_id = "executor.collect_machine_output"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            location_id = [string]$targetMachine.location_id
            target_tile_x = $TargetTileX
            target_tile_y = $TargetTileY
            qualified_item_id =
                [string]$targetMachine.held_item.qualified_item_id
            expected_skill_experience_deltas_json =
                [string]$targetMachine.harvest_experience_deltas_json
            expected_mastery_experience_delta =
                [int]$targetMachine.harvest_mastery_experience_delta
        }
        $collectResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $collectRequest `
            -TimeoutSeconds 120
        Start-Sleep -Milliseconds 300
        $afterSnapshot = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        $afterMachine = Find-MachineAtTile `
            -Snapshot $afterSnapshot `
            -X $TargetTileX `
            -Y $TargetTileY
        $heldAfter = if (
            $null -ne $afterMachine -and
            $null -ne $afterMachine.held_item
        ) {
            [string]$afterMachine.held_item.qualified_item_id
        }
        else {
            ""
        }
        $passed =
            $collectResult.status -eq "applied" -and
            $collectResult.primitive_verification_status -eq "verified" -and
            [string]::IsNullOrWhiteSpace($heldAfter)
        $caseResult = [ordered]@{
            name = $caseName
            machine_qualified_item_id =
                [string]$targetMachine.qualified_item_id
            harvest_experience_raw =
                [string]$targetMachine.harvest_experience_raw
            harvest_experience_entries =
                @($targetMachine.harvest_experience_entries)
            projected_skill_deltas_json =
                [string]$targetMachine.harvest_experience_deltas_json
            projected_mastery_delta =
                [int]$targetMachine.harvest_mastery_experience_delta
            projection_status =
                [string]$targetMachine.harvest_experience_projection_status
            collect_status = $collectResult.status
            collect_verification =
                $collectResult.primitive_verification_status
            collect_block_reasons =
                @($collectResult.block_reasons)
            machine_held_after = $heldAfter
            result = if ($passed) { "passed" } else { "failed" }
        }
        Write-JsonFile `
            (Join-Path $runDirectory "$caseName-before.json") `
            $beforeCollectSnapshot
        Write-JsonFile `
            (Join-Path $runDirectory "$caseName-collect.json") `
            $collectResult
        Write-JsonFile `
            (Join-Path $runDirectory "$caseName-after.json") `
            $afterSnapshot
        $caseResults += [pscustomobject]$caseResult
        $snapshot = $afterSnapshot
    }

    $summary = [ordered]@{
        status = if (
            @($caseResults |
                Where-Object { $_.result -ne "passed" }).Count -eq 0
        ) {
            "passed"
        }
        else {
            "failed"
        }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        qualified_item_id = $QualifiedItemId
        smoke_mods_path = $smokeModsPath
        loaded_mod_allowlist = @(
            "StardewAI.TransparentBridge",
            "StardewAI.RuntimeTestHarness"
        )
        cases = $caseResults
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") { throw "Runtime machine output smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
