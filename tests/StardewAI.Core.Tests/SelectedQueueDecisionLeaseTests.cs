using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class SelectedQueueDecisionLeaseTests
{
    [Fact]
    public void PreservesOneOrderedHighLevelCandidateAcrossMechanicalSteps()
    {
        var lease = SelectedQueueDecisionLease.Create(
            Queue(
                Item("mail.process:route", 0, MailContinuation()),
                Item("mail.process:route", 0, MailContinuation()),
                Item("ship:Farm:71,14:0:296:route:FarmHouse:3,12", 1,
                    ShippingContinuation())),
            Ranking(
                Candidate("mail.process:route", "mail.process_letter"),
                Candidate(
                    "ship:Farm:71,14:0:296:route:FarmHouse:3,12",
                    "economy.ship_items")));

        Assert.Equal(2, lease.Candidates.Count);
        Assert.Equal("mail.process:route", lease.CandidateAt(0).CandidateId);
        Assert.Equal("mail", lease.CandidateAt(0).ObjectiveContinuation!["kind"]!.GetValue<string>());
        Assert.Equal("economy_shipping", lease.CandidateAt(1).ObjectiveContinuation!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void TypedContinuationRebindsRouteCandidateToFreshApproachStage()
    {
        var selected = new SelectedQueueCandidateLock(
            1,
            "ship:Farm:71,14:0:296:route:FarmHouse:3,12",
            "economy.ship_items",
            Candidate(
                "ship:Farm:71,14:0:296:route:FarmHouse:3,12",
                "economy.ship_items"),
            new JsonObject
            {
                ["kind"] = "economy_shipping",
                ["option_id"] = "economy.ship_items",
                ["qualified_item_id"] = "(O)296",
                ["slot_index"] = "0",
                ["quantity"] = "1",
                ["expected_unit_price"] = "100",
                ["bin_location"] = "Farm",
                ["bin_tile_x"] = "71",
                ["bin_tile_y"] = "14",
                ["stand_tile_x"] = "71",
                ["stand_tile_y"] = "13"
            });
        var materialized = new JsonArray(
            Candidate(
                "ship:Farm:71,14:0:296:approach",
                "economy.ship_items",
                ShippingContinuation(standX: "70", standY: "14")),
            Candidate(
                "ship:Farm:71,14:7:309:approach",
                "economy.ship_items",
                ShippingContinuation(slot: "7", qualifiedItemId: "(O)309")));

        var rebound = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(
            materialized,
            selected);

        var candidate = Assert.Single(rebound);
        Assert.Equal(
            "ship:Farm:71,14:0:296:approach",
            candidate!["candidate_id"]!.GetValue<string>());
    }

    [Fact]
    public void CandidateWithoutTypedContinuationFailsClosedWhenIdentityChanges()
    {
        var selected = new SelectedQueueCandidateLock(
            0,
            "forage:Farm:10,10",
            "foraging.collect_spawned_objects",
            Candidate("forage:Farm:10,10", "foraging.collect_spawned_objects"),
            null);
        var materialized = new JsonArray(
            Candidate("forage:Farm:11,10", "foraging.collect_spawned_objects"));

        var rebound = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(
            materialized,
            selected);

        Assert.Empty(rebound);
    }

    private static JsonObject Queue(params JsonObject[] items) => new()
    {
        ["items"] = new JsonArray(items)
    };

    private static JsonObject Ranking(params JsonObject[] candidates) => new()
    {
        ["ranked_event_candidates"] = new JsonArray(candidates)
    };

    private static JsonObject Candidate(
        string candidateId,
        string optionId,
        JsonArray? parameters = null) => new()
    {
        ["candidate_id"] = candidateId,
        ["option_id"] = optionId,
        ["available"] = true,
        ["parameters"] = parameters ?? new JsonArray()
    };

    private static JsonObject Item(
        string candidateId,
        int selectedQueueIndex,
        JsonArray continuationParameters) => new JsonObject
    {
        ["status"] = "pending",
        ["normalized_command"] = new JsonObject
        {
            ["parameters"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "precondition",
                    ["value"] = "candidate_id:" + candidateId
                },
                new JsonObject
                {
                    ["name"] = "budget.accepted_candidate_index",
                    ["value"] = selectedQueueIndex.ToString()
                })
        }
    }.Also(item =>
    {
        foreach (var parameter in continuationParameters)
        {
            item["normalized_command"]!["parameters"]!.AsArray().Add(
                JsonNode.Parse(parameter!.ToJsonString()));
        }
    });

    private static JsonArray MailContinuation() => new(
        Parameter("continuation.option_id", "mail.process_letter"),
        Parameter("continuation.mail_id", "Beat_PK"),
        Parameter("continuation.target_location", "FarmHouse"));

    private static JsonArray ShippingContinuation(
        string slot = "0",
        string qualifiedItemId = "(O)296",
        string standX = "70",
        string standY = "14") => new(
        Parameter("continuation.option_id", "economy.ship_items"),
        Parameter("continuation.qualified_item_id", qualifiedItemId),
        Parameter("continuation.slot_index", slot),
        Parameter("continuation.quantity", "1"),
        Parameter("continuation.expected_unit_price", "100"),
        Parameter("continuation.bin_location", "Farm"),
        Parameter("continuation.bin_tile_x", "71"),
        Parameter("continuation.bin_tile_y", "14"),
        Parameter("continuation.stand_tile_x", standX),
        Parameter("continuation.stand_tile_y", standY));

    private static JsonObject Parameter(string name, string value) => new()
    {
        ["name"] = name,
        ["value"] = value
    };
}

internal static class JsonObjectTestExtensions
{
    public static JsonObject Also(
        this JsonObject value,
        Action<JsonObject> configure)
    {
        configure(value);
        return value;
    }
}
