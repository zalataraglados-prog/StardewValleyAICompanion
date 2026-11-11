using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class CropHarvestPhysicalStockBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeedCategoryEchoUsesTheOriginalSeedInventoryVerifier(bool forged)
    {
        var f = NativePickupInventoryBoundaryTests.ResourceFixture();
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            Edit(snapshot, "player", "inventory", rows => rows[0]!["category"] = -74);
            var count = ReferenceEquals(snapshot, f.Before) ? 10 : 16;
            Edit(snapshot, "player", "seed_inventory", rows => rows.AsArray().Add(new JsonObject { ["slot_index"] = 0,
                ["qualified_item_id"] = "(O)388", ["quality"] = 0, ["stack"] = count, ["seed_id"] = "388", ["crop_catalog_match"] = true }));
        }
        if (forged) Edit(f.After, "player", "seed_inventory", rows => rows[0]!["stack"] = 99);
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State); f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        if (forged) Assert.Throws<InvalidDataException>(() => Read(f.Before, f.After, "(O)388", 1));
        else Assert.True(Read(f.Before, f.After, "(O)388", 1).GetProperty("PhysicalInventoryAndGraphQuantityVerified").GetBoolean());
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, false, 6)]
    [InlineData(true, false, 6)]
    [InlineData(true, true, 6)]
    public void ReusesOriginalRetentionAndGraphChecksWithoutPromotingStockToProduction(bool newSlot, bool newKind, int minimum)
    {
        var f = NativePickupInventoryBoundaryTests.ResourceFixture(newSlot, newKind);
        var original = JsonSerializer.Serialize(new[] { f.Before, f.After });
        var summary = Read(f.Before, f.After, "(O)388", minimum);
        Assert.True(summary.GetProperty("PhysicalInventoryAndGraphQuantityVerified").GetBoolean());
        Assert.Equal(6, summary.GetProperty("Inventory").GetProperty("QuantityIncrease").GetInt32());
        Assert.Equal(minimum, summary.GetProperty("Inventory").GetProperty("RequiredQuantity").GetInt32());
        foreach (var name in new[] { "PersistedItemStateVerified", "ProducerProvenanceVerified", "ResourceCreditAuthorized", "NativeOutcomeVerified", "FormalProductTrainingAuthorized" })
            Assert.False(summary.GetProperty(name).GetBoolean());
        Assert.Equal(original, JsonSerializer.Serialize(new[] { f.Before, f.After }));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("authority")]
    [InlineData("foreign-custody")]
    [InlineData("foreign-access")]
    [InlineData("graph-unknown")]
    [InlineData("graph-metadata")]
    [InlineData("new-slot-unknown")]
    [InlineData("quantity")]
    [InlineData("restricted-quantity")]
    [InlineData("before-quantity")]
    [InlineData("source-slots")]
    [InlineData("capacity")]
    [InlineData("item-metadata")]
    [InlineData("unrelated-item")]
    [InlineData("money")]
    [InlineData("seed-mirror")]
    [InlineData("minimum")]
    [InlineData("foreign-save")]
    [InlineData("tick")]
    public void RejectsUnaccountedStockGraphCurrencyOrIdentityChanges(string change)
    {
        var f = NativePickupInventoryBoundaryTests.ResourceFixture(newSlot: change == "new-slot-unknown");
        var target = change == "before-quantity" ? f.Before : f.After;
        switch (change)
        {
            case "actor": Graph(row => row["player_id"] = 999); break;
            case "authority": Graph(row => row["inventory_nodes"]![0]!["actor_use_authorized"] = false); break;
            case "foreign-custody": Graph(row => row["inventory_nodes"]![1]!["capacity"] = 99); break;
            case "foreign-access": Graph(row => row["access_points"]![0]!["locked_by_other_player"] = false); break;
            case "graph-unknown": Graph(row => row["future_persistent"] = "changed"); break;
            case "graph-metadata": Graph(row => row["inventory_nodes"]![0]!["slots"]![0]!["edibility"] = 99); break;
            case "new-slot-unknown": Graph(row => row["inventory_nodes"]![0]!["slots"]![0]!["future_persistent"] = "unwitnessed"); break;
            case "quantity":
            case "before-quantity": Graph(row => row["quantity_rows"]![0]!["available_quantity"] = 999); break;
            case "restricted-quantity": Graph(row => row["quantity_rows"]![0]!["restricted_quantity"] = 999); break;
            case "source-slots": Graph(row => row["quantity_rows"]![0]!["source_slot_count"] = 999); break;
            case "capacity": Edit(target, "player", "inventory_capacity", row => row["empty_slots"] = 99); break;
            case "item-metadata": Edit(target, "player", "inventory", rows => rows[0]!["special_state"] = "changed"); break;
            case "unrelated-item": Edit(target, "player", "inventory", rows => rows[2]!["special_state"] = "changed"); break;
            case "money": SetMoney(); break;
            case "seed-mirror": Edit(target, "player", "seed_inventory", rows => rows.AsArray().Add(new JsonObject { ["stack"] = 1 })); break;
            case "foreign-save": f.After.SaveId.Value = "foreign"; break;
            case "tick": f.After.GameTick = f.Before.GameTick; break;
        }
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State); f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        Assert.Throws<InvalidDataException>(() => Read(f.Before, f.After, "(O)388", change == "minimum" ? 7 : 1));
        void Graph(Action<JsonNode> edit) => Edit(target, "farm", "material_inventory_graph", edit);
        void SetMoney()
        {
            var player = JsonNode.Parse(target.State["player"].GetRawText())!;
            player["money"]!["value"] = 999; target.State["player"] = JsonSerializer.SerializeToElement(player);
        }
    }

    private static JsonElement Read(SnapshotEnvelope before, SnapshotEnvelope after, string itemId, int minimum)
    {
        var type = typeof(CropTeacherWateringMaintenance).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!;
        try { return JsonSerializer.SerializeToElement(type.GetMethod("VerifyHarvestStock", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [before, after, itemId, minimum])); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private static void Edit(SnapshotEnvelope snapshot, string domain, string field, Action<JsonNode> edit)
    {
        var values = JsonNode.Parse(snapshot.State[domain].GetRawText())!;
        edit(values[field]!["value"]!); snapshot.State[domain] = JsonSerializer.SerializeToElement(values);
    }
}
