param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-collection-donation-routes-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-collection-donation-routes",
    [int] $BackendPort = 5132,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 180)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPostRaw {
    param([string] $Url, [string] $Json, [int] $TimeoutSeconds = 180)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $Json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 45)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-Health {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($health.status -eq "ok") {
                return $health
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-FullSnapshot {
    param([int] $TimeoutSeconds, [string] $ExpectedLocation = "")
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $snapshotUrl
            $location = [string]$snapshot.state.player.location_id.value
            $routeGraph = $snapshot.state.locations.route_graph
            $museum = $snapshot.state.world_progress.museum
            $community = $snapshot.state.world_progress.community_center
            $ready =
                $snapshot.save_id.status -in @("available", "derived") -and
                $routeGraph.status -in @("available", "derived") -and
                $museum.status -in @("available", "derived") -and
                $community.status -in @("available", "derived") -and
                ([string]::IsNullOrWhiteSpace($ExpectedLocation) -or
                    $location -eq $ExpectedLocation)
            $lastStatus =
                "location=$location;route=$($routeGraph.status);" +
                "museum=$($museum.status);community=$($community.status)"
            if ($ready) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full snapshot. Last status: $lastStatus"
}

function Read-CandidateParameter {
    param($Candidate, [string] $Name)
    $row = @($Candidate.parameters) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -First 1
    if ($null -eq $row) {
        return ""
    }
    return [string]$row.value
}

function Find-DirectRouteEdge {
    param($Snapshot, [string] $TargetLocation)
    return @($Snapshot.state.locations.route_graph.value.edges) |
        Where-Object {
            [string]$_.from_location -eq "Town" -and
            [string]$_.target_location -eq $TargetLocation -and
            [bool]$_.resolved
        } |
        Sort-Object from_y, from_x |
        Select-Object -First 1
}

function Read-DonationIdentity {
    param($Snapshot, [string] $CaseName)
    if ($CaseName -eq "museum") {
        $museum = $Snapshot.state.world_progress.museum.value
        $candidate = @($museum.donation_candidates) |
            Where-Object {
                [int]$_.slot_index -eq 11 -and
                [string]$_.qualified_item_id -eq "(O)96"
            } |
            Select-Object -First 1
        if ($null -eq $candidate) {
            throw "Museum fixture donation identity is missing."
        }
        return [ordered]@{
            QualifiedItemId = [string]$candidate.qualified_item_id
            BundleDataKey = ""
            IngredientIndex = -1
            DonatedCountBefore = [int]$museum.donated_count
        }
    }

    foreach ($bundle in @(
        $Snapshot.state.world_progress.community_center.value.bundle_rows
    )) {
        $candidate = @($bundle.donation_candidates) |
            Where-Object { [int]$_.inventory_slot_index -eq 11 } |
            Select-Object -First 1
        if ($null -ne $candidate) {
            return [ordered]@{
                QualifiedItemId = [string]$candidate.qualified_item_id
                BundleDataKey = [string]$bundle.bundle_data_key
                IngredientIndex = [int]$candidate.ingredient_index
                DonatedCountBefore = -1
            }
        }
    }
    throw "Community Center fixture donation identity is missing."
}

function Find-RouteCandidate {
    param($Availability, [string] $OptionId, $Identity)
    return @($Availability.options) |
        Where-Object { [string]$_.option_id -eq $OptionId } |
        ForEach-Object { $_.event_candidates } |
        Where-Object {
            [string]$_.kind -eq "route_connector_tile" -and
            [bool]$_.available -and
            [int]$_.slot_index -eq 11 -and
            [string]$_.qualified_item_id -eq
                [string]$Identity.QualifiedItemId -and
            (Read-CandidateParameter `
                -Candidate $_ `
                -Name "continuation.qualified_item_id") -eq
                [string]$Identity.QualifiedItemId -and
            ($Identity.BundleDataKey -eq "" -or
                (Read-CandidateParameter `
                    -Candidate $_ `
                    -Name "continuation.bundle_data_key") -eq
                    [string]$Identity.BundleDataKey) -and
            ($Identity.IngredientIndex -lt 0 -or
                (Read-CandidateParameter `
                    -Candidate $_ `
                    -Name "continuation.bundle_ingredient_index") -eq
                    [string]$Identity.IngredientIndex)
        } |
        Select-Object -First 1
}

function Invoke-DonationRouteCase {
    param(
        [string] $CaseName,
        [string] $SetupOptionId,
        [string] $OptionId,
        [string] $TargetLocation
    )
    $caseRoot = Join-Path $runDirectory $CaseName
    $loopRoot = Join-Path $caseRoot "live-loop"
    $sourceSnapshotPath = Join-Path $caseRoot "source-snapshot.json"
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null

    $initial = Wait-FullSnapshot -TimeoutSeconds 60
    $edge = Find-DirectRouteEdge `
        -Snapshot $initial -TargetLocation $TargetLocation
    if ($null -eq $edge) {
        throw "No resolved direct Town route to $TargetLocation."
    }

    $setup = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "collection-donation-route-fixture"
        queue_item_id = "collection-donation-route-fixture.$CaseName"
        before_state_hash = [string]$initial.state_hash
        option_id = $SetupOptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        location_id = "Town"
        target_tile_x = [int]$edge.from_x
        target_tile_y = [int]$edge.from_y
        inventory_slot_index = 11
    }
    if ($CaseName -eq "museum") {
        $setup.expected_donated_count_before = 0
        $setup.field_guide_quest_present_before = $false
    }
    else {
        $setup.community_center_fixture_case = "ordinary"
    }
    $setupResult = Invoke-JsonPost `
        -Url "$executorUrl/api/v1/training/execute" `
        -Body $setup
    Write-JsonFile (Join-Path $caseRoot "setup-request.json") $setup
    Write-JsonFile (Join-Path $caseRoot "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or
        $setupResult.primitive_verification_status -ne "verified") {
        throw "$CaseName fixture setup failed."
    }

    Start-Sleep -Milliseconds 750
    $beforeTimeSetup = Wait-FullSnapshot `
        -TimeoutSeconds 60 -ExpectedLocation "Town"
    if ([int]$beforeTimeSetup.state.time.time.value -lt 900) {
        $timeRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "collection-donation-route-fixture"
            queue_item_id =
                "collection-donation-route-fixture.$CaseName.time"
            before_state_hash = [string]$beforeTimeSetup.state_hash
            option_id = "debug.advance_time_to"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_time = 900
        }
        $timeResult = Invoke-JsonPost `
            -Url "$executorUrl/api/v1/training/execute" `
            -Body $timeRequest
        Write-JsonFile (Join-Path $caseRoot "time-result.json") $timeResult
        if ($timeResult.status -ne "applied" -or
            $timeResult.primitive_verification_status -ne "verified") {
            throw "$CaseName fixture time setup failed."
        }
        Start-Sleep -Milliseconds 750
    }
    Wait-FullSnapshot `
        -TimeoutSeconds 60 -ExpectedLocation "Town" | Out-Null
    Invoke-WebRequest `
        -UseBasicParsing `
        -Uri $snapshotUrl `
        -Headers @{ "Accept" = "application/json" } `
        -OutFile $sourceSnapshotPath `
        -TimeoutSec 120
    $sourceJson = Get-Content `
        -LiteralPath $sourceSnapshotPath -Raw -Encoding utf8
    $source = $sourceJson | ConvertFrom-Json
    $identity = Read-DonationIdentity `
        -Snapshot $source -CaseName $CaseName
    $ingest = Invoke-JsonPostRaw `
        -Url "$backendUrl/api/v1/snapshots" -Json $sourceJson
    $availability = Invoke-JsonPost `
        -Url "$backendUrl/api/v1/planner/options/availability" `
        -Body ([ordered]@{
            state_hash = [string]$source.state_hash
            candidate_option_ids = @($OptionId)
            candidates = @()
            include_executor_calibration_options = $false
        })
    Write-JsonFile (Join-Path $caseRoot "source-availability.json") `
        $availability
    $routeCandidate = Find-RouteCandidate `
        -Availability $availability `
        -OptionId $OptionId `
        -Identity $identity
    if ($null -eq $routeCandidate) {
        throw "No exact available $CaseName route candidate."
    }

    $loopProject = Join-Path $ProjectRoot (
        "tools\StardewAI.LiveTrainingLoop\" +
        "StardewAI.LiveTrainingLoop.csproj"
    )
    & dotnet run --no-restore --project $loopProject -- `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorUrl `
        --snapshot-file $sourceSnapshotPath `
        --no-manifest `
        --skip-training `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --required-verified-actions 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options $OptionId `
        --daily-plan-candidate-kind "route_connector_tile" `
        --daily-plan-candidate-id ([string]$routeCandidate.candidate_id) `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items *>&1 |
        Set-Content -LiteralPath (Join-Path $caseRoot "live-loop.log") `
            -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "$CaseName LiveTrainingLoop failed with exit $LASTEXITCODE."
    }

    $artifactRoot = Join-Path $loopRoot (
        "runs\$RunId\live-snapshots"
    )
    $first = Get-Content -LiteralPath (
        Join-Path $artifactRoot "execution-0001.json"
    ) -Raw -Encoding utf8 | ConvertFrom-Json
    $routeExecution = @($first.step_results) |
        Where-Object {
            [string]$_.option_id -eq "executor.traverse_connector"
        } |
        Select-Object -First 1
    $terminalOption = if ($CaseName -eq "museum") {
        "executor.donate_museum_item"
    }
    else {
        "executor.donate_community_center_item"
    }
    $terminalExecution = @($first.step_results) |
        Where-Object { [string]$_.option_id -eq $terminalOption } |
        Select-Object -Last 1
    if ($null -eq $routeExecution -or
        [string]$routeExecution.status -ne "applied" -or
        -not ([string]$routeExecution.primitive_verification_status).StartsWith(
            "verified", [StringComparison]::Ordinal) -or
        $null -eq $terminalExecution -or
        [string]$terminalExecution.status -ne "applied" -or
        -not ([string]$terminalExecution.primitive_verification_status).StartsWith(
            "verified", [StringComparison]::Ordinal) -or
        -not [bool]$first.objective_continuation_completed) {
        throw "$CaseName rolling route or terminal receipt was not verified."
    }

    $after = Wait-FullSnapshot `
        -TimeoutSeconds 60 -ExpectedLocation $TargetLocation
    Write-JsonFile (Join-Path $caseRoot "after-snapshot.json") $after
    if ($CaseName -eq "museum") {
        $terminalStateVerified =
            [int]$after.state.world_progress.museum.value.donated_count -eq
                ($identity.DonatedCountBefore + 1)
    }
    else {
        $bundle = @(
            $after.state.world_progress.community_center.value.bundle_rows
        ) | Where-Object {
            [string]$_.bundle_data_key -eq [string]$identity.BundleDataKey
        } | Select-Object -First 1
        $ingredient = @($bundle.ingredients) | Where-Object {
            [int]$_.ingredient_index -eq [int]$identity.IngredientIndex
        } | Select-Object -First 1
        $terminalStateVerified = $null -ne $ingredient -and
            [bool]$ingredient.completed
    }
    if (-not $terminalStateVerified) {
        throw "$CaseName exact terminal transparent state did not change."
    }

    $summary = [ordered]@{
        status = "passed"
        case = $CaseName
        run_id = $RunId
        option_id = $OptionId
        route_candidate_id = [string]$routeCandidate.candidate_id
        source_location = "Town"
        target_location = $TargetLocation
        qualified_item_id = [string]$identity.QualifiedItemId
        bundle_data_key = [string]$identity.BundleDataKey
        bundle_ingredient_index = [int]$identity.IngredientIndex
        connector_status = [string]$routeExecution.status
        connector_verification =
            [string]$routeExecution.primitive_verification_status
        terminal_option_id = $terminalOption
        terminal_status = [string]$terminalExecution.status
        terminal_verification =
            [string]$terminalExecution.primitive_verification_status
        continuation_completed =
            [bool]$first.objective_continuation_completed
        exact_terminal_state_verified = $terminalStateVerified
        source_state_hash = [string]$source.state_hash
        after_state_hash = [string]$after.state_hash
        backend_ingest_state_hash = [string]$ingest.state_hash
    }
    Write-JsonFile (Join-Path $caseRoot "summary.json") $summary
    return $summary
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767"
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
foreach ($port in @($BackendPort, 8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection `
            -State Listen -LocalPort $port `
            -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process `
        -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$runDirectory = Join-Path $ProjectRoot (
    Join-Path $OutputDirectory $RunId
)
$ledgerDirectory = Join-Path $runDirectory "strategy-ledger"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $ledgerDirectory | Out-Null

& (Join-Path $ProjectRoot `
    "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_STRATEGY_LEDGER_DIR",
    "ASPNETCORE_URLS",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] =
        [Environment]::GetEnvironmentVariable($name, "Process")
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
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_STRATEGY_LEDGER_DIR = $ledgerDirectory
    $env:ASPNETCORE_URLS = $backendUrl
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @(
            "run",
            "--no-restore",
            "--project",
            (Join-Path $ProjectRoot `
                "src\StardewAI.Backend\StardewAI.Backend.csproj"),
            "--no-launch-profile"
        ) `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    Wait-Health -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -PassThru
    $executorHealth = Wait-Health `
        -Url "$executorUrl/health" -TimeoutSeconds 120
    Start-Sleep -Seconds 20
    Wait-FullSnapshot -TimeoutSeconds $StartupTimeoutSeconds | Out-Null

    $cases = @(
        (Invoke-DonationRouteCase `
            -CaseName "museum" `
            -SetupOptionId "debug.setup_museum_donation" `
            -OptionId "museum.donate_items" `
            -TargetLocation "ArchaeologyHouse"),
        (Invoke-DonationRouteCase `
            -CaseName "community-center" `
            -SetupOptionId "debug.setup_community_center_donation" `
            -OptionId "community_center.donate_bundle_items" `
            -TargetLocation "CommunityCenter")
    )
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        formal_training_started = $false
        expected_case_count = 2
        passed_case_count = @(
            $cases | Where-Object { $_.status -eq "passed" }
        ).Count
        cases = $cases
        executor_health = $executorHealth
        game_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id `
            -Force -ErrorAction SilentlyContinue
    }
    if (-not $KeepGameRunning -and
        $gameProcess -and
        -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id `
            -Force -ErrorAction SilentlyContinue
    }
}
