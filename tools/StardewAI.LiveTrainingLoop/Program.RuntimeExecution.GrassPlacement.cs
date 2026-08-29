using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyGrassPlacementRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.plant_grass", StringComparison.Ordinal))
        {
            return;
        }

        request.ExpectedGrassType = ReadQueueParameterInt(item, "expected_grass_type");
        request.ExpectedInitialNumberOfWeeds = ReadQueueParameterInt(item, "expected_initial_number_of_weeds");
        request.GrassPlacementSound = ReadQueueParameterString(item, "placement_sound");
    }
}
