param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-fishing-item-grab-recovery-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-fishing-item-grab-recovery",
    [int] $BackendPort = 5137,
    [int] $StartupTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 64 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 15)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 180)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $json -TimeoutSec $TimeoutSeconds
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

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok" -or
                $response.schema_version -eq "snapshot.v1") {
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
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $location = Read-FieldValue $snapshot "player" "location_id"
            if (-not [string]::IsNullOrWhiteSpace([string]$location)) {
                return $snapshot
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a world snapshot. Last error: $lastError"
}

function Wait-FishingItemGrabSnapshot {
    param(
        [string] $Url,
        [string] $QualifiedItemId,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $menu = Read-FieldValue $snapshot "menus" "menu_specific_state"
            $pending = @($menu.pending_items)
            $target = @($pending | Where-Object {
                [string]$_.qualified_item_id -eq $QualifiedItemId
            })
            $lastState =
                "kind=$($menu.kind);pending=$($pending.Count);target=$($target.Count)"
            if ($menu.kind -eq "fishing_item_grab" -and
                $menu.essential -eq $true -and
                $menu.context_type -eq "StardewValley.Tools.FishingRod" -and
                $pending.Count -eq 1 -and
                $target.Count -eq 1) {
                return $snapshot
            }
        }
        catch {
            $lastState = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Fishing ItemGrabMenu fixture was not transparent. Last state: $lastState"
}

function Get-InventoryItemCount {
    param($Snapshot, [string] $QualifiedItemId)
    $inventory = @(Read-FieldValue $Snapshot "player" "inventory")
    return [int](($inventory | Where-Object {
        $_.is_empty -ne $true -and
        [string]$_.qualified_item_id -eq $QualifiedItemId
    } | Measure-Object -Property stack -Sum).Sum)
}

function New-SetupRequest {
    param(
        [string] $CaseId,
        [string] $BeforeStateHash,
        [string] $SavesPath,
        [string] $QualifiedItemId,
        [int] $Source,
        [bool] $FillInventory
    )
    return [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.setup.$CaseId"
        queue_item_id = "$RunId.setup.$CaseId.item"
        before_state_hash = $BeforeStateHash
        option_id = "debug.setup_fishing_item_grab_reward"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        fishing_reward_menu_source = $Source
        fishing_reward_qualified_item_id = $QualifiedItemId
        fishing_reward_stack_before = 1
        debug_fill_inventory = $FillInventory
    }
}

function Invoke-RecoveryIteration {
    param(
        [string] $LoopRoot,
        [string] $BackendUrl,
        [string] $SnapshotUrl,
        [string] $ExecutorUrl,
        [string] $SavesPath
    )
    dotnet run --no-restore --project `
        (Join-Path $ProjectRoot `
            "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $LoopRoot `
        --backend-url $BackendUrl `
        --bridge-snapshot-url $SnapshotUrl `
        --executor-url $ExecutorUrl `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $SavesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "recovery.stabilize_day" `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items
    if ($LASTEXITCODE -ne 0) {
        throw "Fishing reward recovery loop returned exit code $LASTEXITCODE"
    }
}

function Read-LoopArtifacts {
    param([string] $LoopRoot)
    $snapshotRoot = Join-Path $LoopRoot `
        ("runs\" + $RunId + "\live-snapshots")
    $paths = [ordered]@{
        Queue = Join-Path $snapshotRoot "compiled-queue-0001.json"
        Execution = Join-Path $snapshotRoot "execution-0001.json"
        Before = Join-Path $snapshotRoot "before-snapshot-0001.json"
        After = Join-Path $snapshotRoot "after-snapshot-0001.json"
    }
    foreach ($path in $paths.Values) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing fishing reward recovery artifact: $path"
        }
    }
    return [pscustomobject]@{
        Queue = Get-Content -LiteralPath $paths.Queue -Raw | ConvertFrom-Json
        Execution = Get-Content -LiteralPath $paths.Execution -Raw |
            ConvertFrom-Json
        Before = Get-Content -LiteralPath $paths.Before -Raw | ConvertFrom-Json
        After = Get-Content -LiteralPath $paths.After -Raw | ConvertFrom-Json
        Paths = $paths
    }
}

function Assert-RecoveryCase {
    param(
        [string] $CaseId,
        $Artifacts,
        [string] $QualifiedItemId,
        [bool] $ExpectedDiscard
    )
    $items = @($Artifacts.Queue.items)
    if ($items.Count -ne 1 -or
        $items[0].option_id -ne
            "executor.claim_fishing_item_grab_reward") {
        throw "$CaseId did not compile exactly one fishing reward claim."
    }
    if ($Artifacts.Execution.option_id -ne
            "executor.claim_fishing_item_grab_reward" -or
        $Artifacts.Execution.status -ne "applied" -or
        $Artifacts.Execution.primitive_verification_status -ne "verified") {
        throw "$CaseId did not execute as applied/verified."
    }

    $beforeMenu = Read-FieldValue $Artifacts.Before `
        "menus" "menu_specific_state"
    $afterMenu = Read-FieldValue $Artifacts.After "menus" "active_menu"
    if ($beforeMenu.kind -ne "fishing_item_grab" -or
        [int]$beforeMenu.pending_item_count -ne 1) {
        throw "$CaseId before snapshot did not preserve the exact reward menu."
    }
    if ($afterMenu.is_open -eq $true) {
        throw "$CaseId left the essential reward menu open."
    }

    $beforeCount = Get-InventoryItemCount $Artifacts.Before $QualifiedItemId
    $afterCount = Get-InventoryItemCount $Artifacts.After $QualifiedItemId
    if ($afterCount -ne $beforeCount + 1) {
        throw "$CaseId reward inventory delta was $beforeCount -> $afterCount."
    }

    $discardFacts = @($Artifacts.Execution.changed_facts | Where-Object {
        [string]$_.path -match '^player\.inventory\[\d+\]$'
    })
    if ($ExpectedDiscard -and $discardFacts.Count -ne 1) {
        throw "$CaseId expected one authorized discard fact."
    }
    if (-not $ExpectedDiscard -and $discardFacts.Count -ne 0) {
        throw "$CaseId unexpectedly discarded an inventory item."
    }

    return [ordered]@{
        case_id = $CaseId
        status = "passed"
        option_id = $Artifacts.Execution.option_id
        primitive_verification_status =
            $Artifacts.Execution.primitive_verification_status
        reward_qualified_item_id = $QualifiedItemId
        reward_count_before = $beforeCount
        reward_count_after = $afterCount
        expected_discard = $ExpectedDiscard
        discard_fact_count = $discardFacts.Count
        before_state_hash = $Artifacts.Before.state_hash
        after_state_hash = $Artifacts.After.state_hash
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767"

if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
foreach ($port in @($BackendPort, 8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -LocalPort $port -State Listen `
            -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening; refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" `
        -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running; refusing to touch it."
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot `
    (Join-Path $OutputDirectory $RunId)
if (Test-Path -LiteralPath $runDirectory) {
    throw "Refusing to overwrite an existing smoke run: $runDirectory"
}
$trainingOutputDirectory = Join-Path $runDirectory "training-output"
New-Item -ItemType Directory -Path $trainingOutputDirectory -Force |
    Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "ASPNETCORE_URLS"
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

$backendProcess = $null
$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" -ArgumentList @(
        "run",
        "--no-restore",
        "--project",
        (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile"
    ) -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $runDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $runDirectory "backend.stderr.log") `
        -PassThru
    try { $backendProcess.PriorityClass = "BelowNormal" } catch {}
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $runDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $runDirectory "game.stderr.log") `
        -PassThru
    try { $gameProcess.PriorityClass = "BelowNormal" } catch {}
    Wait-JsonHealth -Url "$executorUrl/health" -TimeoutSeconds 30 | Out-Null
    Wait-WorldSnapshot -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds | Out-Null

    $cases = @(
        [pscustomobject]@{
            Id = "capacity_available"
            QualifiedItemId = "(O)145"
            Source = 0
            FillInventory = $false
        },
        [pscustomobject]@{
            Id = "inventory_full_authorized_discard"
            QualifiedItemId = "(O)162"
            Source = 3
            FillInventory = $true
        }
    )
    $caseSummaries = @()
    foreach ($case in $cases) {
        $caseDirectory = Join-Path $runDirectory $case.Id
        New-Item -ItemType Directory -Path $caseDirectory -Force | Out-Null
        $beforeSetup = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $setupRequest = New-SetupRequest -CaseId $case.Id `
            -BeforeStateHash $beforeSetup.state_hash -SavesPath $savesPath `
            -QualifiedItemId $case.QualifiedItemId -Source $case.Source `
            -FillInventory $case.FillInventory
        $setupResult = Invoke-JsonPost `
            "$executorUrl/api/v1/training/execute" $setupRequest
        if ($setupResult.status -ne "applied" -or
            $setupResult.primitive_verification_status -ne "verified") {
            throw "$($case.Id) fixture setup did not verify."
        }
        $fixtureSnapshot = Wait-FishingItemGrabSnapshot `
            -Url $snapshotUrl -QualifiedItemId $case.QualifiedItemId `
            -TimeoutSeconds 30
        $pending = @((Read-FieldValue $fixtureSnapshot `
            "menus" "menu_specific_state").pending_items)[0]
        if ([bool]$pending.player_can_accept_whole_stack -eq
            [bool]$case.FillInventory) {
            throw "$($case.Id) inventory-capacity fixture is inverted."
        }

        Write-JsonFile (Join-Path $caseDirectory "setup-request.json") `
            $setupRequest
        Write-JsonFile (Join-Path $caseDirectory "setup-result.json") `
            $setupResult
        Write-JsonFile (Join-Path $caseDirectory "fixture-snapshot.json") `
            $fixtureSnapshot

        $loopRoot = Join-Path $caseDirectory "loop"
        Invoke-RecoveryIteration -LoopRoot $loopRoot `
            -BackendUrl $backendUrl -SnapshotUrl $snapshotUrl `
            -ExecutorUrl $executorUrl -SavesPath $savesPath
        $artifacts = Read-LoopArtifacts -LoopRoot $loopRoot
        $caseSummary = Assert-RecoveryCase -CaseId $case.Id `
            -Artifacts $artifacts -QualifiedItemId $case.QualifiedItemId `
            -ExpectedDiscard $case.FillInventory
        Write-JsonFile (Join-Path $caseDirectory "summary.json") $caseSummary
        $caseSummaries += [pscustomobject]$caseSummary
    }

    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        cases = $caseSummaries
        evidence_admission = "not_automatic"
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($name in $environmentNames) {
        $previous = $previousEnvironment[$name]
        if ($null -eq $previous) {
            [Environment]::SetEnvironmentVariable($name, $null)
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $previous)
        }
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force
    }
}
