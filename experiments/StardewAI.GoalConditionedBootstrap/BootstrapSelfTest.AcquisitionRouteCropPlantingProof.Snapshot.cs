using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static SnapshotEnvelope WriteCropPlantingProofBeforeSnapshot(
        string templatePath,
        string outputPath)
    {
        _ = WriteMachineInputLoadProofBeforeSnapshot(
            templatePath,
            outputPath);
        var root = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        root["game_tick"] = 40;

        var time = root["state"]!["time"]!.AsObject();
        time["total_days"] = CropPlantingProofField(0, 40);
        time["season"] = CropPlantingProofField("spring", 40);
        time["weather"] = CropPlantingProofField("sun", 40);

        var player = root["state"]!["player"]!.AsObject();
        player["location_id"]!["value"] = "Farm";
        player["tile_x"]!["value"] = 4;
        player["tile_y"]!["value"] = 6;
        player["energy"]!["value"] = 270;
        player["inventory"]!["value"] =
            CropPlantingProofInventory(stack: 3);
        player["seed_inventory"] = CropPlantingProofField(
            CropPlantingProofSeedInventory(stack: 3),
            40);
        player["inventory_capacity"] = CropPlantingProofField(
            new
            {
                occupied_stacks = 1,
                empty_slots = 11,
                has_empty_slot = true
            },
            40);

        var currentLocation = root["state"]!["current_location"]!
            .AsObject();
        currentLocation["crops"] = CropPlantingProofField(
            Array.Empty<object>(),
            40);
        currentLocation["planting_context"] = CropPlantingProofField(
            new
            {
                location_id = "Farm",
                hoe_dirt_tiles = new[]
                {
                    new
                    {
                        tile_x = 5,
                        tile_y = 6,
                        has_crop = false,
                        seed_results = new[]
                        {
                            new
                            {
                                slot_index = 0,
                                seed_id = "472",
                                hard_rule_allows_planting = true,
                                can_mature_before_season_end_with_paddy_if_eligible =
                                    true,
                                adjusted_grow_days_with_paddy_if_eligible = 4,
                                days_remaining_in_season = 20
                            }
                        }
                    }
                }
            },
            40);
        root["state"]!["farm"]!["crop_catalog"] =
            CropPlantingProofField(
                new[]
                {
                    new
                    {
                        seed_id = "472",
                        harvest_item_id = "24",
                        harvest_item_qualified_id = "(O)24",
                        harvest_unit_sale_price = 35,
                        harvest_min_stack = 1,
                        harvest_max_stack = 1,
                        harvest_max_increase_per_farming_level = 0,
                        extra_harvest_chance = 0,
                        harvest_min_quality = 0,
                        harvest_max_quality = 4,
                        harvest_method = "Grab",
                        regrow_days = -1
                    }
                },
                40);
        SetCropPlantingProofMaterialInventory(root, quantity: 3);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static SnapshotEnvelope WriteCropPlantingProofAfterSnapshot(
        string beforePath,
        string outputPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(beforePath))!.AsObject();
        root["game_tick"] = root["game_tick"]!.GetValue<long>() + 1;
        var player = root["state"]!["player"]!.AsObject();
        player["inventory"]!["value"] =
            CropPlantingProofInventory(stack: 2);
        player["seed_inventory"] = CropPlantingProofField(
            CropPlantingProofSeedInventory(stack: 2),
            41);
        SetCropPlantingProofMaterialInventory(root, quantity: 2);

        var currentLocation = root["state"]!["current_location"]!
            .AsObject();
        currentLocation["crops"] = CropPlantingProofField(
            new[]
            {
                new
                {
                    location_id = "Farm",
                    tile_x = 5,
                    tile_y = 6,
                    harvest_item_id = "24",
                    harvest_item_qualified_id = "(O)24",
                    harvest_source_seed_id = "472",
                    dead = false,
                    ready_for_harvest = false
                }
            },
            41);
        currentLocation["planting_context"] = CropPlantingProofField(
            new
            {
                location_id = "Farm",
                hoe_dirt_tiles = new[]
                {
                    new
                    {
                        tile_x = 5,
                        tile_y = 6,
                        has_crop = true,
                        seed_results = Array.Empty<object>()
                    }
                }
            },
            41);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static JsonArray CropPlantingProofInventory(int stack) =>
        new(JsonSerializer.SerializeToNode(new
        {
            slot_index = 0,
            item_id = "472",
            qualified_item_id = "(O)472",
            stack,
            quality = 0,
            maximum_stack_size = 999,
            is_empty = false,
            sale_price = 10
        }, JsonDefaults.Options));

    private static object[] CropPlantingProofSeedInventory(int stack) =>
    [
        new
        {
            slot_index = 0,
            item_id = "472",
            qualified_item_id = "(O)472",
            seed_id = "472",
            stack
        }
    ];

    private static JsonNode CropPlantingProofField<T>(T value, int tick) =>
        JsonSerializer.SerializeToNode(new
        {
            value,
            status = "available",
            source = new { kind = "test", path = "crop-planting-proof" },
            adapter = "test",
            read_at_tick = tick,
            confidence = 1
        }, JsonDefaults.Options)!;

    private static void SetCropPlantingProofMaterialInventory(
        JsonObject root,
        int quantity)
    {
        var graph = root["state"]!["farm"]!["material_inventory_graph"]![
            "value"]!.AsObject();
        var playerId = graph["player_id"]!.DeepClone();
        var playerNode = graph["inventory_nodes"]!.AsArray().Single(node =>
            node!["inventory_kind"]!.GetValue<string>() ==
            "player_inventory")!.AsObject();
        playerNode["owner_player_id"] = playerId;
        var slots = playerNode["slots"]!.AsArray();
        slots.Clear();
        slots.Add(JsonSerializer.SerializeToNode(new
        {
            slot_index = 0,
            item_id = "472",
            qualified_item_id = "(O)472",
            context_tags = Array.Empty<string>(),
            stack = quantity,
            quality = 0,
            sale_price = 10
        }, JsonDefaults.Options));
        var quantities = graph["quantity_rows"]!.AsArray();
        quantities.Clear();
        quantities.Add(JsonSerializer.SerializeToNode(new
        {
            qualified_item_id = "(O)472",
            quality = 0,
            available_quantity = quantity,
            ready_output_quantity = 0,
            in_process_quantity = 0,
            restricted_quantity = 0,
            source_slot_count = 1
        }, JsonDefaults.Options));
    }
}
