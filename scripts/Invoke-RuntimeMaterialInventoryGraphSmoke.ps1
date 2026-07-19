param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-material-inventory-graph-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-material-inventory-graph-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 60,
    [int] $TargetTileY = 15,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
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

function Wait-MaterialGraph {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $field = $snapshot.state.farm.material_inventory_graph
            $lastStatus = "field_status=$($field.status);schema=$($field.value.schema_version)"
            if ($field.status -in @("available", "derived") -and
                $field.value.schema_version -eq "material_inventory_graph.v1") {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for material inventory graph. Last status: $lastStatus"
}

function Find-AccessPoint {
    param($Graph, [string] $LocationId, [int] $X, [int] $Y)
    @($Graph.access_points) | Where-Object {
        $_.location_id -eq $LocationId -and [int]$_.tile_x -eq $X -and [int]$_.tile_y -eq $Y
    } | Select-Object -First 1
}

function Find-QuantityRow {
    param($Graph, [string] $QualifiedItemId)
    @($Graph.quantity_rows) | Where-Object {
        $_.qualified_item_id -eq $QualifiedItemId -and [int]$_.quality -eq 4
    } | Select-Object -First 1
}

function Add-Check {
    param([System.Collections.Generic.List[object]] $Checks, [string] $Name, [bool] $Passed, $Observed)
    $Checks.Add([ordered]@{ name = $Name; passed = $Passed; observed = $Observed }) | Out-Null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
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

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $initialSnapshot = Wait-MaterialGraph -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-material-inventory-graph-smoke"
        queue_item_id = "runtime-material-inventory-graph-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_material_inventory_graph"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    Start-Sleep -Milliseconds 750
    $snapshot = Wait-MaterialGraph -Url $snapshotUrl -TimeoutSeconds 30
    $graph = $snapshot.state.farm.material_inventory_graph.value
    $farmId = "Farm"
    $checks = [System.Collections.Generic.List[object]]::new()

    Add-Check $checks "setup_verified" ($setupResult.status -eq "applied" -and $setupResult.primitive_verification_status -eq "verified") "$($setupResult.status)/$($setupResult.primitive_verification_status)"
    Add-Check $checks "schema" ($graph.schema_version -eq "material_inventory_graph.v1") $graph.schema_version

    $normal = Find-AccessPoint $graph $farmId $TargetTileX $TargetTileY
    $big = Find-AccessPoint $graph $farmId ($TargetTileX + 2) $TargetTileY
    $junimoA = Find-AccessPoint $graph $farmId ($TargetTileX + 4) $TargetTileY
    $junimoB = Find-AccessPoint $graph $farmId ($TargetTileX + 6) $TargetTileY
    $autoGrabber = Find-AccessPoint $graph $farmId ($TargetTileX + 8) $TargetTileY
    Add-Check $checks "normal_chest_access" ($null -ne $normal -and $normal.access_kind -eq "placed_chest") $normal
    Add-Check $checks "big_chest_access" ($null -ne $big -and $big.special_chest_type -eq "BigChest") $big
    Add-Check $checks "auto_grabber_access" ($null -ne $autoGrabber -and $autoGrabber.access_kind -eq "auto_grabber") $autoGrabber
    Add-Check $checks "junimo_two_access_points" ($null -ne $junimoA -and $null -ne $junimoB) @($junimoA, $junimoB)
    Add-Check $checks "junimo_shared_node" ($null -ne $junimoA -and $junimoA.node_id -eq $junimoB.node_id) "$($junimoA.node_id)|$($junimoB.node_id)"

    $junimoNodes = @(@($graph.inventory_nodes) | Where-Object { $_.global_inventory_id -eq "JunimoChests" })
    $junimoNodeCount = $junimoNodes.Count
    Add-Check $checks "junimo_single_inventory_node" ($junimoNodeCount -eq 1) $junimoNodeCount
    Add-Check $checks "deduplicated_access_point_count" ([int]$graph.deduplicated_access_point_count -ge 1) $graph.deduplicated_access_point_count

    $workbench = @($graph.workbench_links) | Where-Object {
        $_.location_id -eq $farmId -and [int]$_.tile_x -eq ($TargetTileX + 1) -and [int]$_.tile_y -eq $TargetTileY
    } | Select-Object -First 1
    $linked = @($workbench.connected_node_ids)
    Add-Check $checks "workbench_eight_neighbor_rule" ($null -ne $workbench -and $linked.Count -eq 2 -and $linked -contains $normal.node_id -and $linked -contains $big.node_id) $linked

    $expectedRows = @(
        @{ id = "(O)388"; available = 11; ready = 0; processing = 0 },
        @{ id = "(O)390"; available = 13; ready = 0; processing = 0 },
        @{ id = "(O)382"; available = 17; ready = 0; processing = 0 },
        @{ id = "(O)378"; available = 5; ready = 0; processing = 0 },
        @{ id = "(O)380"; available = 7; ready = 0; processing = 0 },
        @{ id = "(O)384"; available = 3; ready = 0; processing = 0 },
        @{ id = "(O)386"; available = 0; ready = 1; processing = 1 }
    )
    foreach ($expected in $expectedRows) {
        $row = Find-QuantityRow $graph $expected.id
        $matches = $null -ne $row -and
            [int]$row.available_quantity -eq $expected.available -and
            [int]$row.ready_output_quantity -eq $expected.ready -and
            [int]$row.in_process_quantity -eq $expected.processing
        Add-Check $checks ("quantity_states_" + $expected.id) $matches $row
    }

    $failed = @($checks | Where-Object { -not $_.passed })
    $summary = [ordered]@{
        status = if ($failed.Count -eq 0) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        checks_passed = $checks.Count - $failed.Count
        checks_total = $checks.Count
        failed_checks = @($failed)
        graph_counts = [ordered]@{
            inventory_nodes = $graph.physical_inventory_count
            access_points = $graph.access_point_count
            deduplicated_access_points = $graph.deduplicated_access_point_count
        }
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "fixture-snapshot.json") $snapshot
    Write-JsonFile (Join-Path $runDirectory "checks.json") $checks
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
    if ($summary.status -ne "passed") { throw "Runtime material inventory graph smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
