param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-clear-obstacle-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-clear-obstacle-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [int] $MaxToolSwings = 8,
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
            $locationReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "player" -and
                $snapshot.state.player.PSObject.Properties.Name -contains "location_id") {
                $locationReadable = $snapshot.state.player.location_id.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);in_game_time=$($snapshot.in_game_time.status);location_id_readable=$locationReadable;completeness=$($snapshot.completeness)"
            if ($saveReadable -and $timeReadable -and $locationReadable) {
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

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

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
    $beforeSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds $StartupTimeoutSeconds

    $baseRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-clear-obstacle-smoke"
        before_state_hash = $beforeSnapshot.state_hash
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = ""
        created_at = ""
        max_crops = $MaxToolSwings
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }

    $setupRequest = [ordered]@{} + $baseRequest
    $setupRequest.queue_item_id = "runtime-clear-obstacle-smoke.setup"
    $setupRequest.option_id = "debug.setup_clear_obstacle"
    $setupRequest.request_nonce = [guid]::NewGuid().ToString("N")
    $setupRequest.created_at = [DateTimeOffset]::UtcNow.ToString("O")
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120

    $readySnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30

    $clearRequest = [ordered]@{} + $baseRequest
    $clearRequest.queue_item_id = "runtime-clear-obstacle-smoke.clear"
    $clearRequest.before_state_hash = $readySnapshot.state_hash
    $clearRequest.option_id = "executor.clear_obstacle"
    $clearRequest.request_nonce = [guid]::NewGuid().ToString("N")
    $clearRequest.created_at = [DateTimeOffset]::UtcNow.ToString("O")
    $clearResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $clearRequest -TimeoutSeconds 120

    $afterSnapshot = Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -Headers @{ "Accept" = "application/json" } -TimeoutSec 10

    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $clearResult.status -eq "applied" -and $clearResult.primitive_verification_status -eq "verified") { "passed" } else { "unexpected_result" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        smapi_process_id = $process.Id
        bridge_state_hash_before = $beforeSnapshot.state_hash
        bridge_state_hash_ready = $readySnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        target_tile = "$TargetTileX,$TargetTileY"
        executor_health = $executorHealth
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        setup_block_reasons = @($setupResult.block_reasons)
        clear_status = $clearResult.status
        clear_verification = $clearResult.primitive_verification_status
        clear_reasons = @($clearResult.primitive_verification_reasons)
        clear_block_reasons = @($clearResult.block_reasons)
        clear_observed_effect = $clearResult.observed_effect
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $beforeSnapshot
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-ready.json") $readySnapshot
    Write-JsonFile (Join-Path $runDirectory "clear-request.json") $clearRequest
    Write-JsonFile (Join-Path $runDirectory "clear-result.json") $clearResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary

    $summary | ConvertTo-Json -Depth 32
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
