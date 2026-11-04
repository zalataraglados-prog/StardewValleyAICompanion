param([string] $CapturedRunDirectory = "")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeRustyKeyEvidence.ps1")
$script:passed = 0
function Assert-Rejected {
    param([scriptblock] $Action)
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "Malformed Rusty Key evidence was accepted." }
    $script:passed++
}
function New-Snapshot {
    return @{
        state = @{
            time = @{ total_days = @{ status = "available"; value = 5 } }
            player = @{
                has_rusty_key = @{ status = "available"; value = $false }
                story_event = @{ status = "available"; value = @{ active = $false } }
            }
            world_progress = @{ museum = @{ status = "available"; value = @{
                donated_count = 59; rusty_key_donation_threshold = 60
                rusty_key_reward_id = "museum60"; rusty_key_reward_action = "MarkEventSeen Host 295672"
                rusty_key_reward_claimed = $false; rusty_key_prerequisite_event_seen = $false
                rusty_key_event_seen = $false; has_rusty_key = $false
            } } }
        }
    }
}
$boundaryCandidate = [pscustomobject]@{
    candidate_id = 'recovery:native_save_boundary'; option_id = 'recovery.stabilize_day'
    kind = 'recovery_sleep_immediately'; available = $true
}
$settleCandidate = [pscustomobject]@{
    candidate_id = 'recovery:wait_for_native_control_settle'; option_id = 'recovery.stabilize_day'
    kind = 'recovery_native_control_settle'; available = $true
}
foreach ($candidate in @($boundaryCandidate, $settleCandidate)) {
    $selected = Select-RustyKeySaveBoundaryCandidate -Ranking @{ ranked_event_candidates = @($candidate) }
    if (-not [object]::ReferenceEquals($candidate, $selected)) { throw 'Native handoff must retain the actual ranked candidate.' }
    $script:passed++
}
foreach ($candidates in @(@(), @($boundaryCandidate, $settleCandidate), @($settleCandidate, $settleCandidate))) {
    Assert-Rejected { Select-RustyKeySaveBoundaryCandidate -Ranking @{ ranked_event_candidates = $candidates } }
}
foreach ($change in @(
    @{ available = $false }, @{ available = 'true' }, @{ kind = 'unrelated_wait' },
    @{ candidate_id = 'unrelated_wait' }, @{ option_id = 'story.advance_event' }
)) {
    $candidate = $settleCandidate.PSObject.Copy()
    foreach ($name in $change.Keys) { $candidate.$name = $change[$name] }
    Assert-Rejected { Select-RustyKeySaveBoundaryCandidate -Ranking @{ ranked_event_candidates = @($candidate) } }
}
$snapshot = New-Snapshot
Assert-RustyKeySnapshot -Snapshot $snapshot -DonatedCount 59 -RewardApplied $false -KeyReceived $false
$script:passed++
foreach ($field in @("rusty_key_reward_claimed", "rusty_key_prerequisite_event_seen", "rusty_key_event_seen", "has_rusty_key")) {
    foreach ($value in @($null, "false", 0, $true)) {
        $snapshot = New-Snapshot
        $snapshot.state.world_progress.museum.value[$field] = $value
        Assert-Rejected { Assert-RustyKeySnapshot $snapshot 59 $false $false }
    }
}
$snapshot = New-Snapshot
$snapshot.state.player.has_rusty_key.status = "unavailable"
Assert-Rejected { Assert-RustyKeySnapshot $snapshot 59 $false $false }
$snapshot = New-Snapshot
$snapshot.state.world_progress.museum.value.donated_count = "59"
Assert-Rejected { Assert-RustyKeySnapshot $snapshot 59 $false $false }
$snapshot = New-Snapshot
$museum = $snapshot.state.world_progress.museum.value
$museum.donated_count = 60
$museum.rusty_key_reward_claimed = $true
$museum.rusty_key_prerequisite_event_seen = $true
Assert-RustyKeySnapshot $snapshot 60 $true $false
$script:passed++
$museum.rusty_key_event_seen = $true
$museum.has_rusty_key = $true
$snapshot.state.player.has_rusty_key.value = $true
Assert-RustyKeySnapshot $snapshot 60 $true $true
$script:passed++
foreach ($active in @($null, "false", $true)) {
    $snapshot.state.player.story_event.value.active = $active
    Assert-Rejected { Assert-RustyKeySnapshot $snapshot 60 $true $true }
}
$steps = @(
    @{ option_id = "executor.donate_museum_item"; status = "applied"; primitive_verification_status = "verified"; block_reasons = @()
        source = "runtime_test_harness_executor"; source_planning_option_id = "museum.donate_items"
        changed_facts = @(
            @{ path = "world_progress.museum.donated_count"; before = "59"; after = "60" },
            @{ path = "quests.mail_received.museum60"; before = "false"; after = "true" },
            @{ path = "player.events_seen.295672"; before = "false"; after = "true" }
        ) },
    @{ option_id = "executor.sleep"; status = "applied"; primitive_verification_status = "verified"; block_reasons = @()
        source = "runtime_test_harness_executor"; source_planning_option_id = "recovery.stabilize_day" },
    @{ option_id = "executor.advance_story_event"; status = "applied"; primitive_verification_status = "verified"; block_reasons = @()
        source = "runtime_test_harness_executor"; source_planning_option_id = "story.advance_event"
        primitive_verification_reasons = @("native_event_input_only", "boundary=event_completed", "skipEvent_not_used")
        changed_facts = @(
            @{ path = "player.story_event.event_id"; before = "66"; after = "" },
            @{ path = "player.story_event.event_seen"; before = "false"; after = "true" }
        ) }
)
Assert-RustyKeyNativeSteps -Steps $steps
$script:passed++
Assert-Rejected { Assert-RustyKeyNativeSteps -Steps $steps[0..1] }
Assert-Rejected { Assert-RustyKeyNativeSteps -Steps ($steps + $steps[2]) }
Assert-Rejected { Assert-RustyKeyNativeSteps -Steps ($steps + @{
    option_id = "debug.setup_museum_donation"; status = "applied"; primitive_verification_status = "verified"; block_reasons = @()
}) }
$steps[2].primitive_verification_reasons = @("native_event_input_only", "boundary=event_completed")
Assert-Rejected { Assert-RustyKeyNativeSteps -Steps $steps }
$steps[2].primitive_verification_reasons += "skipEvent_not_used"
$steps[2].source_planning_option_id = "debug.setup_story_event"
Assert-Rejected { Assert-RustyKeyNativeSteps -Steps $steps }
$steps[2].source_planning_option_id = "story.advance_event"
$steps[0].changed_facts[0].before = "0"
Assert-Rejected { Assert-RustyKeyNativeSteps -Steps $steps }
if (-not [string]::IsNullOrWhiteSpace($CapturedRunDirectory)) {
    $summary = Get-Content -LiteralPath (Join-Path $CapturedRunDirectory "summary.json") -Raw | ConvertFrom-Json
    $captured = @($summary.transitions | ForEach-Object {
        $execution = Get-Content -LiteralPath $_.execution_path -Raw | ConvertFrom-Json
        $execution.step_results
    })
    Assert-RustyKeyNativeSteps -Steps $captured
    $reader = Join-Path (Split-Path -Parent $PSScriptRoot) `
        "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.Core.dll"
    $first = Read-RuntimeLogicalJsonArtifact -Path $summary.transitions[0].before_snapshot_path -CoreAssemblyPath $reader
    $last = Read-RuntimeLogicalJsonArtifact -Path $summary.transitions[-1].after_snapshot_path -CoreAssemblyPath $reader
    Assert-RustyKeySnapshot $first 59 $false $false
    Assert-RustyKeySnapshot $last 60 $true $true
    $script:passed += 3
    Write-Host "Captured native Rusty Key receipts revalidated: $($summary.run_id)"
}
Write-Host "Rusty Key evidence behavior tests passed: $script:passed"
