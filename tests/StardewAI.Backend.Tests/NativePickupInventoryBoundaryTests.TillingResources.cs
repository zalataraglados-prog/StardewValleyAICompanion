using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    // Physical pickup cases isolate the internal composition leaf, using a summary
    // verified by the original pickup reader. They do not prove native till metadata.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TillingResourceCompositionCombinesPhysicalPickupWithEnergyTerrainGsQAndHouse(bool newSlot, bool newKind)
    {
        var f = TillingResourceLeafFixture(newSlot, newKind);
        var originals = TillingResourceRaw(f);
        Assert.Equal(6, f.Pickup.Evidence.ObservedPickupUnits);
        Assert.Single(f.Pickup.Slots);
        TillingResourceVerifyLeaf(f);
        Assert.Equal(originals, TillingResourceRaw(f));
        Assert.Equal(f.Raw.Before.StateHash, SnapshotHash.ComputeStateHash(f.Raw.Before.State));
        Assert.Equal(f.Raw.After.StateHash, SnapshotHash.ComputeStateHash(f.Raw.After.State));
        Assert.Equal(f.Observation.StateHash, SnapshotHash.ComputeStateHash(f.Observation.State));

        // The legacy resource entry must not acquire the selected-till waiver.
        var legacy = Assert.Throws<InvalidDataException>(() => Resources(f.Raw));
        Assert.Contains("Pickup changed unrelated persistent state", legacy.Message);
        Assert.Equal(originals, TillingResourceRaw(f));
    }

    [Theory]
    [InlineData("graph-unknown-field")]
    [InlineData("foreign-node")]
    [InlineData("foreign-lock")]
    [InlineData("slot-metadata")]
    [InlineData("available-quantity")]
    [InlineData("restricted-quantity")]
    [InlineData("source-slot-count")]
    [InlineData("capacity")]
    [InlineData("house-count")]
    [InlineData("house-other-field")]
    [InlineData("money")]
    [InlineData("unknown-persistent")]
    public void TillingResourceCompositionRejectsRawGraphMirrorAndPersistentDriftAfterTillingGates(string change)
    {
        var f = TillingResourceLeafFixture();
        TillingResourceVerifyLeaf(f); // Prove the complete baseline is valid before injecting one fault.
        var after = f.Raw.After;
        switch (change)
        {
            case "graph-unknown-field": Graph(node => node["future_graph_field"] = "changed"); break;
            case "foreign-node": Graph(node => node["inventory_nodes"]![1]!["capacity"] = 99); break;
            case "foreign-lock": Graph(node => node["access_points"]![0]!["locked_by_other_player"] = false); break;
            case "slot-metadata": Graph(node => node["inventory_nodes"]![0]!["slots"]![0]!["display_name"] = "invented"); break;
            case "available-quantity": Graph(node => node["quantity_rows"]![0]!["available_quantity"] = 999); break;
            case "restricted-quantity": Graph(node => node["quantity_rows"]![0]!["restricted_quantity"] = 999); break;
            case "source-slot-count": Graph(node => node["quantity_rows"]![0]!["source_slot_count"] = 999); break;
            case "capacity": Edit(after, "player", "inventory_capacity", node => node["empty_slots"] = 99); break;
            case "house-count": Edit(after, "world_progress", "marriage_house", node => node["house_upgrade"]!["inventory_item_count"] = 99); break;
            case "house-other-field": Edit(after, "world_progress", "marriage_house", node => node["house_upgrade"]!["price"] = 1); break;
            case "money": Put(after, "player", "money", JsonValue.Create(999)!); break;
            case "unknown-persistent": Put(after, "farm", "future_persistent", JsonValue.Create("changed")!); break;
        }
        Rebind(f.Raw);
        var originals = TillingResourceRaw(f);
        _ = TillingResourceComparison(f); // Both earlier till gates pass on this exact faulty frame.
        Assert.Throws<InvalidDataException>(() => TillingResourceVerifyLeaf(f));
        Assert.Equal(originals, TillingResourceRaw(f));
        void Graph(Action<JsonNode> changeGraph) => Edit(after, "farm", "material_inventory_graph", changeGraph);
    }

    [Theory]
    [InlineData("energy", "Hoe energy")]
    [InlineData("extra-terrain", "Crop till")]
    [InlineData("extra-stat", "Crop till")]
    [InlineData("foreign-stat", "Crop till")]
    public void TillingResourceCompositionRejectsUnverifiedEnergyTerrainAndStatistic(string change, string stage)
    {
        var f = TillingResourceLeafFixture();
        TillingResourceVerifyLeaf(f);
        switch (change)
        {
            case "energy": Put(f.Raw.After, "player", "energy", JsonValue.Create(269f)!); break;
            case "extra-terrain": Edit(f.Raw.After, "farm", "terrain_features", rows => rows.AsArray().Add(
                new JsonObject { ["tile_x"] = 72, ["tile_y"] = 16, ["type"] = "StardewValley.TerrainFeatures.HoeDirt" })); break;
            case "extra-stat": Edit(f.Raw.After, "world_progress", "game_state_query_unlock_state",
                node => node["players"]![0]!["stats"]!["dirtHoed"] = 9u); break;
            case "foreign-stat": Edit(f.Raw.After, "world_progress", "game_state_query_unlock_state",
                node => node["players"]![1]!["stats"]!["dirtHoed"] = 2u); break;
        }
        Rebind(f.Raw);
        var originals = TillingResourceRaw(f);
        var error = Assert.Throws<InvalidDataException>(() => TillingResourceVerifyLeaf(f));
        Assert.Contains(stage, error.Message);
        Assert.Equal(originals, TillingResourceRaw(f));
    }

    [Fact]
    public void TillingResourceCompositionPublicZeroPickupRetainsRawInputsAndDeniesCredits()
    {
        var (f, observation) = TillingResourcePublicFixture();
        var originals = JsonSerializer.Serialize(new { f.Request, f.Result, f.Before, f.After, observation });
        var result = NativeOutputObservationBoundary.ReadTillingResources(f.Request, f.Result, f.Before, f.After,
            observation, "Farm", new(71, 16), 1, "(T)Hoe");
        Assert.Empty(result.Inventory.Slots);
        Assert.Equal(0, result.Inventory.Evidence.ObservedPickupUnits);
        Assert.True(result.PhysicalInventoryDeltaVerified);
        Assert.True(result.MaterialGraphAndMirrorsVerified);
        Assert.True(result.TillingEnergyTerrainAndStatisticVerified);
        Assert.True(result.UnrelatedPersistentStateVerified);
        Assert.False(result.ProducerProvenanceVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Equal(originals, JsonSerializer.Serialize(new { f.Request, f.Result, f.Before, f.After, observation }));
        Assert.Equal(f.Before.StateHash, SnapshotHash.ComputeStateHash(f.Before.State));
        Assert.Equal(f.After.StateHash, SnapshotHash.ComputeStateHash(f.After.State));
        Assert.Equal(observation.StateHash, SnapshotHash.ComputeStateHash(observation.State));
    }

    [Fact]
    public void TillingResourceCompositionPublicAnchorPrecedesRawGraphValidation()
    {
        var (f, observation) = TillingResourcePublicFixture();
        _ = NativeOutputObservationBoundary.ReadTillingResources(f.Request, f.Result, f.Before, f.After,
            observation, "Farm", new(71, 16), 1, "(T)Hoe");
        Edit(f.After, "farm", "material_inventory_graph", node => node["future_graph_field"] = "changed");
        f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        var anchor = Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.ReadTillingResources(
            f.Request, f.Result, f.Before, f.After, observation, "Farm", new(71, 16), 1, "(T)Hoe"));
        Assert.Contains("exact native anchor", anchor.Message);
        f.Result.ProductAfterStateHash = f.After.StateHash;
        _ = ReadTillingMetadata(f); // Metadata/payload/debris still verify after the anchor is repaired.
        var graph = Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.ReadTillingResources(
            f.Request, f.Result, f.Before, f.After, observation, "Farm", new(71, 16), 1, "(T)Hoe"));
        Assert.Contains("pickup material graph", graph.Message);
    }

    private sealed record TillingResourceLeaf(PickupFixture Raw, SnapshotEnvelope Observation, NativePickupInventorySummary Pickup);

    private static TillingResourceLeaf TillingResourceLeafFixture(bool newSlot = false, bool newKind = false)
    {
        var f = TillingResourceStockFixture(newSlot, newKind);
        AddTillingResourceFields(f.Before, f.After);
        Rebind(f);
        var observation = TillingResourceObservation(f.Before);
        return new(f, observation, f.Read());
    }

    private static PickupFixture TillingResourceStockFixture(bool newSlot = false, bool newKind = false)
    {
        var f = ResourceFixture(newSlot, newKind);
        foreach (var endpoint in new[] { f.Receipt.InventoryPayloadBefore!, f.Receipt.InventoryPayloadAfter! })
        {
            var tool = endpoint.Slots![1];
            tool.RuntimeType = "StardewValley.Tools.Hoe"; tool.QualifiedItemId = "(T)Hoe";
            var xml = XElement.Parse(tool.SerializedStateXml!); xml.Name = "Hoe";
            xml.Element("itemId")!.Value = "Hoe"; xml.Element("name")!.Value = "Hoe"; SetXml(tool, xml);
        }
        foreach (var counts in new[] { f.Receipt.InventoryBefore, f.Receipt.InventoryAfter })
            counts.Single(row => row.QualifiedItemId == "(T)Axe").QualifiedItemId = "(T)Hoe";
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            Edit(snapshot, "player", "inventory", rows => ConvertTool(rows.AsArray().OfType<JsonObject>().Single(row => row["slot_index"]!.GetValue<int>() == 1)));
            Edit(snapshot, "farm", "material_inventory_graph", graph =>
            {
                ConvertTool(graph["inventory_nodes"]![0]!["slots"]!.AsArray().OfType<JsonObject>().Single(row => row["slot_index"]!.GetValue<int>() == 1));
                graph["quantity_rows"]!.AsArray().OfType<JsonObject>().Single(row => row["qualified_item_id"]!.GetValue<string>() == "(T)Axe")["qualified_item_id"] = "(T)Hoe";
            });
        }
        Rebind(f);
        return f;

        static void ConvertTool(JsonObject row)
        {
            row["runtime_type"] = "StardewValley.Tools.Hoe"; row["qualified_item_id"] = "(T)Hoe";
            row["item_id"] = "Hoe"; row["display_name"] = "Hoe";
            row["context_tags"] = new JsonArray("item_hoe");
        }
    }

    private static (TillingFixture Fixture, SnapshotEnvelope Observation) TillingResourcePublicFixture()
    {
        var f = TillingMetadataFixture();
        var stock = TillingResourceStockFixture();
        // Zero pickup: use the same canonical stock/house/foreign graph at both
        // endpoints, while retaining the metadata fixture's original raw graph/XML.
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            foreach (var domain in stock.Before.State)
                foreach (var field in domain.Value.EnumerateObject())
                    Put(snapshot, domain.Key, field.Name, JsonNode.Parse(field.Value.GetProperty("value").GetRawText())!);
            Put(snapshot, "player", "facing_direction", JsonValue.Create(3)!);
        }
        foreach (var endpoint in new[] { f.Result.ActionOriginObservation!.InventoryPayloadBefore!, f.Result.ActionOriginObservation.InventoryPayloadAfter! })
        {
            var slot = endpoint.Slots![0]; var xml = XElement.Parse(slot.SerializedStateXml!);
            xml.Add(new XElement("edibility", -300)); SetXml(slot, xml);
        }
        AddTillingResourceFields(f.Before, f.After);
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State);
        f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        f.Request.BeforeStateHash = f.Result.BeforeStateHash = f.Result.ActionOriginObservation.BeforeStateHash = f.Before.StateHash;
        f.Result.ProductAfterStateHash = f.After.StateHash;
        return (f, TillingResourceObservation(f.Before));
    }

    private static void AddTillingResourceFields(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        foreach (var snapshot in new[] { before, after })
        {
            var isAfter = ReferenceEquals(snapshot, after);
            Put(snapshot, "player", "energy", JsonValue.Create(isAfter ? 268f : 270f)!);
            Put(snapshot, "player", "max_energy", JsonValue.Create(270)!);
            Put(snapshot, "player", "skills_detail", new JsonObject { ["skills"] = new JsonArray(new JsonObject
                { ["skill_id"] = "farming", ["skill_index"] = 0, ["effective_level"] = 0 }) });
            Put(snapshot, "current_location", "statue_blessing", new JsonObject { ["active_blessing_buffs"] = new JsonArray() });
            var terrain = new JsonArray(new JsonObject { ["tile_x"] = 4, ["tile_y"] = 4, ["type"] = "StardewValley.TerrainFeatures.Tree" });
            if (isAfter) terrain.Add(new JsonObject { ["tile_x"] = 71, ["tile_y"] = 16, ["type"] = "StardewValley.TerrainFeatures.HoeDirt" });
            Put(snapshot, "farm", "terrain_features", terrain);
            Put(snapshot, "world_progress", "game_state_query_unlock_state", new JsonObject
            {
                ["current_player_id"] = snapshot.PlayerId.Value,
                ["players"] = new JsonArray(new JsonObject { ["player_id"] = snapshot.PlayerId.Value, ["is_current"] = true,
                        ["stats"] = new JsonObject { ["dirtHoed"] = isAfter ? 8u : 7u, ["stepsTaken"] = 5 } },
                    new JsonObject { ["player_id"] = "foreign", ["is_current"] = false, ["stats"] = new JsonObject { ["dirtHoed"] = 1u } })
            });
        }
    }

    private static SnapshotEnvelope TillingResourceObservation(SnapshotEnvelope before)
    {
        var observation = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!;
        Put(observation, "current_location", "site_preparation_context", new JsonObject
        {
            ["schema_version"] = "farm_site_preparation_context.v2", ["projection_status"] = "complete_bounded_current_farm_region",
            ["location_id"] = "Farm", ["hoe_tool_slot_index"] = 1, ["hoe_tool_qualified_item_id"] = "(T)Hoe",
            ["hoe_is_efficient"] = false, ["hoe_energy_cost_per_single_tile_action"] = 2d
        });
        observation.StateHash = SnapshotHash.ComputeStateHash(observation.State);
        return observation;
    }

    private static string TillingResourceRaw(TillingResourceLeaf f) =>
        JsonSerializer.Serialize(new { f.Raw.Request, f.Raw.Receipt, f.Raw.Before, f.Raw.After, f.Observation });

    private static void TillingResourceVerifyLeaf(TillingResourceLeaf f) => TillingResourceInvoke("VerifyObservedTillingResources",
        f.Raw.Before, f.Raw.After, f.Observation, "Farm", new MoneyRevenueCropPlantingTile(71, 16), 1, "(T)Hoe", f.Pickup);

    private static SnapshotEnvelope TillingResourceComparison(TillingResourceLeaf f)
    {
        var energy = (SnapshotEnvelope)TillingResourceInvoke("ProjectVerifiedTillingEnergy", f.Raw.Before, f.Raw.After, f.Observation, 1, "(T)Hoe")!;
        return (SnapshotEnvelope)TillingResourceInvoke("ProjectVerifiedTillingTerrainAndStatistic", f.Raw.Before, energy, "Farm", new MoneyRevenueCropPlantingTile(71, 16))!;
    }

    private static object? TillingResourceInvoke(string name, params object[] arguments)
    {
        var method = typeof(CropTeacherSupportingRoute).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        try { return method.Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidDataException invalid) { throw invalid; }
    }
}
