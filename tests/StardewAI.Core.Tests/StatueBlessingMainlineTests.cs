using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StatueBlessingMainlineTests
{
    [Fact]
    public void StatueBlessingClosesFiveEvidenceGatesAndIsTrainingEligible()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("rewards.claim_statue_blessing");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-270" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-270" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-270" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-270" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-270" }, declaration.OutputEvidenceIds);
        Assert.Contains(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(declaration.OptionId, out _));
    }

    [Theory]
    [InlineData(0, "speed")]
    [InlineData(1, "luck")]
    [InlineData(2, "energy")]
    [InlineData(3, "waters")]
    [InlineData(4, "friendship")]
    [InlineData(5, "fangs")]
    [InlineData(6, "butterfly")]
    public void ExactDailyBlessingBecomesOneParameterlessGoalAndOneNativeStep(int blessingId, string kind)
    {
        var snapshot = Snapshot("ready", blessingId, kind, 7);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "rewards.claim_statue_blessing" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "statue_blessing_id" && parameter.Value == blessingId.ToString());
        Assert.Contains("effect_kind=" + kind, candidate.ExpectedEffect, StringComparison.Ordinal);

        var ranked = new[]
        {
            new PolicyEventCandidatePrediction
            {
                CandidateId = candidate.CandidateId,
                OptionId = "rewards.claim_statue_blessing",
                Kind = candidate.Kind,
                Available = candidate.Available,
                LocationId = candidate.LocationId,
                TileX = candidate.TileX,
                TileY = candidate.TileY,
                ExpectedEffect = candidate.ExpectedEffect,
                EstimatedTicks = candidate.EstimatedTicks,
                Parameters = candidate.Parameters
            }
        };
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("claim_statue_blessing", planStep.Kind);
        Assert.Contains(planStep.SafetyConstraints, value => value == "small_model_emits_only_the_parameterless_claim_goal");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("claim_statue_blessing", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "statue_blessing_buff_id" && parameter.Value == "statue_of_blessings_" + blessingId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_x" && parameter.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_y" && parameter.Value == "19");
    }

    [Fact]
    public void ModelSuppliedMechanicalFieldsAreReboundFromFreshSnapshot()
    {
        var snapshot = Snapshot("ready", 4, "friendship", 7);
        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "statue-blessing.rebind",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "claim.blessing",
                    OptionId = "rewards.claim_statue_blessing",
                    Rationale = "claim daily reward",
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "statue_blessing_id", Value = "6" },
                        new SmallModelActionParameter { Name = "target_tile_x", Value = "999" },
                        new SmallModelActionParameter { Name = "native_contract", Value = "forged" }
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "statue_blessing_id" && parameter.Value == "4");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_x" && parameter.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "native_contract" && parameter.Value.StartsWith("Object.checkForAction_StatueOfBlessings", StringComparison.Ordinal));
    }

    [Fact]
    public void AlreadyClaimedDailyStateIsExcludedUpstream()
    {
        var snapshot = Snapshot("already_claimed_today", 2, "energy", 7);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "rewards.claim_statue_blessing" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(candidate.BlockReasons, reason => reason.StartsWith("statue_blessing_not_ready", StringComparison.Ordinal));
    }

    [Fact]
    public void RainOrFestivalDenominatorCannotProduceButterflyBlessing()
    {
        var snapshot = Snapshot("ready", 5, "fangs", 6);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "rewards.claim_statue_blessing" },
            includeExecutorCalibrationOptions: true).Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "statue_blessing_random_upper_bound_exclusive" && parameter.Value == "6");
    }

    private static SnapshotEnvelope Snapshot(string status, int blessingId, string kind, int upperBound)
    {
        var projection = new
        {
            status,
            location_id = "Farm",
            farming_mastery_value = 1,
            farming_mastery_unlocked = true,
            days_played = 42,
            random_upper_bound_exclusive = upperBound,
            blessing_id = blessingId,
            buff_id = "statue_of_blessings_" + blessingId,
            blessing = new { blessing_id = blessingId, buff_id = "statue_of_blessings_" + blessingId, kind, exact_effect = "exact_" + kind, source = "decompile" },
            has_been_blessed_today = status == "already_claimed_today",
            has_active_blessing_buff = status == "already_claimed_today",
            statues = new[]
            {
                new
                {
                    tile_x = 20,
                    tile_y = 20,
                    qualified_item_id = "(BC)StatueOfBlessings",
                    target_runtime_type = "StardewValley.Object",
                    stand_tiles = new[]
                    {
                        new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, available = true },
                        new { tile_x = 19, tile_y = 20, on_map = true, collision_blocked = false, available = true }
                    }
                }
            },
            qualified_item_id = "(BC)StatueOfBlessings",
            interaction_kind = "location_object",
            expected_action_type = "StatueOfBlessings",
            native_contract = "Object.checkForAction_StatueOfBlessings->CheckForActionOnBlessedStatue->Farmer.applyBuff(statue_of_blessings_N)"
        };
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"),
                tile_x = Field(18),
                tile_y = Field(19)
            },
            menus = new
            {
                active_menu = Field(new { is_open = false, type = "none" })
            },
            current_location = new
            {
                statue_blessing = Field(projection)
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-27T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
