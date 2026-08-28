using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeMiniObeliskExecutorTests
{
    [Fact]
    public void ProductionRuntimeReplaysNativePairAndUsesOneNativeLocationAction()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.MiniObelisk.cs");
        var fixture = RuntimeHarnessSources.File("ModEntry.MiniObeliskFixture.cs");

        Assert.Contains("StartMiniObeliskUse(pending);", all);
        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, \"mini_obelisk\"", source);
        Assert.Contains("foreach (var row in location.objects.Pairs)", source);
        Assert.Contains("var firstTile = Vector2.Zero", source);
        Assert.Contains("Vector2.Distance(standTile, pair.First.Tile) > Vector2.Distance(standTile, pair.Second.Tile)", source);
        Assert.Contains("CollisionMask.All, CollisionMask.All", source);
        Assert.Equal(1, Count(source, "active.Location.checkAction("));
        Assert.Contains("Game1.player.CurrentToolIndex = active.RestoreSlotIndex", source);
        Assert.DoesNotContain("setTileLocation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", source, StringComparison.Ordinal);
        Assert.Contains("new StardewObject(source.ToVector2(), \"238\")", fixture);
        Assert.Contains("Game1.player.Position =", fixture);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("movement.use_mini_obelisk"));
    }

    [Fact]
    public void V2PayloadCarriesTheTypedNativePairDestinationAndLanding()
    {
        var request = new TrainingExecutionRequest
        {
            MiniObeliskPairMemberIndex = 0,
            MiniObeliskPairFirstTileX = 10,
            MiniObeliskDestinationTileX = 30,
            MiniObeliskLandingTileY = 31,
            NativeObjectPayload = new NativeObjectExecutionPayload
            {
                Kind = "mini_obelisk",
                MiniObelisk = new MiniObeliskExecutionProjection
                {
                    PairMemberIndex = 0,
                    DestinationTileX = 30,
                    LandingTileY = 31
                }
            }
        };

        Assert.Equal(0, request.NativeObjectPayload.MiniObelisk.PairMemberIndex);
        Assert.Equal(30, request.NativeObjectPayload.MiniObelisk.DestinationTileX);
        Assert.Equal(31, request.NativeObjectPayload.MiniObelisk.LandingTileY);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
