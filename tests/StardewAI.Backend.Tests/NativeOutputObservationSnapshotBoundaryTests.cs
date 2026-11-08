using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeOutputObservationSnapshotBoundaryTests
{
    [Fact]
    public void BindsExactQualityAggregatesAcrossSlotMergesWithoutGrantingProducerCredit()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        var before = Snapshot(99, [Row(0, 3), Row(1, 7), Empty(2)]);
        var after = Snapshot(111, [Row(0, 4), Row(1, 12), Empty(2)]);
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
        var original = JsonSerializer.Serialize(new { before, after });
        var result = NativeOutputObservationSnapshotBoundary.Read(request, receipt, before, after, "Farm");
        Assert.Equal(10, Assert.Single(result.Inventory).InventoryBefore);
        Assert.Equal(16, Assert.Single(result.Inventory).InventoryAfter);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Equal(original, JsonSerializer.Serialize(new { before, after }));
    }

    [Theory]
    [InlineData("before-gap")]
    [InlineData("after-gap")]
    [InlineData("unknown-item")]
    [InlineData("quality")]
    [InlineData("duplicate-slot")]
    [InlineData("missing-slot")]
    [InlineData("empty-with-item")]
    [InlineData("missing-quality")]
    [InlineData("wrong-stack-type")]
    [InlineData("zero-stack")]
    [InlineData("overflow")]
    [InlineData("unavailable")]
    [InlineData("hash")]
    [InlineData("save")]
    [InlineData("actor")]
    [InlineData("day")]
    [InlineData("location")]
    [InlineData("late-capture")]
    public void RejectsUncapturedQuantityChangesAndIncompleteSnapshotInputs(string change)
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        var before = Snapshot(99, [Row(0, 10)]);
        var after = Snapshot(111, [Row(0, 16)]);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!.AsObject();
        var rows = player["inventory"]!["value"]!.AsArray();
        switch (change)
        {
            case "before-gap": before = Snapshot(99, [Row(0, 11)]); break;
            case "after-gap": rows[0]!["stack"] = 17; break;
            case "unknown-item": rows.Add(Row(1, 1, "(O)390")); break;
            case "quality": rows[0]!["quality"] = 2; break;
            case "duplicate-slot": rows.Add(Row(0, 1)); break;
            case "missing-slot": rows[0]!["slot_index"] = 1; break;
            case "empty-with-item": rows[0]!["is_empty"] = true; break;
            case "missing-quality": rows[0]!.AsObject().Remove("quality"); break;
            case "wrong-stack-type": rows[0]!["stack"] = "16"; break;
            case "zero-stack": rows[0]!["stack"] = 0; break;
            case "overflow": rows[0]!["stack"] = int.MaxValue; rows.Add(Row(1, 1)); break;
            case "unavailable": player["inventory"]!["status"] = "unavailable"; break;
            case "save": after.SaveId.Value = "other"; break;
            case "actor": after.PlayerId.Value = "other"; break;
            case "day": after.State["time"] = JsonSerializer.SerializeToElement(new { total_days = new { status = "available", value = 41 } }); break;
            case "location": player["location_id"]!["value"] = "Town"; break;
            case "late-capture": receipt.AfterGameTick = 112; break;
        }
        after.State["player"] = JsonSerializer.SerializeToElement(player);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
        if (change == "hash") after.StateHash = new string('f', 64);
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationSnapshotBoundary.Read(request, receipt, before, after, "Farm"));
    }

    [Fact]
    public void UnchangedUncollectedItemsStillBelongToTheCompleteDenominator()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        receipt.InventoryBefore = [.. receipt.InventoryBefore, new() { QualifiedItemId = "(O)390", Quality = -1, Quantity = 1 }];
        receipt.InventoryAfter = [.. receipt.InventoryAfter, new() { QualifiedItemId = "(O)390", Quality = -1, Quantity = 1 }];
        var before = Snapshot(99, [Row(0, 10), Row(1, 1, "(O)390", -1)]);
        var after = Snapshot(111, [Row(0, 16), Row(1, 1, "(O)390", -1)]);
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
        Assert.Equal(2, NativeOutputObservationSnapshotBoundary.Read(request, receipt, before, after, "Farm").Inventory.Count);
        receipt.InventoryBefore = [receipt.InventoryBefore[0]];
        receipt.InventoryAfter = [receipt.InventoryAfter[0]];
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationSnapshotBoundary.Read(request, receipt, before, after, "Farm"));
    }

    private static JsonObject Row(int slot, int count, string id = "(O)388", int quality = 0) => new()
    { ["slot_index"] = slot, ["qualified_item_id"] = id, ["stack"] = count, ["quality"] = quality, ["is_empty"] = false };
    private static JsonObject Empty(int slot) => new()
    { ["slot_index"] = slot, ["qualified_item_id"] = null, ["stack"] = null, ["quality"] = null, ["is_empty"] = true };
    private static SnapshotEnvelope Snapshot(long tick, JsonObject[] rows)
    {
        var snapshot = new SnapshotEnvelope
        {
            GameTick = tick, GameVersion = "1.6.15", BridgeVersion = "declared-protocol-fixture",
            SaveId = new() { Status = "available", Value = "save" }, PlayerId = new() { Status = "available", Value = "player" },
            State = new()
            {
                ["player"] = JsonSerializer.SerializeToElement(new
                { inventory = new { status = "available", value = rows }, location_id = new { status = "available", value = "Farm" } }),
                ["time"] = JsonSerializer.SerializeToElement(new { total_days = new { status = "available", value = 40 } })
            }
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
