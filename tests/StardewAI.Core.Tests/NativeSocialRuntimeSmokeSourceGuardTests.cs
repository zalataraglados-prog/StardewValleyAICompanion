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
    private static readonly string SocialSmokeSource = File.ReadAllText(
        FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));
    private static readonly string NpcAdapterSource = File.ReadAllText(
        FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "NpcReadAdapter.cs"));

    [Fact]
    public void SmokeScriptUsesEOnlyRuntimePaths()
    {
        var source = SocialSmokeSource;

        Assert.Contains("E:\\StardewValleyAICompanion-runtime", source, StringComparison.Ordinal);
        Assert.Contains("$RuntimeRoot", source, StringComparison.Ordinal);
        Assert.Contains("$savesPath", source, StringComparison.Ordinal);
        Assert.Contains("$runtimeGameDir", source, StringComparison.Ordinal);

        Assert.DoesNotContain("I:\\StardewValleyAICompanion", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Program Files", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$env:APPDATA", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SmokeScriptLaunchesHiddenWithAudioNull()
    {
        var source = SocialSmokeSource;

        Assert.Contains("WindowStyle Hidden", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SDL_AUDIODRIVER", source, StringComparison.Ordinal);
        Assert.Contains("ALSOFT_DRIVERS", source, StringComparison.Ordinal);
        Assert.Contains("\"dummy\"", source, StringComparison.Ordinal);
        Assert.Contains("\"null\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasPortConflictGuard()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Test-PortConflictGuard", source, StringComparison.Ordinal);
        Assert.Contains("Get-NetTCPConnection", source, StringComparison.Ordinal);
        Assert.Contains("8765", source, StringComparison.Ordinal);
        Assert.Contains("8767", source, StringComparison.Ordinal);
        Assert.Contains("$BackendPort", source, StringComparison.Ordinal);
        Assert.Contains("port_conflict_detected", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasExactProcessCleanup()
    {
        var source = SocialSmokeSource;

        Assert.Contains("KeepGameRunning", source, StringComparison.Ordinal);
        Assert.Contains("Stop-Process", source, StringComparison.Ordinal);
        Assert.Contains("HasExited", source, StringComparison.Ordinal);
        Assert.Contains("$gameProcess.Id", source, StringComparison.Ordinal);
        Assert.Contains("$backendProcess.Id", source, StringComparison.Ordinal);

        Assert.DoesNotContain("Stop-Process -Name", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Process Stardew", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SmokeScriptPreservesEnvironmentVariables()
    {
        var source = SocialSmokeSource;

        Assert.Contains("previousEnv", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TEST_SAVES", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_SAVE_ISOLATION_PATH", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TRAINING_MODE", source, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER", source, StringComparison.Ordinal);
        Assert.Contains("$env:ALSOFT_DRIVERS", source, StringComparison.Ordinal);
        Assert.Contains("$env:ASPNETCORE_URLS", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsFindLegalSocialTalkCandidate()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Find-LegalSocialTalkCandidate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("simple_non_villager_npc", source, StringComparison.Ordinal);
        Assert.DoesNotContain("current_route_window_complete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_npc_not_in_player_location", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_special_npc_check_action_branch_unsupported", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsFindLegalSocialGiftCandidate()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Find-LegalSocialGiftCandidate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("can_receive_gifts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("protected_from_auto_sell", source, StringComparison.Ordinal);
        Assert.DoesNotContain("object_quest_item", source, StringComparison.Ordinal);
        Assert.DoesNotContain("can_be_given_as_gift", source, StringComparison.Ordinal);
        Assert.DoesNotContain("special_item", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_special_switch_item_branch_unsupported", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_daily_limit_exhausted", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_weekly_limit_exhausted", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsManualSocialExecutionRequestBuilder()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Build-SocialTalkExecutionRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Build-SocialGiftExecutionRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Compute-StandTile", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsDirectSocialTrainingExecute()
    {
        var source = SocialSmokeSource;

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("\"executor.social_interact\"", StringComparison.Ordinal) &&
                (line.Contains("schema_version", StringComparison.Ordinal) ||
                 line.Contains("training_execution_request", StringComparison.Ordinal) ||
                 line.Contains("queue_item_id", StringComparison.Ordinal)))
            {
                Assert.Fail("Script contains a hand-written request line with option_id = executor.social_interact: " + line.Trim());
            }
        }
    }

    [Fact]
    public void SmokeScriptHasNoDirectNpcOrFriendshipOrOutcomeMutation()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("changeFriendship", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set_Friendship", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GiftsToday =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GiftsThisWeek =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("talkedToToday =", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".receiveGift(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("tryToReceiveActiveObject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", source, StringComparison.Ordinal);
        Assert.DoesNotContain("teleport", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasProductionChainArtifactsWithCorrectFieldPaths()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ranking-response-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-ranking-response.json", source, StringComparison.Ordinal);
        Assert.Contains("gift-ranking-response.json", source, StringComparison.Ordinal);
        Assert.Contains("plan-execution-episode-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-episode.json", source, StringComparison.Ordinal);
        Assert.Contains("gift-episode.json", source, StringComparison.Ordinal);

        Assert.Contains("daily-plan-response-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("compiled-queue-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("execution-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-feature-rows.jsonl", source, StringComparison.Ordinal);
        Assert.Contains("gift-feature-rows.jsonl", source, StringComparison.Ordinal);

        Assert.Contains("gift-failed.json", source, StringComparison.Ordinal);

        Assert.DoesNotContain("gift-skipped.json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("passed_talk_only", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesCorrectPlanStepKindMapping()
    {
        var source = SocialSmokeSource;

        Assert.Contains("move_to_tile", source, StringComparison.Ordinal);
        Assert.Contains("move_to_social_stand", source, StringComparison.Ordinal);
        Assert.Contains("social_interact", source, StringComparison.Ordinal);

        Assert.DoesNotContain("executor.move_to_social_stand", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".plan_steps", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".candidates", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesCorrectQueueOptionIdMapping()
    {
        var source = SocialSmokeSource;

        Assert.Contains("executor.move_to_tile", source, StringComparison.Ordinal);
        Assert.Contains("executor.social_interact", source, StringComparison.Ordinal);

        Assert.DoesNotContain("executor.move_to_social_stand", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDeploysModsBeforeLaunch()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Deploy-TransparentBridgeToRuntime.ps1", source, StringComparison.Ordinal);
        Assert.Contains("Deploy-RuntimeTestHarnessToRuntime.ps1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptTalksBeforeGiftWithProductionRecoveryClose()
    {
        var source = SocialSmokeSource;

        Assert.Contains("social.talk_npc", source, StringComparison.Ordinal);
        Assert.Contains("social.gift_npc", source, StringComparison.Ordinal);
        Assert.Contains("recovery.stabilize_day", source, StringComparison.Ordinal);
        Assert.Contains("close-dialogue", source, StringComparison.Ordinal);

        Assert.Contains("executor.close_menu", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFailClosedWhenGiftFails()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift LiveTrainingLoop returned exit code", source, StringComparison.Ordinal);
        Assert.Contains("gift-failed.json", source, StringComparison.Ordinal);
        Assert.Contains("full talk+gift smoke requires both phases", source, StringComparison.Ordinal);

        Assert.DoesNotContain("passed_talk_only", source, StringComparison.Ordinal);
        Assert.DoesNotContain("passed_all", source, StringComparison.Ordinal);
        Assert.DoesNotContain("gift-skipped.json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"skipped\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresEpisodeRankingAndFeatureRowArtifacts()
    {
        var source = SocialSmokeSource;

        Assert.Contains("plan-execution-episode-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("ranking-response-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-feature-rows.jsonl", source, StringComparison.Ordinal);
        Assert.Contains("gift-feature-rows.jsonl", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresRankingProvenanceForCandidates()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ranked_event_candidates", source, StringComparison.Ordinal);
        Assert.Contains("social_talk_current", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_current", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasRouteGraphBfsTraversal()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Invoke-RouteGraphBfsToNpc", source, StringComparison.Ordinal);
        Assert.Contains("route_graph", source, StringComparison.Ordinal);
        Assert.Contains("locations.route_graph", source, StringComparison.Ordinal);
        Assert.Contains("Queue", source, StringComparison.Ordinal);
        Assert.Contains("traverse_connector", source, StringComparison.Ordinal);

        Assert.DoesNotContain("teleport", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("directWarp", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SmokeScriptExecutesProductionSocialRouteCandidateBeforeLegacyTraversalFallback()
    {
        var source = SocialSmokeSource;

        var productionStage = source.IndexOf("Verify-ProductionSocialRouteStepArtifacts", StringComparison.Ordinal);
        var fallbackStage = source.IndexOf("Invoke-RouteGraphBfsToNpc", productionStage, StringComparison.Ordinal);
        Assert.True(productionStage >= 0);
        Assert.True(fallbackStage > productionStage);
        Assert.Contains("production-route-loop", source, StringComparison.Ordinal);
        Assert.Contains("kind -eq \"route_connector_tile\"", source, StringComparison.Ordinal);
        Assert.Contains("continuation.option_id", source, StringComparison.Ordinal);
        Assert.Contains("continuation.npc_name", source, StringComparison.Ordinal);
        Assert.Contains("continuation.target_location", source, StringComparison.Ordinal);
        Assert.Contains("social_route.position_source", source, StringComparison.Ordinal);
        Assert.Contains("social_route.future_schedule_projection", source, StringComparison.Ordinal);
        Assert.Contains("fresh_snapshot_replan_required=true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSocialRouteSmokeRequiresCompiledAndVerifiedConnectorArtifacts()
    {
        var source = SocialSmokeSource;

        Assert.Contains("production-route-ranking-response.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-daily-plan-response.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-compiled-queue.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-execution.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-episode.json", source, StringComparison.Ordinal);
        Assert.Contains("Production social route plan must compile exactly one executor.traverse_connector queue item", source, StringComparison.Ordinal);
        Assert.Contains("Production social route execution must contain one applied/verified traverse_connector result", source, StringComparison.Ordinal);
        Assert.Contains("Production social route arrival mismatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSocialRouteOnlyModeReportsItsBoundedRuntimeScope()
    {
        var source = SocialSmokeSource;

        Assert.Contains("[switch] $ProductionRouteOnly", source, StringComparison.Ordinal);
        Assert.Contains("one_production_social_route_connector_then_fresh_snapshot", source, StringComparison.Ordinal);
        Assert.Contains("full_multi_connector_pursuit_verified = $false", source, StringComparison.Ordinal);
        Assert.Contains("future_schedule_projection = \"not_used\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialContinuationDialogueRecoveryFlagReachesRuntimeSafetyGate()
    {
        var loopSource = LiveTrainingLoopSources.All;
        var runtimeSource = RuntimeHarnessSources.All;

        Assert.Contains("social_continuation_dialogue_recovery", loopSource, StringComparison.Ordinal);
        Assert.Contains("executionRequest.SocialContinuationDialogueRecovery", loopSource, StringComparison.Ordinal);
        Assert.Contains("request.SocialContinuationDialogueRecovery", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("allowSpeakerlessSocialContinuation", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("currentBox.characterDialogue?.speaker?.Name ?? string.Empty", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("string.Equals(currentSpeakerName, advance.InitialSpeakerName", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("dialogueBox.responses", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("Game1.eventUp", runtimeSource, StringComparison.Ordinal);
    }

}
