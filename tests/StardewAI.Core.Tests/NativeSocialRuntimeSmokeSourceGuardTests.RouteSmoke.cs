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
    public void SmokeScriptReliesOnProductionRouteGraph()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("directWarp", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("teleport", source, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("route_graph", source, StringComparison.Ordinal);
        Assert.Contains("locations.route_graph", source, StringComparison.Ordinal);
        Assert.Contains("executor.traverse_connector", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptBfsTraversalSavesAllEdgeRequestsAndResults()
    {
        var source = SocialSmokeSource;

        Assert.Contains("route-graph-bfs-edge", source, StringComparison.Ordinal);
        Assert.Contains("-request.json", source, StringComparison.Ordinal);
        Assert.Contains("-result.json", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-success.json", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-failed.json", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-no-npc.json", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptBfsFailsClosedWhenNoRouteExists()
    {
        var source = SocialSmokeSource;

        Assert.Contains("No bounded transparent route", source, StringComparison.Ordinal);
        Assert.Contains("failed_closed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRecordsLocationStateHashBeforeAfter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("before_location", source, StringComparison.Ordinal);
        Assert.Contains("after_talk_location", source, StringComparison.Ordinal);
        Assert.Contains("after_gift_location", source, StringComparison.Ordinal);
        Assert.Contains("before_state_hash", source, StringComparison.Ordinal);
        Assert.Contains("after_talk_state_hash", source, StringComparison.Ordinal);
        Assert.Contains("after_gift_state_hash", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesPowerShell51CompatibleSyntax()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("??", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesCorrectRouteGraphEdgeFieldNames()
    {
        var source = SocialSmokeSource;

        Assert.Contains(".resolved", source, StringComparison.Ordinal);
        Assert.Contains(".from_location", source, StringComparison.Ordinal);
        Assert.Contains(".from_x", source, StringComparison.Ordinal);
        Assert.Contains(".from_y", source, StringComparison.Ordinal);
        Assert.Contains(".kind", source, StringComparison.Ordinal);
        Assert.Contains(".target_location", source, StringComparison.Ordinal);

        Assert.DoesNotContain("source_location_id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("target_location_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresExactTraverseConnectorRequestFields()
    {
        var source = SocialSmokeSource;

        Assert.Contains("target_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("target_tile_y", source, StringComparison.Ordinal);
        Assert.Contains("connector_kind", source, StringComparison.Ordinal);
        Assert.Contains("expected_target_location", source, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_y", source, StringComparison.Ordinal);

        Assert.DoesNotContain("\"placeholder\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDoesNotDefaultMissingKindToWarp()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("\"warp\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("edge.kind", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresAppliedAndVerifiedTraverseResult()
    {
        var source = SocialSmokeSource;

        Assert.Contains("result not applied/verified", source, StringComparison.Ordinal);
        Assert.Contains("primitive_verification_status", source, StringComparison.Ordinal);
        Assert.Contains("\"applied\"", source, StringComparison.Ordinal);
        Assert.Contains("\"verified\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresArrivalTileVerificationAfterTraverse()
    {
        var source = SocialSmokeSource;

        Assert.Contains("arrival tile mismatch", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-bad-arrival-tile.json", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFiltersRouteEdgesByResolvedContract()
    {
        var source = SocialSmokeSource;

        Assert.Contains(".resolved -eq", source, StringComparison.Ordinal);
        Assert.Contains(".from_location", source, StringComparison.Ordinal);
        Assert.Contains(".target_location", source, StringComparison.Ordinal);
        Assert.Contains(".from_x", source, StringComparison.Ordinal);
        Assert.Contains(".from_y", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsEdgeConnectorKindFieldAlias()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("edgeData.connector_kind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edge.connector_kind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.branch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsWrongTileCoordinateAliases()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("edgeData.target_tile_x", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.target_tile_y", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.warp_tile_x", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.warp_tile_y", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesEdgeFromCoordinatesForTargetTile()
    {
        var source = SocialSmokeSource;

        Assert.Contains("target_tile_x =", source, StringComparison.Ordinal);
        Assert.Contains("target_tile_y =", source, StringComparison.Ordinal);
        if (source.Contains("from_x", StringComparison.Ordinal) && source.Contains("target_tile_x", StringComparison.Ordinal))
        {
            Assert.Contains("edgeData.target_x", source, StringComparison.Ordinal);
            Assert.Contains("edgeData.target_y", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SmokeScriptRequiresEdgeKindNotBeNullOrEmpty()
    {
        var source = SocialSmokeSource;

        Assert.Contains("missing or empty kind", source, StringComparison.Ordinal);
        Assert.Contains("has empty kind", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasGetParameterValueHelper()
    {
        var source = SocialSmokeSource;

        Assert.Contains("function Get-ParameterValue", source, StringComparison.Ordinal);
        Assert.Contains("Required parameter", source, StringComparison.Ordinal);
        Assert.Contains("Duplicate parameter", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasGetCandidateIdFromPreconditionsHelper()
    {
        var source = SocialSmokeSource;

        Assert.Contains("function Get-CandidateIdFromPreconditions", source, StringComparison.Ordinal);
        Assert.Contains("candidate_id:*", source, StringComparison.Ordinal);
        Assert.Contains("No candidate_id precondition", source, StringComparison.Ordinal);
        Assert.Contains("Multiple candidate_id preconditions", source, StringComparison.Ordinal);
        Assert.Contains("Get-CandidateIdFromPreconditions -Preconditions", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptReadsNpcNameSlotAndStackFromParameters()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"npc_name\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"slot_index\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"qualified_item_id\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterInt -Parameters $selectedCandidate.parameters -Name \"item_stack_before\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"gift_updates_normal_limits\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterInt -Parameters $selectedCandidate.parameters -Name \"npc_tile_x\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterInt -Parameters $selectedCandidate.parameters -Name \"stand_tile_x\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptAcceptsTileZero()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("-eq 0", source.Split(new[] { "throw" }, StringSplitOptions.None)
            .Where(s => s.Contains("tile")).DefaultIfEmpty(string.Empty).First(), StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptAcceptsOneItemGiftNullAfter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("stack should be null when exactly one item consumed", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Gift execution missing gift_stack_after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresMultiItemStackDecrement()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift stack after must be non-null when before=", source, StringComparison.Ordinal);
        Assert.Contains("stack did not decrease by exactly 1", source, StringComparison.Ordinal);
        Assert.Contains("stack before must be >= 1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptValidatesExactFacingTowardNpc()
    {
        var source = SocialSmokeSource;

        Assert.Contains("does not point toward NPC", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 0", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 1", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 2", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 3", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresExactlyOneSocialResult()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Expected exactly 1 verified applied social_interact result", source, StringComparison.Ordinal);
        Assert.Contains("Expected exactly 1 social_interact plan step", source, StringComparison.Ordinal);
        Assert.Contains("Expected exactly 1 executor.social_interact queue item", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveTrainingLoopRankingNonOverwrittenPaths()
    {
        var loopSource = LiveTrainingLoopSources.All;

        Assert.Contains("ranking-response-\" + iteration.ToString(\"D4\") + \".json\"", loopSource, StringComparison.Ordinal);
        Assert.Contains("continuation-refresh-evidence-\" + iteration.ToString(\"D4\")", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("replan-ranking-response-", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ranking-response-0000", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ranking-response-0001.json", loopSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresGiftSlotAndItemIdNonNull()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift execution missing gift_slot_before", source, StringComparison.Ordinal);
        Assert.Contains("Gift candidate missing qualified_item_id", source, StringComparison.Ordinal);
        Assert.Contains("Gift execution missing gift_item_id_before", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptPreservesGiftCountersWhenNotNormalLimits()
    {
        var source = SocialSmokeSource;

        Assert.Contains("gift_updates_normal_limits", source, StringComparison.Ordinal);
        Assert.Contains("candidateGiftUpdatesNormal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionGiftPursuitUsesExistingRollingSocialChain()
    {
        var source = SocialSmokeSource;

        Assert.Contains("[switch] $ProductionGiftPursuitOnly", source, StringComparison.Ordinal);
        Assert.Contains("--daily-plan-candidate-options \"social.gift_npc\"", source, StringComparison.Ordinal);
        Assert.Contains("Verify-ProductionSocialPursuitArtifacts", source, StringComparison.Ordinal);
        Assert.Contains("-ExpectedContinuationOption \"social.gift_npc\"", source, StringComparison.Ordinal);
        Assert.Contains("-RequireSingleItemGiftConsumed", source, StringComparison.Ordinal);
        Assert.Contains("Production social pursuit did not verify any connector traversal", source, StringComparison.Ordinal);
        Assert.Contains("Production gift pursuit expected stack_before=1", source, StringComparison.Ordinal);
        Assert.Contains("Production gift pursuit expected stack_after=null", source, StringComparison.Ordinal);
        Assert.Contains("same_objective_multi_connector_single_item_gift_pursuit", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleGiftFixtureIsDebugOnlyAndDoesNotMutateNpcOutcome()
    {
        var runtimeSource = RuntimeHarnessSources.All;
        var fixtureSource = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Social.Fixtures.cs"));

        Assert.Contains("debug.setup_single_gift_item", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("ExecuteSetupSingleGiftItem", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("ItemRegistry.Create(request.QualifiedItemId, 1)", fixtureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("changeFriendship", fixtureSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GiftsToday", fixtureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GiftsThisWeek", fixtureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("receiveGift(", fixtureSource, StringComparison.Ordinal);
    }

}
