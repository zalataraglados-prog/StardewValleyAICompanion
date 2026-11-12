using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OriginalOvernightReachesHarvestThroughOneOwnerWithoutReplacingItsInitialSource()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var harvest = await f.PrepareHarvest(owner);
        await owner.ExecuteOriginalHarvestAsync(harvest, f.Shared.Runtime.Client, () => f.EmitHarvest(owner));
        await f.RebindHarvest(owner, harvest);
        Assert.Equal(1, f.NativeNightCalls); Assert.Equal(1, f.NativeHarvestCalls);
        Assert.Equal(5, f.Shared.Runtime.Handler.Commits); Assert.Null(owner.Pending);
        Assert.Equal(f.HarvestAfter, owner.CurrentSnapshotPath);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000005-accepted.json")))!;
        Assert.True(accepted["native_harvest_prefix_verified"]!.GetValue<bool>());
        Assert.Equal(2, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.False(accepted["whole_wave_output_retention_verified"]!.GetValue<bool>());
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(f.HarvestAfter, reopened.CurrentSnapshotPath); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(5, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task OriginalOvernightAdvancesTheSameOwnerFromWateringThroughItsNativeSaveBoundary()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var before = owner.CurrentSnapshotPath;
        var operation = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(operation, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, operation);
        Assert.NotEqual(before, owner.CurrentSnapshotPath); Assert.Equal(f.NightAfter, owner.CurrentSnapshotPath);
        Assert.Null(owner.Pending); Assert.Equal(1, f.NativeNightCalls); Assert.Equal(3, f.Shared.Runtime.Handler.Commits);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000003-accepted.json")))!;
        Assert.True(accepted["native_overnight_control_verified"]!.GetValue<bool>());
        Assert.Equal(2, accepted["verified_crop_tile_count"]!.GetValue<int>());
        Assert.Equal(0, accepted["unverified_layout_tile_count"]!.GetValue<int>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.False(reopened.HeadRecoveryRequired); Assert.Equal(3, f.Shared.Runtime.Handler.Commits);
    }
}
