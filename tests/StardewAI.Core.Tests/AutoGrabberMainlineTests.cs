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

public sealed class AutoGrabberMainlineTests
{
    [Fact]
    public void AutoGrabberIsRegisteredAsAnAdmittedNativeMechanicalAction()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("animals.collect_auto_grabber_contents");

        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-278" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-278" }, declaration.RuntimeEvidenceIds);
        Assert.Contains(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(declaration.OptionId, out _));
    }

    [Fact]
    public void ExactProjectionCompilesThroughCandidateDailyPlanAndQueue()
    {
        var snapshot = Snapshot("ready", transferableStacks: 2, transferQuantity: 5);
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "animals.collect_auto_grabber_contents" }, includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "auto_grabber_expected_transfer_quantity" && parameter.Value == "5");
        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        Assert.Equal("collect_auto_grabber_contents", Assert.Single(plan.Steps).Kind);
        var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);

        Assert.Empty(queueItem.BlockingReasons);
        Assert.Equal("collect_auto_grabber_contents", Assert.Single(queueItem.NormalizedCommand.Steps).StepType);
        Assert.Contains("player.inventory_quantity+=5", queueItem.NormalizedCommand.Steps[0].ExpectedEffect,
            StringComparison.Ordinal);
        Assert.Contains(queueItem.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "safe_slot_index" && parameter.Value == "5");
    }

    [Theory]
    [InlineData("blocked_empty", 0, 0, "auto_grabber_not_ready:blocked_empty")]
    [InlineData("blocked_inventory_rejects_all_stacks", 0, 0, "auto_grabber_not_ready:blocked_inventory_rejects_all_stacks")]
    public void EmptyOrUnfittableAutoGrabberIsRejectedUpstream(
        string status,
        int transferableStacks,
        int transferQuantity,
        string expectedReason)
    {
        var snapshot = Snapshot(status, transferableStacks, transferQuantity);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "animals.collect_auto_grabber_contents" }, includeExecutorCalibrationOptions: true)
            .Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(expectedReason, candidate.BlockReasons);
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = "animals.collect_auto_grabber_contents",
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        TileX = candidate.TileX,
        TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SnapshotEnvelope Snapshot(string status, int transferableStacks, int transferQuantity)
    {
        var beforeRows = transferQuantity > 0
            ? new[]
            {
                Row(0, "(O)184", 2, 'a'),
                Row(1, "(O)176", 3, 'b')
            }
            : Array.Empty<object>();
        var transferableRows = transferableStacks > 0 ? beforeRows : Array.Empty<object>();
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
                        tile_x = 10, tile_y = 10, item_id = "165", qualified_item_id = "(BC)165",
                        parent_sheet_index = 165, big_craftable = true, name = "Auto-Grabber",
                        type = "StardewValley.Object", object_type = "Crafting",
                        auto_grabber_collection = new
                        {
                            status, canonical_item_id = "165", canonical_qualified_item_id = "(BC)165",
                            held_container_runtime_type = "StardewValley.Objects.Chest",
                            contents_before_json = JsonSerializer.Serialize(beforeRows),
                            transferable_contents_json = JsonSerializer.Serialize(transferableRows),
                            remaining_contents_json = "[]",
                            content_stack_count_before = beforeRows.Length,
                            transferable_stack_count = transferableStacks,
                            expected_stack_count_after = 0,
                            content_quantity_before = transferQuantity,
                            expected_transfer_quantity = transferQuantity,
                            expected_quantity_after = 0,
                            expected_native_location_action_return = true,
                            target_runtime_type = "StardewValley.Object",
                            stand_tiles = new[]
                            {
                                new { tile_x = 10, tile_y = 9, on_map = true, collision_blocked = false, object_trap_blocked = false, available = true }
                            },
                            interaction_kind = "location_object_menu_transaction", expected_action_type = "AutoGrabber",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(BC)165->CheckForActionOnAutoGrabber->ItemGrabMenu->receiveLeftClick->grabItemFromAutoGrabber->player.inventory"
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

    private static object Row(int slot, string itemId, int quantity, char hashCharacter) => new
    {
        SourceSlotIndex = slot,
        RuntimeType = "StardewValley.Object",
        QualifiedItemId = itemId,
        Quality = 0,
        SourceUnitStateSha256 = new string(hashCharacter, 64),
        InventoryUnitStateSha256 = new string(hashCharacter, 64),
        Quantity = quantity
    };

    private static object Field(object value) => new
    {
        value, status = "available", source = new { kind = "game_object", path = "test" },
        adapter = "test", read_at_tick = 1, confidence = 1
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
