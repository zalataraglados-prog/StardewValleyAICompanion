function Select-RustyKeySaveBoundaryCandidate {
    param([Parameter(Mandatory)] $Ranking)
    $candidates = @($Ranking.ranked_event_candidates | Where-Object {
        $_.available -is [bool] -and $_.available -and
        $_.option_id -eq 'recovery.stabilize_day' -and
        ($_.candidate_id -eq 'recovery:native_save_boundary' -or
            ($_.candidate_id -eq 'recovery:wait_for_native_control_settle' -and
                $_.kind -eq 'recovery_native_control_settle'))
    })
    if ($candidates.Count -ne 1) {
        throw "Rusty Key native handoff needs one safe save-boundary or native-control-settle candidate; found $($candidates.Count)."
    }
    return $candidates[0]
}

function Assert-RustyKeySnapshot {
    param(
        [Parameter(Mandatory)] $Snapshot,
        [Parameter(Mandatory)] [int] $DonatedCount,
        [Parameter(Mandatory)] [bool] $RewardApplied,
        [Parameter(Mandatory)] [bool] $KeyReceived
    )
    foreach ($entry in @(
        @("world_progress", "museum"),
        @("player", "has_rusty_key"),
        @("player", "story_event"),
        @("time", "total_days")
    )) {
        if ([string]$Snapshot.state.($entry[0]).($entry[1]).status -notin
                @("available", "derived")) {
            throw "Rusty Key required field is unreadable: $($entry -join '.')"
        }
    }
    $museum = Read-StateValue $Snapshot "world_progress" "museum"
    if ($museum.donated_count -isnot [long] -and
        $museum.donated_count -isnot [int]) {
        throw "Rusty Key donated count is not an integer."
    }
    if ($museum.donated_count -ne $DonatedCount -or
        $museum.rusty_key_donation_threshold -ne 60 -or
        [string]$museum.rusty_key_reward_id -ne "museum60" -or
        [string]$museum.rusty_key_reward_action -ne
            "MarkEventSeen Host 295672") {
        throw "Rusty Key locked reward or donation count drifted."
    }
    foreach ($entry in @(
        @($museum.rusty_key_reward_claimed, $RewardApplied),
        @($museum.rusty_key_prerequisite_event_seen, $RewardApplied),
        @($museum.rusty_key_event_seen, $KeyReceived),
        @($museum.has_rusty_key, $KeyReceived),
        @((Read-StateValue $Snapshot "player" "has_rusty_key"), $KeyReceived)
    )) {
        if ($entry[0] -isnot [bool] -or $entry[0] -ne $entry[1]) {
            throw "Rusty Key native boolean is missing, mistyped or unexpected."
        }
    }
    if ($KeyReceived) {
        $active = (Read-StateValue $Snapshot "player" "story_event").active
        if ($active -isnot [bool] -or $active -ne $false) {
            throw "Rusty Key story has not returned control."
        }
    }
}

function Assert-RustyKeyNativeSteps {
    param([Parameter(Mandatory)] [object[]] $Steps)
    foreach ($step in $Steps) {
        if ([string]$step.option_id -like "debug.*" -or
            [string]$step.status -ne "applied" -or
            [string]$step.source -ne "runtime_test_harness_executor" -or
            [string]$step.primitive_verification_status -ne "verified" -or
            @($step.block_reasons).Count -ne 0) {
            throw "Rusty Key chain contains an unverified or debug step."
        }
    }
    $donations = @($Steps | Where-Object {
        $_.option_id -eq "executor.donate_museum_item"
    })
    $sleeps = @($Steps | Where-Object { $_.option_id -eq "executor.sleep" })
    $stories = @($Steps | Where-Object {
        $_.option_id -eq "executor.advance_story_event" -and
        @($_.changed_facts | Where-Object {
            $_.path -eq "player.story_event.event_id" -and
            [string]$_.before -eq "66" -and
            [string]::IsNullOrEmpty([string]$_.after)
        }).Count -eq 1
    })
    if ($donations.Count -ne 1 -or $sleeps.Count -ne 1 -or
        $stories.Count -ne 1) {
        throw "Rusty Key chain needs one donation, sleep and event 66 completion."
    }
    if ([string]$donations[0].source_planning_option_id -ne "museum.donate_items" -or
        [string]$sleeps[0].source_planning_option_id -ne "recovery.stabilize_day" -or
        [string]$stories[0].source_planning_option_id -ne "story.advance_event") {
        throw "Rusty Key native receipts lost their planning lineage."
    }
    foreach ($fact in @(
        @("world_progress.museum.donated_count", "59", "60"),
        @("quests.mail_received.museum60", "false", "true"),
        @("player.events_seen.295672", "false", "true")
    )) {
        if (@($donations[0].changed_facts | Where-Object {
            $_.path -eq $fact[0] -and [string]$_.before -eq $fact[1] -and
            [string]$_.after -eq $fact[2]
        }).Count -ne 1) {
            throw "Rusty Key donation receipt lacks exact native fact: $($fact[0])"
        }
    }
    foreach ($reason in @(
        "native_event_input_only", "boundary=event_completed", "skipEvent_not_used"
    )) {
        if (@($stories[0].primitive_verification_reasons) -notcontains $reason) {
            throw "Rusty Key event receipt lacks native completion: $reason"
        }
    }
    if (@($stories[0].changed_facts | Where-Object {
        $_.path -eq "player.story_event.event_seen" -and
        [string]$_.before -eq "false" -and [string]$_.after -eq "true"
    }).Count -ne 1) {
        throw "Rusty Key event 66 was not natively marked seen."
    }
}
