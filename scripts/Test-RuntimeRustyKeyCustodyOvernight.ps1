[CmdletBinding()]
param([Parameter(Mandatory)] [string] $OutputRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyEvidence.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyCustodyRoundTrip.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyCustodyOvernight.ps1')
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$passed = [Collections.Generic.List[string]]::new()
$context = [pscustomobject]@{ RunId = 'synthetic-overnight'; ExecutorRoot = 'http://unused.invalid'; SnapshotUrl = 'http://unused.invalid'
    BackendUrl = 'http://unused.invalid'; SnapshotProfile = 'full'; IsolatedSavesPath = $OutputRoot; LoopDll = (Join-Path $OutputRoot 'fake/loop.dll'); ArtifactDirectory = '' }
$proof = @{ run_id = $context.RunId; custody_reservation_id = 'custody'; protected_reservation_id = 'stone'
    chest_node_id = 'chest:synthetic'; acquisition_receipt_sha256 = ('a' * 64) }
function New-OvernightLedger {
    return (@{ save_id = 'synthetic-save'; player_id = '1'; material_reservations = @(
        @{ reservation_id = 'custody'; status = 'active'; node_id = $proof.chest_node_id; slot_index = 0; quantity = 1
            source_state_hash = 'original-position'; qualified_item_id = '(O)549'; output_custody = @{
                fresh_terminal_receipt_sha256 = $proof.acquisition_receipt_sha256; initial_position = @{ binding_sha256 = ('b' * 64) } } },
        @{ reservation_id = 'stone'; status = 'active'; node_id = $proof.chest_node_id; slot_index = 1; quantity = 3; goal_id = 'fixture.other_goal_stone' }
    ) } | ConvertTo-Json -Depth 30 | ConvertFrom-Json)
}
function New-OvernightSnapshot([int] $Clock, [int] $Day = 6, [int] $Donated = 59) {
    return (@{ state_hash = "synthetic-$Day-$Clock"; save_id = @{ status = 'available'; value = 'synthetic-save' }
        player_id = @{ status = 'available'; value = '1' }; state = @{
            time = @{ total_days = @{ status = 'available'; value = $Day }; time = @{ status = 'available'; value = $Clock } }
            player = @{ has_rusty_key = @{ status = 'available'; value = $false }; story_event = @{ status = 'available'; value = @{ active = $false } } }
            world_progress = @{ museum = @{ status = 'available'; value = @{ donated_count = $Donated; rusty_key_donation_threshold = 60
                rusty_key_reward_id = 'museum60'; rusty_key_reward_action = 'MarkEventSeen Host 295672'; has_rusty_key = $false
                rusty_key_reward_claimed = ($Donated -eq 60); rusty_key_prerequisite_event_seen = ($Donated -eq 60); rusty_key_event_seen = $false } } }
            farm = @{ material_inventory_graph = @{ status = 'available'; value = @{ status = 'available'; inventory_nodes = @(
                @{ node_id = $proof.chest_node_id; slots = @(@{ slot_index = 0; qualified_item_id = '(O)549'; stack = 1 },
                    @{ slot_index = 1; qualified_item_id = '(O)390'; stack = 3 }) }) } } }
        } } | ConvertTo-Json -Depth 30 | ConvertFrom-Json)
}
function Read-RuntimeLogicalJsonArtifact { param($Path, $CoreAssemblyPath) return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
function Invoke-JsonPost { param($Url, $Body)
    $script:calls++
    if ($Body.option_id -cne 'executor.wait_ticks' -or $Body.wait_ticks -ne 600 -or $Body.wait_max_slices -ne 3 -or
        $Body.wait_until_time -ne 1900 -or $Body.save_isolation_path -cne $context.IsolatedSavesPath) { throw 'Wait bypassed the original bounded native primitive.' }
    if ($script:mode -ceq 'ambiguous') { throw 'synthetic response loss; do not replay' }
    $result = @{ run_id = $(if ($script:mode -ceq 'foreign_receipt') { 'foreign' } else { $context.RunId }); queue_id = $Body.queue_id
        queue_item_id = $Body.queue_item_id; option_id = $Body.option_id; before_state_hash = $Body.before_state_hash
        status = 'applied'; primitive_verification_status = 'verified'; actual_ticks = 1800 }
    return @{ Value = $result; Raw = ($result | ConvertTo-Json) }
}
function Get-FreshRuntimeSnapshot { param($Url, $TimeoutSeconds)
    $time = if ($script:mode -ceq 'no_progress') { 1500 } else { 1500 + 100 * $script:calls }
    $snapshot = New-OvernightSnapshot $time
    if ($script:mode -ceq 'foreign_actor') { $snapshot.player_id.value = '2' }
    if ($script:mode -ceq 'missing_stock') { $snapshot.state.farm.material_inventory_graph.value.inventory_nodes[0].slots = @() }
    return @{ Value = $snapshot; Raw = ($snapshot | ConvertTo-Json -Depth 30) }
}
function Save-RuntimeSnapshotAndIngest { param($Path, $Capture, $BackendUrl, $SnapshotProfile) Write-Utf8Text -Path $Path -Value $Capture.Raw }
function Save-RuntimeRustyKeyLatestLedger { param($Context, $StateHash, $Path)
    $ledger = New-OvernightLedger
    if ($script:mode -ceq 'ledger_drift') { $ledger.material_reservations[0].source_state_hash = 'rewritten' }
    Write-JsonFile -Path $Path -Value $ledger
    return $Path
}
foreach ($script:mode in @('normal', 'ambiguous', 'foreign_receipt', 'no_progress', 'foreign_actor', 'missing_stock', 'ledger_drift', 'missing_clock', 'late_day')) {
    $context.ArtifactDirectory = Join-Path $OutputRoot $script:mode
    $proof.storage_after_snapshot_path = Join-Path $context.ArtifactDirectory 'stored.json'
    $proof.storage_ledger_path = Join-Path $context.ArtifactDirectory 'ledger.json'
    $snapshot = New-OvernightSnapshot 1500
    if ($script:mode -ceq 'missing_clock') { $snapshot.state.time.time.status = 'unavailable' }
    if ($script:mode -ceq 'late_day') { $snapshot.state.time.total_days.value = 222 }
    Write-JsonFile -Path $proof.storage_after_snapshot_path -Value $snapshot
    Write-JsonFile -Path $proof.storage_ledger_path -Value (New-OvernightLedger)
    $script:calls = 0; $rejected = $false
    try { $wait = Invoke-RuntimeRustyKeyCustodyEveningWait -Context $context -Proof $proof } catch { $rejected = $true }
    $expectedCalls = if ($script:mode -ceq 'normal') { 4 } elseif ($script:mode -in @('missing_clock', 'late_day')) { 0 } else { 1 }
    if ($rejected -ne ($script:mode -cne 'normal') -or $script:calls -ne $expectedCalls) { throw "Native custody wait contract failed: $script:mode" }
    if ($script:mode -ceq 'normal') {
        if ($wait.evening_time -ne 1900 -or $wait.steps.Count -ne 4 -or $wait.formal_training_authorized) { throw 'Wait promoted evidence or did not reach the evening.' }
        $script:normalWait = $wait
        $rejected = $false
        try { Invoke-RuntimeRustyKeyCustodyEveningWait -Context $context -Proof $proof | Out-Null } catch { $rejected = $true }
        if (-not $rejected -or $script:calls -ne 4) { throw 'Existing wait was blindly replayed.' }
        $passed.Add('no_wait_replay')
    }
    $passed.Add("wait_$script:mode")
}
foreach ($mode in @('normal', 'rewritten_position', 'changed_origin', 'missing_stone', 'extra_day', 'foreign_actor', 'no_sleep', 'unverified_control')) {
    $directory = Join-Path $OutputRoot "night-$mode"
    $night = @{ schema_version = 'end_of_night_transaction.v1'; run_id = $context.RunId; status = 'blocked'
        transaction_id = 'night'; queue_id = 'sleep'; sleep_queue_item_id = 'sleep-item'; evidence_path = 'night.json'
        start_total_days = 6; expected_total_days = 7; debug_negative_fixture_injected = $false
        saw_native_save_commit = $true; saw_expected_new_day = $true; saw_safe_new_day_ready_state = $true; failure_reason = 'end_of_night_transition_timeout:controller_handoff' }
    $receipt = @{ schema_version = 'queue_execution_receipt.v1'; run_id = $context.RunId; option_id = 'executor.sleep'; status = 'applied'
        primitive_verification_status = 'verified'; queue_id = 'sleep'; queue_item_id = 'sleep-item'; end_of_night_transaction_id = 'night'
        end_of_night_transaction_evidence_path = 'night.json'; end_of_night_transaction_status = 'ready_for_safe_next_day_action' }
    $next = New-OvernightSnapshot 600 7
    $stored = New-OvernightLedger
    switch ($mode) {
        'rewritten_position' { $stored.material_reservations[0].source_state_hash = 'midnight-rewrite' }
        'changed_origin' { $stored.material_reservations[0].output_custody.fresh_terminal_receipt_sha256 = ('f' * 64) }
        'missing_stone' { $next.state.farm.material_inventory_graph.value.inventory_nodes[0].slots = @($next.state.farm.material_inventory_graph.value.inventory_nodes[0].slots[0]) }
        'extra_day' { $next.state.time.total_days.value = 8 }
        'foreign_actor' { $next.player_id.value = '2' }
        'no_sleep' { $receipt.option_id = 'executor.wait_ticks' }
    }
    $final = New-OvernightSnapshot 1000 7 60
    $finalLedger = New-OvernightLedger
    $finalLedger.material_reservations[0].status = 'completed'
    foreach ($item in @(@('next.json', $next), @('next-ledger.json', $stored), @('final.json', $final), @('final-ledger.json', $finalLedger), @('sleep.json', $receipt))) {
        Write-JsonFile -Path (Join-Path $directory $item[0]) -Value $item[1]
    }
    $rolling = @{ run_id = $context.RunId; status = 'native_donation_threshold_observed'; input_identity_verified = ($mode -cne 'unverified_control')
        last_ledger_path = (Join-Path $directory 'final-ledger.json'); steps = @(
            @{ kind = 'day_recovery'; after_snapshot_path = (Join-Path $directory 'next.json'); ledger_path = (Join-Path $directory 'next-ledger.json'); execution_path = (Join-Path $directory 'sleep.json') },
            @{ kind = 'custody_transfer'; after_snapshot_path = (Join-Path $directory 'final.json'); ledger_path = (Join-Path $directory 'final-ledger.json'); execution_path = (Join-Path $directory 'other.json') },
            @{ kind = 'held_donation'; after_snapshot_path = (Join-Path $directory 'final.json'); ledger_path = (Join-Path $directory 'final-ledger.json'); execution_path = (Join-Path $directory 'other.json') }) }
    Write-JsonFile -Path (Join-Path $directory 'other.json') -Value @{ option_id = 'executor.transfer_material' }
    $rejected = $false
    try {
        $result = Assert-RuntimeRustyKeyCustodyOvernight -Context $context -Rolling $rolling -Proof $proof -WaitProof $script:normalWait -NativeNight $night
        if (-not $result.native_output_custody_overnight_observed -or $result.full_end_of_night_handoff_verified -or $result.formal_training_authorized) { throw 'Local custody evidence was narrowed or promoted.' }
    } catch { $rejected = $true; if ($mode -ceq 'normal') { throw } }
    if ($rejected -ne ($mode -cne 'normal')) { throw "Overnight custody guard failed: $mode" }
    $passed.Add("night_$mode")
}
$rejected = $false
try { & (Join-Path $PSScriptRoot 'Invoke-RuntimeRustyKeyThresholdSmoke.ps1') -CustodyOvernight -NoBuild }
catch { $rejected = $_.Exception.Message -like 'Custody overnight requires*' }
if (-not $rejected) { throw 'Invalid overnight options reached runtime startup.' }
$passed.Add('overnight_preflight_requires_native_acquisition_and_storage')
Write-JsonFile -Path (Join-Path $OutputRoot 'summary.json') -Value @{ status = 'passed'; passed_count = $passed.Count; passed = $passed.ToArray(); native_game_run = $false }
Write-Output "Rusty Key custody overnight checks passed: $($passed.Count); no game launched."
