using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalWateringPinsCompilationClaimsOnceAndPreservesActualPrefix(bool partial)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        Assert.True(operation.IsWateringMaintenance);
        Assert.False(operation.IsSupportingRoute);
        var calls = 0;
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, () =>
        { calls++; return f.EmitNative(applied: partial ? 1 : 2, failedSuffix: partial); });
        await f.Rebind(owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(1, calls);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
        var acceptance = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal(partial ? 1 : 2, acceptance["applied_tile_count"]!.GetValue<int>());
        Assert.Equal(!partial, acceptance["complete_batch"]!.GetValue<bool>());
        Assert.False(acceptance["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.NotNull(acceptance["maintenance_native_claim_sha256"]);
        Assert.Null(acceptance["route_native_claim_sha256"]);
    }

    [Fact]
    public async Task OriginalWateringContinuesOriginalRouteWithoutAnotherOwnerOrScheduler()
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var watering = await f.Prepare(owner);
        await owner.ExecuteOriginalWateringAsync(watering, f.Shared.Runtime.Client, () => f.EmitNative());
        await f.Rebind(owner, watering);
        var waterHead = owner.CurrentSnapshotPath;
        var route = await f.Shared.Prepare(owner, 2);
        Assert.Equal(2, route.Sequence);
        await owner.ExecuteOriginalRouteAsync(route, f.Shared.Runtime.Client, () => f.Shared.EmitNative(2, waterHead));
        await f.Shared.Rebind(owner, route);
        var final = owner.CurrentSnapshotPath;
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(final, reopened.CurrentSnapshotPath);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
    }
}
