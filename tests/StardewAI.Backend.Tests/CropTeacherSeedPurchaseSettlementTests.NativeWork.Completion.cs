using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.LiveTrainingLoop;
using Xunit;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private static readonly MethodInfo VerifyCompletionMethod = typeof(CropTeacherManagedSeedOwner).GetMethod(
        "VerifySharedNativeObjectiveCompletion",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Missing VerifySharedNativeObjectiveCompletion.");

    [Fact]
    public void VerifySharedNativeObjectiveCompletion_AcceptsLegalFreshActualShip()
    {
        var initialQueue = CreateQueue(CreateItemNode("executor.move_to_tile"), CreateItemNode("executor.ship_inventory_item_to_bin"));
        var verifiedItems = new[]
        {
            DeserializeItem(CreateItemNode("executor.move_to_tile")),
            DeserializeItem(CreateItemNode("executor.ship_inventory_item_to_bin"))
        };
        var raw = CreateReceipt(selectedCompleted: true, objectiveCompleted: true, CreateShippingObjective());
        var original = CreateLock(CreateShippingObjective());

        Assert.True(InvokeVerify(initialQueue, raw, original, verifiedItems, hasMechanicalContinuation: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void VerifySharedNativeObjectiveCompletion_UsesOnlyTheSoleRecordedCompletedObjectiveWhenCursorIsCleared(int count)
    {
        var queue = CreateQueue(CreateItemNode("executor.ship_inventory_item_to_bin"));
        var raw = CreateReceipt(true, true, CreateShippingObjective());
        raw["objective_continuation"] = null;
        raw["completed_objective_continuations"] = new JsonArray(Enumerable.Range(0, count)
            .Select(_ => (JsonNode)CreateShippingObjective()).ToArray());
        var items = new[] { DeserializeItem(CreateItemNode("executor.ship_inventory_item_to_bin")) };
        if (count == 1) Assert.True(InvokeVerify(queue, raw, CreateLock(CreateShippingObjective()), items, true));
        else Assert.Throws<InvalidDataException>(() => InvokeVerify(queue, raw, CreateLock(CreateShippingObjective()), items, true));
    }

    [Fact]
    public void VerifySharedNativeObjectiveCompletion_RejectsWhenOldQueueAppearsCompleteButActualIsOnlyMove()
    {
        var initialQueue = CreateQueue(CreateItemNode("executor.move_to_tile"), CreateItemNode("executor.ship_inventory_item_to_bin"));
        var verifiedItems = new[] { DeserializeItem(CreateItemNode("executor.move_to_tile")) };
        var raw = CreateReceipt(selectedCompleted: true, objectiveCompleted: true, CreateShippingObjective());
        var original = CreateLock(CreateShippingObjective());

        Assert.Throws<InvalidDataException>(() =>
            InvokeVerify(initialQueue, raw, original, verifiedItems, hasMechanicalContinuation: true));
    }

    [Fact]
    public void VerifySharedNativeObjectiveCompletion_RejectsFalseSelectedCompletedFlagWhenObjectiveIncomplete()
    {
        var initialQueue = CreateQueue(CreateItemNode("executor.ship_inventory_item_to_bin"));
        var verifiedItems = new[] { DeserializeItem(CreateItemNode("executor.ship_inventory_item_to_bin")) };
        var raw = CreateReceipt(selectedCompleted: true, objectiveCompleted: false, CreateShippingObjective());
        var original = CreateLock(CreateShippingObjective());

        Assert.Throws<InvalidDataException>(() =>
            InvokeVerify(initialQueue, raw, original, verifiedItems, hasMechanicalContinuation: true));
    }

    [Fact]
    public void VerifySharedNativeObjectiveCompletion_IncompleteFreshWorkReturnsFalse()
    {
        var initialQueue = CreateQueue(CreateItemNode("executor.move_to_tile"), CreateItemNode("executor.ship_inventory_item_to_bin"));
        var verifiedItems = new[] { DeserializeItem(CreateItemNode("executor.move_to_tile")) };
        var raw = CreateReceipt(selectedCompleted: false, objectiveCompleted: false, CreateShippingObjective());
        var original = CreateLock(CreateShippingObjective());

        Assert.False(InvokeVerify(initialQueue, raw, original, verifiedItems, hasMechanicalContinuation: true));
    }

    [Fact]
    public void VerifySharedNativeObjectiveCompletion_PreservesOldImmutableBehavior()
    {
        var initialQueue = CreateQueue(CreateItemNode("executor.ship_inventory_item_to_bin"));
        var original = CreateLock(CreateShippingObjective());
        var emptyVerified = Array.Empty<ActionQueueItem>();

        var fullCompletedRaw = CreateReceipt(selectedCompleted: true, objectiveCompleted: true, CreateShippingObjective());
        Assert.False(InvokeVerify(initialQueue, fullCompletedRaw, original, emptyVerified, hasMechanicalContinuation: false));

        var partialCompletedRaw = CreateReceipt(selectedCompleted: false, objectiveCompleted: true, CreateShippingObjective());
        Assert.True(InvokeVerify(initialQueue, partialCompletedRaw, original, emptyVerified, hasMechanicalContinuation: false));
    }

    [Theory]
    [InlineData("qualified_item_id", "(O)148")]
    [InlineData("slot_index", "8")]
    [InlineData("quantity", "2")]
    [InlineData("expected_unit_price", "40")]
    [InlineData("target_tile_x", "72")]
    [InlineData("target_tile_y", "15")]
    public void VerifySharedNativeObjectiveCompletion_RejectsActualShippingParameterDriftDespiteUnchangedContinuationMetadata(
        string driftedParameter,
        string driftedValue)
    {
        var initialQueue = CreateQueue(CreateItemNode("executor.ship_inventory_item_to_bin"));
        var driftedActual = DeserializeItem(CreateItemNode("executor.ship_inventory_item_to_bin", driftedParameter, driftedValue));
        var raw = CreateReceipt(selectedCompleted: true, objectiveCompleted: true, CreateShippingObjective());
        var original = CreateLock(CreateShippingObjective());

        Assert.Throws<InvalidDataException>(() =>
            InvokeVerify(initialQueue, raw, original, new[] { driftedActual }, hasMechanicalContinuation: true));
    }

    [Fact]
    public void VerifySharedNativeObjectiveCompletion_RejectsWrongObservedTypedObjective()
    {
        var initialQueue = CreateQueue(CreateItemNode("executor.ship_inventory_item_to_bin"));
        var verifiedItems = new[] { DeserializeItem(CreateItemNode("executor.ship_inventory_item_to_bin")) };
        var wrongObserved = CreateShippingObjective();
        wrongObserved["slot_index"] = "8";
        var raw = CreateReceipt(selectedCompleted: true, objectiveCompleted: true, wrongObserved);
        var original = CreateLock(CreateShippingObjective());

        Assert.Throws<InvalidDataException>(() =>
            InvokeVerify(initialQueue, raw, original, verifiedItems, hasMechanicalContinuation: true));
    }

    private static bool InvokeVerify(
        JsonObject initialQueue,
        JsonObject raw,
        SelectedQueueCandidateLock original,
        IReadOnlyList<ActionQueueItem> verifiedItems,
        bool hasMechanicalContinuation)
    {
        try
        {
            return (bool)VerifyCompletionMethod.Invoke(
                null,
                new object[] { initialQueue, raw, original, verifiedItems, hasMechanicalContinuation })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static SelectedQueueCandidateLock CreateLock(JsonObject objective, string optionId = "economy.ship_items") =>
        new(0, "cand-1", optionId, new JsonObject(), objective);

    private static JsonObject CreateQueue(params JsonObject[] items) =>
        new() { ["items"] = new JsonArray(items.Select(i => (JsonNode)i).ToArray()) };

    private static JsonObject CreateReceipt(bool selectedCompleted, bool objectiveCompleted, JsonObject observedObjective) =>
        new()
        {
            ["selected_candidate_completed"] = selectedCompleted,
            ["objective_continuation_completed"] = objectiveCompleted,
            ["objective_continuation"] = observedObjective
        };

    private static JsonObject CreateShippingObjective() =>
        new()
        {
            ["kind"] = "economy_shipping",
            ["option_id"] = "economy.ship_items",
            ["target_location"] = "Farm",
            ["item_id"] = "147",
            ["qualified_item_id"] = "(O)147",
            ["slot_index"] = "9",
            ["quantity"] = "1",
            ["expected_unit_price"] = "45",
            ["bin_location"] = "Farm",
            ["bin_tile_x"] = "71",
            ["bin_tile_y"] = "14",
            ["stand_tile_x"] = "71",
            ["stand_tile_y"] = "15"
        };

    private static JsonObject CreateItemNode(string primitiveOptionId, string? driftName = null, string? driftValue = null)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["continuation.option_id"] = "economy.ship_items",
            ["continuation.target_location"] = "Farm",
            ["continuation.item_id"] = "147",
            ["continuation.qualified_item_id"] = "(O)147",
            ["continuation.slot_index"] = "9",
            ["continuation.quantity"] = "1",
            ["continuation.expected_unit_price"] = "45",
            ["continuation.bin_location"] = "Farm",
            ["continuation.bin_tile_x"] = "71",
            ["continuation.bin_tile_y"] = "14",
            ["continuation.stand_tile_x"] = "71",
            ["continuation.stand_tile_y"] = "15",
            ["qualified_item_id"] = "(O)147",
            ["slot_index"] = "9",
            ["quantity"] = "1",
            ["expected_unit_price"] = "45",
            ["target_tile_x"] = "71",
            ["target_tile_y"] = "14"
        };
        if (driftName is not null && driftValue is not null)
            pairs[driftName] = driftValue;

        return new JsonObject
        {
            ["option_id"] = primitiveOptionId,
            ["normalized_command"] = new JsonObject
            {
                ["option_id"] = primitiveOptionId,
                ["parameters"] = new JsonArray(pairs.Select(kvp => (JsonNode)new JsonObject
                {
                    ["name"] = kvp.Key,
                    ["value"] = kvp.Value
                }).ToArray())
            }
        };
    }

    private static ActionQueueItem DeserializeItem(JsonObject itemNode) =>
        JsonSerializer.Deserialize<ActionQueueItem>(itemNode.ToJsonString())
        ?? throw new InvalidOperationException("Failed to deserialize ActionQueueItem.");
}
