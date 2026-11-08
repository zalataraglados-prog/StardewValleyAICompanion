using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task RefillControlUsesOriginalBlockedLoopReceiptAndBoundedFullCapture()
    {
        using var f = new OwnedWateringFixture(water: 0, atRefillApproach: true);
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        await f.Basis.Emit(applied: 0, failedSuffix: true, refillOnlySuffix: true, retainedQueuePath: f.QueuePath);
        using var transport = new OriginalWateringTransport(f);
        using var client = new HttpClient(transport);
        await InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync", client, client, f.Shared.Runtime.LiveOptions, 1, owner, operation);
        Assert.Equal(1, transport.NativeRequests);
        Assert.InRange(transport.FullSnapshotReads, 1, 4);
        Assert.Null(owner.Pending);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.True(accepted["native_refill_control_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
    }
}
