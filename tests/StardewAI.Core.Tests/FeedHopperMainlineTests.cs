using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FeedHopperMainlineTests
{
    [Fact]
    public void FeedHopperIsRegisteredAsAnAdmittedNativeMechanicalAction()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("animals.withdraw_feed_hopper_hay");

        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-276" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-276" }, declaration.RuntimeEvidenceIds);
        Assert.Contains(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(declaration.OptionId, out _));
    }

    [Fact]
    public void ExactProjectionCompilesThroughCandidateDailyPlanAndQueue()
    {
        var snapshot = Snapshot("ready", unfedAnimals: 2, expectedWithdrawal: 2);
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "animals.withdraw_feed_hopper_hay" }, includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "feed_hopper_expected_withdrawal_quantity" && parameter.Value == "2");
        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        Assert.Equal("withdraw_feed_hopper_hay", Assert.Single(plan.Steps).Kind);
        var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);

        Assert.Empty(queueItem.BlockingReasons);
        Assert.Equal("withdraw_feed_hopper_hay", Assert.Single(queueItem.NormalizedCommand.Steps).StepType);
        Assert.Contains("root_location.pieces_of_hay-=2", queueItem.NormalizedCommand.Steps[0].ExpectedEffect,
            StringComparison.Ordinal);
        Assert.Contains(queueItem.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "safe_slot_index" && parameter.Value == "5");
    }

    [Theory]
    [InlineData("blocked_no_unfed_animals", 0, 1, "feed_hopper_not_ready:blocked_no_unfed_animals")]
    [InlineData("blocked_silo_empty", 2, 0, "feed_hopper_not_ready:blocked_silo_empty")]
    public void UselessOrImpossibleWithdrawalIsRejectedUpstream(
        string status,
        int unfedAnimals,
        int expectedWithdrawal,
        string expectedReason)
    {
        var snapshot = Snapshot(status, unfedAnimals, expectedWithdrawal);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "animals.withdraw_feed_hopper_hay" }, includeExecutorCalibrationOptions: true)
            .Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(expectedReason, candidate.BlockReasons);
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = "animals.withdraw_feed_hopper_hay",
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        TileX = candidate.TileX,
        TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SnapshotEnvelope Snapshot(string status, int unfedAnimals, int expectedWithdrawal)
    {
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Coop"), tile_x = Field(8), tile_y = Field(9), inventory = Field(Array.Empty<object>()),
                safe_item_context = Field(new
                {
                    current_tool_index = 2, active_object_selected = true, safe_slot_available = true,
                    safe_slot_index = 5, safe_slot_kind = "empty", policy = "prefer_empty_slot_then_tool_slot"
                })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) },
            current_location = new
            {
                objects = Field(new[]
                {
                    new
                    {
                        tile_x = 10, tile_y = 10, item_id = "99", qualified_item_id = "(BC)99",
                        parent_sheet_index = 99, big_craftable = true, name = "Feed Hopper",
                        type = "StardewValley.Object", object_type = "Crafting",
                        feed_hopper_withdrawal = new
                        {
                            status, canonical_item_id = "99", canonical_qualified_item_id = "(BC)99",
                            hay_qualified_item_id = "(O)178", root_location_id = "Farm",
                            silo_hay_before = expectedWithdrawal > 0 ? 10 : 0,
                            animal_count = 2, animal_limit = 4, placed_hay_count = 0,
                            remaining_trough_capacity = 4, unfed_animal_count = unfedAnimals,
                            expected_withdrawal_quantity = expectedWithdrawal,
                            inventory_accepts_exact_withdrawal = expectedWithdrawal > 0,
                            expected_silo_hay_after = expectedWithdrawal > 0 ? 10 - expectedWithdrawal : 0,
                            expected_inventory_hay_delta = expectedWithdrawal,
                            expected_native_location_action_return = true,
                            target_runtime_type = "StardewValley.Object",
                            stand_tiles = new[]
                            {
                                new { tile_x = 10, tile_y = 9, on_map = true, collision_blocked = false, object_trap_blocked = false, available = true }
                            },
                            interaction_kind = "location_object", expected_action_type = "FeedHopper",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(BC)99->CheckForActionOnFeedHopper->root_location.piecesOfHay_minus_exact_withdrawal->player.inventory_(O)178_plus_exact_withdrawal"
                        }
                    }
                })
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-28T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static object Field(object value) => new
    {
        value, status = "available", source = new { kind = "game_object", path = "test" },
        adapter = "test", read_at_tick = 1, confidence = 1
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
