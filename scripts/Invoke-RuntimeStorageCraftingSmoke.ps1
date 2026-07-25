param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-storage-crafting-smoke-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-storage-crafting-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [string] $RecipeName = "Chest"
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ Accept = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) `
        -TimeoutSec $TimeoutSeconds
}

function Wait-Health {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($health.status -eq "ok") { return $health }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Find-StorageRecipe {
    param($Snapshot, [string] $Name)
    foreach ($row in @(
        $Snapshot.state.player.storage_crafting.value.rows
    )) {
        if ([string]$row.recipe_name -eq $Name) {
            return $row
        }
    }
    return $null
}

function Wait-StorageRecipe {
    param(
        [string] $Url,
        [string] $Name,
        [int] $TimeoutSeconds,
        [switch] $RequireReady
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url
            $field = $snapshot.state.player.storage_crafting
            $row = Find-StorageRecipe -Snapshot $snapshot -Name $Name
            $ready = $null -ne $row -and
                [string]$row.craft_candidate_status -eq
                    "ready_for_native_personal_crafting_menu"
            $lastStatus =
                "save=$($snapshot.save_id.status)" +
                ";field=$($field.status)" +
                ";projection=$($field.value.projection_status)" +
                ";row=$($null -ne $row);ready=$ready"
            if ($snapshot.save_id.status -in @(
                    "available",
                    "derived"
                ) -and
                $field.status -in @("available", "derived") -and
                $null -ne $row -and
                (-not $RequireReady -or $ready)) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for storage recipe. Last status: $lastStatus"
}

function Inventory-Count {
    param($Snapshot, [string] $QualifiedId)
    $sum = 0
    foreach ($item in @($Snapshot.state.player.inventory.value)) {
        if ([string]$item.qualified_item_id -eq $QualifiedId) {
            $sum += [int]$item.stack
        }
    }
    return $sum
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=training_machine"
$executeUrl =
    "http://127.0.0.1:8767/api/v1/training/execute"
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

$runDirectory = Join-Path $ProjectRoot (
    Join-Path $OutputDirectory $RunId
)
New-Item -ItemType Directory -Force -Path $runDirectory |
    Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH =
        $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID =
        $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $process = Start-Process -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -PassThru
    $health = Wait-Health `
        -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $initial = Wait-StorageRecipe `
        -Url $snapshotUrl `
        -Name $RecipeName `
        -TimeoutSeconds $StartupTimeoutSeconds

    $setup = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-storage-crafting-smoke"
        queue_item_id = "runtime-storage-crafting-smoke.setup"
        before_state_hash = $initial.state_hash
        option_id = "debug.setup_storage_crafting_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        recipe_name = $RecipeName
    }
    $setupResult = Invoke-JsonPost `
        -Url $executeUrl -Body $setup
    if ($setupResult.status -ne "applied") {
        Write-JsonFile `
            (Join-Path $runDirectory "setup-result.json") `
            $setupResult
        throw "Storage crafting fixture failed."
    }

    Start-Sleep -Milliseconds 750
    $before = Wait-StorageRecipe `
        -Url $snapshotUrl `
        -Name $RecipeName `
        -TimeoutSeconds 30 `
        -RequireReady
    $row = Find-StorageRecipe `
        -Snapshot $before -Name $RecipeName
    $qualifiedId = [string]$row.output_qualified_item_id
    $countBefore = Inventory-Count `
        -Snapshot $before -QualifiedId $qualifiedId
    $craft = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-storage-crafting-smoke"
        queue_item_id = "runtime-storage-crafting-smoke.craft"
        before_state_hash = $before.state_hash
        option_id = "executor.craft_storage_item"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        recipe_name = [string]$row.recipe_name
        output_qualified_item_id = $qualifiedId
        output_item_id = [string]$row.output_item_id
        output_count = [int]$row.output_count_per_craft
        times_crafted_before = [int]$row.times_crafted
        ingredient_rows_json = ConvertTo-Json `
            -InputObject @($row.ingredient_rows) `
            -Depth 32 -Compress
        crafting_source = "native_personal_crafting_menu"
    }
    $craftResult = Invoke-JsonPost `
        -Url $executeUrl -Body $craft
    Start-Sleep -Milliseconds 750
    $after = Wait-StorageRecipe `
        -Url $snapshotUrl `
        -Name $RecipeName `
        -TimeoutSeconds 30
    $afterRow = Find-StorageRecipe `
        -Snapshot $after -Name $RecipeName
    $countAfter = Inventory-Count `
        -Snapshot $after -QualifiedId $qualifiedId
    $passed =
        $craftResult.status -eq "applied" -and
        $craftResult.primitive_kind -eq
            "craft_storage_item" -and
        $craftResult.primitive_verification_status -eq
            "verified" -and
        $countAfter -eq (
            $countBefore +
            [int]$row.output_count_per_craft
        ) -and
        [int]$afterRow.times_crafted -eq (
            [int]$row.times_crafted +
            [int]$row.output_count_per_craft
        )
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        recipe_name = $RecipeName
        qualified_item_id = $qualifiedId
        inventory_count_before = $countBefore
        inventory_count_after = $countAfter
        times_crafted_before = [int]$row.times_crafted
        times_crafted_after = [int]$afterRow.times_crafted
        setup_status = $setupResult.status
        craft_status = $craftResult.status
        craft_primitive_kind = $craftResult.primitive_kind
        craft_verification =
            $craftResult.primitive_verification_status
        craft_reasons =
            @($craftResult.primitive_verification_reasons)
        craft_block_reasons =
            @($craftResult.block_reasons)
        state_hash_before = $before.state_hash
        state_hash_after = $after.state_hash
        state_hash_changed =
            $before.state_hash -ne $after.state_hash
        executor_health = $health
        smapi_process_id = $process.Id
    }

    Write-JsonFile `
        (Join-Path $runDirectory "initial-snapshot.json") `
        $initial
    Write-JsonFile `
        (Join-Path $runDirectory "setup-result.json") `
        $setupResult
    Write-JsonFile `
        (Join-Path $runDirectory "before-snapshot.json") `
        $before
    Write-JsonFile `
        (Join-Path $runDirectory "craft-result.json") `
        $craftResult
    Write-JsonFile `
        (Join-Path $runDirectory "after-snapshot.json") `
        $after
    Write-JsonFile `
        (Join-Path $runDirectory "summary.json") `
        $summary
    $summary | ConvertTo-Json -Depth 12
    if (-not $passed) {
        throw "Runtime storage crafting smoke failed. See $runDirectory"
    }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force `
            -ErrorAction SilentlyContinue
    }
}
