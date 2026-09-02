using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class NativeSocialRuntimeSmokeSourceGuardTests
{
    [Fact]
    public void CollisionGridTreatsLockedFriendshipTouchDoorsAsDynamicObstacles()
    {
        var source = ShopAccessReadAdapterSources.All;

        Assert.Contains("ReadFriendshipDoorGate(location, touchAction)", source, StringComparison.Ordinal);
        Assert.Contains("friendshipDoor is { AllowedNow: false }", source, StringComparison.Ordinal);
        Assert.Contains("friendship_door_allowed_now", source, StringComparison.Ordinal);
        Assert.Contains("getFriendshipHeartLevelForNPC(name) >= 2", source, StringComparison.Ordinal);
        Assert.Contains("location.IsGreenRainingHere()", source, StringComparison.Ordinal);
        Assert.Contains("Game1.year == 1", source, StringComparison.Ordinal);
        Assert.Contains("Sebastian", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesSocialExecutionFieldsDirectly()
    {
        var source = SocialSmokeSource;

        Assert.Contains("social_native_handled", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_name", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_location_before", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("social_player_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("social_player_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("social_player_facing_before", source, StringComparison.Ordinal);
        Assert.Contains("social_dialogue_open_before", source, StringComparison.Ordinal);
        Assert.Contains("social_dialogue_open_after", source, StringComparison.Ordinal);
        Assert.Contains("social_talked_to_today_before", source, StringComparison.Ordinal);
        Assert.Contains("social_talked_to_today_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_stack_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_stack_after", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_before", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_this_week_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_this_week_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_item_id_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_item_id_after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresGiftStackDecreaseByOne()
    {
        var source = SocialSmokeSource;

        Assert.Contains("gift_stack_before", source, StringComparison.Ordinal);
        Assert.Contains("gift_stack_after", source, StringComparison.Ordinal);
        Assert.Contains("stack did not decrease by exactly 1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresFriendshipNonNullBeforeAfter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("friendship_before", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_before", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_after", source, StringComparison.Ordinal);
        Assert.Contains("missing friendship before", source, StringComparison.Ordinal);
        Assert.Contains("missing friendship after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresGiftCounterIncrements()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gifts today did not increment by 1", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptNeverReturnsPassedTalkOnlyOrSkipped()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("passed_talk_only", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"skipped\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("gift-skipped.json", source, StringComparison.Ordinal);

        Assert.Contains("\"passed\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesProductionRecoveryCloseNotHandWrittenRequest()
    {
        var source = SocialSmokeSource;

        Assert.Contains("recovery.stabilize_day", source, StringComparison.Ordinal);
        Assert.Contains("close-dialogue-loop", source, StringComparison.Ordinal);

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("executor.close_menu", StringComparison.Ordinal) &&
                line.Contains("option_id", StringComparison.Ordinal) &&
                line.Contains("\"executor.close_menu\"", StringComparison.Ordinal))
            {
                Assert.Fail("Script contains a hand-written close request with option_id = executor.close_menu: " + line.Trim());
            }
        }
    }

    [Fact]
    public void SmokeScriptStartsAspNetCoreBackend()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ASPNETCORE_URLS", source, StringComparison.Ordinal);
        Assert.Contains("BackendPort", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.Backend.csproj", source, StringComparison.Ordinal);
        Assert.Contains("$backendUrl/health", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasBackendProcessCleanup()
    {
        var source = SocialSmokeSource;

        Assert.Contains("$backendProcess", source, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Id $backendProcess.Id", source, StringComparison.Ordinal);
        Assert.Contains("$backendProcess.HasExited", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesMoveBeforeSocialUsingActualFieldPaths()
    {
        var source = SocialSmokeSource;

        Assert.Contains("kind", source, StringComparison.Ordinal);
        Assert.Contains("step_id", source, StringComparison.Ordinal);
        Assert.Contains("move_to_tile", source, StringComparison.Ordinal);
        Assert.Contains("move_to_social_stand", source, StringComparison.Ordinal);
        Assert.Contains("source_action_id", source, StringComparison.Ordinal);
        Assert.Contains("executor.move_to_tile", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCompileChainRequiresBackendAndLiveTrainingLoop()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Build-SocialTalkExecutionRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Build-SocialGiftExecutionRequest", source, StringComparison.Ordinal);

        Assert.Contains("dotnet run", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.Backend", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.LiveTrainingLoop", source, StringComparison.Ordinal);
        Assert.Contains("--use-daily-plan", source, StringComparison.Ordinal);
        Assert.Contains("social.talk_npc", source, StringComparison.Ordinal);
        Assert.Contains("social.gift_npc", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DailyPlanCompilerMapsSocialTalkToSocialInteractPlanStep()
    {
        var compilerSource = DailyPlanCompilerSources.All;

        Assert.Contains("social_talk_current", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_gift_current", compilerSource, StringComparison.Ordinal);
        Assert.Contains("SocialInteractionSteps", compilerSource, StringComparison.Ordinal);
        Assert.Contains("\"social_interact\"", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_action_kind", compilerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionQueueCompilerValidatesSocialInteractWithStandTileAdjacency()
    {
        var compilerSource = ActionQueueCompilerSources.All;

        Assert.Contains("ValidateSocialInteractPlan", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_stand_not_adjacent_to_npc", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_candidate_stand_npc_mismatch", compilerSource, StringComparison.Ordinal);
        Assert.Contains("executor.social_interact", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_action_kind", compilerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveTrainingLoopSavesRankingResponse()
    {
        var loopSource = LiveTrainingLoopSources.All;

        Assert.Contains("ranking-response-", loopSource, StringComparison.Ordinal);
        Assert.Contains("continuation-refresh-evidence-", loopSource, StringComparison.Ordinal);
        Assert.Contains("policy_model_invoked\"] = false", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("replan-ranking-response-", loopSource, StringComparison.Ordinal);
        Assert.Contains("WriteAllTextAsync", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NextRankingIndex", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IncrementRankingIndex", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_rankingIndex", loopSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesAvailableCandidateSelectionNotRankDefault()
    {
        var source = SocialSmokeSource;

        Assert.Contains(".available -eq", source, StringComparison.Ordinal);
        Assert.Contains("candidate_id", source, StringComparison.Ordinal);
        Assert.Contains("timeline_status -eq \"blocked\"", source, StringComparison.Ordinal);
        Assert.Contains("option_id -eq \"social.talk_npc\"", source, StringComparison.Ordinal);
        Assert.Contains("option_id -eq \"social.gift_npc\"", source, StringComparison.Ordinal);
        Assert.Contains("daily-plan-max-candidates 1", source, StringComparison.Ordinal);

        Assert.DoesNotContain(".rank -ge 0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossChecksCandidateIdThroughPlanQueueExecution()
    {
        var source = SocialSmokeSource;

        Assert.Contains("planCandidateId", source, StringComparison.Ordinal);
        Assert.Contains("moveStepCandidateId", source, StringComparison.Ordinal);
        Assert.Contains("source_action_id", source, StringComparison.Ordinal);
        Assert.Contains("queue_item_id", source, StringComparison.Ordinal);
        Assert.Contains("candidate_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptValidatesTalkManhattanAdjacencyAndFacing()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Manhattan-adjacent", source, StringComparison.Ordinal);
        Assert.Contains("player_facing_before", source, StringComparison.Ordinal);
        Assert.Contains("does not point toward NPC", source, StringComparison.Ordinal);
        Assert.Contains("talked_to_before", source, StringComparison.Ordinal);
        Assert.Contains("talked_to_after", source, StringComparison.Ordinal);
        Assert.Contains("social_menu_open_before", source, StringComparison.Ordinal);
        Assert.Contains("social_current_dialogue_count_before", source, StringComparison.Ordinal);
        Assert.Contains("social_current_dialogue_key_before", source, StringComparison.Ordinal);
        Assert.Contains("social_current_dialogue_speaker_name_before", source, StringComparison.Ordinal);
        Assert.Contains("post-talk dialogue/menu signal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptValidatesGiftStackNullOrDecrementAndWeekCounter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("item_stack_before", source, StringComparison.Ordinal);
        Assert.Contains("gift_updates_normal_limits", source, StringComparison.Ordinal);
        Assert.Contains("gifts_week_before", source, StringComparison.Ordinal);
        Assert.Contains("gifts_week_after", source, StringComparison.Ordinal);
        Assert.Contains("should be null when exactly one item consumed", source, StringComparison.Ordinal);
        Assert.Contains("Gifts this week did not increment", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDerivesSummaryFlagsFromEvidenceNotHardcoded()
    {
        var source = SocialSmokeSource;

        Assert.Contains("giftStackDecreased", source, StringComparison.Ordinal);
        Assert.Contains("talkArtifacts.HasRankedCandidate", source, StringComparison.Ordinal);
        Assert.Contains("giftVerification.HasRankedCandidate", source, StringComparison.Ordinal);

        Assert.DoesNotContain("gift_stack_decreased_by_one = $true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptIngestsSnapshotBeforePreTalkRankProbe()
    {
        var source = SocialSmokeSource;

        Assert.Contains("bridge-snapshot-ingested.json", source, StringComparison.Ordinal);
        Assert.Contains("/api/v1/snapshots", source, StringComparison.Ordinal);
        Assert.Contains("pre-talk-rank-probe.json", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDoesNotUseDirectCoordinateTeleportForMovement()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("teleport", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("setPosition", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug.visible_walk", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGiftLegalityDelegatedToProductionSocialCandidateBuilder()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("is_divorced", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("social_gift_divorced", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_weekly_limit", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_can_receive_gifts_incomplete", source, StringComparison.Ordinal);
    }

}
