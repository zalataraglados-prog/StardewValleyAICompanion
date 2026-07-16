param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-route-to-shop-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $TargetLocation = "JojaMart",
    [string] $ExpectedShopId = "",
    [string] $PreferredQualifiedItemId = "",
    [string] $OutputDirectory = "artifacts\runtime-route-to-shop-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $OpenTimeWaitSeconds = 180,
    [switch] $VisibleGame,
    [switch] $HumanSpeedDiagnostics,
    [switch] $BuyFirstSafeItem,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 180)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
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
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") { return $response }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 5
            $location = Read-FieldValue $snapshot "player" "location_id"
            if (-not [string]::IsNullOrWhiteSpace([string]$location)) { return $snapshot }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready snapshot."
}

function Find-RoutePath {
    param($Snapshot, [string] $StartLocation, [string] $TargetLocation)
    $graph = Read-FieldValue $Snapshot "locations" "route_graph"
    if ($null -eq $graph -or $null -eq $graph.edges) { return @() }
    $edges = @($graph.edges | Where-Object {
        $_.resolved -eq $true -and
        -not [string]::IsNullOrWhiteSpace([string]$_.from_location) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.target_location) -and
        $null -ne $_.from_x -and $null -ne $_.from_y
    })
    $queue = New-Object System.Collections.Queue
    $queue.Enqueue($StartLocation)
    $visited = @{ $StartLocation = $true }
    $previousLocation = @{}
    $previousEdge = @{}
    while ($queue.Count -gt 0) {
        $location = [string]$queue.Dequeue()
        if ($location -eq $TargetLocation) { break }
        foreach ($edge in @($edges | Where-Object { ([string]$_.from_location) -eq $location } | Sort-Object from_y, from_x, target_location)) {
            $next = [string]$edge.target_location
            if ($visited.ContainsKey($next)) { continue }
            $visited[$next] = $true
            $previousLocation[$next] = $location
            $previousEdge[$next] = $edge
            $queue.Enqueue($next)
        }
    }
    if (-not $visited.ContainsKey($TargetLocation)) { return @() }
    $path = @()
    $cursor = $TargetLocation
    while ($cursor -ne $StartLocation) {
        $edge = $previousEdge[$cursor]
        $path = @($edge) + $path
        $cursor = $previousLocation[$cursor]
    }
    return $path
}

function Find-NextRouteEdges {
    param($Snapshot, [string] $StartLocation, [string] $TargetLocation, [hashtable] $VisitedLocations)
    $graph = Read-FieldValue $Snapshot "locations" "route_graph"
    $playerX = [int](Read-FieldValue $Snapshot "player" "tile_x")
    $playerY = [int](Read-FieldValue $Snapshot "player" "tile_y")
    $path = Find-RoutePath -Snapshot $Snapshot -StartLocation $StartLocation -TargetLocation $TargetLocation
    $nextTarget = if ($path.Count -gt 0) { [string]$path[0].target_location } else { $TargetLocation }
    $edgeSources = @()
    $currentConnectors = Read-FieldValue $Snapshot "locations" "route_connectors"
    if ($null -ne $currentConnectors -and $null -ne $currentConnectors.connectors) {
        $edgeSources += @($currentConnectors.connectors | Where-Object {
            -not [string]::IsNullOrWhiteSpace([string]$_.target_location) -and
            $null -ne $_.tile_x -and
            $null -ne $_.tile_y
        } | ForEach-Object {
            [pscustomobject]@{
                resolved = $true
                from_location = $StartLocation
                from_x = $_.tile_x
                from_y = $_.tile_y
                kind = $_.kind
                target_location = $_.target_location
                target_x = $_.target_x
                target_y = $_.target_y
                source_property = "locations.route_connectors"
                raw_action = $_.action
                open_time = $null
                close_time = $null
            }
        })
    }
    if ($null -ne $graph -and $null -ne $graph.edges) {
        $edgeSources += @($graph.edges)
    }

    $records = @($edgeSources | Where-Object {
        $_.resolved -eq $true -and
        ([string]$_.from_location) -eq $StartLocation -and
        -not [string]::IsNullOrWhiteSpace([string]$_.target_location) -and
        ([string]$_.target_location) -eq $nextTarget -and
        $null -ne $_.from_x -and $null -ne $_.from_y
    } | ForEach-Object {
        $next = [string]$_.target_location
        [pscustomobject]@{
            Edge = $_
            PathLength = $path.Count
            Distance = [Math]::Abs($playerX - [int]$_.from_x) + [Math]::Abs($playerY - [int]$_.from_y)
        }
    })
    return @($records | Sort-Object PathLength, Distance, @{ Expression = { $_.Edge.from_y } }, @{ Expression = { $_.Edge.from_x } }, @{ Expression = { $_.Edge.target_location } } | ForEach-Object { $_.Edge })
}

