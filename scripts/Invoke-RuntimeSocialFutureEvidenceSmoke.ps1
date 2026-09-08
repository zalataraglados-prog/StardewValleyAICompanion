[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$SaveSlot = "自动化_442159967",
    [string]$RunId = ("runtime-social-future-evidence-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int]$StartupTimeoutSeconds = 180,
    [int]$MaximumFutureSnapshotBuildMilliseconds = 3000,
    [int]$MaximumFutureSnapshotBytes = 52428800
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-SaveFingerprint {
    param([string]$Path)

    $root = [System.IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    $lines = Get-ChildItem -LiteralPath $Path -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
            if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Save fingerprint escaped source root: $fullPath"
            }
            $relativePath = $fullPath.Substring($root.Length)
            $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
            $relativePath + "|" + $hash
        }
    $payload = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        [BitConverter]::ToString($sha256.ComputeHash($payload)).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Wait-WorldSnapshot {
    param([string]$Url, [int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -Headers @{ Accept = "application/json" } -TimeoutSec 15
            $snapshot = $response.Content | ConvertFrom-Json
            $lastStatus = "save=$($snapshot.save_id.status);time=$($snapshot.state.time.time.status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.time.time.status -in @("available", "derived")) {
                return [pscustomobject]@{
                    Raw = $response.Content
                    Parsed = $snapshot
                }
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for isolated world snapshot. Last status: $lastStatus"
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$sourceSavesRoot = Join-Path $RuntimeRoot "saves"
$sourceSavePath = Join-Path $sourceSavesRoot $SaveSlot
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if (-not (Test-Path -LiteralPath $sourceSavePath -PathType Container)) {
    throw "Isolated source save not found: $sourceSavePath"
}
if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort 8765 -ErrorAction SilentlyContinue)) {
    throw "Port 8765 is already listening. Refusing to attach to an existing bridge."
}
if ($null -ne (Get-Process -Name @("StardewModdingAPI", "Stardew Valley") -ErrorAction SilentlyContinue)) {
    throw "A Stardew process is already running. Refusing to attach or stop it."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-social-future-evidence-smoke\" + $RunId)
$cloneRoot = Join-Path $runDirectory "isolated-saves"
$clonePath = Join-Path $cloneRoot $SaveSlot
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
$socialSnapshotPath = Join-Path $runDirectory "social-snapshot.json"
$futureSnapshotPath = Join-Path $runDirectory "social-future-snapshot.json"
$performancePath = Join-Path $runDirectory "performance.json"
$summaryPath = Join-Path $runDirectory "summary.json"
$sourceFingerprintBefore = Get-SaveFingerprint -Path $sourceSavePath
New-Item -ItemType Directory -Force -Path $cloneRoot | Out-Null
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
Copy-Item -LiteralPath $sourceSavePath -Destination $clonePath -Recurse

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory ("Mods\" + $modName)) `
        -Destination (Join-Path $smokeModsPath $modName) -Recurse
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $cloneRoot
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $cloneRoot
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $gameProcess = Start-Process -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    Wait-WorldSnapshot `
        -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=light&fresh=true" `
        -TimeoutSeconds $StartupTimeoutSeconds | Out-Null

    $social = Wait-WorldSnapshot `
        -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=social&fresh=true" `
        -TimeoutSeconds 60
    Write-Utf8NoBom -Path $socialSnapshotPath -Content $social.Raw

    $futureStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $future = Wait-WorldSnapshot `
        -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=social_future&fresh=true" `
        -TimeoutSeconds 120
    $futureStopwatch.Stop()
    Write-Utf8NoBom -Path $futureSnapshotPath -Content $future.Raw

    $performance = Invoke-RestMethod -UseBasicParsing `
        -Uri "http://127.0.0.1:8765/api/v1/performance" `
        -TimeoutSec 15
    Write-Utf8NoBom -Path $performancePath `
        -Content ($performance | ConvertTo-Json -Depth 32)

    $socialHeavyFieldAbsent =
        $null -eq $social.Parsed.state.locations.social_route_date_evidence
    $routeField = $future.Parsed.state.locations.social_route_date_evidence
    $catalogField = $future.Parsed.state.npcs.schedule_catalog
    $locations = @($routeField.value.locations)
    $unsupportedActionTiles = @($locations | ForEach-Object {
        @($_.unsupported_route_action_tiles)
    })
    $unsupportedActionRecords = @($unsupportedActionTiles | ForEach-Object {
        @($_.actions)
    })
    $allMapsExact = $locations.Count -gt 0 -and
        @($locations | Where-Object {
            $_.projection_status -ne "exact_current_date_static_native_walkability"
        }).Count -eq 0
    $futureBytes = [System.Text.Encoding]::UTF8.GetByteCount($future.Raw)
    $futureMetric = @($performance.snapshot_profiles |
        Where-Object { $_.profile -eq "social_future" } |
        Select-Object -Last 1)
    $recordedBuildMs = if ($futureMetric.Count -eq 1) {
        [double]$futureMetric[0].last_build_ms
    }
    else {
        [double]$futureStopwatch.Elapsed.TotalMilliseconds
    }
    $sourceFingerprintAfter = Get-SaveFingerprint -Path $sourceSavePath
    $passed =
        $socialHeavyFieldAbsent -and
        $routeField.status -in @("available", "derived") -and
        $routeField.value.schema_version -eq "social_route_date_evidence.v2" -and
        $catalogField.status -in @("available", "derived") -and
        $routeField.value.projection_status -eq "current_date_static_route_and_gate_evidence_complete_movement_timing_pending" -and
        [int]$routeField.value.location_count -eq $locations.Count -and
        [int](($locations | Measure-Object -Property unsupported_route_action_tile_count -Sum).Sum) -eq
            $unsupportedActionTiles.Count -and
        [int](($locations | Measure-Object -Property unsupported_route_action_record_count -Sum).Sum) -eq
            $unsupportedActionRecords.Count -and
        @($unsupportedActionTiles | Where-Object {
            [int]$_.action_record_count -ne @($_.actions).Count
        }).Count -eq 0 -and
        $allMapsExact -and
        $recordedBuildMs -le $MaximumFutureSnapshotBuildMilliseconds -and
        $futureBytes -le $MaximumFutureSnapshotBytes -and
        $sourceFingerprintBefore -eq $sourceFingerprintAfter

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_social_future_evidence_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        ordinary_social_heavy_field_absent = $socialHeavyFieldAbsent
        future_profile = "social_future"
        capture_total_days = [int]$routeField.value.capture_total_days
        capture_time = [int]$routeField.value.capture_time
        social_route_schema_version = [string]$routeField.value.schema_version
        location_count = $locations.Count
        walkable_tile_count = [int](($locations | Measure-Object -Property static_walkable_tile_count -Sum).Sum)
        unsupported_route_action_tile_count = [int](($locations | Measure-Object -Property unsupported_route_action_tile_count -Sum).Sum)
        unsupported_route_action_record_count = [int](($locations | Measure-Object -Property unsupported_route_action_record_count -Sum).Sum)
        action_gate_count = [int](($locations | ForEach-Object { @($_.action_gates).Count } | Measure-Object -Sum).Sum)
        all_maps_exact = $allMapsExact
        schedule_villager_count = @($catalogField.value.villagers).Count
        future_snapshot_bytes = $futureBytes
        future_snapshot_request_elapsed_ms = [Math]::Round($futureStopwatch.Elapsed.TotalMilliseconds, 3)
        future_snapshot_recorded_build_ms = $recordedBuildMs
        maximum_future_snapshot_build_ms = $MaximumFutureSnapshotBuildMilliseconds
        maximum_future_snapshot_bytes = $MaximumFutureSnapshotBytes
        performance_gate_passed = $recordedBuildMs -le $MaximumFutureSnapshotBuildMilliseconds -and
            $futureBytes -le $MaximumFutureSnapshotBytes
        source_save_fingerprint_before = $sourceFingerprintBefore
        source_save_fingerprint_after = $sourceFingerprintAfter
        source_save_untouched = $sourceFingerprintBefore -eq $sourceFingerprintAfter
        social_snapshot_path = $socialSnapshotPath
        social_future_snapshot_path = $futureSnapshotPath
        performance_path = $performancePath
        isolated_clone_path = $clonePath
    }
    Write-Utf8NoBom -Path $summaryPath `
        -Content ($summary | ConvertTo-Json -Depth 32)
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) {
        throw "Runtime social future evidence smoke failed: $runDirectory"
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
