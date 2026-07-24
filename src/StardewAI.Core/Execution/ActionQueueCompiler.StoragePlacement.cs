using System;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[]
            CompilePlaceStorageStep(
                SmallModelAction action)
        {
            var slotIndex = ReadIntParameter(
                action,
                "inventory_slot_index");
            var targetX = ReadIntParameter(
                action,
                "target_tile_x");
            var targetY = ReadIntParameter(
                action,
                "target_tile_y");
            var locationId = ReadParameter(
                action,
                "location_id");
            var qualifiedItemId = ReadParameter(
                action,
                "qualified_item_id");
            if (!slotIndex.HasValue ||
                !targetX.HasValue ||
                !targetY.HasValue ||
                string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(
                    qualifiedItemId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "place_storage",
                    locationId + "(" + targetX.Value +
                    "," + targetY.Value + "):slot" +
                    slotIndex.Value + ":" +
                    qualifiedItemId,
                    "player.inventory[" +
                    slotIndex.Value +
                    "].stack_decreases=1;" +
                    "current_location.chests[" +
                    locationId + ":" + targetX.Value +
                    "," + targetY.Value +
                    "].qualified_item_id=" +
                    qualifiedItemId,
                    30)
            };
        }
    }
}
