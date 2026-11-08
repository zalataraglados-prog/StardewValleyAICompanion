using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("count")]
    [InlineData("complete")]
    [InlineData("tiles")]
    public async Task WateringOwnerReplaysNativePrefixRatherThanTrustingAcceptedSummary(string changed)
    {
        using var f = new WateringFixture();
        await f.Emit(applied: 1, failedSuffix: true);
        await using var owner = await f.Shared.Files.Open();
        var operation = await owner.AdmitWateringMaintenanceAsync(Id, f.Transition, f.Shared.Files.Native);
        await RunWateringOwner(f, owner, operation);
        await owner.DisposeAsync();
        var path = Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json");
        var accepted = JsonNode.Parse(File.ReadAllText(path))!;
        if (changed == "count") accepted["applied_tile_count"] = 2;
        if (changed == "complete") accepted["complete_batch"] = true;
        if (changed == "tiles") accepted["applied_tiles"]![0]!["tile_x"] = 9;
        Fixture.Save(path, accepted);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Shared.Files.Open());
        Assert.Contains("watering prefix acceptance changed", error.Message);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
    }
}
