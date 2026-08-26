using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyTentPlacementRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.place_tent", StringComparison.Ordinal))
        {
            return;
        }
        request.TentRectangleX = ReadQueueParameterInt(item, "rectangle_x");
        request.TentRectangleY = ReadQueueParameterInt(item, "rectangle_y");
        request.TentRectangleWidth = ReadQueueParameterInt(item, "rectangle_width");
        request.TentRectangleHeight = ReadQueueParameterInt(item, "rectangle_height");
        request.TentAnchorTileX = ReadQueueParameterInt(item, "anchor_tile_x");
        request.TentAnchorTileY = ReadQueueParameterInt(item, "anchor_tile_y");
    }
}
