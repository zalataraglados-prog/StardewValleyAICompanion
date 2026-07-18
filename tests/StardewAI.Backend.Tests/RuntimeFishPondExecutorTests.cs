using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeFishPondExecutorTests
{
    [Fact]
    public void FishPondRequestCarriesTypedBeforeAndAfterState()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.complete_fish_pond_request",
            BuildingTileX = 10,
            BuildingTileY = 20,
            FishTypeItemId = "698",
            ExpectedFishCount = 1,
            ExpectedMaximumOccupantsBefore = 1,
            ExpectedMaximumOccupantsAfter = 3,
            ExpectedNeededItemCountAfter = -1,
            RequestItemToolbarSlotsJson = "[{\"slot_index\":0}]"
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var result = JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(request, options), options)!;

        Assert.Equal(10, result.BuildingTileX);
        Assert.Equal(3, result.ExpectedMaximumOccupantsAfter);
        Assert.Equal(-1, result.ExpectedNeededItemCountAfter);
        Assert.Contains("slot_index", result.RequestItemToolbarSlotsJson);
    }

    [Fact]
    public void RuntimeFishPondPathUsesNativeCheckActionWithoutDirectStateMutation()
    {
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartFishPondService", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var runtime = source[start..];

        Assert.Contains("StartFishPondService(pending);", source);
        Assert.Contains("active.Location.checkAction(", runtime);
        Assert.Contains("active.Pond.hasCompletedRequest.Value", runtime);
        Assert.Contains("active.Pond.output.Value is null", runtime);
        Assert.DoesNotContain("gainExperience(", runtime);
        Assert.DoesNotMatch(@"active\.Pond\.output\.Value\s*=(?!=)", runtime);
        Assert.DoesNotMatch(@"active\.Pond\.maxOccupants\.Value\s*=(?!=)", runtime);
        Assert.DoesNotMatch(@"active\.Pond\.neededItemCount\.Value\s*=(?!=)", runtime);
    }
}
