using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryTransientBoundaryTests
{
    private static NativeInventoryTransientSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt) =>
        NativeOutputObservationBoundary.ReadInventoryTransients(request, receipt, 99, 111, "Farm", "123");

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) Fixture(bool pickaxe = false)
    {
        var (request, receipt) = pickaxe ? NativeInventoryPayloadBoundaryTests.IdentityFixture(
            "StardewValley.Tools.Pickaxe", "(T)Pickaxe", true, "<bigCraftable>false</bigCraftable>") :
            NativeInventoryPayloadBoundaryTests.Fixture();
        request.ActionOriginInventoryTransients = true;
        var actor = Guid.NewGuid().ToString("N");
        foreach (var endpoint in new[] { receipt.InventoryPayloadBefore!, receipt.InventoryPayloadAfter! })
        {
            endpoint.SelectionTransientState = new()
            {
                SchemaVersion = "native_selection_transients.v1", ActorReferenceId = actor, ItemStowed = false,
                TemporaryItem = new() { IsNull = true }, CurrentItem = ItemReference(endpoint.Slots![1])
            };
            foreach (var slot in endpoint.Slots!.Where(slot => slot.IsEmpty == false))
                slot.TransientState = ItemState(slot.IsTool == true, pickaxe && slot.IsTool == true);
        }
        return (request, receipt);
    }

    private static ActionOriginItemTransientState ItemState(bool tool, bool pickaxe = false) => new()
    {
        SchemaVersion = "native_item_transients.v1", SetFlagOnPickup = Text(null), TempDataPresent = false,
        Cache = new() { QualifiedItemId = Text(null), ContextTagsDirty = true, ContextTagsPresent = false },
        Tool = tool ? new() { LastUser = new() { IsNull = true }, PlayUseSounds = false, Description = Text(null), DisplayName = Text("") } : null,
        Pickaxe = pickaxe ? new() { BoulderTileX = -4, BoulderTileY = 37, HitsToBoulder = int.MaxValue } : null
    };

    private static ActionOriginNullableText Text(string? value) => new() { IsNull = value is null, Value = value };
    private static ActionOriginNativeItemReference ItemReference(ActionOriginInventoryPayloadSlot slot) => new()
    { IsNull = false, ReferenceId = slot.ItemReferenceId, RuntimeType = slot.RuntimeType, IsTool = slot.IsTool };
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
