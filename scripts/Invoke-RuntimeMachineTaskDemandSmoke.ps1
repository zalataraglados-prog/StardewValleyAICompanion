param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-machine-task-demand-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-machine-task-demand",
    [int] $StartupTimeoutSeconds = 120,
    [int] $CompletionTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 10)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") { return $response }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $machinesReadable =
                $null -ne $snapshot.state.farm.machines -and
                $snapshot.state.farm.machines.status -in @("available", "derived")
            if ($snapshot.save_id.status -in @("available", "derived") -and $machinesReadable) {
                return $snapshot
            }
            $lastStatus = "save=$($snapshot.save_id.status);machines=$machinesReadable"
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world snapshot. Last status: $lastStatus"
}

function Find-Machine {
    param($Snapshot, [int] $X, [int] $Y)
    foreach ($machine in @($Snapshot.state.farm.machines.value)) {
        if ([int]$machine.tile_x -eq $X -and [int]$machine.tile_y -eq $Y) {
            return $machine
        }
    }
    return $null
}

function Wait-LoadableMachine {
    param([string] $Url, [int] $X, [int] $Y, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url -TimeoutSeconds 10
        $machine = Find-Machine -Snapshot $snapshot -X $X -Y $Y
        if ($null -ne $machine -and @($machine.loadable_inputs).Count -gt 0) {
            return [pscustomobject]@{
                snapshot = $snapshot
                machine = $machine
                input = @($machine.loadable_inputs)[0]
            }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Machine input projection did not become available."
}

function Wait-MachineReady {
    param([string] $Url, [int] $X, [int] $Y, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url -TimeoutSeconds 10
        $machine = Find-Machine -Snapshot $snapshot -X $X -Y $Y
        if ($null -ne $machine -and [bool]$machine.ready_for_harvest -and $null -ne $machine.held_item) {
            return [pscustomobject]@{ snapshot = $snapshot; machine = $machine }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Natural Charcoal Kiln processing did not complete in time."
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slot found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $source = Join-Path (Join-Path $gameDir "Mods") $modName
    $target = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force
}

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    SMAPI_MODS_PATH = $env:SMAPI_MODS_PATH
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
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir `
        -WindowStyle Hidden -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30 | Out-Null
    $snapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    $caseResults = @()
    foreach ($case in @(
        [pscustomobject]@{ name = "ordinary"; family = "ordinary_quest"; id = "960001" },
        [pscustomobject]@{ name = "special"; family = "special_order"; id = "MachineTaskOrder" }
    )) {
        $setupMachine = [ordered]@{
            schema_version = "training_execution_request.v1"; run_id = $RunId
            queue_id = "machine-task-demand"; queue_item_id = "$($case.name).setup-machine"
            before_state_hash = $snapshot.state_hash; option_id = "debug.setup_machine_input_target"
            execution_mode = "training_singleplayer"; actor = "training_farmer.main"
            save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            location_id = "Farm"; target_tile_x = $TargetTileX; target_tile_y = $TargetTileY
            expected_shop_id = "(BC)114"; qualified_item_id = "(O)388"; quantity = 10
        }
        $machineSetupResult = Invoke-JsonPost -Url $executeUrl -Body $setupMachine
        if ($machineSetupResult.status -ne "applied") {
            throw "$($case.name) machine fixture failed: $(@($machineSetupResult.block_reasons) -join ',')"
        }
        $loadable = Wait-LoadableMachine -Url $snapshotUrl -X $TargetTileX -Y $TargetTileY -TimeoutSeconds 30
        $prediction = $loadable.input.predicted_output
        $outputId = [string]$prediction.item.qualified_item_id
        if ($outputId -ne "(O)382" -or [int]$prediction.additional_consumed_item_count -ne 0) {
            throw "$($case.name) projection was not exact zero-additional-consumption Coal."
        }

        $setupTask = [ordered]@{
            schema_version = "training_execution_request.v1"; run_id = $RunId
            queue_id = "machine-task-demand"; queue_item_id = "$($case.name).setup-task"
            before_state_hash = $loadable.snapshot.state_hash; option_id = "debug.setup_collection_task_fixture"
            execution_mode = "training_singleplayer"; actor = "training_farmer.main"
            save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            quest_family = $case.family; quest_id = $case.id; qualified_item_id = $outputId
            quest_expected_target_count = 1
        }
        $taskSetupResult = Invoke-JsonPost -Url $executeUrl -Body $setupTask
        if ($taskSetupResult.status -ne "applied") {
            throw "$($case.name) task fixture failed: $(@($taskSetupResult.block_reasons) -join ',')"
        }
        $beforeLoad = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $loadRequest = [ordered]@{
            schema_version = "training_execution_request.v1"; run_id = $RunId
            queue_id = "machine-task-demand"; queue_item_id = "$($case.name).load"
            before_state_hash = $beforeLoad.state_hash; option_id = "executor.load_machine_input"
            execution_mode = "training_singleplayer"; actor = "training_farmer.main"
            save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            location_id = "Farm"; target_tile_x = $TargetTileX; target_tile_y = $TargetTileY
            input_slot_index = [int]$loadable.input.slot_index
            qualified_item_id = [string]$loadable.input.qualified_item_id
            predicted_output_qualified_item_id = $outputId
            predicted_output_context_tags_json = (@($prediction.output_context_tags) | ConvertTo-Json -Compress)
            predicted_output_additional_consumed_item_count = 0
            machine_prediction_training_kind = "exact"
            quest_candidate_id = "runtime_fixture:$($case.id)"; quest_family = $case.family
            quest_id = $(if ($case.family -eq "ordinary_quest") { $case.id } else { "" })
            quest_key = $(if ($case.family -eq "special_order") { $case.id } else { "" })
            quest_runtime_type = $(if ($case.family -eq "ordinary_quest") { "ResourceCollectionQuest" } else { "CollectObjective" })
            quest_objective_index = $(if ($case.family -eq "special_order") { 0 } else { $null })
            quest_expected_current_count = 0; quest_expected_target_count = 1
            quest_acquisition_source_step = $true; quest_acquisition_target_step = $false
        }
        $loadResult = Invoke-JsonPost -Url $executeUrl -Body $loadRequest
        if ($loadResult.status -ne "applied" -or $loadResult.quest_progress_after -ne 0) {
            throw "$($case.name) native task-bound load failed: $(@($loadResult.block_reasons) -join ',')"
        }

        $ready = Wait-MachineReady -Url $snapshotUrl -X $TargetTileX -Y $TargetTileY `
            -TimeoutSeconds $CompletionTimeoutSeconds
        $collectRequest = [ordered]@{
            schema_version = "training_execution_request.v1"; run_id = $RunId
            queue_id = "machine-task-demand"; queue_item_id = "$($case.name).collect"
            before_state_hash = $ready.snapshot.state_hash; option_id = "executor.collect_machine_output"
            execution_mode = "training_singleplayer"; actor = "training_farmer.main"
            save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            location_id = "Farm"; target_tile_x = $TargetTileX; target_tile_y = $TargetTileY
            qualified_item_id = $outputId
            expected_skill_experience_deltas_json = [string]$ready.machine.harvest_experience_deltas_json
            expected_mastery_experience_delta = [int]$ready.machine.harvest_mastery_experience_delta
            quest_candidate_id = "runtime_fixture:$($case.id)"; quest_family = $case.family
            quest_id = $(if ($case.family -eq "ordinary_quest") { $case.id } else { "" })
            quest_key = $(if ($case.family -eq "special_order") { $case.id } else { "" })
            quest_runtime_type = $(if ($case.family -eq "ordinary_quest") { "ResourceCollectionQuest" } else { "CollectObjective" })
            quest_objective_index = $(if ($case.family -eq "special_order") { 0 } else { $null })
            quest_expected_current_count = 0; quest_expected_target_count = 1
            quest_acquisition_source_step = $false; quest_acquisition_target_step = $true
        }
        $collectResult = Invoke-JsonPost -Url $executeUrl -Body $collectRequest
        $passed = $collectResult.status -eq "applied" -and
            [int]$collectResult.quest_progress_before -eq 0 -and
            [int]$collectResult.quest_progress_after -eq 1
        Write-JsonFile (Join-Path $runDirectory "$($case.name)-setup-machine.json") $machineSetupResult
        Write-JsonFile (Join-Path $runDirectory "$($case.name)-setup-task.json") $taskSetupResult
        Write-JsonFile (Join-Path $runDirectory "$($case.name)-load.json") $loadResult
        Write-JsonFile (Join-Path $runDirectory "$($case.name)-ready.json") $ready.snapshot
        Write-JsonFile (Join-Path $runDirectory "$($case.name)-collect.json") $collectResult
        $caseResults += [pscustomobject]@{
            name = $case.name; family = $case.family; input = "(O)388"; output = $outputId
            load_status = $loadResult.status; progress_after_load = $loadResult.quest_progress_after
            collect_status = $collectResult.status; progress_after_collect = $collectResult.quest_progress_after
            result = $(if ($passed) { "passed" } else { "failed" })
        }
        if (-not $passed) {
            throw "$($case.name) native collection did not advance the exact task."
        }
        $snapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    }

    $summary = [ordered]@{
        status = $(if (@($caseResults | Where-Object result -ne "passed").Count -eq 0) { "passed" } else { "failed" })
        run_id = $RunId; save_slot = $SaveSlot; machine = "(BC)114"; input = "(O)388"
        output = "(O)382"; cases = $caseResults
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    if ($summary.status -ne "passed") { throw "Machine task demand smoke failed." }
    Write-Output (Join-Path $runDirectory "summary.json")
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Wait-Process -Id $process.Id -Timeout 15 -ErrorAction SilentlyContinue
    }
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("Env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("Env:" + $entry.Key) -Value $entry.Value
        }
    }
}