function Wait-UntilOpenTime {
    param([string] $SnapshotUrl, $Edge, [int] $TimeoutSeconds)
    if ($null -eq $Edge.open_time) { return Invoke-RestMethod -Method Get -Uri $SnapshotUrl -Headers @{ "Accept" = "application/json" } -TimeoutSec 10 }
    $openTime = [int]$Edge.open_time
    $closeTime = if ($null -eq $Edge.close_time) { 2600 } else { [int]$Edge.close_time }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Invoke-RestMethod -Method Get -Uri $SnapshotUrl -Headers @{ "Accept" = "application/json" } -TimeoutSec 10
        $time = [int](Read-FieldValue $snapshot "time" "time")
        if ($time -ge $openTime -and $time -lt $closeTime) { return $snapshot }
        if ($time -ge $closeTime) { throw "Connector closed for the day: $($Edge.raw_action)" }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for connector open time: $($Edge.raw_action)"
}

function Wait-LocationSnapshot {
    param([string] $SnapshotUrl, [string] $ExpectedLocation, [int] $TimeoutSeconds = 90, [int] $RequestTimeoutSeconds = 90)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastLocation = ""
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $SnapshotUrl -Headers @{ "Accept" = "application/json" } -TimeoutSec $RequestTimeoutSeconds
            $lastLocation = [string](Read-FieldValue $snapshot "player" "location_id")
            if ($lastLocation -eq $ExpectedLocation) { return $snapshot }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for location $ExpectedLocation; last location was $lastLocation. Last error: $lastError"
}

function Invoke-TraverseEdge {
    param($Snapshot, $Edge, [string] $RunId, [string] $SavePath)
    $kind = [string]$Edge.kind
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-to-shop-smoke"
        queue_item_id = "route.$($Edge.from_location).to.$($Edge.target_location).$([guid]::NewGuid().ToString('N'))"
        before_state_hash = $Snapshot.state_hash
        option_id = "executor.traverse_connector"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavePath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        target_tile_x = [int]$Edge.from_x
        target_tile_y = [int]$Edge.from_y
        connector_kind = $kind
        expected_target_location = [string]$Edge.target_location
        expected_arrival_tile_x = if ($null -ne $Edge.target_x) { [int]$Edge.target_x } else { $null }
        expected_arrival_tile_y = if ($null -ne $Edge.target_y) { [int]$Edge.target_y } else { $null }
    }
    Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 600
}

