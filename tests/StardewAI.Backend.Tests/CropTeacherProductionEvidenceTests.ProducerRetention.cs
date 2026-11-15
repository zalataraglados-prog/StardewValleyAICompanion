using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    [Fact]
    public async Task ProducerRetentionRebuildsOriginalWholeWaveWithoutChangingLegacyInventoryMeaning()
    {
        var f = new Fixture(); var evidence = await f.Build();
        var old = Read<MoneyRevenueCropOutputRetentionProof>(Assert.Single(evidence.OutputRetentionProofPaths));
        var request = Read<MoneyRevenueCropOutputRetentionProofRequest>(old.RequestPath);
        request.SchemaVersion = "money_revenue_crop_output_retention_proof_request.v2";
        var path = Path.Combine(f.Output, "producer-retention-request.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request, JsonOptions));
        var proof = MoneyRevenueCropOutputRetentionProofBuilder.Build(path);
        Assert.Equal("money_revenue_crop_output_retention_proof.v2", proof.SchemaVersion);
        Assert.Equal(old.InventoryQuantityBefore, proof.InventoryQuantityBefore);
        Assert.Equal(old.InventoryQuantityAfter, proof.InventoryQuantityAfter);
        Assert.Equal(old.InventoryQuantityIncrease, proof.InventoryQuantityIncrease);
        Assert.Equal(old.MinimumUnits, proof.ProducerRetention!.RetainedQuantity);
        Assert.True(Assert.Single(proof.ProducerRetention.Segments).OwnedHarvest);
        Assert.False(proof.FormalProductTrainingAuthorized);
        Assert.Null(old.ProducerRetention);
        Assert.DoesNotContain("producer_retention", JsonSerializer.Serialize(old, JsonOptions));
    }

    [Fact]
    public void ProducerRetentionNeverCreditsForeignIncreaseAndDebitsLossBeforeReplenishment()
    {
        var start = RetentionFrame(0, 10);
        var account = Account(start);
        var harvest = RetentionFrame(1, 12);
        Observe(account, start, harvest, true);
        var foreign = RetentionFrame(2, 22);
        Observe(account, harvest, foreign, false);
        Assert.Equal(2, Quantity(account));
        var consumed = RetentionFrame(3, 20);
        Observe(account, foreign, consumed, false);
        var replaced = RetentionFrame(4, 22);
        Observe(account, consumed, replaced, false);
        Assert.Equal(0, Quantity(account));
        var nextHarvest = RetentionFrame(5, 24);
        Observe(account, replaced, nextHarvest, true);
        Assert.Equal(2, Quantity(account)); // Aggregate increase 14 is not owned retained output.
    }

    [Fact]
    public void ProducerRetentionPreservesFungibleSlotMovesButCannotTurnForeignQualityIntoOwnedCredit()
    {
        var start = RetentionFrame(0, 0); var harvested = RetentionFrame(1, 4);
        var account = Account(start); Observe(account, start, harvested, true);
        var moved = RetentionFrame(2, 4, slot: 9);
        Observe(account, harvested, moved, false);
        Assert.Equal(4, Quantity(account));
        var silver = RetentionFrame(3, 4, slot: 9, quality: 1);
        Observe(account, moved, silver, false);
        Assert.Equal(0, Quantity(account));
    }

    [Fact]
    public void ProducerRetentionUsesOriginalEmptySlotShapeAndDoesNotTreatContextualPriceAsNewStock()
    {
        var start = RetentionFrame(0, 0); var harvested = RetentionFrame(1, 4);
        var account = Account(start); Observe(account, start, harvested, true);
        var moved = RetentionFrame(2, 4, slot: 9);
        var player = JsonNode.Parse(moved.State["player"].GetRawText())!;
        var row = player["inventory"]!["value"]![0]!;
        row["sell_to_store_price"] = 200;
        row["protected_from_auto_sell"] = true;
        row["auto_sell_protection_reasons"] = new JsonArray("declared-context");
        player["inventory"]!["value"]!.AsArray().Add(new JsonObject
            { ["slot_index"] = 0, ["qualified_item_id"] = null, ["stack"] = null, ["quality"] = null, ["is_empty"] = true });
        moved.State["player"] = JsonSerializer.SerializeToElement(player);
        moved.StateHash = SnapshotHash.ComputeStateHash(moved.State);
        Observe(account, harvested, moved, false);
        Assert.Equal(4, Quantity(account));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("tick")]
    [InlineData("actor")]
    [InlineData("hash")]
    [InlineData("duplicate_slot")]
    [InlineData("owned_loss")]
    public void ProducerRetentionRejectsUnboundFramesOrInvalidOwnedStock(string fault)
    {
        var start = RetentionFrame(0, 2); var after = RetentionFrame(1, 3);
        var account = Account(start); var before = start;
        if (fault == "source") before = RetentionFrame(0, 9);
        if (fault == "tick") after.GameTick = 0;
        if (fault == "actor") after.PlayerId.Value += "foreign";
        if (fault == "hash") after.StateHash = "forged";
        if (fault == "owned_loss") after = RetentionFrame(1, 1);
        if (fault == "duplicate_slot")
        {
            var player = JsonNode.Parse(after.State["player"].GetRawText())!;
            player["inventory"]!["value"]!.AsArray().Add(player["inventory"]!["value"]![0]!.DeepClone());
            after.State["player"] = JsonSerializer.SerializeToElement(player);
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        }
        var error = Assert.Throws<TargetInvocationException>(() => Observe(account, before, after, true));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static object Account(SnapshotEnvelope source) => Activator.CreateInstance(typeof(MoneyRevenueCropRetentionAccount),
        BindingFlags.Instance | BindingFlags.NonPublic, null, [source, "(O)400"], null)!;
    private static void Observe(object account, SnapshotEnvelope before, SnapshotEnvelope after, bool harvest) =>
        account.GetType().GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(account, [before, after, harvest]);
    private static int Quantity(object account) => (int)account.GetType().GetProperty("Quantity", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(account)!;
    private static SnapshotEnvelope RetentionFrame(int tick, int count, int slot = 0, int quality = 0)
    {
        var frame = new SnapshotEnvelope { GameTick = tick, GameVersion = "declared-test", SaveId = new() { Value = "save" }, PlayerId = new() { Value = "1" } };
        frame.State["player"] = JsonSerializer.SerializeToElement(new { inventory = new { status = "available", value = count == 0 ? Array.Empty<object>() : new object[]
            { new { slot_index = slot, qualified_item_id = "(O)400", stack = count, quality, sell_to_store_price = quality == 0 ? 175 : 218, is_empty = false } } } });
        frame.StateHash = SnapshotHash.ComputeStateHash(frame.State);
        return frame;
    }
}
