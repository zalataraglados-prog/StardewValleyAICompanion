using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void CompilerPreservesSocialEnvelopeAndBlocksNativeExecutor()
    {
        var snapshot = CompleteSocialSnapshot();
        var request = Request(snapshot.StateHash, "social.gift_npc");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" },
            new SmallModelActionParameter { Name = "slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)66" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("social_requires_daily_plan_compilation", queue.Items[0].BlockingReasons);
        Assert.NotNull(queue.Items[0].NormalizedCommand.SocialPlan);
        var socialPlan = queue.Items[0].NormalizedCommand.SocialPlan!;
        Assert.Equal("gift", socialPlan.ActionKind);
        Assert.Equal("Abigail", socialPlan.RequestedNpcName);
        Assert.Equal(0, socialPlan.RequestedSlotIndex);
        Assert.Contains(socialPlan.TrainingRecordingContract, item => item == "friendship_points_before_after_delta");
        Assert.Empty(queue.Items[0].NormalizedCommand.Steps);
    }

    [Fact]
    public void DailyPlanCompilerCompilesTalkCandidateIntoMoveAndSocialInteract()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.True(socialCandidate.Available);

        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("social_interact", plan.Steps[1].Kind);
        Assert.DoesNotContain(plan.Steps[1].SafetyConstraints, s => s == "social_runtime_executor_not_implemented");
        Assert.Contains(plan.Steps[1].Parameters, p => p.Name == "social_action_kind" && p.Value == "talk");
        Assert.Contains(plan.Steps[1].Parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(plan.Steps[1].Parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.True(plan.Steps[0].TargetTileX.HasValue && plan.Steps[1].TargetTileX.HasValue);
        Assert.NotEqual(plan.Steps[0].TargetTileX, plan.Steps[1].TargetTileX);
    }

    [Fact]
    public void DailyPlanCompilerCompilesGiftCandidateIntoMoveAndSocialInteract()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.True(socialCandidate.Available);

        var prediction = ToPrediction(socialCandidate, "social.gift_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("social_interact", plan.Steps[1].Kind);
        var socialStep = plan.Steps[1];
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_name" && p.Value == "Abigail");
        Assert.Contains(socialStep.Parameters, p => p.Name == "qualified_item_id" && p.Value == "(O)66");
        Assert.Contains(socialStep.Parameters, p => p.Name == "social_action_kind" && p.Value == "gift");
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Equal(10, socialStep.TargetTileX);
        Assert.Equal(10, socialStep.TargetTileY);
        Assert.NotEqual(plan.Steps[0].TargetTileX, socialStep.TargetTileX);
    }

    [Fact]
    public void SocialInteractPlansToExecutorOptionWithCorrectMapping()
    {
        var compiler = new ActionQueueCompiler();
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        var queue = compiler.Compile(plan, snapshot);

        var items = queue.Items;
        Assert.Equal(2, items.Length);
        Assert.Equal("executor.move_to_tile", items[0].OptionId);
        Assert.Equal("executor.social_interact", items[1].OptionId);
        Assert.Equal("pending", items[1].Status);
        Assert.DoesNotContain("social_runtime_executor_not_implemented", items[1].BlockingReasons);
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_action_kind_talk_or_gift_required");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_stand_not_adjacent_to_npc");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_candidate_stand_npc_mismatch");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_candidate_stand_npc_evidence_missing");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_npc_name_required");

        var socialParams = items[1].NormalizedCommand.Parameters;
        Assert.Contains(socialParams, p => p.Name == "social_action_kind" && p.Value == "talk");
        Assert.Contains(socialParams, p => p.Name == "npc_name" && p.Value == "Abigail");
        Assert.Contains(socialParams, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(socialParams, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Contains(socialParams, p => p.Name == "stand_tile_x");
        Assert.Contains(socialParams, p => p.Name == "stand_tile_y");

        var targetStandX = items[0].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_x")?.Value;
        var targetStandY = items[0].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_y")?.Value;
        var targetNpcX = items[1].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_x")?.Value;
        var targetNpcY = items[1].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_y")?.Value;
        Assert.NotNull(targetNpcX);
        Assert.NotNull(targetNpcY);
        Assert.NotEqual(targetStandX, targetNpcX);
    }

    [Fact]
    public void DirectHighLevelSocialActionCannotBypassCompilation()
    {
        var snapshot = CompleteSocialSnapshot();
        var request = Request(snapshot.StateHash, "social.talk_npc");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.DoesNotContain("social_native_executor_not_implemented", queue.Items[0].BlockingReasons);
        Assert.Empty(queue.Items[0].NormalizedCommand.Steps);
    }

    [Fact]
    public void SocialCandidateIncludesRouteDistanceInParameters()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);

        Assert.Contains(socialCandidate.Parameters, p => p.Name == "route_distance_tiles");
        Assert.Contains(socialCandidate.Parameters, p => p.Name == "route_distance_ticks");
        Assert.Contains(socialCandidate.Parameters, p => p.Name == "native_interaction_planner_budget_ticks");

        var distanceTiles = int.Parse(socialCandidate.Parameters.First(p => p.Name == "route_distance_tiles").Value);
        var distanceTicks = int.Parse(socialCandidate.Parameters.First(p => p.Name == "route_distance_ticks").Value);
        var plannerBudget = int.Parse(socialCandidate.Parameters.First(p => p.Name == "native_interaction_planner_budget_ticks").Value);
        Assert.True(distanceTiles >= 0);
        Assert.True(distanceTicks >= 0);
        Assert.Equal(120, plannerBudget);
        Assert.Equal(distanceTicks + plannerBudget, socialCandidate.EstimatedTicks);
    }

    [Fact]
    public void AvailabilityDurationIsUnknownForBlockedCandidateNoStandTile()
    {
        var blockedCollision = "{\"width\":20,\"height\":20,\"notable_tiles\":[" +
            "{\"tile_x\":11,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":9,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":11,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":9,\"collision_blocked\":true}" +
            "]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: blockedCollision);
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var candidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_no_reachable_adjacent_stand_tile", candidate.BlockReasons);

        Assert.Equal(-1, candidate.EstimatedTicks);
        var distanceTiles = int.Parse(candidate.Parameters.First(p => p.Name == "route_distance_tiles").Value);
        Assert.True(distanceTiles < 0);
        var distanceTicks = int.Parse(candidate.Parameters.First(p => p.Name == "route_distance_ticks").Value);
        Assert.Equal(-1, distanceTicks);
        Assert.Contains(candidate.Parameters, p => p.Name == "native_interaction_planner_budget_ticks");
        Assert.Contains("estimated_duration_ticks=-1", candidate.ExpectedEffect);
        Assert.Contains("duration_estimate_status=planner_budget_assumption_pending_runtime_calibration", candidate.ExpectedEffect);
    }

    [Fact]
    public void DailyPlanCompilerSkipsWhenSocialCandidateMissingStandTile()
    {
        var blockedCollision = "{\"width\":20,\"height\":20,\"notable_tiles\":[" +
            "{\"tile_x\":11,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":9,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":11,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":9,\"collision_blocked\":true}" +
            "]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: blockedCollision);
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.False(socialCandidate.Available);
        Assert.Contains("social_no_reachable_adjacent_stand_tile", socialCandidate.BlockReasons);

        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void ExecutorSocialInteractIsRegisteredAndHasCorrectParameters()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        var spec = registry.GetRequired("executor.social_interact");

        Assert.NotNull(spec);
        Assert.Equal("social", spec.Domain);
        Assert.Equal(OptionBehaviorCategories.Mechanical, spec.BehaviorCategory);
        Assert.Equal(CompilerResponsibilities.FullActionExpansion, spec.CompilerResponsibility);
        Assert.Equal(TrainingRoles.ExecutorCalibration, spec.TrainingRole);
        Assert.DoesNotContain("social_runtime_executor_not_implemented", spec.SafetyConstraints);
    }

    [Fact]
    public void CompileChainPreservesNpcNameAndActionKindThroughSocialInteractRequest()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        var prediction = ToPrediction(socialCandidate, "social.gift_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        var socialItem = queue.Items.FirstOrDefault(i => i.OptionId == "executor.social_interact");
        Assert.NotNull(socialItem);
        var parameters = socialItem.NormalizedCommand.Parameters;
        Assert.Contains(parameters, p => p.Name == "npc_name" && p.Value == "Abigail");
        Assert.Contains(parameters, p => p.Name == "social_action_kind" && p.Value == "gift");
        Assert.Contains(parameters, p => p.Name == "qualified_item_id" && p.Value == "(O)66");
        Assert.Contains(parameters, p => p.Name == "slot_index" && p.Value == "0");
        Assert.Contains(parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Contains(parameters, p => p.Name == "stand_tile_x");
        Assert.Contains(parameters, p => p.Name == "stand_tile_y");
        Assert.Contains(parameters, p => p.Name == "expected_friendship_delta" && p.Value == "80");

        var npcTargetX = parameters.FirstOrDefault(p => p.Name == "target_tile_x")?.Value;
        var npcTargetY = parameters.FirstOrDefault(p => p.Name == "target_tile_y")?.Value;
        Assert.Equal("10", npcTargetX);
        Assert.Equal("10", npcTargetY);

        var blocking = socialItem.BlockingReasons;
        Assert.DoesNotContain("social_runtime_executor_not_implemented", blocking);
        Assert.DoesNotContain(blocking, r => r == "social_action_kind_talk_or_gift_required");
        Assert.DoesNotContain(blocking, r => r == "social_stand_not_adjacent_to_npc");
        Assert.DoesNotContain(blocking, r => r == "social_candidate_stand_npc_mismatch");
        Assert.DoesNotContain(blocking, r => r == "social_candidate_stand_npc_evidence_missing");
    }

    [Fact]
    public void SelectReachableStandTileNavigatesObstaclesAndShortestRouteExceedsManhattan()
    {
        var blockedTiles = new List<string>();
        for (var y = 1; y <= 4; y++)
        {
            for (var x = 3; x <= 8; x++)
            {
                blockedTiles.Add("{\"tile_x\":" + x + ",\"tile_y\":" + y + ",\"collision_blocked\":true}");
            }
        }
        var collisionValue = "{\"width\":16,\"height\":10,\"notable_tiles\":[" + string.Join(",", blockedTiles) + "]}";
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":3,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]",
            collisionValue: collisionValue,
            playerTileX: 2,
            playerTileY: 3);
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.True(socialCandidate.Available);

        var standX = socialCandidate.Parameters.First(p => p.Name == "stand_tile_x").Value;
        var standY = socialCandidate.Parameters.First(p => p.Name == "stand_tile_y").Value;
        var npcX = socialCandidate.Parameters.First(p => p.Name == "npc_tile_x").Value;
        var npcY = socialCandidate.Parameters.First(p => p.Name == "npc_tile_y").Value;
        var distanceTiles = int.Parse(socialCandidate.Parameters.First(p => p.Name == "route_distance_tiles").Value);

        Assert.Equal("10", npcX);
        Assert.Equal("3", npcY);
        var manhattanToNpc = Math.Abs(2 - 10) + Math.Abs(3 - 3);
        Assert.True(distanceTiles > manhattanToNpc,
            "Route distance (" + distanceTiles + ") should exceed Manhattan distance (" + manhattanToNpc + ") due to obstacle wall");

        Assert.True(int.Parse(standX) >= 9, "Stand tile should be on right side of obstacle wall");
    }

    [Fact]
    public void PrimitiveSocialInteractWithoutCandidateEvidenceIsBlocked()
    {
        var snapshot = CompleteSocialSnapshot();
        var request = Request(snapshot.StateHash, "executor.social_interact");
        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        var blocking = queue.Items[0].BlockingReasons;
        Assert.DoesNotContain("social_runtime_executor_not_implemented", blocking);
        Assert.Contains("social_candidate_stand_npc_evidence_missing", blocking);
    }

    [Fact]
    public void SocialInteractPlanStepNpcTileDiffersFromStandTile()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        var moveStep = plan.Steps[0];
        var socialStep = plan.Steps[1];
        Assert.Equal("move_to_tile", moveStep.Kind);
        Assert.Equal("social_interact", socialStep.Kind);
        Assert.True(moveStep.TargetTileX.HasValue);
        Assert.True(moveStep.TargetTileY.HasValue);
        Assert.True(socialStep.TargetTileX.HasValue);
        Assert.True(socialStep.TargetTileY.HasValue);
        Assert.NotEqual(moveStep.TargetTileX, socialStep.TargetTileX);
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Contains(socialStep.Parameters, p => p.Name == "stand_tile_x");
        Assert.Contains(socialStep.Parameters, p => p.Name == "stand_tile_y");
    }

    private static PolicyEventCandidatePrediction ToPrediction(EventCandidate candidate, string optionId)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = candidate.CandidateId,
            OptionId = optionId,
            Kind = candidate.Kind,
            Available = candidate.Available,
            Score = 1.0,
            LocationId = candidate.LocationId,
            TileX = candidate.TileX,
            TileY = candidate.TileY,
            ExpectedEffect = candidate.ExpectedEffect,
            EstimatedTicks = candidate.EstimatedTicks,
            EnergyCost = candidate.EnergyCost,
            AllowedNow = candidate.AllowedNow,
            AllowedToday = candidate.AllowedToday,
            NextOpenTime = candidate.NextOpenTime,
            EffectiveOpenTime = candidate.EffectiveOpenTime,
            ClosesAt = candidate.ClosesAt,
            WaitCost = candidate.WaitCost,
            GateReasons = candidate.GateReasons,
            TimelineStatus = candidate.Available ? "ready_now" : candidate.AllowedToday == true ? "deferred" : "blocked",
            ScheduledWaitCost = candidate.AllowedToday == true ? candidate.WaitCost : null,
            Parameters = candidate.Parameters,
            BlockReasons = candidate.BlockReasons
        };
    }

}
