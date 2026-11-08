using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OriginalRouteMultiHopReplansFromAcceptedIntermediateLocationOnTheSameChain()
    {
        using var f = new OwnedRouteFixture(multiHop: true);
        await using var owner = await f.Files.Open();
        var first = await f.Prepare(owner);
        await owner.ExecuteOriginalRouteAsync(first, f.Runtime.Client, () => f.EmitNative());
        await f.Rebind(owner, first);
        var middle = owner.CurrentSnapshotPath;
        var second = await f.Prepare(owner, 2);
        Assert.Equal(2, second.Sequence);
        await owner.ExecuteOriginalRouteAsync(second, f.Runtime.Client, () => f.EmitNative(2, middle));
        await f.Rebind(owner, second);
        var accepted = Directory.GetFiles(f.Files.OwnerRoot, "operation-*-accepted.json").OrderBy(path => path).ToArray();
        Assert.Equal(2, accepted.Length);
        Assert.False(JsonNode.Parse(File.ReadAllText(accepted[0]))!["route_target_reached"]!.GetValue<bool>());
        Assert.True(JsonNode.Parse(File.ReadAllText(accepted[1]))!["route_target_reached"]!.GetValue<bool>());
        Assert.Equal(2, f.Runtime.Handler.Commits);
        Assert.Null(owner.Pending);
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(2, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false, "warp")]
    [InlineData(true, "warp")]
    [InlineData(false, "building_door")]
    public async Task OriginalRouteUsesOriginalCompilerAndSameOwnerNativeInvocationThenRebind(bool multiHop, string connectorKind)
    {
        using var f = new OwnedRouteFixture(multiHop, connectorKind);
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        var calls = 0;
        await owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, () => { calls++; return f.EmitNative(); });
        Assert.Same(operation, owner.Pending);
        Assert.Equal(0, f.Runtime.Handler.Commits);
        await f.Rebind(owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(1, calls);
        Assert.Equal(1, f.Runtime.Handler.Commits);
        var final = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        AssertOtherRootsUnchanged(f.Source.Ledger, final);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal(!multiHop, accepted["route_target_reached"]!.GetValue<bool>());
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.False(reopened.HeadRecoveryRequired);
    }
}
