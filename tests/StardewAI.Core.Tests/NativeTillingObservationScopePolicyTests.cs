using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class NativeTillingObservationScopePolicyTests
{
    [Fact]
    public void ExplicitTillingDoesNotExpandTheOldMoveClearanceScope()
    {
        foreach (var option in new[] { "executor.move_to_tile", "executor.clear_obstacle" })
        {
            Assert.Null(NativeObservationScopePolicy.Rejection("old", "training_singleplayer", option, Identities));
            Assert.Equal("action_origin_observation_scope_unsupported",
                NativeObservationScopePolicy.TillingRejection("new", "training_singleplayer", option, Identities));
        }
        Assert.Equal("action_origin_observation_scope_unsupported",
            NativeObservationScopePolicy.Rejection("old", "training_singleplayer", "executor.till_soil", Identities));
        Assert.Null(NativeObservationScopePolicy.TillingRejection("new", "training_singleplayer", "executor.till_soil", Identities));
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    [InlineData("mode")]
    [InlineData("bomb")]
    public void ExplicitTillingPreservesOriginalBoundedIdentityRules(string fault)
    {
        var scope = fault == "scope" ? "bad\nname" : "till:1";
        Assert.NotNull(NativeObservationScopePolicy.TillingRejection(scope, fault == "mode" ? "coop_companion" : "training_singleplayer",
            fault == "bomb" ? "executor.place_bomb" : "executor.till_soil", fault == "identity" ? Array.Empty<string>() : Identities));
    }

    private static readonly string[] Identities = Enumerable.Repeat("bound", 6).ToArray();
}
