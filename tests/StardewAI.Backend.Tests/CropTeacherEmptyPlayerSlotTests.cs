using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherEmptyPlayerSlotTests
{
    private static readonly MethodInfo VerifyConsumedInventory = typeof(CropTeacherSeedPlantingSupport).Assembly
        .GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary", throwOnError: true)!
        .GetMethod("VerifyConsumedInventory", BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void FinalSeedAcceptsReaderNullItemShapeWithNonNullDonateContext()
    {
        var beforeRows = InventoryRows(1);
        var afterRows = AfterRows(0);
        Assert.True(JsonNode.DeepEquals(beforeRows[1]!["donate_color_context"], afterRows[1]!["donate_color_context"]));
        Assert.NotNull(afterRows[1]!["donate_color_context"]);
        Verify(Snapshot(beforeRows), Snapshot(afterRows), 0);
    }

    [Fact]
    public void RemainingSeedStackChangesOnlyStackAndPreservesAllMetadata()
    {
        Verify(Snapshot(InventoryRows(3)), Snapshot(AfterRows(2)), 2);
    }

    [Theory]
    [InlineData(0, "color_null")]
    [InlineData(0, "color_flag")]
    [InlineData(0, "color_parent")]
    [InlineData(0, "color_tags")]
    [InlineData(0, "color_status")]
    [InlineData(0, "context_tags")]
    [InlineData(0, "unknown_metadata")]
    [InlineData(0, "other_row")]
    [InlineData(0, "compaction")]
    [InlineData(2, "color_null")]
    [InlineData(2, "color_flag")]
    [InlineData(2, "color_parent")]
    [InlineData(2, "color_tags")]
    [InlineData(2, "color_status")]
    [InlineData(2, "context_tags")]
    [InlineData(2, "unknown_metadata")]
    [InlineData(2, "other_row")]
    [InlineData(2, "compaction")]
    public void ConsumptionStillRejectsMetadataOtherRowAndCompactionDrift(int remaining, string change)
    {
        var afterRows = AfterRows(remaining);
        var selected = afterRows[1]!;
        switch (change)
        {
            case "color_null": selected["donate_color_context"] = null; break;
            case "color_flag": selected["donate_color_context"]!["is_colored_object"] = true; break;
            case "color_parent": selected["donate_color_context"]!["preserved_parent_item_id"] = "24"; break;
            case "color_tags": selected["donate_color_context"]!["preserved_parent_base_context_tags"] = new JsonArray("injected"); break;
            case "color_status": selected["donate_color_context"]!["projection_status"] = "injected"; break;
            case "context_tags": selected["context_tags"] = new JsonArray("injected"); break;
            case "unknown_metadata": selected["unknown_metadata"]!["guarded"] = "drift"; break;
            case "other_row": afterRows[0]!["stack"] = 99; break;
            case "compaction": afterRows.RemoveAt(1); afterRows[1]!["slot_index"] = 6; break;
        }
        var error = Assert.Throws<TargetInvocationException>(() =>
            Verify(Snapshot(InventoryRows(remaining + 1)), Snapshot(afterRows), remaining));
        Assert.Contains("owned planting exact inventory/no compaction", Assert.IsType<InvalidDataException>(error.InnerException).Message);
    }

    private static void Verify(SnapshotEnvelope before, SnapshotEnvelope after, int remaining) =>
        VerifyConsumedInventory.Invoke(null, new object[] { before, after, 6, remaining });

    private static SnapshotEnvelope Snapshot(JsonArray rows) => new()
    {
        State = new Dictionary<string, JsonElement>
        {
            ["player"] = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["inventory"] = new JsonObject { ["status"] = "available", ["value"] = rows }
            })
        }
    };

    private static JsonArray InventoryRows(int stack) => new(SeedSlot(2, 7), SeedSlot(6, stack), EmptySlot(9));

    private static JsonArray AfterRows(int remaining)
    {
        var rows = InventoryRows(remaining + 1);
        if (remaining == 0) rows[1] = EmptySlot(6);
        else rows[1]!["stack"] = remaining;
        return rows;
    }

    private static JsonObject SeedSlot(int index, int stack)
    {
        var row = JsonNode.Parse("""
            {
              "slot_index": 6, "item_id": "472", "qualified_item_id": "(O)472", "display_name": "Parsnip Seeds",
              "stack": 1, "quality": 0, "category": -74, "maximum_stack_size": 999,
              "can_be_shipped": false, "can_be_trashed": true, "sell_to_store_price": 10, "sale_price": 10,
              "runtime_type": "StardewValley.Object", "special_state": { "retained": true }, "special_item": false,
              "context_tags": ["item_parsnip_seeds"], "base_tag_not_giftable": false,
              "donate_color_context": { "is_colored_object": false, "preserved_parent_item_id": null,
                "preserved_parent_base_context_tags": [], "projection_status": "not_applicable_not_colored_object" },
              "is_object": true, "object_type": "Seeds", "object_quest_item": false, "object_big_craftable": false,
              "can_be_given_as_gift": false, "is_furniture": false, "is_wallpaper": false,
              "protected_from_auto_sell": true, "auto_sell_protection_reasons": ["fixture"], "is_empty": false,
              "unknown_metadata": { "guarded": "retained" }
            }
            """)!.AsObject();
        row["slot_index"] = index; row["stack"] = stack;
        return row;
    }

    private static JsonObject EmptySlot(int index)
    {
        // Independent expected shape from PlayerReadAdapter.Collect and ReadDonateColorContext(null),
        // not from the production projector. The unknown field deliberately remains guarded.
        var row = JsonNode.Parse("""
            {
              "slot_index": 6, "item_id": null, "qualified_item_id": null, "display_name": null,
              "stack": null, "quality": null, "category": null, "maximum_stack_size": null,
              "can_be_shipped": null, "can_be_trashed": null, "sell_to_store_price": null, "sale_price": null,
              "runtime_type": null, "special_state": null, "special_item": null, "context_tags": null,
              "base_tag_not_giftable": null,
              "donate_color_context": { "is_colored_object": false, "preserved_parent_item_id": null,
                "preserved_parent_base_context_tags": [], "projection_status": "not_applicable_not_colored_object" },
              "is_object": false, "object_type": null, "object_quest_item": null, "object_big_craftable": null,
              "can_be_given_as_gift": null, "is_furniture": false, "is_wallpaper": false,
              "protected_from_auto_sell": false, "auto_sell_protection_reasons": [], "is_empty": true,
              "unknown_metadata": { "guarded": "retained" }
            }
            """)!.AsObject();
        row["slot_index"] = index;
        return row;
    }
}
