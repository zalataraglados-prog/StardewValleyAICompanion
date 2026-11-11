using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("refill_flag")]
    [InlineData("prefix_flag")]
    [InlineData("count")]
    [InlineData("complete")]
    [InlineData("approach_flag")]
    public async Task RefillControlRebuildsZeroCreditFlagsBeforeAcceptingRestartedHead(string changed)
    {
        using var f = new OwnedWateringFixture(water: 0, atRefillApproach: true);
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, () => f.EmitNative(applied: 0, failedSuffix: true, refillOnlySuffix: true));
        await f.Rebind(owner, operation);
        await owner.DisposeAsync();
        var path = Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json");
        var accepted = JsonNode.Parse(File.ReadAllText(path))!;
        if (changed == "refill_flag") accepted["native_refill_control_verified"] = false;
        if (changed == "prefix_flag") accepted["native_watering_prefix_verified"] = true;
        if (changed == "count") accepted["applied_tile_count"] = 1;
        if (changed == "complete") accepted["complete_batch"] = true;
        if (changed == "approach_flag") accepted["native_refill_approach_verified"] = true;
        Fixture.Save(path, accepted);
        CropTeacherManagedSeedOwner? reopened = null;
        var error = await Record.ExceptionAsync(async () => { reopened = await f.Shared.Files.Open(); });
        if (reopened is not null) await reopened.DisposeAsync();
        Assert.Contains("watering prefix acceptance changed", Assert.IsType<InvalidDataException>(error).Message);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("native_refill_control_verified")]
    [InlineData("native_refill_approach_verified")]
    public async Task RefillControlCannotBeForgedIntoAnOrdinaryPositivePrefixSummary(string flag)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, () => f.EmitNative());
        await f.Rebind(owner, operation);
        await owner.DisposeAsync();
        var path = Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json");
        var accepted = JsonNode.Parse(File.ReadAllText(path))!;
        accepted[flag] = true;
        Fixture.Save(path, accepted);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Shared.Files.Open());
    }
}
