param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $RunId = (
        "runtime-fresh-save-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 15)
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
    [pscustomobject]@{
        raw = [string]$response.Content
        value = $response.Content | ConvertFrom-Json
    }
}

function Wait-Json {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            return Invoke-JsonGet -Url $Url -TimeoutSeconds 8
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-FreshWorld {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $snapshotUrl
            $snapshot = $response.value
            $location = [string](Read-FieldValue `
                $snapshot "player" "location_id")
            $season = [string](Read-FieldValue $snapshot "time" "season")
            $day = [int](Read-FieldValue $snapshot "time" "day")
            $progress = Read-FieldValue `
                $snapshot "world_progress" "full_shipment_progress"
            if (-not [string]::IsNullOrWhiteSpace($location) -and
                $season -eq "spring" -and
                $day -eq 1 -and
                $null -ne $progress -and
                [int]$progress.eligible_item_count -eq 154 -and
                [int]$progress.shipped_eligible_item_count -eq 0 -and
                [int]$progress.missing_item_count -eq 154) {
                return $response
            }
            $lastState =
                "location=$location;season=$season;day=$day;" +
                "eligible=$($progress.eligible_item_count);" +
                "shipped=$($progress.shipped_eligible_item_count)"
        }
        catch { $lastState = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for native fresh world. Last state: $lastState"
}

function Wait-NativeSaveSlot {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_checked"
    while ((Get-Date) -lt $deadline) {
        $slots = @(Get-ChildItem -LiteralPath $isolatedSavesPath -Directory)
        if ($slots.Count -eq 1) {
            $saveData = Join-Path $slots[0].FullName $slots[0].Name
            $saveInfo = Join-Path $slots[0].FullName "SaveGameInfo"
            if ((Test-Path -LiteralPath $saveData -PathType Leaf) -and
                (Test-Path -LiteralPath $saveInfo -PathType Leaf)) {
                return $slots[0]
            }
            $lastState = "slot_exists_without_complete_native_files"
        }
        else {
            $lastState = "slot_count=$($slots.Count)"
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for native save commit. Last state: $lastState"
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorHealthUrl = "http://127.0.0.1:8767/health"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) {
    throw "SMAPI executable not found: $smapi"
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen `
            -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Fresh-save smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" `
        -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot (
    "artifacts\runtime-fresh-save\" + $RunId)
$isolatedSavesPath = Join-Path $artifactDirectory (
    "fresh-save-" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
$smokeModsPath = Join-Path $artifactDirectory "smoke-mods"
New-Item -ItemType Directory -Path $isolatedSavesPath | Out-Null
New-Item -ItemType Directory -Path $trainingOutputDirectory | Out-Null
New-Item -ItemType Directory -Path $smokeModsPath | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$loadedModAllowlist = @(
    "StardewAI.TransparentBridge",
    "StardewAI.RuntimeTestHarness"
)
foreach ($modName in $loadedModAllowlist) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    if (-not (Test-Path -LiteralPath $sourceMod -PathType Container)) {
        throw "Required fresh-save smoke mod is missing: $sourceMod"
    }
    New-Item -ItemType Directory -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod `
        -Recurse -Force
}

$harnessConfigPath = Join-Path (
    Join-Path $smokeModsPath "StardewAI.RuntimeTestHarness"
) "config.json"
if (Test-Path -LiteralPath $harnessConfigPath -PathType Leaf) {
    $harnessConfig = Get-Content -LiteralPath $harnessConfigPath -Raw |
        ConvertFrom-Json
    $harnessConfig.SlotName = ""
    [IO.File]::WriteAllText(
        $harnessConfigPath,
        ($harnessConfig | ConvertTo-Json -Depth 32),
        [Text.UTF8Encoding]::new($false))
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_TEST_CREATE_FRESH_SAVE",
    "STARDEWAI_TEST_FRESH_PLAYER_NAME",
    "STARDEWAI_TEST_FRESH_FARM_NAME",
    "STARDEWAI_TEST_FRESH_FAVORITE_THING",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] =
        [Environment]::GetEnvironmentVariable($name)
}

$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $isolatedSavesPath
    $env:STARDEWAI_TEST_SLOT = $null
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_TEST_CREATE_FRESH_SAVE = "true"
    $env:STARDEWAI_TEST_FRESH_PLAYER_NAME = "StardewAI"
    $env:STARDEWAI_TEST_FRESH_FARM_NAME = "ProofFarm"
    $env:STARDEWAI_TEST_FRESH_FAVORITE_THING = "Parsnip"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSavesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $game = Start-Process -FilePath $smapi `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "game.stderr.log") -PassThru
    Wait-Json -Url $executorHealthUrl `
        -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
    $snapshotResponse = Wait-FreshWorld `
        -TimeoutSeconds $StartupTimeoutSeconds
    $saveSlot = Wait-NativeSaveSlot -TimeoutSeconds 60
    $snapshotPath = Join-Path $artifactDirectory "fresh-snapshot.json"
    [System.IO.File]::WriteAllText(
        $snapshotPath,
        $snapshotResponse.raw,
        [System.Text.UTF8Encoding]::new($false))

    $achievements = @(Read-FieldValue `
        $snapshotResponse.value "world_progress" "achievements")
    if ($achievements -contains 34) {
        throw "Fresh save unexpectedly has Full Shipment achievement 34."
    }

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_fresh_save_smoke.v1"
        status = "passed"
        run_id = $RunId
        game_version = [string]$snapshotResponse.value.game_version
        state_hash = [string]$snapshotResponse.value.state_hash
        save_id = [string]$snapshotResponse.value.save_id.value
        native_save_slot = $saveSlot.Name
        isolated_save_root = $isolatedSavesPath
        season = [string](Read-FieldValue `
            $snapshotResponse.value "time" "season")
        day = [int](Read-FieldValue `
            $snapshotResponse.value "time" "day")
        full_shipment_eligible_item_count = 154
        full_shipment_shipped_item_count = 0
        full_shipment_missing_item_count = 154
        achievement_34 = $false
        loaded_mod_allowlist = $loadedModAllowlist
        smapi_mods_path = $smokeModsPath
        snapshot_path = $snapshotPath
    }
    $summary | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $artifactDirectory `
            "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    if (-not $KeepGameRunning -and
        $null -ne $game -and
        -not $game.HasExited) {
        Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
        $game.WaitForExit(10000) | Out-Null
    }
}
