using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeCropHarvestCommunityCenterBoundaryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    public void InventoryPreviewComparisonDoesNotTreatFutureDonationAsActualProgress(int beforeUnits, int afterUnits)
    {
        var (before, after) = Fixture(beforeUnits, afterUnits);
        var original = JsonSerializer.Serialize(new[] { before, after });
        var compared = Read(before, after)!;
        Assert.False(compared["value"]!["bundles"]!["0"]![0]!.GetValue<bool>());
        Assert.False(compared["value"]!["completed"]!.GetValue<bool>());
        Assert.Equal(beforeUnits == 0 ? 0 : 1, compared["value"]!["bundle_rows"]![0]!["donation_candidates"]!.AsArray().Count);
        Assert.Equal(original, JsonSerializer.Serialize(new[] { before, after }));
    }

    [Theory]
    [InlineData("completion-bit")]
    [InlineData("reward")]
    [InlineData("mail")]
    [InlineData("unknown-progress")]
    [InlineData("ingredient")]
    [InlineData("unknown-candidate")]
    [InlineData("duplicate-candidate")]
    [InlineData("slot")]
    [InlineData("item-id")]
    [InlineData("quality")]
    [InlineData("stack")]
    [InlineData("total")]
    [InlineData("required")]
    [InlineData("ingredient-index")]
    [InlineData("before-total")]
    public void RefusesChangedRealProgressOrContradictoryInventoryPreview(string change)
    {
        var (before, after) = Fixture(1, 2);
        switch (change)
        {
            case "completion-bit": Center(after, row => row["bundles"]!["0"]![0] = true); break;
            case "reward": Center(after, row => row["bundle_rewards"]!["0"] = true); break;
            case "mail": Center(after, row => row["pending_area_mail_flags"]!.AsArray().Add("ccPantry")); break;
            case "unknown-progress": Center(after, row => row["future_persistent"] = "changed"); break;
            case "ingredient": Center(after, row => row["bundle_rows"]![0]!["ingredients"]![0]!["required_stack"] = 2); break;
            case "unknown-candidate": Candidate(after, row => row["future_persistent"] = "unproven"); break;
            case "duplicate-candidate": Center(after, row => row["bundle_rows"]![0]!["donation_candidates"]!.AsArray()
                .Add(row["bundle_rows"]![0]!["donation_candidates"]![0]!.DeepClone())); break;
            case "slot": Candidate(after, row => row["inventory_slot_index"] = 99); break;
            case "item-id": Candidate(after, row => row["item_id"] = "191"); break;
            case "quality": Candidate(after, row => row["quality"] = 4); break;
            case "stack": Candidate(after, row => row["stack_before"] = 3); break;
            case "total": Candidate(after, row => row["inventory_item_total_before"] = 3); break;
            case "required": Candidate(after, row => row["required_stack"] = 2); break;
            case "ingredient-index": Candidate(after, row => row["ingredient_index"] = 1); break;
            case "before-total": Candidate(before, row => row["inventory_item_total_before"] = 99); break;
        }
        before.StateHash = SnapshotHash.ComputeStateHash(before.State); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        Assert.Throws<InvalidDataException>(() => Read(before, after));
    }

    private static JsonObject? Read(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var type = typeof(CropTeacherWateringMaintenance).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!;
        try { return (JsonObject?)type.GetMethod("CompareHarvestCommunityCenterProgress", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [before, after]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private static (SnapshotEnvelope Before, SnapshotEnvelope After) Fixture(int beforeUnits, int afterUnits)
    {
        var before = NativeWildTreeClearanceBoundaryTests.Fixture(0).Before;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!;
        after.GameTick += 2;
        foreach (var snapshot in new[] { before, after })
        {
            var units = ReferenceEquals(snapshot, before) ? beforeUnits : afterUnits;
            var inventory = JsonNode.Parse(snapshot.State["player"].GetProperty("inventory").GetProperty("value").GetRawText())!.AsArray();
            if (units > 0) inventory.Add(new JsonObject { ["slot_index"] = 1, ["is_empty"] = false, ["item_id"] = "190", ["qualified_item_id"] = "(O)190",
                ["runtime_type"] = "StardewValley.Object", ["quality"] = 0, ["stack"] = units });
            Put(snapshot, "player", "inventory", inventory);
            var candidate = new CommunityCenterDonationCandidateRef { InventorySlotIndex = 1, IngredientIndex = 0, ItemId = "190", QualifiedItemId = "(O)190",
                RuntimeType = "StardewValley.Object", Quality = 0, StackBefore = units, StackAfter = units - 1, RequiredStack = 1,
                InventoryItemTotalBefore = units, InventoryItemTotalAfter = units - 1, CompletedIngredientCountBefore = 0, CompletedIngredientCountAfter = 1,
                CompletesBundle = true, ExpectedBundleRewardAvailableAfter = true, ExpectedCompleteBundleCountAfter = 1, CompletesArea = true,
                ExpectedAreaCompleteAfter = true, ExpectedAreaCompletionMailPendingAfter = true, ActionStatus = "community_center_not_current_location" };
            Put(snapshot, "world_progress", "community_center", new JsonObject
            {
                ["completed"] = false, ["bundles"] = new JsonObject { ["0"] = new JsonArray(false) }, ["bundle_rewards"] = new JsonObject { ["0"] = false },
                ["pending_area_mail_flags"] = new JsonArray(), ["future_persistent"] = "retained",
                ["bundle_rows"] = new JsonArray(new JsonObject { ["bundle_id"] = 0, ["required_slot_count"] = 1, ["completed_ingredient_count"] = 0,
                    ["ingredients"] = new JsonArray(JsonSerializer.SerializeToNode(new CommunityCenterIngredientProgressRef { IngredientIndex = 0,
                        ItemIdOrCategory = "190", RequiredStack = 1, MinimumQuality = 0 })),
                    ["donation_candidates"] = units == 0 ? new JsonArray() : new JsonArray(JsonSerializer.SerializeToNode(candidate)) })
            });
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        return (before, after);
    }

    private static void Candidate(SnapshotEnvelope snapshot, Action<JsonNode> edit)
        => Center(snapshot, row => edit(row["bundle_rows"]![0]!["donation_candidates"]![0]!));

    private static void Center(SnapshotEnvelope snapshot, Action<JsonNode> edit)
    {
        var progress = JsonNode.Parse(snapshot.State["world_progress"].GetRawText())!;
        edit(progress["community_center"]!["value"]!); snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(progress);
    }

    private static void Put(SnapshotEnvelope snapshot, string domain, string field, JsonNode value)
    {
        var fields = JsonNode.Parse(snapshot.State[domain].GetRawText())!;
        fields[field] = new JsonObject { ["status"] = "available", ["value"] = value };
        snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
    }
}
