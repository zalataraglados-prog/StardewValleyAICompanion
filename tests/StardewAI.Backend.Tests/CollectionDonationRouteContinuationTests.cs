using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class CollectionDonationRouteContinuationTests
{
    [Fact]
    public void MuseumContinuationLocksExactItemAndCompletesOnlyAtDonation()
    {
        var route = QueueItem("executor.traverse_connector", new Dictionary<string, string>
        {
            ["continuation.option_id"] = "museum.donate_items",
            ["continuation.inventory_slot_index"] = "0",
            ["continuation.qualified_item_id"] = "(O)96"
        });
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.Equal("museum_donation", continuation!["kind"]!.GetValue<string>());
        var ranked = JsonNode.Parse("""
        [{"option_id":"museum.donate_items","parameters":[
          {"name":"inventory_slot_index","value":"0"},
          {"name":"qualified_item_id","value":"(O)96"}
        ]},{"option_id":"museum.donate_items","parameters":[
          {"name":"inventory_slot_index","value":"1"},
          {"name":"qualified_item_id","value":"(O)97"}
        ]}]
        """)!.AsArray();
        Assert.Single(QueueReplanFilter.FilterRankedCandidates(ranked, continuation));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            route, continuation, "applied"));

        var terminal = QueueItem("executor.donate_museum_item", new Dictionary<string, string>
        {
            ["inventory_slot_index"] = "0",
            ["qualified_item_id"] = "(O)96"
        });
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            terminal, continuation, "applied"));
    }

    [Fact]
    public void CommunityCenterContinuationLocksExactBundleIngredient()
    {
        var route = QueueItem("executor.traverse_connector", new Dictionary<string, string>
        {
            ["continuation.option_id"] = "community_center.donate_bundle_items",
            ["continuation.bundle_data_key"] = "Pantry/0",
            ["continuation.bundle_ingredient_index"] = "1",
            ["continuation.inventory_slot_index"] = "0",
            ["continuation.qualified_item_id"] = "(O)24",
            ["continuation.expected_item_quality"] = "0",
            ["continuation.required_stack"] = "2"
        });
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.Equal("community_center_donation", continuation!["kind"]!.GetValue<string>());
        var ranked = JsonNode.Parse("""
        [{"option_id":"community_center.donate_bundle_items","parameters":[
          {"name":"bundle_data_key","value":"Pantry/0"},
          {"name":"bundle_ingredient_index","value":"1"},
          {"name":"inventory_slot_index","value":"0"},
          {"name":"qualified_item_id","value":"(O)24"},
          {"name":"expected_item_quality","value":"0"},
          {"name":"required_stack","value":"2"}
        ]},{"option_id":"community_center.donate_bundle_items","parameters":[
          {"name":"bundle_data_key","value":"Pantry/0"},
          {"name":"bundle_ingredient_index","value":"2"},
          {"name":"inventory_slot_index","value":"0"},
          {"name":"qualified_item_id","value":"(O)24"},
          {"name":"expected_item_quality","value":"0"},
          {"name":"required_stack","value":"2"}
        ]}]
        """)!.AsArray();
        Assert.Single(QueueReplanFilter.FilterRankedCandidates(ranked, continuation));

        var terminal = QueueItem(
            "executor.donate_community_center_item",
            new Dictionary<string, string>
            {
                ["bundle_data_key"] = "Pantry/0",
                ["bundle_ingredient_index"] = "1",
                ["inventory_slot_index"] = "0",
                ["qualified_item_id"] = "(O)24",
                ["expected_item_quality"] = "0",
                ["required_stack"] = "2"
            });
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            terminal, continuation, "applied"));
    }

    private static JsonObject QueueItem(
        string optionId,
        IReadOnlyDictionary<string, string> parameters)
    {
        return new JsonObject
        {
            ["option_id"] = optionId,
            ["normalized_command"] = new JsonObject
            {
                ["parameters"] = new JsonArray(parameters.Select(pair =>
                    (JsonNode)new JsonObject
                    {
                        ["name"] = pair.Key,
                        ["value"] = pair.Value
                    }).ToArray())
            }
        };
    }
}
