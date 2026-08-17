param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-cooking-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8793,
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

function Wait-WorldReady([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 15
            $location = $snapshot.state.player.location_id
            $lastStatus = "save=$($snapshot.save_id.status);location=$($location.status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $location.status -in @("available", "derived") -and
                -not [string]::IsNullOrWhiteSpace([string]$location.value)) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for loaded world. Last status: $lastStatus"
}

function Wait-CookingSnapshot([string] $SourceKind, [int] $TimeoutSeconds = 90) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 15
            $cooking = $snapshot.state.player.cooking
            $rows = @($cooking.value.rows | Where-Object {
                [string]$_.recipe_name -eq "Fried Egg" -and
                [string]$_.cooking_source_kind -eq $SourceKind -and
                [string]$_.craft_candidate_status -eq "ready_for_native_cooking_page"
            })
            $lastStatus = "save=$($snapshot.save_id.status);cooking=$($cooking.status);rows=$($rows.Count)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $cooking.status -in @("available", "derived") -and $rows.Count -gt 0) {
                return [pscustomobject]@{ snapshot = $snapshot; row = $rows[0] }
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for ready $SourceKind cooking row. Last status: $lastStatus"
}

function Invoke-CookingCase([int] $Ordinal, [string] $SourceKind) {
    $caseName = ("{0:D2}-{1}" -f $Ordinal, $SourceKind)
    $caseDirectory = Join-Path $artifactDirectory $caseName
    $loopRoot = Join-Path $caseDirectory "loop"
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null

    $initial = Wait-Json $snapshotUrl 60
    $fixture = Invoke-JsonPost $executorUrl ([ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId
        queue_id = "runtime-cooking-fixture"; queue_item_id = "$RunId.fixture.$SourceKind"
        before_state_hash = [string]$initial.state_hash; option_id = "debug.setup_cooking_fixture"
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"
        save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O"); cooking_source_kind = $SourceKind
    })
    Write-Json (Join-Path $caseDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Cooking fixture failed for ${SourceKind}: $(@($fixture.block_reasons) -join ',')"
    }

    Start-Sleep -Milliseconds 750
    $ready = Wait-CookingSnapshot $SourceKind 90
    $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"
    Write-Json $snapshotPath $ready.snapshot

    $loopArguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @(
        $loopDll,
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
        "--daily-plan-candidate-options", "crafting.cook_recipe",
        "--daily-plan-candidate-kind", "cook_recipe",
        "--daily-plan-explicit-confirmation",
        "--daily-plan-candidate-parameter", "recipe_name=Fried Egg",
        "--daily-plan-candidate-parameter", "craft_count=1",
        "--daily-plan-candidate-parameter", "cooking_reason=isolated_native_runtime_verification",
        "--daily-plan-candidate-parameter", "cooking_source_id=$($ready.row.cooking_source_id)",
        "--after-snapshot-wait-ms", "500")) {
        $loopArguments.Add([string]$value)
    }
    $loopOutput = & dotnet $loopArguments
    $loopOutput | Set-Content -LiteralPath (Join-Path $caseDirectory "live-training-loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Cooking case $caseName failed with exit code $LASTEXITCODE." }

    $snapshotDirectory = Join-Path $loopRoot ("runs\" + $RunId + "\live-snapshots")
    $queue = Get-Content -LiteralPath (Join-Path $snapshotDirectory "compiled-queue-0001.json") -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath (Join-Path $snapshotDirectory "execution-0001.json") -Raw | ConvertFrom-Json
    $queueItem = @($queue.items | Where-Object { $_.option_id -eq "executor.cook_recipe" }) | Select-Object -First 1
    $result = @($execution.step_results | Where-Object { $_.option_id -eq "executor.cook_recipe" }) | Select-Object -Last 1
    $passed = $null -ne $queueItem -and $null -ne $result -and $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified"
    $summary = [ordered]@{
        ordinal = $Ordinal
        source_kind = $SourceKind
        source_id = [string]$ready.row.cooking_source_id
        output_quality = [int]$ready.row.output_quality
        seasoning_consumed = [bool]$ready.row.seasoning_consumed
        status = if ($passed) { "passed" } else { "failed" }
        execution_status = [string]$result.status
        verification_status = [string]$result.primitive_verification_status
        verification_reasons = @($result.primitive_verification_reasons)
        observed_effect = [string]$result.observed_effect
        block_reasons = @($result.block_reasons)
    }
    Write-Json (Join-Path $caseDirectory "summary.json") $summary
    if (-not $passed) { throw "Runtime cooking case failed: $caseDirectory" }
    return [pscustomobject]$summary
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorBaseUrl = "http://127.0.0.1:8767"
$executorUrl = "$executorBaseUrl/api/v1/training/execute"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI missing: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Runtime cooking smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-cooking\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") `
    -c Release --nologo "-p:GamePath=$gameDirectory" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop Release build failed with exit code $LASTEXITCODE." }

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
    Wait-WorldReady $StartupTimeoutSeconds | Out-Null

    $cases = @(
        Invoke-CookingCase 1 "kitchen"
        Invoke-CookingCase 2 "cookout_kit"
    )
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_cooking_smoke.v1"
        status = if (@($cases | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        scope = "native_kitchen_and_cookout_crafting_page_cooking_chain"
        cases = $cases
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if ($summary.status -ne "passed") { throw "Runtime cooking smoke failed: $artifactDirectory" }
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
