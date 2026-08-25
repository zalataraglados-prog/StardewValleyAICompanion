using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFurniturePlacementRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.place_furniture", StringComparison.Ordinal))
        {
            return;
        }
        request.FurnitureInventoryRotationBefore = ReadQueueParameterInt(item, "inventory_current_rotation");
        request.FurnitureDesiredRotation = ReadQueueParameterInt(item, "desired_current_rotation");
        request.FurnitureRotationSteps = ReadQueueParameterInt(item, "rotation_steps_from_inventory");
        request.FurnitureType = ReadQueueParameterInt(item, "furniture_type");
        request.FurnitureCanFreePlace = ReadQueueParameterBool(item, "can_free_place_furniture");
        request.FurnitureExpectedPassable = ReadQueueParameterBool(item, "expected_passable");
        request.FurniturePlacementEndpoint = ReadQueueParameterString(item, "placement_endpoint");
        request.FurnitureExpectedAnchorX = ReadQueueParameterInt(item, "expected_anchor_x");
        request.FurnitureExpectedAnchorY = ReadQueueParameterInt(item, "expected_anchor_y");
        request.FurnitureFootprintWidth = ReadQueueParameterInt(item, "footprint_width");
        request.FurnitureFootprintHeight = ReadQueueParameterInt(item, "footprint_height");
        request.FurnitureTableIndex = ReadQueueParameterInt(item, "table_index");
        request.FurnitureTableTileX = ReadQueueParameterInt(item, "table_tile_x");
        request.FurnitureTableTileY = ReadQueueParameterInt(item, "table_tile_y");
    }
}
