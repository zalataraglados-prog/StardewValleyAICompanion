using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeAnimalProductExecutorTests
{
    [Fact]
    public void AnimalProductRequestCarriesExactSideEffectProjection()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.collect_animal_product",
            TargetRuntimeIdentity = "123",
            RequiredToolKind = "Milk Pail",
            ExpectedOutputQuality = 2,
            ExpectedAnimalCrackerMultiplier = 1,
            ExpectedEnergyDelta = -4,
            ExpectedFriendshipBefore = 500,
            ExpectedFriendshipAfter = 505,
            ExpectedStatIncrementsJson = "[]"
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("executor.collect_animal_product", roundTrip.OptionId);
        Assert.Equal("Milk Pail", roundTrip.RequiredToolKind);
        Assert.Equal(-4, roundTrip.ExpectedEnergyDelta);
        Assert.Equal(505, roundTrip.ExpectedFriendshipAfter);
    }

    [Fact]
    public void RuntimeUsesNativeAnimalToolsWithoutDirectStateMutation()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartAnimalProductHarvest(pending);", source);
        Assert.Contains("Game1.player.BeginUsingTool();", source);
        Assert.Contains("MilkPail pail => pail.animal", source);
        Assert.Contains("Shears shears => shears.animal", source);
        Assert.Contains("farmingExperienceBefore", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("friendshipAfter", source);
        Assert.Contains("Game1.stats.Get(stat.StatName) == stat.After", source);
        Assert.DoesNotContain("active.Animal.currentProduce.Value = null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("active.Animal.friendshipTowardFarmer.Value =", source, StringComparison.Ordinal);
    }
}
