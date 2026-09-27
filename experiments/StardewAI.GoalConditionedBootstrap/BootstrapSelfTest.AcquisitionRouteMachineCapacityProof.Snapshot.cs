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

    private static SnapshotEnvelope WriteMachineInputLoadProofBeforeSnapshot(
        string templatePath,
        string outputPath)
    {
        _ = WriteMachineCapacityProofBeforeSnapshot(templatePath, outputPath);
        var root = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        root["game_tick"] = 20;
        var player = root["state"]!["player"]!.AsObject();
        player["tile_x"]!["value"] = 6;
        player["tile_y"]!["value"] = 5;
        player["inventory"]!["value"] =
            JsonSerializer.SerializeToNode(new[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "262",
                    qualified_item_id = "(O)262",
                    stack = 1,
                    quality = 0,
                    maximum_stack_size = 999,
                    is_empty = false,
                    sale_price = 25
                }
            }, JsonDefaults.Options);
        player["inventory_capacity"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                occupied_stacks = 1,
                empty_slots = 11,
                has_empty_slot = true
            },
            status = "available",
            source = new { kind = "test", path = "machine-input-load-proof" },
            adapter = "test",
            read_at_tick = 20,
            confidence = 1
        }, JsonDefaults.Options);
        player["machine_placement"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                projection_status =
                    "complete_all_inventory_machines_across_loaded_persistent_locations",
                static_projection_fingerprint =
                    "machine-input-load-no-inventory-machine",
                rows = Array.Empty<object>()
            },
            status = "available",
            source = new { kind = "test", path = "machine-input-load-proof" },
            adapter = "test",
            read_at_tick = 20,
            confidence = 1
        }, JsonDefaults.Options);
        SetMachineInputLoadProofMaterialInventory(root, quantity: 1);
        root["state"]!["farm"]!["machines"] =
            MachineInputLoadProofMachines(processing: false, readAtTick: 20);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static SnapshotEnvelope WriteMachineInputLoadProofAfterSnapshot(
        string beforePath,
        string outputPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(beforePath))!.AsObject();
        root["game_tick"] = root["game_tick"]!.GetValue<long>() + 1;
        var player = root["state"]!["player"]!.AsObject();
        player["inventory"]!["value"] = new JsonArray();
        SetMachineInputLoadProofMaterialInventory(root, quantity: 0);
        root["state"]!["farm"]!["machines"] =
            MachineInputLoadProofMachines(processing: true, readAtTick: 21);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static JsonNode MachineInputLoadProofMachines(
        bool processing,
        int readAtTick)
    {
        var row = new
        {
            location_id = "Farm",
            location_kind = "farm_outdoor",
            location_is_current = true,
            machine_operational_context_valid = true,
            location_is_player_controlled = true,
            owner_player_id = 1,
            tile_x = 7,
            tile_y = 5,
            qualified_item_id = "(BC)12",
            display_name = "Keg",
            machine_row_count_total = 1,
            machine_row_snapshot_status = "complete_no_row_truncation",
            machine_input_probe_eligible_count = processing ? 0 : 1,
            machine_has_input = true,
            machine_has_output = true,
            ready_for_harvest = false,
            minutes_until_ready = processing ? 1750 : -1,
            last_output_rule_id = processing ? "keg_wheat" : null,
            last_input_item = processing
                ? new
                {
                    item_id = "262",
                    qualified_item_id = "(O)262",
                    stack = 1,
                    quality = 0
                }
                : null,
            held_item = processing
                ? new
                {
                    item_id = "346",
                    qualified_item_id = "(O)346",
                    stack = 1,
                    quality = 0,
                    sale_price = 200
                }
                : null,
            active_output_authoritative_route_sources = processing
                ? new[]
                {
                    new
                    {
                        route_kind = "machine_output",
                        source_id = MachineCapacityProofMachineSourceId,
                        qualified_item_id = "(O)346"
                    }
                }
                : Array.Empty<object>(),
            machine_execution_semantics = new
            {
                status = "available",
                execution_status = "available_data_driven",
                input_dispatch_kind = "base_object_data_driven",
                prediction_training_status =
                    "exact_current_snapshot_probe_supported"
            },
            machine_data = new
            {
                status = "available",
                has_output = true,
                additional_consumed_item_count = 0,
                output_rule_count = 1,
                output_rules = new[]
                {
                    new
                    {
                        id = "keg_wheat",
                        required_item_id = "(O)262",
                        required_count = 1,
                        minutes_until_ready = 1750,
                        output_item = new
                        {
                            item_id = "346",
                            qualified_item_id = "(O)346",
                            stack = 1,
                            quality = 0,
                            sale_price = 200
                        }
                    }
                }
            },
            loadable_inputs = processing
                ? Array.Empty<object>()
                : new object[]
                {
                    new
                    {
                        slot_index = 0,
                        item_id = "262",
                        qualified_item_id = "(O)262",
                        stack = 1,
                        quality = 0,
                        sale_price = 25,
                        predicted_output = new
                        {
                            status = "available",
                            training_eligibility_status =
                                "exact_current_snapshot_probe_supported",
                            source =
                                "MachineDataUtility.GetOutputItem(probe:true)",
                            matched_rule_id = "keg_wheat",
                            matched_rule_index = 0,
                            matched_output_index = 0,
                            required_item_id = "(O)262",
                            required_count = 1,
                            additional_consumed_item_count = 0,
                            effective_minutes_until_ready = 1750,
                            output_context_tags = new[]
                            {
                                "artisan_good",
                                "id_o_346"
                            },
                            item = new
                            {
                                item_id = "346",
                                qualified_item_id = "(O)346",
                                stack = 1,
                                quality = 0,
                                sale_price = 200
                            },
                            sale_price = 200,
                            stack = 1,
                            quality = 0,
                            authoritative_route_sources = new[]
                            {
                                new
                                {
                                    route_kind = "machine_output",
                                    source_id =
                                        MachineCapacityProofMachineSourceId,
                                    qualified_item_id = "(O)346"
                                }
                            }
                        },
                        probe_source =
                            "Object.performObjectDropInAction(probe:true)",
                        load_executor_status = "covered_for_runtime_load"
                    }
                }
        };
        return JsonSerializer.SerializeToNode(new
        {
            value = new[] { row },
            status = "available",
            source = new { kind = "test", path = "machine-input-load-proof" },
            adapter = "test",
            read_at_tick = readAtTick,
            confidence = 1
        }, JsonDefaults.Options)!;
    }

    private static void SetMachineInputLoadProofMaterialInventory(
        JsonObject root,
        int quantity)
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
        if (quantity <= 0)
        {
            return;
        }

        slots.Add(JsonSerializer.SerializeToNode(new
        {
            slot_index = 0,
            item_id = "262",
            qualified_item_id = "(O)262",
            context_tags = Array.Empty<string>(),
            stack = quantity,
            quality = 0,
            sale_price = 25
        }, JsonDefaults.Options));
        quantities.Add(JsonSerializer.SerializeToNode(new
        {
            qualified_item_id = "(O)262",
            quality = 0,
            available_quantity = quantity,
            ready_output_quantity = 0,
            in_process_quantity = 0,
            restricted_quantity = 0,
            source_slot_count = 1
        }, JsonDefaults.Options));
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
        var parsnip = entries.Single(entry =>
            entry!["qualified_item_id"]!.GetValue<string>() == "(O)24")!
            .AsObject();
        parsnip["currency"] = 0;
        parsnip["price"] = 100;
        parsnip["stock"] = 2;
        parsnip["infinite_stock"] = false;
        parsnip["can_buy_item"] = true;
        parsnip["executor_purchase_preview_enabled"] = true;
        parsnip["executor_block_reasons"] = new JsonArray();
        parsnip["trade_item_qualified_id"] = null;
        parsnip["effective_trade_item_count"] = null;
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
        entries.Add(JsonSerializer.SerializeToNode(new
        {
            synced_key = "fixture-wheat-seed",
            item_id = "262",
            qualified_item_id = "(O)262",
            display_name = "Wheat Seeds",
            stack = 1,
            quality = 0,
            currency = 0,
            price = 80,
            stock = 999,
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
