using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static SnapshotEnvelope WriteMachineCapacityProofBeforeSnapshot(
        string templatePath,
        string outputPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        root["game_tick"] = 10;
        var player = root["state"]!["player"]!.AsObject();
        player["location_id"]!["value"] = "Farm";
        player["tile_x"]!["value"] = 5;
        player["tile_y"]!["value"] = 5;
        var inventory = player["inventory"]!["value"]!.AsArray();
        inventory.Clear();
        player["inventory"]!["adapter"] = "test";
        inventory.Add(JsonSerializer.SerializeToNode(new
        {
            slot_index = 4,
            item_id = "12",
            qualified_item_id = "(BC)12",
            stack = 1,
            quality = 0,
            maximum_stack_size = 999,
            is_empty = false
        }, JsonDefaults.Options));
        player["machine_placement"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                projection_status =
                    "complete_all_inventory_machines_across_loaded_persistent_locations",
                static_projection_fingerprint =
                    "machine-capacity-file-backed-placement-layout",
                rows = new[]
                {
                    new
                    {
                        inventory_slot_index = 4,
                        item_id = "12",
                        qualified_item_id = "(BC)12",
                        stack = 1,
                        locations = new[]
                        {
                            new
                            {
                                location_id = "Farm",
                                location_is_current = true,
                                machine_operational_context_valid = true,
                                placement_probe_status =
                                    "native_legal_tiles_available",
                                static_legal_tile_count = 1,
                                static_legal_tile_ranges = new[]
                                {
                                    new
                                    {
                                        y = 5,
                                        start_x = 7,
                                        end_x = 7
                                    }
                                }
                            }
                        }
                    }
                }
            },
            status = "available",
            source = new { kind = "test", path = "machine-capacity-proof" },
            adapter = "test",
            read_at_tick = 10,
            confidence = 1
        }, JsonDefaults.Options);
        root["state"]!["current_location"]!["map"] =
            JsonSerializer.SerializeToNode(new
            {
                value = new { width = 10, height = 10 },
                status = "available",
                source = new
                {
                    kind = "test",
                    path = "machine-capacity-proof"
                },
                adapter = "test",
                read_at_tick = 10,
                confidence = 1
            }, JsonDefaults.Options);
        root["state"]!["locations"]!["collision_grid"]!["value"]![
            "width"] = 10;
        var farmRouteEvidence = root["state"]!["locations"]![
                "social_route_date_evidence"]!["value"]!["locations"]!
            .AsArray()
            .Single(location =>
                location!["location_id"]!.GetValue<string>() == "Farm")!
            .AsObject();
        farmRouteEvidence["map_width"] = 10;
        farmRouteEvidence["static_walkable_tile_count"] = 100;
        farmRouteEvidence["static_walkable_tile_ranges"] =
            JsonSerializer.SerializeToNode(
                Enumerable.Range(0, 10).Select(y => new
                {
                    y,
                    start_x = 0,
                    end_x = 9
                }).ToArray(),
                JsonDefaults.Options);
        var routeGraph = root["state"]!["locations"]!["route_graph"]!;
        routeGraph["adapter"] = "test";
        var shopEndpoint = routeGraph["value"]!["edges"]!.AsArray()
            .Single(edge =>
                edge!["kind"]!.GetValue<string>() == "shop_endpoint" &&
                edge["shop_id"]!.GetValue<string>() == "FixtureShop")!
            .AsObject();
        shopEndpoint["resolved"] = true;
        root["state"]!["menus"]!["active_menu"]!["adapter"] = "test";
        root["state"]!["player"]!["energy"]!["adapter"] = "test";
        root["state"]!["time"]!["time"]!["adapter"] = "test";
        AddMachineCapacityProofShopPreview(root);
        SetMachineCapacityProofMaterialInventory(root, containsMachine: true);
        root["state"]!["farm"]!["machines"] =
            JsonSerializer.SerializeToNode(new
            {
                value = Array.Empty<object>(),
                status = "available",
                source = new
                {
                    kind = "test",
                    path = "machine-capacity-proof"
                },
                adapter = "test",
                read_at_tick = 10,
                confidence = 1
            }, JsonDefaults.Options);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static SnapshotEnvelope WriteMachineCapacityProofAfterSnapshot(
        string beforePath,
        string outputPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(beforePath))!.AsObject();
        root["game_tick"] = root["game_tick"]!.GetValue<long>() + 2;
        var player = root["state"]!["player"]!.AsObject();
        player["tile_x"]!["value"] = 6;
        player["tile_y"]!["value"] = 5;
        player["inventory"]!["value"] = new JsonArray();
        player["machine_placement"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                projection_status =
                    "complete_all_inventory_machines_across_loaded_persistent_locations",
                static_projection_fingerprint =
                    "machine-capacity-file-backed-placement-complete",
                rows = Array.Empty<object>()
            },
            status = "available",
            source = new { kind = "test", path = "machine-capacity-proof" },
            adapter = "test",
            read_at_tick = 12,
            confidence = 1
        }, JsonDefaults.Options);
        SetMachineCapacityProofMaterialInventory(root, containsMachine: false);
        root["state"]!["farm"]!["machines"] =
            JsonSerializer.SerializeToNode(new
            {
                value = new[]
                {
                    new
                    {
                        location_id = "Farm",
                        tile_x = 7,
                        tile_y = 5,
                        qualified_item_id = "(BC)12",
                        machine_row_count_total = 1,
                        machine_row_snapshot_status =
                            "complete_no_row_truncation",
                        machine_input_probe_eligible_count = 0,
                        location_is_player_controlled = true,
                        owner_player_id = 1,
                        ready_for_harvest = false,
                        minutes_until_ready = 0,
                        machine_has_input = true,
                        machine_has_output = true,
                        held_item = (object?)null,
                        active_output_authoritative_route_sources =
                            Array.Empty<object>(),
                        loadable_inputs = Array.Empty<object>()
                    }
                },
                status = "available",
                source = new
                {
                    kind = "test",
                    path = "machine-capacity-proof"
                },
                adapter = "test",
                read_at_tick = 12,
                confidence = 1
            }, JsonDefaults.Options);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static void SetMachineCapacityProofMaterialInventory(
        JsonObject root,
        bool containsMachine)
    {
        var graph = root["state"]!["farm"]!["material_inventory_graph"]![
            "value"]!.AsObject();
        var playerId = graph["player_id"]!.DeepClone();
        var nodes = graph["inventory_nodes"]!.AsArray();
        var playerNode = nodes.Single(node =>
            node!["inventory_kind"]!.GetValue<string>() ==
            "player_inventory")!.AsObject();
        playerNode["owner_player_id"] = playerId;
        var slots = playerNode["slots"]!.AsArray();
        slots.Clear();
        var quantities = graph["quantity_rows"]!.AsArray();
        quantities.Clear();
        if (!containsMachine)
        {
            return;
        }

        slots.Add(JsonSerializer.SerializeToNode(new
        {
            slot_index = 4,
            item_id = "12",
            qualified_item_id = "(BC)12",
            context_tags = Array.Empty<string>(),
            stack = 1,
            quality = 0,
            sale_price = 0
        }, JsonDefaults.Options));
        quantities.Add(JsonSerializer.SerializeToNode(new
        {
            qualified_item_id = "(BC)12",
            quality = 0,
            available_quantity = 1,
            ready_output_quantity = 0,
            in_process_quantity = 0,
            restricted_quantity = 0,
            source_slot_count = 1
        }, JsonDefaults.Options));
    }

    private static void AddMachineCapacityProofShopPreview(JsonObject root)
    {
        var preview = root["state"]!["locations"]!["shops"]!["value"]![
            "shops"]![0]!["stock_preview"]!.AsObject();
        var entries = preview["entries"]!.AsArray();
        entries.Add(JsonSerializer.SerializeToNode(new
        {
            synced_key = "fixture-beer",
            qualified_item_id = "(O)346",
            stack = 1,
            quality = 0,
            currency = 0,
            price = 200,
            stock = 1,
            infinite_stock = false,
            can_buy_item = true,
            executor_purchase_preview_enabled = true,
            executor_block_reasons = Array.Empty<string>(),
            trade_item_qualified_id = (string?)null,
            effective_trade_item_count = (int?)null
        }, JsonDefaults.Options));
        preview["entry_count"] = entries.Count;
    }

    private static SnapshotEnvelope WriteMachineCapacityProofSnapshot(
        JsonObject root,
        string path)
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            root["state"]!.ToJsonString(JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Machine-capacity proof snapshot state is null.");
        root["state_hash"] = SnapshotHash.ComputeStateHash(state);
        File.WriteAllText(path, root.ToJsonString(JsonDefaults.Options));
        return JsonSerializer.Deserialize<SnapshotEnvelope>(
            root.ToJsonString(JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Machine-capacity proof snapshot is null.");
    }
}
