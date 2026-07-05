param(
    [Alias("BridgeUrl")]
    [string] $BridgeBaseUrl = "http://127.0.0.1:8765",
    [Alias("BackendUrl")]
    [string] $BackendBaseUrl = "http://127.0.0.1:5000",
    [Alias("ArtifactsDirectory")]
    [string] $OutputDirectory = "artifacts\smapi-runtime-acceptance",
    [string] $IsolatedStardewDirectory = "",
    [switch] $IngestBackend
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $json = $Value | ConvertTo-Json -Depth 100
    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Invoke-JsonGet {
    param([Parameter(Mandatory = $true)] [string] $Url)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" }
}

function Invoke-RawJsonGet {
    param([Parameter(Mandatory = $true)] [string] $Url)
    $client = [System.Net.WebClient]::new()
    try {
        $client.Headers.Set("Accept", "application/json")
        $client.DownloadString($Url)
    }
    finally {
        $client.Dispose()
    }
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] $Body
    )

    $json = $Body | ConvertTo-Json -Depth 100
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json" -Body $json
}

function Invoke-RawJsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [string] $Json
    )

    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $Json
}

function Test-Envelope {
    param(
        [Parameter(Mandatory = $true)] $Envelope,
        [Parameter(Mandatory = $true)] [string] $Path
    )

    $required = @("value", "status", "source", "adapter", "read_at_tick", "confidence")
    foreach ($name in $required) {
        if (-not ($Envelope.PSObject.Properties.Name -contains $name)) {
            throw "Missing envelope property '$name' at '$Path'."
        }
    }

    if ($Envelope.status -notin @("available", "derived", "unavailable", "stale", "error")) {
        throw "Unknown envelope status '$($Envelope.status)' at '$Path'."
    }

    if ($Envelope.status -notin @("available", "derived") -and $null -ne $Envelope.value) {
        throw "Non-readable envelope at '$Path' carries a value."
    }
}

function Test-SnapshotEnvelopeShape {
    param([Parameter(Mandatory = $true)] $Snapshot)

    if ($Snapshot.schema_version -ne "snapshot.v1") {
        throw "snapshot.schema_version must be snapshot.v1."
    }
    if ([string]::IsNullOrWhiteSpace($Snapshot.state_hash)) {
        throw "snapshot.state_hash is required."
    }
    if ($null -eq $Snapshot.state) {
        throw "snapshot.state is required."
    }

    foreach ($section in $Snapshot.state.PSObject.Properties) {
        foreach ($field in $section.Value.PSObject.Properties) {
            Test-Envelope -Envelope $field.Value -Path "state.$($section.Name).$($field.Name)"
        }
    }
}

function Test-CapabilityManifestShape {
    param([Parameter(Mandatory = $true)] $Capabilities)

    if ($Capabilities.schema_version -ne "capabilities.v1") {
        throw "capabilities.schema_version must be capabilities.v1."
    }
    if ($Capabilities.can_write_game_state -ne $false) {
        throw "capabilities.can_write_game_state must be false for Phase 1A-3."
    }
    if ($Capabilities.can_execute_commands -ne $false) {
        throw "capabilities.can_execute_commands must be false for Phase 1A-3."
    }
}

function Get-EventArray {
    param([Parameter(Mandatory = $true)] $EventResponse)

    if ($EventResponse.PSObject.Properties.Name -contains "events") {
        return @($EventResponse.events)
    }

    return @($EventResponse)
}

function Test-EventStreamShape {
    param(
        [Parameter(Mandatory = $true)] $EventResponse,
        [Parameter(Mandatory = $true)] [string[]] $SnapshotHashes
    )

    if ($EventResponse.PSObject.Properties.Name -contains "schema_version") {
        if ($EventResponse.schema_version -ne "event_stream.v1") {
            throw "event stream schema_version must be event_stream.v1."
        }
        if ($EventResponse.latest_snapshot_hash -notin $SnapshotHashes) {
            throw "event stream latest_snapshot_hash must match a captured snapshot.state_hash."
        }
        if ($EventResponse.chain_status -ne "ok") {
            throw "event stream chain_status must be ok."
        }
        foreach ($required in @("latest_event_sequence", "latest_event_hash", "events", "count", "next_after_sequence")) {
            if (-not ($EventResponse.PSObject.Properties.Name -contains $required)) {
                throw "event stream is missing '$required'."
            }
        }
    }

    $previous = $null
    foreach ($event in (Get-EventArray $EventResponse)) {
        if ($event.schema_version -ne "event.v1") {
            throw "event.schema_version must be event.v1 for event '$($event.event_id)'."
        }
        foreach ($required in @("event_sequence", "previous_event_hash", "event_hash", "state_hash_before", "state_hash_after")) {
            if (-not ($event.PSObject.Properties.Name -contains $required)) {
                throw "event.$required is required for event '$($event.event_id)'."
            }
        }
        if (-not ($event.PSObject.Properties.Name -contains "changed_fields")) {
            throw "event.changed_fields is required for event '$($event.event_id)'."
        }
        if ($null -ne $previous -and $event.previous_event_hash -ne $previous.event_hash) {
            throw "event hash chain is broken between sequence '$($previous.event_sequence)' and '$($event.event_sequence)'."
        }
        $previous = $event
    }
}