function Test-RouteGateAllowsEdge {
    param($Snapshot, $Edge)
    $gateContext = Read-FieldValue $Snapshot "locations" "route_gate_context"
    if ($null -eq $gateContext -or $null -eq $gateContext.action_gates) { return $true }

    $edgeX = [int]$Edge.from_x
    $edgeY = [int]$Edge.from_y
    $targetLocation = [string]$Edge.target_location
    $gate = @($gateContext.action_gates | Where-Object {
        $null -ne $_.tile_x -and
        $null -ne $_.tile_y -and
        [int]$_.tile_x -eq $edgeX -and
        [int]$_.tile_y -eq $edgeY -and
        [string]::Equals([string]$_.target_location, $targetLocation, [StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1)[0]
    return $null -eq $gate -or $gate.allowed_now -ne $false
}

function Find-ShopEndpoint {
    param($Snapshot, [string] $ExpectedShopId)
    $tiles = Read-FieldValue $Snapshot "current_location" "shop_action_tiles"
    if ($null -eq $tiles) { return $null }
    $npcPositions = @(Read-FieldValue $Snapshot "npcs" "positions")
    return @($tiles | Where-Object {
        $kind = [string]$_.parsed.kind
        $shopId = [string]$_.parsed.shop_id
        $ownerNpc = [string]$_.parsed.owner_npc
        $endpointX = [int]$_.tile_x
        $endpointY = [int]$_.tile_y
        $ownerArea = $_.parsed.owner_service_area
        $ownerStatus = $_.owner_service_status
        $serviceTimeStatus = $_.service_time_status
        $timeAvailable = $null -eq $serviceTimeStatus -or $serviceTimeStatus.allowed_now -ne $false
        $ownerAvailable = [string]::IsNullOrWhiteSpace($ownerNpc) -or @($npcPositions | Where-Object {
            $npcX = [int]$_.tile_x
            $npcY = [int]$_.tile_y
            $distance = [Math]::Abs($npcX - $endpointX) + [Math]::Abs($npcY - $endpointY)
            $inOwnerArea = $false
            if ($null -ne $ownerStatus -and $ownerStatus.owner_required -eq $true) {
                $inOwnerArea = $ownerStatus.in_service_area -eq $true
            }
            elseif ($null -ne $ownerArea -and $null -ne $ownerArea.width -and $null -ne $ownerArea.height) {
                $areaX = [int]$ownerArea.x
                $areaY = [int]$ownerArea.y
                $areaWidth = [int]$ownerArea.width
                $areaHeight = [int]$ownerArea.height
                $inOwnerArea = $areaWidth -gt 0 -and $areaHeight -gt 0 -and
                    $npcX -ge $areaX -and $npcX -lt ($areaX + $areaWidth) -and
                    $npcY -ge $areaY -and $npcY -lt ($areaY + $areaHeight)
            }
            else {
                $inOwnerArea = $distance -le 2 -and $npcY -le $endpointY
            }

            [string]::Equals([string]$_.name, $ownerNpc, [StringComparison]::OrdinalIgnoreCase) -and $inOwnerArea
        }).Count -gt 0
        ($kind -eq "open_shop" -or $kind -eq "legacy_buy" -or $kind -eq "joja_shop" -or $kind -eq "dialogue_shop" -or $kind -eq "direct_or_dialogue_shop") -and
        $timeAvailable -and
        $ownerAvailable -and
        ([string]::IsNullOrWhiteSpace($ExpectedShopId) -or [string]::Equals($shopId, $ExpectedShopId, [StringComparison]::OrdinalIgnoreCase))
    } | Sort-Object tile_y, tile_x | Select-Object -First 1)[0]
}

function Invoke-MoveToTile {
    param($Snapshot, [int] $X, [int] $Y, [string] $RunId, [string] $SavePath)
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-to-shop-smoke"
        queue_item_id = "move.to.$X.$Y.$([guid]::NewGuid().ToString('N'))"
        before_state_hash = $Snapshot.state_hash
        option_id = "executor.move_to_tile"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavePath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        target_tile_x = $X
        target_tile_y = $Y
    }
    Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 600
}

function Invoke-AdvanceTimeTo {
    param($Snapshot, [int] $TargetTime, [string] $RunId, [string] $SavePath)
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-to-shop-smoke"
        queue_item_id = "debug.advance_time_to.$TargetTime.$([guid]::NewGuid().ToString('N'))"
        before_state_hash = $Snapshot.state_hash
        option_id = "debug.advance_time_to"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavePath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        target_time = $TargetTime
    }
    Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 60
}

function Invoke-ShopInteract {
    param($Snapshot, $Endpoint, [string] $RunId, [string] $SavePath)
    $expected = switch ([string]$Endpoint.parsed.kind) {
        "legacy_buy" { "Buy"; break }
        "joja_shop" { "JojaShop"; break }
        "dialogue_shop" { [string]$Endpoint.action; break }
        "direct_or_dialogue_shop" { [string]$Endpoint.action; break }
        default { "OpenShop" }
    }
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-to-shop-smoke"
        queue_item_id = "interact.shop.$([guid]::NewGuid().ToString('N'))"
        before_state_hash = $Snapshot.state_hash
        option_id = "executor.interact"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavePath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        target_tile_x = [int]$Endpoint.tile_x
        target_tile_y = [int]$Endpoint.tile_y
        interaction_kind = "map_action"
        expected_action_type = $expected
    }
    Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 180
}

