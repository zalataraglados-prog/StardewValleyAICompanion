using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CrabPotSupplyMainlineTests
{
    [Fact]
    public void MissingInfrastructureBuysOneExactFishShopCrabPot()
    {
        var snapshot = Snapshot(
            supplyQualifiedItemId: "(O)710",
            baitRequired: false,
            includeStock: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.collect_crab_pots" });
        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("buy_shop_item", candidate.Kind);
        Assert.Equal("FishShop", candidate.ShopId);
        Assert.Equal("(O)710", candidate.QualifiedItemId);
        Assert.Equal(1500, candidate.UnitPrice);
        AssertParameter(
            candidate.Parameters,
            "continuation.option_id",
            "fishing.collect_crab_pots");
        AssertParameter(
            candidate.Parameters,
            "continuation.crab_pot_supply_kind",
            "crab_pot");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.fishing.complete_master_angler");
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal(
            new[] { "buy_shop_item", "close_menu" },
            plan.Steps.Select(step => step.Kind));
        Assert.All(plan.Steps, step => Assert.Contains(
            step.Parameters,
            parameter => parameter.Name == "continuation.option_id" &&
                parameter.Value == "fishing.collect_crab_pots"));
    }

    [Fact]
    public void MissingBaitIsPurchasedBeforeRemotePotService()
    {
        var snapshot = Snapshot(
            supplyQualifiedItemId: "(O)685",
            baitRequired: true,
            includeStock: true);
        var candidate = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator()
                    .Evaluate(
                        snapshot,
                        new[] { "fishing.collect_crab_pots" })
                    .Options)
            .EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("buy_shop_item", candidate.Kind);
        Assert.Equal("(O)685", candidate.QualifiedItemId);
        Assert.Equal(5, candidate.UnitPrice);
        AssertParameter(
            candidate.Parameters,
            "continuation.crab_pot_supply_kind",
            "bait");
    }

    [Fact]
    public void MissingFishShopStockFailsClosed()
    {
        var snapshot = Snapshot(
            supplyQualifiedItemId: "(O)710",
            baitRequired: false,
            includeStock: false);
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.collect_crab_pots" })
            .Options);

        Assert.False(option.Available);
        Assert.DoesNotContain(
            option.EventCandidates,
            candidate => candidate.Available &&
                candidate.Kind == "buy_shop_item");
    }

    private static SnapshotEnvelope Snapshot(
        string supplyQualifiedItemId,
        bool baitRequired,
        bool includeStock)
    {
        var inventory = baitRequired
            ? new JsonArray(new JsonObject
            {
                ["slot_index"] = 2,
                ["qualified_item_id"] = "(O)710",
                ["category"] = -9,
                ["stack"] = 1
            })
            : new JsonArray();
        var networkRows = baitRequired
            ? new JsonArray(new JsonObject
            {
                ["location_id"] = "Forest",
                ["tile_x"] = 10,
                ["tile_y"] = 10,
                ["exact_base_crab_pot"] = true,
                ["production_domain_complete"] = true,
                ["service_status"] = "bait_required",
                ["production_signature"] = "Forest|0.2|freshwater|715",
                ["possible_qualified_item_ids"] = new JsonArray("(O)715"),
                ["conservative_serviced_probability_status"] =
                    "complete_native_supported_bait_lower_bound",
                ["conservative_serviced_outcome_rows"] = new JsonArray(
                    new JsonObject
                    {
                        ["qualified_item_id"] = "(O)715",
                        ["single_cycle_probability"] = 0.05
                    })
            })
            : new JsonArray();
        var stockEntries = includeStock
            ? new JsonArray(ShopEntry(supplyQualifiedItemId))
            : new JsonArray();
        var placementCount = baitRequired ? 1 : 0;
        var state = new JsonObject
        {
            ["time"] = new JsonObject
            {
                ["total_days"] = Field(10),
                ["time"] = Field(1000)
            },
            ["player"] = new JsonObject
            {
                ["location_id"] = Field("Beach"),
                ["tile_x"] = Field(10),
                ["tile_y"] = Field(10),
                ["energy"] = Field(270),
                ["money"] = Field(5000),
                ["inventory"] = Field(inventory),
                ["crab_pot_crafting"] = Field(new JsonObject
                {
                    ["projection_status"] =
                        "complete_learned_crab_pot_recipe_projection",
                    ["learned_crab_pot_recipe_count"] = 0,
                    ["recipe_learned"] = false,
                    ["rows"] = new JsonArray()
                }),
                ["crab_pot_placement"] = Field(new JsonObject
                {
                    ["projection_status"] =
                        "complete_inventory_crab_pots_across_loaded_persistent_locations",
                    ["inventory_crab_pot_count"] = placementCount,
                    ["rows"] = new JsonArray(),
                    ["potential_placement_locations"] = new JsonArray(
                        new JsonObject
                        {
                            ["location_id"] = "Beach",
                            ["placement_probe_status"] =
                                "native_legal_water_tiles_available",
                            ["static_legal_tile_ranges"] = new JsonArray(
                                new JsonObject
                                {
                                    ["y"] = 12,
                                    ["start_x"] = 15,
                                    ["end_x"] = 15,
                                    ["production_signature"] =
                                        "Beach|0.2|ocean|715",
                                    ["native_order_catch_rows"] =
                                        new JsonArray(new JsonObject
                                        {
                                            ["qualified_item_id"] = "(O)715"
                                        }),
                                    ["conservative_serviced_probability_status"] =
                                        "complete_native_supported_bait_lower_bound",
                                    ["conservative_serviced_outcome_rows"] =
                                        new JsonArray(new JsonObject
                                        {
                                            ["qualified_item_id"] = "(O)715",
                                            ["single_cycle_probability"] = 0.05
                                        })
                                })
                        })
                }),
                ["crab_pot_network"] = Field(new JsonObject
                {
                    ["projection_status"] =
                        "complete_crab_pots_across_loaded_persistent_locations",
                    ["placed_pot_count"] = networkRows.Count,
                    ["rows"] = networkRows
                })
            },
            ["menus"] = new JsonObject
            {
                ["active_menu"] = Field(new JsonObject
                {
                    ["is_open"] = true,
                    ["type"] = "ShopMenu"
                }),
                ["shop_stock"] = Field(new JsonObject
                {
                    ["shop_id"] = "FishShop",
                    ["read_only"] = false,
                    ["safety_timer"] = 0,
                    ["entries"] = stockEntries
                })
            },
            ["current_location"] = new JsonObject
            {
                ["objects"] = Field(new JsonArray())
            },
            ["locations"] = new JsonObject
            {
                ["collision_grid"] = Field(new JsonObject
                {
                    ["location_id"] = "Beach",
                    ["width"] = 100,
                    ["height"] = 100,
                    ["notable_tiles"] = new JsonArray()
                })
            },
            ["world_progress"] = new JsonObject
            {
                ["fish_collection_progress"] = Field(new JsonObject
                {
                    ["eligible_species_count"] = 72,
                    ["caught_eligible_species_count"] = 71,
                    ["missing_species_count"] = 1,
                    ["missing_item_ids"] = new JsonArray("715"),
                    ["items"] = FishRows()
                })
            }
        };
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            state.ToJsonString())!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(values),
            GameTick = 1,
            RealTimestamp = "2026-10-21T00:00:00Z",
            Completeness = "complete",
            State = values
        };
    }

    private static JsonObject ShopEntry(string qualifiedItemId)
    {
        var isCrabPot = qualifiedItemId == "(O)710";
        return new JsonObject
        {
            ["item_id"] = isCrabPot ? "710" : "685",
            ["qualified_item_id"] = qualifiedItemId,
            ["display_name"] = isCrabPot ? "Crab Pot" : "Bait",
            ["stack"] = 1,
            ["quality"] = 0,
            ["price"] = isCrabPot ? 1500 : 5,
            ["stock"] = int.MaxValue,
            ["infinite_stock"] = true,
            ["synced_key"] = qualifiedItemId,
            ["executor_purchase_enabled"] = true,
            ["executor_block_reasons"] = new JsonArray(),
            ["can_buy_item"] = true,
            ["can_afford_one_with_currency"] = true,
            ["can_afford_one_with_trade_item"] = true,
            ["could_inventory_accept"] = true,
            ["currency_balance"] = 5000
        };
    }

    private static JsonArray FishRows()
    {
        return new JsonArray(Enumerable.Range(0, 72)
            .Select(index => (JsonNode)new JsonObject
            {
                ["item_id"] = index == 0 ? "715" : "test_" + index,
                ["qualified_item_id"] = index == 0
                    ? "(O)715"
                    : "(O)test_" + index,
                ["caught"] = index != 0
            })
            .ToArray());
    }

    private static JsonObject Field(JsonNode value) => new()
    {
        ["value"] = value,
        ["status"] = "available"
    };

    private static JsonObject Field(string value) => Field(JsonValue.Create(value)!);

    private static JsonObject Field(int value) => Field(JsonValue.Create(value)!);

    private static void AssertParameter(
        IEnumerable<StardewAI.Contracts.Execution.SmallModelActionParameter> parameters,
        string name,
        string value)
    {
        Assert.Contains(parameters, parameter =>
            parameter.Name == name && parameter.Value == value);
    }
}
