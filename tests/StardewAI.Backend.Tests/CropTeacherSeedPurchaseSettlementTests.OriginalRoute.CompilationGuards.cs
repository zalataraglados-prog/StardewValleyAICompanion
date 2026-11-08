using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("target")]
    [InlineData("budget")]
    [InlineData("opaque_id")]
    public async Task OriginalRouteReplayPreservesSemanticFieldsAndPinsOpaqueIdsAfterAdmission(string changed)
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        await f.Prepare(owner);
        await owner.DisposeAsync();
        var queue = JsonNode.Parse(File.ReadAllText(f.QueuePath))!;
        var command = queue["items"]![0]!["normalized_command"]!;
        if (changed == "target")
            command["parameters"]!.AsArray().Single(row => row!["name"]!.GetValue<string>() == "expected_target_location")!["value"] = "Town";
        if (changed == "budget") command["steps"]![0]!["estimated_ticks"] = command["steps"]![0]!["estimated_ticks"]!.GetValue<int>() + 1;
        if (changed == "opaque_id") command["steps"]![0]!["step_id"] = "different-opaque-id-after-admission";
        Fixture.Save(f.QueuePath, queue);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Files.Open());
        Assert.Contains(changed == "opaque_id" ? "admission inputs changed" : "does not replay its original compiler", error.Message);
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.Files.Native, "route-native-claim.json")));
    }
}
