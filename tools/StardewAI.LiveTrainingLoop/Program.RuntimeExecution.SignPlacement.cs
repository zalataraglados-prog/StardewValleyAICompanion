using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplySignPlacementRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.place_sign", StringComparison.Ordinal))
        {
            return;
        }
        request.SignPlacementKind = ReadQueueParameterString(item, "placement_kind");
        request.SignExpectedPassable = ReadQueueParameterBool(item, "expected_passable");
        request.SignExpectedDisplayItemEmpty = ReadQueueParameterBool(item, "expected_display_item_empty");
        request.SignExpectedDisplayType = ReadQueueParameterInt(item, "expected_display_type");
        request.SignExpectedText = ReadQueueParameterString(item, "expected_sign_text");
        request.SignExpectedShowNextIndex = ReadQueueParameterBool(item, "expected_show_next_index");
    }
}