function Invoke-DialogueShopResponse {
    param($Snapshot, $Endpoint, [string] $RunId, [string] $SavePath)
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-to-shop-smoke"
        queue_item_id = "dialogue.shop.$([guid]::NewGuid().ToString('N'))"
        before_state_hash = $Snapshot.state_hash
        option_id = "executor.choose_dialogue_response"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavePath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        expected_dialogue_key = [string]$Endpoint.parsed.dialogue_key
        dialogue_response_key = [string]$Endpoint.parsed.shop_response_key
        expected_shop_id = [string]$Endpoint.parsed.shop_id
    }
    Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 180
}

function Wait-ShopReadySnapshot {
    param([string] $SnapshotUrl, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Invoke-RestMethod -Method Get -Uri $SnapshotUrl -Headers @{ "Accept" = "application/json" } -TimeoutSec 10
        $activeMenu = Read-FieldValue $snapshot "menus" "active_menu"
        $shopStock = Read-FieldValue $snapshot "menus" "shop_stock"
        if ($null -ne $activeMenu -and [string]$activeMenu.type -eq "ShopMenu" -and $null -ne $shopStock -and [int]$shopStock.safety_timer -le 0) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for ShopMenu with safety_timer <= 0."
}

function Wait-MenuClosedSnapshot {
    param([string] $SnapshotUrl, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Invoke-JsonGet -Url $SnapshotUrl -TimeoutSeconds 10
        $activeMenu = Read-FieldValue $snapshot "menus" "active_menu"
        if ($null -eq $activeMenu -or $activeMenu.is_open -eq $false -or [string]$activeMenu.type -eq "none") {
            return $snapshot
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for active menu to close."
}

function Select-SafePurchaseEntry {
    param($Snapshot, [string] $PreferredQualifiedItemId)
    $shopStock = Read-FieldValue $Snapshot "menus" "shop_stock"
    if ($null -eq $shopStock -or $null -eq $shopStock.entries) { return $null }
    $entries = @($shopStock.entries | Where-Object {
        $_.executor_purchase_enabled -eq $true -and
        $_.can_buy_item -eq $true -and
        $_.can_afford_one_with_currency -eq $true -and
        $_.could_inventory_accept -eq $true
    })
    if (-not [string]::IsNullOrWhiteSpace($PreferredQualifiedItemId)) {
        $preferred = @($entries | Where-Object { [string]::Equals([string]$_.qualified_item_id, $PreferredQualifiedItemId, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)
        if ($preferred.Count -gt 0) { return $preferred[0] }
    }

    return @($entries | Sort-Object @{ Expression = { [int]$_.price }; Ascending = $true }, qualified_item_id | Select-Object -First 1)
}

function Invoke-BuyShopItem {
    param($Snapshot, $Entry, [string] $RunId, [string] $SavePath)
    $shopStock = Read-FieldValue $Snapshot "menus" "shop_stock"
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-to-shop-smoke"
        queue_item_id = "buy.shop.$([guid]::NewGuid().ToString('N'))"
        before_state_hash = $Snapshot.state_hash
        option_id = "executor.buy_shop_item"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavePath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        shop_item_id = [string]$Entry.item_id
        qualified_item_id = [string]$Entry.qualified_item_id
        quantity = 1
        max_unit_price = [int]$Entry.price
        expected_shop_id = [string]$shopStock.shop_id
    }
    Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 180
}

function Invoke-CloseMenu {
    param($Snapshot, [string] $RunId, [string] $SavePath)
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-to-shop-smoke"
        queue_item_id = "close.menu.$([guid]::NewGuid().ToString('N'))"
        before_state_hash = $Snapshot.state_hash
        option_id = "executor.close_menu"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavePath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
    }
    Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 60
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$LightSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=light"
$RouteSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=route"
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
    STARDEWAI_EXECUTOR_REQUEST_TIMEOUT_SECONDS = $env:STARDEWAI_EXECUTOR_REQUEST_TIMEOUT_SECONDS
    STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS = $env:STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

$process = $null
$stepResults = New-Object System.Collections.Generic.List[object]
$summary = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_EXECUTOR_REQUEST_TIMEOUT_SECONDS = if ($HumanSpeedDiagnostics) { "3600" } else { "600" }
    $env:STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS = if ($HumanSpeedDiagnostics) { "true" } else { "false" }
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $startProcessArgs = @{
        FilePath = $smapiExe
        WorkingDirectory = $runtimeGameDir
        PassThru = $true
    }
    if (-not $VisibleGame) {
        $startProcessArgs.WindowStyle = "Hidden"
    }
    $process = Start-Process @startProcessArgs
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $snapshot = Wait-WorldSnapshot -Url $RouteSnapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $visitedLocations = @{}
    Write-JsonFile (Join-Path $runDirectory "snapshot-000-before.json") $snapshot

    for ($i = 0; $i -lt 8; $i++) {
        $currentLocation = [string](Read-FieldValue $snapshot "player" "location_id")
        if ($currentLocation -eq $TargetLocation) { break }
        $visitedLocations[$currentLocation] = $true
        $candidateEdges = Find-NextRouteEdges -Snapshot $snapshot -StartLocation $currentLocation -TargetLocation $TargetLocation -VisitedLocations $visitedLocations
        if ($candidateEdges.Count -eq 0) { throw "No transparent resolved route path from $currentLocation to $TargetLocation." }
        $traversed = $false
        foreach ($edge in $candidateEdges) {
            if ($HumanSpeedDiagnostics -and $null -ne $edge.open_time -and ([string]$edge.kind -eq "locked_door_warp" -or [string]$edge.kind -eq "action_warp")) {
                $edgeX = [int]$edge.from_x
                $edgeY = [int]$edge.from_y
                $playerX = [int](Read-FieldValue $snapshot "player" "tile_x")
                $playerY = [int](Read-FieldValue $snapshot "player" "tile_y")
                if ([Math]::Abs($playerX - $edgeX) + [Math]::Abs($playerY - $edgeY) -ne 1) {
                    $stands = @(
                        @{ x = $edgeX + 1; y = $edgeY },
                        @{ x = $edgeX - 1; y = $edgeY },
                        @{ x = $edgeX; y = $edgeY + 1 },
                        @{ x = $edgeX; y = $edgeY - 1 }
                    )
                    $movedToDoorStand = $false
                    foreach ($stand in $stands) {
                        $moveResult = Invoke-MoveToTile -Snapshot $snapshot -X $stand.x -Y $stand.y -RunId $RunId -SavePath $savesPath
                        $stepResults.Add([ordered]@{ kind = "move_to_locked_door_stand"; edge = $edge; target = $stand; result = $moveResult }) | Out-Null
                        if ($moveResult.status -eq "applied" -and $moveResult.primitive_verification_status -eq "verified") {
                            $movedToDoorStand = $true
                            Start-Sleep -Milliseconds 500
                            $snapshot = Wait-LocationSnapshot -SnapshotUrl $RouteSnapshotUrl -ExpectedLocation $currentLocation -TimeoutSeconds 30
                            break
                        }
                    }
                    if (-not $movedToDoorStand) { throw "Could not move adjacent to locked door endpoint $edgeX,$edgeY before waiting for open time." }
                }
            }
            if ($null -ne $edge.open_time -and -not $HumanSpeedDiagnostics) {
                $currentTime = [int](Read-FieldValue $snapshot "time" "time")
                $openTime = [int]$edge.open_time
                if ($currentTime -lt $openTime) {
                    $timeResult = Invoke-AdvanceTimeTo -Snapshot $snapshot -TargetTime $openTime -RunId $RunId -SavePath $savesPath
                    $stepResults.Add([ordered]@{ kind = "debug_advance_time_to"; target_time = $openTime; result = $timeResult }) | Out-Null
                    Start-Sleep -Milliseconds 500
                    $snapshot = Invoke-JsonGet -Url $RouteSnapshotUrl
                }
            }
            $snapshot = Wait-UntilOpenTime -SnapshotUrl $LightSnapshotUrl -Edge $edge -TimeoutSeconds $OpenTimeWaitSeconds
            $snapshot = Invoke-JsonGet -Url $RouteSnapshotUrl
            if (-not (Test-RouteGateAllowsEdge -Snapshot $snapshot -Edge $edge)) {
                $stepResults.Add([ordered]@{ kind = "skip_route_gate_blocked"; edge = $edge; reason = "route_gate_context_allowed_now_false" }) | Out-Null
                continue
            }
            Write-JsonFile (Join-Path $runDirectory ("snapshot-before-traverse-{0:D2}-{1}-to-{2}.json" -f $i, $edge.from_location, $edge.target_location)) $snapshot
            $result = Invoke-TraverseEdge -Snapshot $snapshot -Edge $edge -RunId $RunId -SavePath $savesPath
            $stepResults.Add([ordered]@{ kind = "traverse"; edge = $edge; result = $result }) | Out-Null
            if ($result.status -eq "applied" -and $result.primitive_verification_status -eq "verified") {
                $traversed = $true
                break
            }
        }
        if (-not $traversed) { throw "Traverse failed: no candidate edge from $currentLocation could advance toward $TargetLocation" }
        $snapshot = Wait-LocationSnapshot -SnapshotUrl $RouteSnapshotUrl -ExpectedLocation ([string]$edge.target_location) -TimeoutSeconds 30
        Write-JsonFile (Join-Path $runDirectory ("snapshot-route-{0:D2}.json" -f $i)) $snapshot
    }

    $locationAfterRoute = [string](Read-FieldValue $snapshot "player" "location_id")
    if ($locationAfterRoute -ne $TargetLocation) { throw "Route did not reach $TargetLocation; current location is $locationAfterRoute." }
    $endpoint = Find-ShopEndpoint -Snapshot $snapshot -ExpectedShopId $ExpectedShopId
    if ($null -eq $endpoint) { throw "No matching shop endpoint found in $TargetLocation. Expected shop id: $ExpectedShopId" }

    $playerX = [int](Read-FieldValue $snapshot "player" "tile_x")
    $playerY = [int](Read-FieldValue $snapshot "player" "tile_y")
    $endpointX = [int]$endpoint.tile_x
    $endpointY = [int]$endpoint.tile_y
    if ([Math]::Abs($playerX - $endpointX) + [Math]::Abs($playerY - $endpointY) -ne 1) {
        $stands = @(
            @{ x = $endpointX + 1; y = $endpointY },
            @{ x = $endpointX - 1; y = $endpointY },
            @{ x = $endpointX; y = $endpointY + 1 },
            @{ x = $endpointX; y = $endpointY - 1 }
        )
        $moved = $false
        foreach ($stand in $stands) {
            $moveResult = Invoke-MoveToTile -Snapshot $snapshot -X $stand.x -Y $stand.y -RunId $RunId -SavePath $savesPath
            $stepResults.Add([ordered]@{ kind = "move_to_shop_stand"; target = $stand; result = $moveResult }) | Out-Null
            if ($moveResult.status -eq "applied" -and $moveResult.primitive_verification_status -eq "verified") {
                $moved = $true
                Start-Sleep -Milliseconds 500
                $snapshot = Invoke-JsonGet -Url $RouteSnapshotUrl
                break
            }
        }
        if (-not $moved) { throw "Could not move adjacent to shop endpoint $endpointX,$endpointY." }
    }

    $interactResult = Invoke-ShopInteract -Snapshot $snapshot -Endpoint $endpoint -RunId $RunId -SavePath $savesPath
    $stepResults.Add([ordered]@{ kind = "interact_shop"; endpoint = $endpoint; result = $interactResult }) | Out-Null
    $dialogueResult = $null
    if (([string]$endpoint.parsed.kind -eq "dialogue_shop" -or [string]$endpoint.parsed.kind -eq "direct_or_dialogue_shop") -and $interactResult.status -eq "applied") {
        Start-Sleep -Milliseconds 500
        $dialogueSnapshot = Invoke-JsonGet -Url $RouteSnapshotUrl
        $activeMenu = Read-FieldValue $dialogueSnapshot "menus" "active_menu"
        if ($null -ne $activeMenu -and [string]$activeMenu.type -eq "DialogueBox") {
            Write-JsonFile (Join-Path $runDirectory "snapshot-before-dialogue-response.json") $dialogueSnapshot
            $dialogueResult = Invoke-DialogueShopResponse -Snapshot $dialogueSnapshot -Endpoint $endpoint -RunId $RunId -SavePath $savesPath
            $stepResults.Add([ordered]@{ kind = "dialogue_shop_response"; endpoint = $endpoint; result = $dialogueResult }) | Out-Null
            Start-Sleep -Milliseconds 500
        }
    }
    $buyEntry = $null
    $buyResult = $null
    $closeResult = $null
    if ($BuyFirstSafeItem) {
        $shopReady = Wait-ShopReadySnapshot -SnapshotUrl $RouteSnapshotUrl -TimeoutSeconds 15
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-buy.json") $shopReady
        $buyEntry = Select-SafePurchaseEntry -Snapshot $shopReady -PreferredQualifiedItemId $PreferredQualifiedItemId
        if ($null -eq $buyEntry) { throw "No executor-enabled safe purchase entry found in open shop menu." }
        $buyResult = Invoke-BuyShopItem -Snapshot $shopReady -Entry $buyEntry -RunId $RunId -SavePath $savesPath
        $stepResults.Add([ordered]@{ kind = "buy_shop_item"; entry = $buyEntry; result = $buyResult }) | Out-Null
        Start-Sleep -Milliseconds 500
        $afterBuy = Invoke-JsonGet -Url $RouteSnapshotUrl
        Write-JsonFile (Join-Path $runDirectory "snapshot-after-buy-before-close.json") $afterBuy
        $closeResult = Invoke-CloseMenu -Snapshot $afterBuy -RunId $RunId -SavePath $savesPath
        $stepResults.Add([ordered]@{ kind = "close_menu_after_buy"; result = $closeResult }) | Out-Null
        Start-Sleep -Milliseconds 500
        $null = Wait-MenuClosedSnapshot -SnapshotUrl $RouteSnapshotUrl -TimeoutSeconds 10
    }
    $after = Invoke-JsonGet -Url $RouteSnapshotUrl
    $finalActiveMenu = Read-FieldValue $after "menus" "active_menu"
    $finalMenuClosed = $null -eq $finalActiveMenu -or $finalActiveMenu.is_open -eq $false -or [string]$finalActiveMenu.type -eq "none"
    $summary = [ordered]@{
        status = if ($interactResult.status -eq "applied" -and $interactResult.primitive_verification_status -eq "verified" -and ($null -eq $dialogueResult -or ($dialogueResult.status -eq "applied" -and $dialogueResult.primitive_verification_status -eq "verified")) -and (-not $BuyFirstSafeItem -or ($buyResult.status -eq "applied" -and $buyResult.primitive_verification_status -eq "verified" -and $closeResult.status -eq "applied" -and $closeResult.primitive_verification_status -eq "verified" -and $finalMenuClosed))) { "passed" } else { "failed" }
        run_id = $RunId
        target_location = $TargetLocation
        expected_shop_id = $ExpectedShopId
        final_location = (Read-FieldValue $after "player" "location_id")
        active_menu = $finalActiveMenu
        final_menu_closed = [bool]$finalMenuClosed
        route_step_count = @($stepResults | Where-Object { $_.kind -eq "traverse" }).Count
        interact_status = $interactResult.status
        interact_verification = $interactResult.primitive_verification_status
        dialogue_status = if ($null -eq $dialogueResult) { "not_required" } else { $dialogueResult.status }
        dialogue_verification = if ($null -eq $dialogueResult) { "not_required" } else { $dialogueResult.primitive_verification_status }
        buy_requested = [bool]$BuyFirstSafeItem
        buy_item = if ($null -eq $buyEntry) { $null } else { [ordered]@{ qualified_item_id = [string]$buyEntry.qualified_item_id; item_id = [string]$buyEntry.item_id; price = [int]$buyEntry.price; display_name = [string]$buyEntry.display_name } }
        buy_status = if ($null -eq $buyResult) { "not_requested" } else { $buyResult.status }
        buy_verification = if ($null -eq $buyResult) { "not_requested" } else { $buyResult.primitive_verification_status }
        close_menu_status = if ($null -eq $closeResult) { "not_requested" } else { $closeResult.status }
        close_menu_verification = if ($null -eq $closeResult) { "not_requested" } else { $closeResult.primitive_verification_status }
        executor_health = $executorHealth
        kept_game_running = [bool]$KeepGameRunning
    }
    Write-JsonFile (Join-Path $runDirectory "step-results.json") $stepResults
    Write-JsonFile (Join-Path $runDirectory "snapshot-after.json") $after
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 48
    if ($summary.status -ne "passed") { throw "Route-to-shop smoke failed." }
}
catch {
    $summary = [ordered]@{
        status = "failed"
        run_id = $RunId
        target_location = $TargetLocation
        error = $_.Exception.Message
        route_step_count = @($stepResults | Where-Object { $_.kind -eq "traverse" }).Count
        kept_game_running = [bool]$KeepGameRunning
    }
    Write-JsonFile (Join-Path $runDirectory "step-results.json") $stepResults
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    throw
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) { Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue }
        else { Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value }
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
