using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class NativeWateringObservationScopePolicyTests
{
    [Fact]
    public void WateringRequiresItsOwnScopeAndDoesNotBroadenMoveClearanceOrTilling()
    {
        const string mode = "training_singleplayer";
        Assert.Null(NativeObservationScopePolicy.WateringRejection("water:1", mode, "executor.water_crop", Identities));
        Assert.NotNull(NativeObservationScopePolicy.Rejection("water:1", mode, "executor.water_crop", Identities));
        Assert.NotNull(NativeObservationScopePolicy.TillingRejection("water:1", mode, "executor.water_crop", Identities));
        foreach (var option in new[] { "executor.move_to_tile", "executor.clear_obstacle", "executor.till_soil", "executor.place_bomb" })
            Assert.NotNull(NativeObservationScopePolicy.WateringRejection("water:1", mode, option, Identities));
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    [InlineData("mode")]
    public void WateringRetainsBoundedScopeIdentityAndSingleplayerRules(string fault)
    {
        Assert.NotNull(NativeObservationScopePolicy.WateringRejection(fault == "scope" ? "bad\nname" : "water:1",
            fault == "mode" ? "coop_companion" : "training_singleplayer", "executor.water_crop",
            fault == "identity" ? Array.Empty<string>() : Identities));
    }

    private static readonly string[] Identities = Enumerable.Repeat("bound", 6).ToArray();
}
