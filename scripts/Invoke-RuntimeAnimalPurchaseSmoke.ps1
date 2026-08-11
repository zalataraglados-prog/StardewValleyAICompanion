[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-animal-purchase-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 5133,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10
            if ($null -ne $value) { return $value }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([int] $TimeoutSeconds = 180) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $lastStatus = "save=$($snapshot.save_id.status);player=$($snapshot.state.player.location_id.status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.player.location_id.status -in @("available", "derived")) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a world-ready snapshot. Last status: $lastStatus"
}

function Wait-PurchaseSnapshot([int] $TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot 15
        $menuType = [string]$snapshot.state.menus.active_menu.value.type
        $catalogStatus = [string]$snapshot.state.farm.animal_purchase_catalog.status
        $lastStatus = "menu=$menuType;catalog=$catalogStatus;location=$($snapshot.state.player.location_id.value)"
        if ($menuType -eq "PurchaseAnimalsMenu" -and $catalogStatus -in @("available", "derived")) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for transparent animal purchase state. Last status: $lastStatus"
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorBaseUrl = "http://127.0.0.1:8767"
$executorUrl = "$executorBaseUrl/api/v1/training/execute"

if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI missing: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Runtime animal purchase smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-animal-purchase\" + $RunId)
$loopRoot = Join-Path $artifactDirectory "loop"
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") `
    -c Release --no-restore --nologo -p:GamePath="$gameDirectory" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop Release build failed." }

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "ASPNETCORE_URLS")
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $artifactDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" -ArgumentList @(
        "run", "--no-restore", "--project",
        (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile") -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru
    Wait-Json "$executorBaseUrl/health" 45 | Out-Null
    $initial = Wait-WorldSnapshot $StartupTimeoutSeconds

    $fixtureRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-animal-purchase-fixture"
        queue_item_id = "$RunId.fixture"
        before_state_hash = [string]$initial.state_hash
        option_id = "debug.setup_animal_purchase"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        animal_type_id = "White Chicken"
    }
    $fixture = Invoke-JsonPost $executorUrl $fixtureRequest
    Write-Json (Join-Path $artifactDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Animal purchase fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $before = Wait-PurchaseSnapshot 60
    $snapshotPath = Join-Path $artifactDirectory "before-snapshot.json"
    Write-Json $snapshotPath $before
    Invoke-RestMethod -Method Post -Uri "$backendUrl/api/v1/snapshots" `
        -ContentType "application/json; charset=utf-8" -Body (Get-Content -LiteralPath $snapshotPath -Raw) `
        -TimeoutSec 120 | Out-Null
    $availability = Invoke-JsonPost "$backendUrl/api/v1/planner/options/availability" ([ordered]@{
        state_hash = [string]$before.state_hash
        candidate_option_ids = @("animals.purchase")
        candidates = @()
        include_executor_calibration_options = $true
    })
    Write-Json (Join-Path $artifactDirectory "availability.json") $availability
    $candidate = @($availability.options | Where-Object { $_.option_id -eq "animals.purchase" } |
        ForEach-Object { $_.event_candidates } | Where-Object {
            [bool]$_.available -and [string]$_.kind -eq "purchase_animal"
        }) | Select-Object -First 1
    if ($null -eq $candidate) { throw "No available terminal animals.purchase candidate was compiled." }

    & dotnet (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll") `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorBaseUrl `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --skip-training `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options animals.purchase `
        --daily-plan-candidate-kind purchase_animal `
        --daily-plan-candidate-id ([string]$candidate.candidate_id) `
        --after-snapshot-wait-ms 1000
    if ($LASTEXITCODE -ne 0) { throw "Animal purchase LiveTrainingLoop failed with exit code $LASTEXITCODE." }

    $snapshotDirectory = Join-Path $loopRoot "runs\$RunId\live-snapshots"
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $queueItem = @($queue.items | Where-Object { $_.option_id -eq "executor.purchase_animal" }) | Select-Object -First 1
    $stepResult = @($execution.step_results | Where-Object { $_.option_id -eq "executor.purchase_animal" }) | Select-Object -First 1
    $after = Wait-WorldSnapshot 30
    Write-Json (Join-Path $artifactDirectory "after-snapshot.json") $after
    $passed = $null -ne $queueItem -and $null -ne $stepResult -and
        $stepResult.status -eq "applied" -and
        $stepResult.primitive_verification_status -eq "verified" -and
        @($stepResult.primitive_verification_reasons) -contains "native_PurchaseAnimalsMenu_stock_home_and_name_controls_used" -and
        @($stepResult.primitive_verification_reasons) -contains "exact_new_animal_type_owner_home_name_occupancy_and_money_receipt_verified"
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_animal_purchase_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        evidence_id = "EVD-247"
        run_id = $RunId
        save_slot = $SaveSlot
        candidate_id = [string]$candidate.candidate_id
        queue_option_id = [string]$queueItem.option_id
        execution_status = [string]$stepResult.status
        verification_status = [string]$stepResult.primitive_verification_status
        verification_reasons = @($stepResult.primitive_verification_reasons)
        observed_effect = [string]$stepResult.observed_effect
        final_location = [string]$after.state.player.location_id.value
        queue_path = $queuePath
        execution_path = $executionPath
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if (-not $passed) { throw "Runtime animal purchase smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
