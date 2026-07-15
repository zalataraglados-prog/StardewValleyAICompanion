param(
    [string] $ExecutorBaseUrl = "http://127.0.0.1:8767",
    [string] $BridgeBaseUrl = "http://127.0.0.1:8765",
    [string] $RunId = $env:STARDEWAI_TRAINING_RUN_ID,
    [string] $SaveIsolationPath = $env:STARDEWAI_SAVE_ISOLATION_PATH,
    [string] $OutputDirectory = "artifacts\runtime-interact-positive-smoke",
    [switch] $FailWhenNoEndpoint
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $Value | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] $Body,
        [int] $TimeoutSeconds = 120
    )

    $json = $Body | ConvertTo-Json -Depth 32
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Read-FieldValue {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field
    )

    if ($null -eq $Snapshot.state) {
        return $null
    }

    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) {
        return $null
    }

    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) {
        return $null
    }

    return $fieldNode.value
}

function Find-Endpoint {
    param([Parameter(Mandatory = $true)] $Snapshot)

    $playerTile = Read-FieldValue $Snapshot "player" "tile"
    $playerX = $playerTile.x
    $playerY = $playerTile.y
    $tiles = Read-FieldValue $Snapshot "current_location" "shop_action_tiles"
    if ($null -eq $tiles) {
        return $null
    }

    $candidates = @($tiles | Where-Object {
        $kind = $_.parsed.kind
        $expected = switch ([string]$kind) {
            "legacy_buy" { "Buy"; break }
            "joja_shop" { "JojaShop"; break }
            "open_shop" { "OpenShop"; break }
            default { "" }
        }
        ($expected -eq "OpenShop" -or $expected -eq "Buy" -or $expected -eq "JojaShop") -and
            ([Math]::Abs([int]$_.tile_x - [int]$playerX) + [Math]::Abs([int]$_.tile_y - [int]$playerY)) -eq 1
    } | Sort-Object tile_y, tile_x)

    if ($candidates.Count -eq 0) {
        return $null
    }

    return $candidates[0]
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    throw "RunId is required. Pass -RunId or set STARDEWAI_TRAINING_RUN_ID."
}

if ([string]::IsNullOrWhiteSpace($SaveIsolationPath)) {
    throw "SaveIsolationPath is required. Pass -SaveIsolationPath or set STARDEWAI_SAVE_ISOLATION_PATH."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$runDirectory = Join-Path $OutputDirectory $RunId
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$health = Invoke-RestMethod -Method Get -Uri "$ExecutorBaseUrl/health" -Headers @{ "Accept" = "application/json" }
if ($health.status -ne "ok") {
    throw "Executor health check failed."
}

$before = Invoke-RestMethod -Method Get -Uri "$BridgeBaseUrl/api/v1/snapshot" -Headers @{ "Accept" = "application/json" }
$endpoint = Find-Endpoint $before
if ($null -eq $endpoint) {
    $summary = [ordered]@{
        status = "skipped_no_adjacent_endpoint"
        run_id = $RunId
        location = (Read-FieldValue $before "player" "location_id")
        player_tile = (Read-FieldValue $before "player" "tile")
        reason = "current_location.shop_action_tiles has no adjacent transparent shop endpoint"
    }
    Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") $before
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
    if ($FailWhenNoEndpoint) {
        throw "No adjacent transparent shop endpoint in current location."
    }
    exit 0
}

$expectedActionType = switch ([string]$endpoint.parsed.kind) {
    "legacy_buy" { "Buy"; break }
    "joja_shop" { "JojaShop"; break }
    default { "OpenShop" }
}
$request = [ordered]@{
    schema_version = "training_execution_request.v1"
    run_id = $RunId
    queue_id = "runtime-interact-positive-smoke"
    queue_item_id = "runtime-interact-positive-smoke.item.1"
    before_state_hash = $before.state_hash
    option_id = "executor.interact"
    execution_mode = "training_singleplayer"
    actor = "training_farmer.main"
    save_isolation_path = $SaveIsolationPath
    request_nonce = [guid]::NewGuid().ToString("N")
    created_at = [DateTimeOffset]::UtcNow.ToString("O")
    max_crops = 512
    target_tile_x = [int]$endpoint.tile_x
    target_tile_y = [int]$endpoint.tile_y
    interaction_kind = "map_action"
    expected_action_type = $expectedActionType
}

$result = Invoke-JsonPost "$ExecutorBaseUrl/api/v1/training/execute" $request
$after = Invoke-RestMethod -Method Get -Uri "$BridgeBaseUrl/api/v1/snapshot" -Headers @{ "Accept" = "application/json" }
$summary = [ordered]@{
    status = if ($result.status -eq "applied" -and $result.primitive_verification_status -eq "verified") { "passed" } else { "failed" }
    run_id = $RunId
    endpoint = $endpoint
    expected_action_type = $expectedActionType
    result_status = $result.status
    primitive_verification_status = $result.primitive_verification_status
    block_reasons = @($result.block_reasons)
    before_state_hash = $before.state_hash
    after_state_hash = $after.state_hash
}

Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") $before
Write-JsonFile (Join-Path $runDirectory "interact-result.json") $result
Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $after
Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
$summary | ConvertTo-Json -Depth 32

if ($summary.status -ne "passed") {
    throw "Positive interact smoke failed."
}
