param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-animal-management-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8792,
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

function Wait-WorldSnapshot([int] $TimeoutSeconds = 120) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $lastStatus = "save=$($snapshot.save_id.status);animals=$($snapshot.state.farm.animals.status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.farm.animals.status -in @("available", "derived")) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for animal snapshot. Last status: $lastStatus"
}

function Invoke-ManagementCase([int] $Ordinal, [string] $AnimalName, [string] $Intent) {
    $caseName = ("{0:D2}-{1}" -f $Ordinal, $Intent)
    $caseDirectory = Join-Path $artifactDirectory $caseName
    $loopRoot = Join-Path $caseDirectory "loop"
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null
    $before = Wait-WorldSnapshot 60
    $animal = @($before.state.farm.animals.value | Where-Object { [string]$_.display_name -eq $AnimalName }) |
        Select-Object -First 1
    if ($null -eq $animal) { throw "Transparent animal missing for $caseName ($AnimalName)." }
    if ([string]$animal.management_query_status -ne "ready") {
        throw "Animal management projection was not ready for ${caseName}: $($animal.management_query_status)"
    }
    $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"
    Write-Json $snapshotPath $before

    $parameters = [System.Collections.Generic.List[string]]::new()
    $parameters.Add("animal_id=$($animal.animal_id)")
    $parameters.Add("management_intent=$Intent")
    $parameters.Add("management_reason=isolated_runtime_four_branch_verification")
    switch ($Intent) {
        "rename" { $parameters.Add("target_name=AIMRenamed") }
        "toggle_reproduction" {
            $target = (-not [bool]$animal.management_allow_reproduction).ToString().ToLowerInvariant()
            $parameters.Add("target_allow_reproduction=$target")
        }
        "move_home" {
            $targetHome = @($animal.management_compatible_move_homes) | Select-Object -First 1
            if ($null -eq $targetHome) { throw "Compatible target home missing for move_home." }
            $parameters.Add("target_home_building_type=$($targetHome.building_type)")
            $parameters.Add("target_home_building_tile_x=$($targetHome.building_tile_x)")
            $parameters.Add("target_home_building_tile_y=$($targetHome.building_tile_y)")
        }
        "sell" { $parameters.Add("confirm_irreversible_sale=true") }
    }

    $loopArguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @(
        (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"),
        "--root", $loopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $executorBaseUrl,
        "--snapshot-file", $snapshotPath,
        "--no-manifest",
        "--run-id", $RunId,
        "--save-isolation-path", $savesPath,
        "--iterations", "1",
        "--skip-training",
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", "animals.manage_animal",
        "--daily-plan-candidate-kind", "manage_animal",
        "--daily-plan-explicit-confirmation",
        "--after-snapshot-wait-ms", "500")) {
        $loopArguments.Add([string]$value)
    }
    foreach ($parameter in $parameters) {
        $loopArguments.Add("--daily-plan-candidate-parameter")
        $loopArguments.Add($parameter)
    }
    $loopOutput = & dotnet $loopArguments
    $loopOutput | Set-Content -LiteralPath (Join-Path $caseDirectory "live-training-loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Animal management case $caseName failed with exit code $LASTEXITCODE." }

    $snapshotDirectory = Join-Path $loopRoot ("runs\" + $RunId + "\live-snapshots")
    $queue = Get-Content -LiteralPath (Join-Path $snapshotDirectory "compiled-queue-0001.json") -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath (Join-Path $snapshotDirectory "execution-0001.json") -Raw | ConvertFrom-Json
    $queueItem = @($queue.items | Where-Object { $_.option_id -eq "executor.manage_animal" }) | Select-Object -First 1
    $result = @($execution.step_results | Where-Object { $_.option_id -eq "executor.manage_animal" }) | Select-Object -Last 1
    $passed = $null -ne $queueItem -and $null -ne $result -and $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified"
    $summary = [ordered]@{
        ordinal = $Ordinal
        intent = $Intent
        animal_id = [string]$animal.animal_id
        status = if ($passed) { "passed" } else { "failed" }
        execution_status = [string]$result.status
        verification_status = [string]$result.primitive_verification_status
        verification_reasons = @($result.primitive_verification_reasons)
        observed_effect = [string]$result.observed_effect
        block_reasons = @($result.block_reasons)
    }
    Write-Json (Join-Path $caseDirectory "summary.json") $summary
    if (-not $passed) { throw "Runtime animal management case failed: $caseDirectory" }
    return [pscustomobject]$summary
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
        throw "Runtime animal management smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-animal-management\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

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
    $fixture = Invoke-JsonPost $executorUrl ([ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId
        queue_id = "runtime-animal-management-fixture"; queue_item_id = "$RunId.fixture"
        before_state_hash = [string]$initial.state_hash; option_id = "debug.setup_animal_management"
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"
        save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    })
    Write-Json (Join-Path $artifactDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Animal management fixture failed: $(@($fixture.block_reasons) -join ',')"
    }
    Start-Sleep -Milliseconds 750

    $cases = @(
        Invoke-ManagementCase 1 "AIMRename" "rename"
        Invoke-ManagementCase 2 "AIMToggle" "toggle_reproduction"
        Invoke-ManagementCase 3 "AIMMove" "move_home"
        Invoke-ManagementCase 4 "AIMSell" "sell"
    )
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_animal_management_smoke.v1"
        status = if (@($cases | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        scope = "native_initial_pet_query_rename_reproduction_move_and_sell_four_branch_chain"
        cases = $cases
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if ($summary.status -ne "passed") { throw "Runtime animal management smoke failed: $artifactDirectory" }
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
