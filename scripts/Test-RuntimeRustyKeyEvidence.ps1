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
$steps = @(
    @{ option_id = "executor.donate_museum_item"; status = "applied"; primitive_verification_status = "verified"; block_reasons = @() },
    @{ option_id = "executor.sleep"; status = "applied"; primitive_verification_status = "verified"; block_reasons = @() },
    @{ option_id = "executor.advance_story_event"; status = "applied"; primitive_verification_status = "verified"; block_reasons = @()
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
Write-Host "Rusty Key evidence behavior tests passed: $script:passed"
