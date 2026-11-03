[CmdletBinding()]
param([string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('rusty-key-geode-boundary-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyEvidence.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyGeodeBoundary.ps1')
$passed = [Collections.Generic.List[string]]::new()
$context = [pscustomobject]@{
    RunId = 'synthetic-boundary'; SnapshotUrl = 'http://unused.invalid/snapshot'
    ExecutorRoot = 'http://unused.invalid'; IsolatedSavesPath = $OutputRoot
}

function New-BoundarySnapshot([int] $Index) {
    $output = if ($script:mode -eq 'second-target') { '(O)96' } else { '(O)538' }
    if ($script:mode -eq 'output-drift' -and $Index -eq 3) { $output = '(O)539' }
    $snapshot = @{
        save_id = @{ status = 'available'; value = 'synthetic-save' }
        player_id = @{ status = 'available'; value = '1' }; state_hash = "synthetic-$Index"; game_version = '1.6.15'
        state = @{
            time = @{ total_days = @{ status = 'available'; value = 5 } }
            player = @{
                has_rusty_key = @{ status = 'available'; value = $false }
                story_event = @{ status = 'available'; value = @{ active = $false } }
                inventory = @{ status = 'available'; value = @(@{ qualified_item_id = '(O)275'; slot_index = 0; stack = 2 }) }
                geode_processing = @{ status = 'available'; value = @{
                    projection_status = 'complete_locked_base_1.6.15'; base_service_status = 'ready'; geodes_cracked_before = 42
                    inventory_inputs = @(@{
                        qualified_item_id = '(O)275'; slot_index = 0; stack_before = 2; status = 'available'; kind = 'exact'
                        expected_output = @{ qualified_item_id = $output; stack = 1; inventory_persists = $true }
                        authoritative_route_sources = @(@{ route_kind = 'native_geode_drop'; source_id = 'synthetic-source'; qualified_item_id = $output })
                    })
                } }
            }
            world_progress = @{ museum = @{ status = 'available'; value = @{
                donated_count = 59; rusty_key_donation_threshold = 60
                rusty_key_reward_id = 'museum60'; rusty_key_reward_action = 'MarkEventSeen Host 295672'
                rusty_key_reward_claimed = $false; rusty_key_prerequisite_event_seen = $false
                rusty_key_event_seen = $false; has_rusty_key = $false
                donatable_items = @(@{ qualified_item_id = $output; donated = ($Index -lt 2) })
            } } }
        }
    }
    if ($Index -eq 2 -or ($Index -eq 3 -and $script:mode -eq 'target-present')) {
        $snapshot.state.player.inventory.value += @{ qualified_item_id = $output; slot_index = 11; stack = 2 }
    }
    $input = $snapshot.state.player.geode_processing.value.inventory_inputs[0]
    switch ($script:mode) {
        'stale-identity' { $snapshot.player_id.status = 'stale' }
        'version-drift' { $snapshot.game_version = '1.6.14' }
        'foreign-save' { if ($Index -eq 3) { $snapshot.save_id.value = 'foreign-save' } }
        'stale-source' { $snapshot.state.player.geode_processing.status = 'stale' }
        'no-source' { $input.authoritative_route_sources = @() }
        'not-donatable' { $snapshot.state.world_progress.museum.value.donatable_items = @() }
        'non-exact' { $input.kind = 'domain' }
        'nonpersistent' { $input.expected_output.inventory_persists = $false }
        'empty-output' { $input.expected_output.stack = 0 }
        'counter-drift' { if ($Index -eq 3) { $snapshot.state.player.geode_processing.value.geodes_cracked_before = 43 } }
        'already-donated' { if ($Index -eq 3) { $snapshot.state.world_progress.museum.value.donatable_items[0].donated = $true } }
        'untyped-donation' { if ($Index -eq 3) { $snapshot.state.world_progress.museum.value.donatable_items[0].donated = 'false' } }
    }
    return $snapshot
}

# Only transport is doubled: the real boundary orchestration, files and guards run.
function Get-FreshRuntimeSnapshot {
    param($Url, $TimeoutSeconds)
    $value = New-BoundarySnapshot $script:captureCount
    $script:captureCount++
    return [pscustomobject]@{ Value = $value; Raw = ($value | ConvertTo-Json -Depth 40) }
}
function Invoke-JsonPost {
    param($Url, $Body)
    $script:requests.Add($Body)
    $value = @{
        status = $(if ($script:mode -eq 'setup-rejected' -and $script:requests.Count -eq 2) { 'blocked' } else { 'applied' })
        primitive_verification_status = 'verified'
    }
    return [pscustomobject]@{ Value = $value; Raw = ($value | ConvertTo-Json) }
}
function Reset-Case([string] $Name) {
    $script:mode = $Name
    $script:captureCount = 0
    $script:requests = [Collections.Generic.List[object]]::new()
}
foreach ($name in @('normal','second-target')) {
    Reset-Case $name
    $directory = Join-Path $OutputRoot $name
    $report = Initialize-RuntimeRustyKeyGeodeBoundary -Context $context -Directory $directory
    $expected = if ($name -eq 'normal') { '(O)538' } else { '(O)96' }
    if ($report.target_qualified_item_id -cne $expected -or $report.proves_native_acquisition -ne $false -or
        $report.formal_training_authorized -ne $false -or $report.target_inventory_quantity -ne 0 -or
        $report.native_start_snapshot_sha256 -cne (Get-RuntimeFileSha256 $report.native_start_snapshot_path) -or
        $script:requests.Count -ne 3 -or $script:captureCount -ne 4 -or
        ($script:requests.option_id -join ',') -cne 'debug.setup_geode_processing,debug.setup_museum_donation,debug.setup_geode_processing' -or
        $script:requests[1].qualified_item_id -cne $expected -or
        ($script:requests[0].geodes_cracked_before -ne $script:requests[2].geodes_cracked_before) -or
        @($script:requests | Where-Object { $_.save_isolation_path -cne $context.IsolatedSavesPath -or $_.run_id -cne $context.RunId }).Count -ne 0) {
        throw "Geode boundary positive control failed: $name"
    }
    $passed.Add($name)
}
foreach ($name in @('stale-identity','version-drift','foreign-save','stale-source','no-source','not-donatable','non-exact',
        'nonpersistent','empty-output','output-drift','counter-drift','target-present','already-donated','untyped-donation','setup-rejected')) {
    Reset-Case $name
    $directory = Join-Path $OutputRoot $name
    $rejected = $false
    try { Initialize-RuntimeRustyKeyGeodeBoundary -Context $context -Directory $directory | Out-Null }
    catch { $rejected = $true }
    if (-not $rejected -or (Test-Path -LiteralPath (Join-Path $directory 'boundary.json'))) {
        throw "Malformed geode boundary was admitted: $name"
    }
    if ($name -in @('stale-source','no-source','not-donatable','non-exact','nonpersistent','empty-output') -and $script:requests.Count -ne 1) {
        throw "Unproven geode output reached museum mutation: $name"
    }
    $passed.Add($name)
}
Reset-Case 'existing-root'
$rejected = $false
try { Initialize-RuntimeRustyKeyGeodeBoundary -Context $context -Directory (Join-Path $OutputRoot 'normal') | Out-Null }
catch { $rejected = $true }
if (-not $rejected -or $script:requests.Count -ne 0 -or $script:captureCount -ne 0) { throw 'Existing boundary was replayed.' }
$passed.Add('existing-root')
Reset-Case 'missing-isolation'
$context.IsolatedSavesPath = ''
$rejected = $false
try { Initialize-RuntimeRustyKeyGeodeBoundary -Context $context -Directory (Join-Path $OutputRoot 'missing-isolation') | Out-Null }
catch { $rejected = $true }
if (-not $rejected -or $script:requests.Count -ne 0 -or $script:captureCount -ne 0) { throw 'Missing save isolation reached runtime.' }
$passed.Add('missing-isolation')
foreach ($missing in @('RequirementInventoryPath','AcquisitionLoweringPath','MasterAnglerWindowsPath',
        'RouteTimingPath','FishingForecastManifestPath')) {
    Reset-Case ('preflight-' + $missing)
    $directory = Join-Path $OutputRoot $script:mode
    $arguments = @{ SupplyMode = 'current_geode'; NoBuild = $true; OutputDirectory = $directory }
    foreach ($name in @('RequirementInventoryPath','AcquisitionLoweringPath','MasterAnglerWindowsPath',
            'RouteTimingPath','FishingForecastManifestPath')) {
        if ($name -cne $missing) { $arguments[$name] = $PSCommandPath }
    }
    $message = ''
    try { & (Join-Path $PSScriptRoot 'Invoke-RuntimeRustyKeyThresholdSmoke.ps1') @arguments | Out-Null }
    catch { $message = $_.Exception.Message }
    if ($message -cne "Current geode supply requires explicit $missing." -or
        (Test-Path -LiteralPath $directory) -or $script:requests.Count -ne 0 -or $script:captureCount -ne 0) {
        throw "Missing authority input was not rejected before runtime startup: $missing; $message"
    }
    $passed.Add($script:mode)
}
Write-JsonFile -Path (Join-Path $OutputRoot 'self-test.json') -Value ([ordered]@{
    status = 'passed'; passed_count = $passed.Count; passed = $passed.ToArray(); native_game_run = $false
})
Write-Host "Rusty Key geode boundary checks passed: $($passed.Count); no game launched."
