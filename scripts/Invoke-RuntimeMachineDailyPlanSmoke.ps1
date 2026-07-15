param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-machine-daily-plan-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-machine-daily-plan-smoke",
    [int] $BackendPort = 5128,
    [int] $StartupTimeoutSeconds = 180,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $QualifiedItemId = "(O)262",
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 64
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
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

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    if ($null -eq $Snapshot.state) { return $null }
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function JsonString([string] $Value) {
    return ($Value | ConvertTo-Json -Compress)
}

function CanonicalJson($Value) {
    if ($null -eq $Value) { return "null" }
    if ($Value -is [string]) { return JsonString $Value }
    if ($Value -is [bool]) { return ($Value | ConvertTo-Json -Compress).ToLowerInvariant() }
    if ($Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [decimal]) {
        return [System.Convert]::ToString($Value, [System.Globalization.CultureInfo]::InvariantCulture)
    }
    if ($Value -is [System.Collections.IDictionary]) {
        $parts = @()
        foreach ($key in ($Value.Keys | Sort-Object)) {
            $parts += (JsonString ([string] $key)) + ":" + (CanonicalJson $Value[$key])
        }
        return "{" + ($parts -join ",") + "}"
    }
    if ($Value -is [pscustomobject]) {
        $map = [ordered]@{}
        foreach ($property in $Value.PSObject.Properties) {
            $map[$property.Name] = $property.Value
        }
        return CanonicalJson $map
    }
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $parts = @()
        foreach ($item in $Value) { $parts += CanonicalJson $item }
        return "[" + ($parts -join ",") + "]"
    }
    return ($Value | ConvertTo-Json -Depth 96 -Compress)
}

function Compute-StateHash($State) {
    $canonical = CanonicalJson $State
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") })
    }
    finally {
        $sha.Dispose()
    }
}

function Find-MachineAtTile {
    param($Snapshot, [int] $X, [int] $Y)
    $machines = Read-FieldValue $Snapshot "farm" "machines"
    foreach ($machine in @($machines)) {
        if ([int]$machine.tile_x -eq $X -and [int]$machine.tile_y -eq $Y) { return $machine }
    }
    return $null
}

function Find-LoadableInput {
    param($Machine, [string] $ItemId)
    if ($null -eq $Machine -or $null -eq $Machine.loadable_inputs) { return $null }
    foreach ($input in @($Machine.loadable_inputs)) {
        if ([string]$input.qualified_item_id -eq $ItemId) { return $input }
    }
    return $null
}

function Read-QueueParameter {
    param($QueueItem, [string] $Name)
    foreach ($parameter in @($QueueItem.normalized_command.parameters)) {
        if ([string]$parameter.name -eq $Name) { return [string]$parameter.value }
    }
    return ""
}

function Wait-FullMachineSnapshot {
    param([string] $Url, [int] $TimeoutSeconds, [int] $X, [int] $Y, [string] $ItemId)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $machine = Find-MachineAtTile -Snapshot $snapshot -X $X -Y $Y
            $input = Find-LoadableInput -Machine $machine -ItemId $ItemId
            $prediction = if ($null -ne $input) { $input.predicted_output } else { $null }
            $predictionOk = $null -ne $prediction -and [string]$prediction.status -eq "available"
            $lastStatus = "save_id=$($snapshot.save_id.status);machine=$($null -ne $machine);input=$($null -ne $input);prediction=$predictionOk"
            if ($saveReadable -and $null -ne $machine -and $null -ne $input -and $predictionOk) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full snapshot with transparent machine input/native prediction. Last status: $lastStatus"
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=training_machine"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
$snapshotPath = Join-Path $runDirectory "full-machine-snapshot.json"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
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
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
}

$gameProcess = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    Wait-JsonHealth -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds | Out-Null

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-daily-plan-smoke"
        queue_item_id = "runtime-machine-daily-plan-smoke.setup"
        before_state_hash = "setup"
        option_id = "debug.setup_machine_input_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        qualified_item_id = $QualifiedItemId
        quantity = 2
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult

    Start-Sleep -Seconds 3
    $snapshot = Wait-FullMachineSnapshot -Url $snapshotUrl -TimeoutSeconds 60 -X $TargetTileX -Y $TargetTileY -ItemId $QualifiedItemId
    Write-JsonFile $snapshotPath $snapshot

    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url "http://127.0.0.1:8767" `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 6 `
        --daily-plan-candidate-options "farm.process_machines" `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop returned exit code $LASTEXITCODE" }

    $reportPath = Join-Path $loopRoot (Join-Path "runs" (Join-Path $RunId "live-training-loop-report.json"))
    $dailyPlanPath = Join-Path $loopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\daily-plan-response-0001.json"))
    $queuePath = Join-Path $loopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\compiled-queue-0001.json"))
    $executionPath = Join-Path $loopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\execution-0001.json"))
    $datasetPath = Join-Path $loopRoot "datasets\live-training-feature-rows.jsonl"
    if (-not (Test-Path -LiteralPath $reportPath)) { throw "Live loop report missing: $reportPath" }
    if (-not (Test-Path -LiteralPath $dailyPlanPath)) { throw "Daily plan response missing: $dailyPlanPath" }
    if (-not (Test-Path -LiteralPath $queuePath)) { throw "Compiled queue missing: $queuePath" }
    if (-not (Test-Path -LiteralPath $executionPath)) { throw "Execution aggregate missing: $executionPath" }
    if (-not (Test-Path -LiteralPath $datasetPath)) { throw "Dataset missing: $datasetPath" }

    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $queueItems = @($queue.items)
    $loadItem = $queueItems | Where-Object { $_.option_id -eq "executor.load_machine_input" } | Select-Object -First 1
    if ($null -eq $loadItem) {
        Write-JsonFile (Join-Path $runDirectory "daily-plan-rejected.json") $dailyPlan
        Write-JsonFile (Join-Path $runDirectory "queue-rejected.json") $queue
        throw "Machine daily-plan did not compile a load_machine_input item."
    }
    $loadExecution = @($execution.step_results) | Where-Object {
        $_.option_id -eq "executor.load_machine_input" -and
        $_.status -eq "applied" -and
        $_.primitive_verification_status -eq "verified"
    } | Select-Object -First 1
    if ($null -eq $loadExecution) {
        Write-JsonFile (Join-Path $runDirectory "execution-rejected.json") $execution
        throw "Live queue did not reach and verify the compiled load_machine_input item."
    }

    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        target_tile = "$TargetTileX,$TargetTileY"
        qualified_item_id = $QualifiedItemId
        setup_status = $setupResult.status
        machine_snapshot_has_loadable_input = $true
        daily_plan_status = $dailyPlan.status
        action_queue_status = $queue.status
        queue_item_count = @($queue.items).Count
        compiled_load_item_id = $loadItem.queue_item_id
        executed_step_count = @($execution.step_results).Count
        verified_execution_status = $loadExecution.status
        verified_execution_reason = @($loadExecution.primitive_verification_reasons)
        dataset_path = $datasetPath
        report_path = $reportPath
        daily_plan_path = $dailyPlanPath
        queue_path = $queuePath
        execution_path = $executionPath
        executor_health = $executorHealth
        smapi_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (-not $KeepGameRunning -and $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
