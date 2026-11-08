using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static SimulatedFactChange[] AlreadyCollectedPickupFacts(TrainingExecutionRequest request, GameLocation location,
        Point target, int inventoryNow, ActionOriginOutputResolution? origin)
    {
        if (origin is not null)
            return new[]
            {
                new SimulatedFactChange
                {
                    Path = "action_origin.producer_inventory.count[" + request.QualifiedItemId + "]",
                    Before = InventoryQuantity(origin.Record.InventoryBefore, request.QualifiedItemId!, request.DeferredPickupMinimumQuality!.Value).ToString(),
                    After = inventoryNow.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "action_origin.collected_quantity_lower_bound[" + request.QualifiedItemId + "]",
                    Before = "0", After = origin.DirectInventoryQuantity.ToString()
                }
            };
        return new[]
        {
            new SimulatedFactChange
            {
                Path = "player.inventory.count[" + request.QualifiedItemId + "]",
                Before = request.InventoryItemTotalBefore!.Value.ToString(), After = inventoryNow.ToString()
            },
            new SimulatedFactChange
            {
                Path = "locations[" + location.NameOrUniqueName + "].debris.near[" + target.X + "," + target.Y + "]",
                Before = "present_in_snapshot", After = "absent_at_executor_start"
            }
        };
    }
}
