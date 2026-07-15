param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-till-soil-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-till-soil-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $SetupTargetTileX = -1,
    [int] $SetupTargetTileY = -1,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $json = $Value | ConvertTo-Json -Depth 64
    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] $Body,
        [int] $TimeoutSeconds = 120
    )

    $json = $Body | ConvertTo-Json -Depth 32
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") {
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
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 5
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $timeReadable = $snapshot.in_game_time.status -in @("available", "derived")
            $farmReadable = $false
            if ($null -ne $snapshot.state -and
                $null -ne $snapshot.state.farm) {
                $farmReadable = $true
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);in_game_time=$($snapshot.in_game_time.status);farm_readable=$farmReadable;completeness=$($snapshot.completeness)"
            if ($saveReadable -and $timeReadable -and $farmReadable) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Find-ChangedFact {
    param(
        [Parameter(Mandatory = $true)] $Result,
        [Parameter(Mandatory = $true)] [string] $Path
    )

    foreach ($fact in @($Result.changed_facts)) {
        if ($fact.path -eq $Path) {
            return $fact
        }
    }

    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}

if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}

if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }

    $SaveSlot = $slot.Name
}

$slotPath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $slotPath -PathType Container)) {
    throw "Isolated save slot not found: $slotPath"
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

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru

    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $initialSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-till-soil-smoke"
        queue_item_id = "runtime-till-soil-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_till_soil_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }

    if ($SetupTargetTileX -ge 0 -and $SetupTargetTileY -ge 0) {
        $setupRequest["target_tile_x"] = $SetupTargetTileX
        $setupRequest["target_tile_y"] = $SetupTargetTileY
    }

    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    $setupSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30

    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-initial.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-setup.json") $setupSnapshot

    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        $summary = [ordered]@{
            status = "setup_failed"
            run_id = $RunId
            save_slot = $SaveSlot
            saves_path = $savesPath
            smapi_process_id = $process.Id
            executor_health = $executorHealth
            setup_status = $setupResult.status
            setup_verification = $setupResult.primitive_verification_status
            setup_block_reasons = @($setupResult.block_reasons)
            kept_game_running = [bool]$KeepGameRunning
        }

        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        $summary | ConvertTo-Json -Depth 32
        return
    }

    $targetTile = "$($setupResult.target_tile_x),$($setupResult.target_tile_y)"
    $terrainFactPath = "farm.terrain_features[$targetTile].type"
    $setupTerrainFact = Find-ChangedFact -Result $setupResult -Path $terrainFactPath
    if ($null -eq $setupTerrainFact -or $setupTerrainFact.after -ne "none") {
        throw "Setup artifact did not prove absent/non-tilled terrain feature for $terrainFactPath."
    }

    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-till-soil-smoke"
        queue_item_id = "runtime-till-soil-smoke.till"
        before_state_hash = $setupSnapshot.state_hash
        option_id = "executor.till_soil"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = [int]$setupResult.target_tile_x
        target_tile_y = [int]$setupResult.target_tile_y
    }

    $result = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 120
    $afterSnapshot = Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -Headers @{ "Accept" = "application/json" } -TimeoutSec 10
    $tillTerrainFact = Find-ChangedFact -Result $result -Path $terrainFactPath

    $summary = [ordered]@{
        status = if ($result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and $null -ne $tillTerrainFact -and $tillTerrainFact.before -ne "HoeDirt" -and $tillTerrainFact.after -eq "HoeDirt") { "passed" } else { "unexpected_result" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        smapi_process_id = $process.Id
        bridge_state_hash_initial = $initialSnapshot.state_hash
        bridge_state_hash_after_setup = $setupSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        target_tile = $targetTile
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        setup_terrain_before = $setupTerrainFact.before
        setup_terrain_after = $setupTerrainFact.after
        till_status = $result.status
        till_verification = $result.primitive_verification_status
        till_reasons = @($result.primitive_verification_reasons)
        till_block_reasons = @($result.block_reasons)
        till_terrain_before = if ($null -ne $tillTerrainFact) { $tillTerrainFact.before } else { "missing" }
        till_terrain_after = if ($null -ne $tillTerrainFact) { $tillTerrainFact.after } else { "missing" }
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "till-request.json") $request
    Write-JsonFile (Join-Path $runDirectory "till-result.json") $result
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary

    $summary | ConvertTo-Json -Depth 32
    if ($summary.status -eq "unexpected_result") {
        throw "Runtime till-soil smoke failed with unexpected_result. See $runDirectory"
    }
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value
        }
    }

    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
