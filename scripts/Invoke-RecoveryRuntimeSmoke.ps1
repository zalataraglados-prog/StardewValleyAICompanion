param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-recovery-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-recovery-smoke",
    [int] $StartupTimeoutSeconds = 120,
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

function Copy-OrderedMap {
    param([Parameter(Mandatory = $true)] $Source)

    $copy = [ordered]@{}
    foreach ($key in $Source.Keys) {
        $copy[$key] = $Source[$key]
    }

    return $copy
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
    $bridgeSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot" -TimeoutSeconds $StartupTimeoutSeconds

    $baseRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-recovery-smoke"
        before_state_hash = $bridgeSnapshot.state_hash
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = ""
        created_at = ""
        max_crops = 512
    }

    $closeRequest = Copy-OrderedMap $baseRequest
    $closeRequest.queue_item_id = "runtime-recovery-smoke.close-menu"
    $closeRequest.option_id = "executor.close_menu"
    $closeRequest.request_nonce = [guid]::NewGuid().ToString("N")
    $closeRequest.created_at = [DateTimeOffset]::UtcNow.ToString("O")
    $closeResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $closeRequest -TimeoutSeconds 120

    $sleepRequest = Copy-OrderedMap $baseRequest
    $sleepRequest.queue_item_id = "runtime-recovery-smoke.sleep"
    $sleepRequest.option_id = "executor.sleep"
    $sleepRequest.request_nonce = [guid]::NewGuid().ToString("N")
    $sleepRequest.created_at = [DateTimeOffset]::UtcNow.ToString("O")
    $sleepResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $sleepRequest -TimeoutSeconds 180

    $afterSnapshot = Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8765/api/v1/snapshot" -Headers @{ "Accept" = "application/json" } -TimeoutSec 10

    $summary = [ordered]@{
        status = if (($closeResult.status -in @("no_op", "applied")) -and $sleepResult.status -eq "applied") { "completed" } elseif ($sleepResult.status -eq "blocked") { "completed_with_sleep_block" } else { "unexpected_result" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        smapi_process_id = $process.Id
        bridge_state_hash_before = $bridgeSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        executor_health = $executorHealth
        close_menu_status = $closeResult.status
        close_menu_verification = $closeResult.primitive_verification_status
        sleep_status = $sleepResult.status
        sleep_verification = $sleepResult.primitive_verification_status
        sleep_block_reasons = @($sleepResult.block_reasons)
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $bridgeSnapshot
    Write-JsonFile (Join-Path $runDirectory "close-menu-result.json") $closeResult
    Write-JsonFile (Join-Path $runDirectory "sleep-result.json") $sleepResult
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