$resolvedIsolatedStardewDirectory = $null
if (-not [string]::IsNullOrWhiteSpace($IsolatedStardewDirectory)) {
    if (-not (Test-Path -LiteralPath $IsolatedStardewDirectory -PathType Container)) {
        throw "Isolated Stardew directory does not exist: '$IsolatedStardewDirectory'."
    }

    $resolvedIsolatedStardewDirectory = (Resolve-Path -LiteralPath $IsolatedStardewDirectory).Path
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$startedAt = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $OutputDirectory $startedAt
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$bridgeSnapshot = Invoke-JsonGet "$BridgeBaseUrl/api/v1/snapshot"
$bridgeCapabilities = Invoke-JsonGet "$BridgeBaseUrl/api/v1/capabilities"
$bridgeEvents = Invoke-JsonGet "$BridgeBaseUrl/api/v1/events?limit=200"
$bridgeSnapshotAfterEvents = $null
if (($bridgeEvents.PSObject.Properties.Name -contains "latest_snapshot_hash") -and $bridgeEvents.latest_snapshot_hash -ne $bridgeSnapshot.state_hash) {
    $bridgeSnapshotAfterEvents = Invoke-JsonGet "$BridgeBaseUrl/api/v1/snapshot"
}

Test-SnapshotEnvelopeShape $bridgeSnapshot
Test-CapabilityManifestShape $bridgeCapabilities
if ($null -ne $bridgeSnapshotAfterEvents) {
    Test-SnapshotEnvelopeShape $bridgeSnapshotAfterEvents
}
$capturedSnapshotHashes = @($bridgeSnapshot.state_hash, $bridgeSnapshotAfterEvents.state_hash) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
Test-EventStreamShape $bridgeEvents $capturedSnapshotHashes

Write-JsonFile (Join-Path $runDirectory "bridge-snapshot.json") $bridgeSnapshot
if ($null -ne $bridgeSnapshotAfterEvents) {
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-events.json") $bridgeSnapshotAfterEvents
}
Write-JsonFile (Join-Path $runDirectory "bridge-capabilities.json") $bridgeCapabilities
Write-JsonFile (Join-Path $runDirectory "bridge-events.json") $bridgeEvents

$backendSummary = [ordered]@{
    ingest_requested = [bool]$IngestBackend
    snapshot_ingest = $null
    capabilities_ingest = $null
    event_ingest_count = 0
    latest_snapshot = $null
    events = $null
    capabilities = $null
    sync = $null
    hash_match_after_ingest = $null
}

if ($IngestBackend) {
    $backendSummary.snapshot_ingest = Invoke-RawJsonPost "$BackendBaseUrl/api/v1/snapshots" (Invoke-RawJsonGet "$BridgeBaseUrl/api/v1/snapshot")
    $backendSummary.capabilities_ingest = Invoke-RawJsonPost "$BackendBaseUrl/api/v1/capabilities" (Invoke-RawJsonGet "$BridgeBaseUrl/api/v1/capabilities")

    foreach ($event in (Get-EventArray $bridgeEvents | Where-Object { $_.state_hash_after -eq $backendSummary.snapshot_ingest.state_hash })) {
        Invoke-JsonPost "$BackendBaseUrl/api/v1/events" $event | Out-Null
        $backendSummary.event_ingest_count++
    }

    $backendSummary.latest_snapshot = Invoke-JsonGet "$BackendBaseUrl/api/v1/snapshots/latest"
    $backendSummary.events = Invoke-JsonGet "$BackendBaseUrl/api/v1/events"
    $backendSummary.capabilities = Invoke-JsonGet "$BackendBaseUrl/api/v1/capabilities"
    $backendSummary.sync = Invoke-JsonGet "$BackendBaseUrl/api/v1/sync"
    $backendSummary.hash_match_after_ingest = $backendSummary.latest_snapshot.state_hash -eq $backendSummary.snapshot_ingest.state_hash
}

Write-JsonFile (Join-Path $runDirectory "backend-summary.json") $backendSummary

$manualChecklist = [ordered]@{
    run_directory = (Resolve-Path -LiteralPath $runDirectory).Path
    isolated_stardew_directory = $resolvedIsolatedStardewDirectory
    bridge_base_url = $BridgeBaseUrl
    backend_base_url = $BackendBaseUrl
    bridge_state_hash = $bridgeSnapshot.state_hash
    bridge_game_tick = $bridgeSnapshot.game_tick
    bridge_in_game_time = $bridgeSnapshot.in_game_time
    bridge_completeness = $bridgeSnapshot.completeness
    unavailable_fields = $bridgeSnapshot.unavailable_fields
    manual_checks = @(
        "Compare visible save/farm/player identity with identity and environment fields.",
        "Compare visible clock/date/weather with state.time and snapshot.in_game_time.",
        "Compare player location, facing, money, health, stamina, selected tool, active menu, and inventory with state.player.",
        "Move within the same location, capture again, and confirm tile/hash/event behavior.",
        "Warp to another location, capture again, and confirm LocationChanged event.",
        "Change inventory, capture again, and confirm InventoryChanged event.",
        "Open and close menus, capture again, and confirm MenuChanged event.",
        "Wait for a time change, capture again, and confirm TimeChanged event.",
        "If Backend ingest was enabled, confirm backend-summary.hash_match_after_ingest is true."
    )
}

Write-JsonFile (Join-Path $runDirectory "manual-checklist.json") $manualChecklist

Write-Host "SMAPI runtime acceptance capture complete."
Write-Host "Run directory: $((Resolve-Path -LiteralPath $runDirectory).Path)"
Write-Host "Bridge snapshot hash: $($bridgeSnapshot.state_hash)"
if ($IngestBackend) {
    Write-Host "Backend hash match after ingest: $($backendSummary.hash_match_after_ingest)"
}
