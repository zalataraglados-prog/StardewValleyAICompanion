using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("count")]
    [InlineData("complete")]
    [InlineData("tiles")]
    public async Task OriginalWateringReconstructsPrefixBeforeTrustingRestartedAcceptance(string changed)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, () => f.EmitNative(applied: 1, failedSuffix: true));
        await f.Rebind(owner, operation);
        await owner.DisposeAsync();
        var path = Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json");
        var accepted = JsonNode.Parse(File.ReadAllText(path))!;
        if (changed == "count") accepted["applied_tile_count"] = 2;
        if (changed == "complete") accepted["complete_batch"] = true;
        if (changed == "tiles") accepted["applied_tiles"]![0]!["tile_x"] = 9;
        Fixture.Save(path, accepted);
        CropTeacherManagedSeedOwner? reopened = null;
        var error = await Record.ExceptionAsync(async () => { reopened = await f.Shared.Files.Open(); });
        if (reopened is not null) await reopened.DisposeAsync();
        Assert.Contains("watering prefix acceptance changed", Assert.IsType<InvalidDataException>(error).Message);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
    }
}
