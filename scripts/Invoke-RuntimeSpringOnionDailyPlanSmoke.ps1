param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-spring-onion-daily-plan-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-spring-onion-daily-plan",
    [int] $BackendPort = 5134,
    [int] $StartupTimeoutSeconds = 180,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([Parameter(Mandatory = $true)] [string] $Path, [Parameter(Mandatory = $true)] $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([Parameter(Mandatory = $true)] [string] $Url, [Parameter(Mandatory = $true)] $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 64
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPostRaw {
    param([Parameter(Mandatory = $true)] [string] $Url, [Parameter(Mandatory = $true)] [string] $Json, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $Json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([Parameter(Mandatory = $true)] [string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([Parameter(Mandatory = $true)] [string] $Url, [Parameter(Mandatory = $true)] [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
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

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    if ($null -eq $Snapshot.state) { return $null }
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Find-SpringOnion {
    param($Snapshot)
    return @(Read-FieldValue $Snapshot "current_location" "crops") |
        Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY -and
            [bool]$_.ready_for_harvest -and
            [bool]$_.forage_crop -and
            [string]$_.forage_crop_id -eq "1" -and
            [string]$_.harvest_item_id -eq "399" -and
            [string]$_.harvest_item_qualified_id -eq "(O)399" -and
            [string]$_.harvest_item_projection_status -eq "exact_from_decompiled_native_spring_onion_branch" -and
            [string]$_.harvest_method -eq "Grab" -and
            [string]$_.harvest_experience_skill_id -eq "foraging" -and
            [int]$_.harvest_experience_on_success_min -eq 3 -and
            [int]$_.harvest_experience_on_success_max -eq 3 -and
            [string]$_.harvest_experience_projection_status -eq "exact_from_decompiled_native_harvest"
        } |
        Select-Object -First 1
}

function Find-CropAtTarget {
    param($Snapshot)
    return @(Read-FieldValue $Snapshot "current_location" "crops") |
        Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY
        } |
        Select-Object -First 1
}

function Get-InventoryCount {
    param($Snapshot, [string] $QualifiedItemId)
    $count = 0
    foreach ($item in @(Read-FieldValue $Snapshot "player" "inventory")) {
        if ([string]$item.qualified_item_id -eq $QualifiedItemId) {
            $count += [int]$item.stack
        }
    }
    return $count
}

function Get-SkillExperience {
    param($Snapshot, [string] $SkillId)
    $skills = (Read-FieldValue $Snapshot "player" "skills_detail").skills
    $skill = @($skills | Where-Object { [string]$_.skill_id -eq $SkillId }) | Select-Object -First 1
    if ($null -eq $skill) { return $null }
    return [int]$skill.experience
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds, [switch] $RequireSpringOnion)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $saveReady = $snapshot.save_id.status -in @("available", "derived")
            $timeReady = $snapshot.in_game_time.status -in @("available", "derived")
            $locationId = [string](Read-FieldValue $snapshot "player" "location_id")
            $cropsReady = $null -ne $snapshot.state.current_location.crops -and
                $snapshot.state.current_location.crops.status -in @("available", "derived")
            $inventoryReady = $null -ne $snapshot.state.player.inventory -and
                $snapshot.state.player.inventory.status -in @("available", "derived")
            $skillsReady = $null -ne $snapshot.state.player.skills_detail -and
                $snapshot.state.player.skills_detail.status -in @("available", "derived")
            $springOnion = if ($RequireSpringOnion) { Find-SpringOnion -Snapshot $snapshot } else { $null }
            $lastStatus = "save=$saveReady;time=$timeReady;location=$locationId;crops=$cropsReady;inventory=$inventoryReady;skills=$skillsReady;spring_onion=$($null -ne $springOnion)"
            if ($saveReady -and $timeReady -and $locationId -eq "Farm" -and $cropsReady -and $inventoryReady -and $skillsReady -and
                (-not $RequireSpringOnion -or $null -ne $springOnion)) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for an exact loaded spring-onion world. Last status: $lastStatus"
}

function Read-QueueParameter {
    param($QueueItem, [string] $Name)
    foreach ($parameter in @($QueueItem.normalized_command.parameters)) {
        if ([string]$parameter.name -eq $Name) {
            return [string]$parameter.value
        }
    }
    return ""
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
$snapshotPath = Join-Path $runDirectory "spring-onion-snapshot.json"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
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
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
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
    $initialSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-spring-onion-daily-plan"
        queue_item_id = "runtime-spring-onion-daily-plan.setup"
        before_state_hash = [string]$initialSnapshot.state_hash
        option_id = "debug.setup_harvest_crop_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        rule_key = "spring_onion"
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Spring-onion fixture setup was not applied and verified."
    }

    Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 60 -RequireSpringOnion | Out-Null
    Invoke-WebRequest `
        -UseBasicParsing `
        -Uri $snapshotUrl `
        -Headers @{ "Accept" = "application/json" } `
        -OutFile $snapshotPath `
        -TimeoutSec 60
    $snapshotJson = Get-Content -LiteralPath $snapshotPath -Raw -Encoding UTF8
    $readySnapshot = $snapshotJson | ConvertFrom-Json
    $targetCrop = Find-SpringOnion -Snapshot $readySnapshot
    if ($null -eq $targetCrop) {
        throw "The raw snapshot no longer contains the exact ready native spring onion."
    }
    $inventoryBefore = Get-InventoryCount -Snapshot $readySnapshot -QualifiedItemId "(O)399"
    $foragingExperienceBefore = Get-SkillExperience -Snapshot $readySnapshot -SkillId "foraging"
    Invoke-JsonPostRaw -Url "$backendUrl/api/v1/snapshots" -Json $snapshotJson -TimeoutSeconds 30 | Out-Null

    $availabilityRequest = [ordered]@{
        state_hash = [string]$readySnapshot.state_hash
        candidate_option_ids = @("foraging.harvest_spring_onions")
        candidates = @()
        include_executor_calibration_options = $false
    }
    $availability = Invoke-JsonPost -Url "$backendUrl/api/v1/planner/options/availability" -Body $availabilityRequest -TimeoutSeconds 30
    Write-JsonFile (Join-Path $runDirectory "availability.json") $availability
    $targetCandidates = @(
        $availability.options |
        Where-Object { [string]$_.option_id -eq "foraging.harvest_spring_onions" } |
        ForEach-Object { $_.event_candidates } |
        Where-Object {
            [string]$_.kind -eq "harvest_crop_tile" -and
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY -and
            [string]$_.qualified_item_id -eq "(O)399" -and
            [bool]$_.available
        })
    if ($targetCandidates.Count -ne 1) {
        throw "Expected one available high-level spring-onion candidate; found $($targetCandidates.Count)."
    }
    $targetCandidateId = [string]$targetCandidates[0].candidate_id

    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url "http://127.0.0.1:8767" `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --skip-training `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "foraging.harvest_spring_onions" `
        --daily-plan-candidate-kind "harvest_crop_tile" `
        --daily-plan-candidate-id $targetCandidateId `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items
    if ($LASTEXITCODE -ne 0) {
        throw "LiveTrainingLoop returned exit code $LASTEXITCODE."
    }

    $runRoot = Join-Path $loopRoot (Join-Path "runs" $RunId)
    $dailyPlanPath = Join-Path $runRoot "live-snapshots\daily-plan-response-0001.json"
    $queuePath = Join-Path $runRoot "live-snapshots\compiled-queue-0001.json"
    $executionPath = Join-Path $runRoot "live-snapshots\execution-0001.json"
    foreach ($requiredPath in @($dailyPlanPath, $queuePath, $executionPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Expected high-level runtime artifact is missing: $requiredPath"
        }
    }

    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $harvestItem = @($queue.items) |
        Where-Object {
            [string]$_.option_id -eq "executor.harvest_crop" -and
            [int](Read-QueueParameter $_ "target_tile_x") -eq $TargetTileX -and
            [int](Read-QueueParameter $_ "target_tile_y") -eq $TargetTileY -and
            [string](Read-QueueParameter $_ "harvest_item_qualified_id") -eq "(O)399" -and
            [string](Read-QueueParameter $_ "harvest_item_projection_status") -eq "exact_from_decompiled_native_spring_onion_branch" -and
            [string](Read-QueueParameter $_ "forage_crop") -eq "true" -and
            [string](Read-QueueParameter $_ "forage_crop_id") -eq "1"
        } |
        Select-Object -First 1
    if ($null -eq $harvestItem) {
        throw "High-level DailyPlan did not compile the exact spring onion into executor.harvest_crop."
    }
    $harvestExecution = @($execution.step_results) |
        Where-Object {
            [string]$_.queue_item_id -eq [string]$harvestItem.queue_item_id -and
            [string]$_.option_id -eq "executor.harvest_crop" -and
            [string]$_.status -eq "applied" -and
            [string]$_.primitive_verification_status -eq "verified"
        } |
        Select-Object -First 1
    if ($null -eq $harvestExecution) {
        throw "The compiled spring-onion executor item did not produce an applied/verified native receipt."
    }

    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $afterSnapshot
    if ($null -ne (Find-CropAtTarget -Snapshot $afterSnapshot)) {
        throw "The native spring-onion crop remains at the target tile after verified execution."
    }
    if ([string]$afterSnapshot.state_hash -eq [string]$readySnapshot.state_hash) {
        throw "The bridge state hash did not change after verified spring-onion execution."
    }
    $inventoryAfter = Get-InventoryCount -Snapshot $afterSnapshot -QualifiedItemId "(O)399"
    $foragingExperienceAfter = Get-SkillExperience -Snapshot $afterSnapshot -SkillId "foraging"
    if ($inventoryAfter -le $inventoryBefore) {
        throw "Native spring-onion inventory count did not increase."
    }
    if ($foragingExperienceAfter - $foragingExperienceBefore -ne 3) {
        throw "Native spring-onion Foraging XP did not increase by exactly 3."
    }

    $summary = [ordered]@{
        status = "passed"
        evidence_id = "EVD-335"
        run_id = $RunId
        save_slot = $SaveSlot
        source_option_id = "foraging.harvest_spring_onions"
        selected_candidate_id = $targetCandidateId
        selected_candidate_kind = "harvest_crop_tile"
        compiled_option_id = [string]$harvestItem.option_id
        compiled_queue_item_id = [string]$harvestItem.queue_item_id
        target_location = [string](Read-FieldValue $readySnapshot "player" "location_id")
        target_tile = "$TargetTileX,$TargetTileY"
        target_forage_crop_id = [string]$targetCrop.forage_crop_id
        target_qualified_item_id = [string]$targetCrop.harvest_item_qualified_id
        daily_plan_status = [string]$dailyPlan.action_queue.status
        action_queue_status = [string]$queue.status
        execution_status = [string]$harvestExecution.status
        execution_verification = [string]$harvestExecution.primitive_verification_status
        execution_reasons = @($harvestExecution.primitive_verification_reasons)
        inventory_before = $inventoryBefore
        inventory_after = $inventoryAfter
        foraging_experience_before = $foragingExperienceBefore
        foraging_experience_after = $foragingExperienceAfter
        crop_present_after = $false
        bridge_state_hash_ready = [string]$readySnapshot.state_hash
        bridge_state_hash_after = [string]$afterSnapshot.state_hash
        executor_health = $executorHealth
        daily_plan_path = $dailyPlanPath
        queue_path = $queuePath
        execution_path = $executionPath
        smapi_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
    }
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
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (-not $KeepGameRunning -and $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
