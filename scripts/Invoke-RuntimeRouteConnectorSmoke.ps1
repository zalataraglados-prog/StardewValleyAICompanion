param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-route-connector-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-route-connector-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $Value | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $Path -Encoding utf8
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
            $locationReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "player" -and
                $snapshot.state.player.PSObject.Properties.Name -contains "location_id") {
                $locationReadable = $snapshot.state.player.location_id.status -in @("available", "derived")
            }

            $lastStatus = "location_id_readable=$locationReadable;completeness=$($snapshot.completeness)"
            if ($locationReadable) {
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

function Read-FieldValue {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field
    )

    if ($null -eq $Snapshot.state) {
        return $null
    }

    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) {
        return $null
    }

    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) {
        return $null
    }

    return $fieldNode.value
}

function Find-FarmHouseExitConnector {
    param([Parameter(Mandatory = $true)] $Snapshot)

    $warps = Read-FieldValue $Snapshot "current_location" "warps"
    if ($null -eq $warps) {
        return $null
    }

    return @($warps | Where-Object {
        (([string]$_.target_location) -eq "Farm") -or (([string]$_.target_name) -eq "Farm")
    } | Sort-Object y, x | Select-Object -First 1)[0]
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
    $before = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot" -TimeoutSeconds $StartupTimeoutSeconds
    $location = Read-FieldValue $before "player" "location_id"
    $connector = Find-FarmHouseExitConnector $before

    if ($location -ne "FarmHouse" -or $null -eq $connector) {
        $summary = [ordered]@{
            status = "skipped_no_farmhouse_farm_connector"
            run_id = $RunId
            save_slot = $SaveSlot
            location = $location
            reason = "expected current FarmHouse snapshot with a Farm warp connector"
            executor_health = $executorHealth
            kept_game_running = [bool]$KeepGameRunning
        }
        Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $before
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        $summary | ConvertTo-Json -Depth 32
        return
    }

    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-connector-smoke"
        queue_item_id = "runtime-route-connector-smoke.farmhouse-to-farm"
        before_state_hash = $before.state_hash
        option_id = "executor.traverse_connector"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        target_tile_x = [int]$connector.x
        target_tile_y = [int]$connector.y
        connector_kind = "warp"
        expected_target_location = if ([string]::IsNullOrWhiteSpace([string]$connector.target_location)) { [string]$connector.target_name } else { [string]$connector.target_location }
        expected_arrival_tile_x = [int]$connector.target_x
        expected_arrival_tile_y = [int]$connector.target_y
    }

    $result = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 180
    Start-Sleep -Milliseconds 500
    $after = Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8765/api/v1/snapshot" -Headers @{ "Accept" = "application/json" } -TimeoutSec 10

    $summary = [ordered]@{
        status = if ($result.status -eq "applied" -and $result.primitive_verification_status -eq "verified") { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        connector = $connector
        result_status = $result.status
        primitive_verification_status = $result.primitive_verification_status
        primitive_verification_reasons = @($result.primitive_verification_reasons)
        block_reasons = @($result.block_reasons)
        before_location = $location
        after_location = (Read-FieldValue $after "player" "location_id")
        before_state_hash = $before.state_hash
        after_state_hash = $after.state_hash
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $before
    Write-JsonFile (Join-Path $runDirectory "traverse-connector-request.json") $request
    Write-JsonFile (Join-Path $runDirectory "traverse-connector-result.json") $result
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after.json") $after
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32

    if ($summary.status -ne "passed") {
        throw "Route connector smoke failed."
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
