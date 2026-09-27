using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static SnapshotEnvelope
        WriteMachineMaterialTransferProofBeforeSnapshot(
            string templatePath,
            string outputPath)
    {
        _ = WriteMachineCapacityProofBeforeSnapshot(templatePath, outputPath);
        var root = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        root["game_tick"] = 30;
        var player = root["state"]!["player"]!.AsObject();
        player["tile_x"]!["value"] = 8;
        player["tile_y"]!["value"] = 8;
        player["inventory"]!["value"] = new JsonArray();
        player["inventory_capacity"] =
            MachineMaterialTransferInventoryCapacity(
                occupiedStacks: 0,
                readAtTick: 30);
        player["machine_placement"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                projection_status =
                    "complete_all_inventory_machines_across_loaded_persistent_locations",
                static_projection_fingerprint =
                    "machine-material-transfer-no-inventory-machine",
                rows = Array.Empty<object>()
            },
            status = "available",
            source = new
            {
                kind = "test",
                path = "machine-material-transfer-proof"
            },
            adapter = "test",
            read_at_tick = 30,
            confidence = 1
        }, JsonDefaults.Options);
        SetMachineMaterialTransferProofInventory(root, afterTransfer: false);
        root["state"]!["farm"]!["machines"] =
            MachineMaterialTransferProofMachines(
                afterTransfer: false,
                readAtTick: 30);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static SnapshotEnvelope
        WriteMachineMaterialTransferProofAfterSnapshot(
            string beforePath,
            string outputPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(beforePath))!.AsObject();
        root["game_tick"] = root["game_tick"]!.GetValue<long>() + 2;
        var player = root["state"]!["player"]!.AsObject();
        player["tile_x"]!["value"] = 4;
        player["tile_y"]!["value"] = 6;
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
        player["inventory_capacity"] =
            MachineMaterialTransferInventoryCapacity(
                occupiedStacks: 1,
                readAtTick: 32);
        SetMachineMaterialTransferProofInventory(root, afterTransfer: true);
        root["state"]!["farm"]!["machines"] =
            MachineMaterialTransferProofMachines(
                afterTransfer: true,
                readAtTick: 32);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static JsonNode MachineMaterialTransferInventoryCapacity(
        int occupiedStacks,
        int readAtTick) => JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                occupied_stacks = occupiedStacks,
                empty_slots = 12 - occupiedStacks,
                has_empty_slot = true
            },
            status = "available",
            source = new
            {
                kind = "test",
                path = "machine-material-transfer-proof"
            },
            adapter = "test",
            read_at_tick = readAtTick,
            confidence = 1
        }, JsonDefaults.Options)!;

    private static JsonNode MachineMaterialTransferProofMachines(
        bool afterTransfer,
        int readAtTick)
    {
        var machines = MachineInputLoadProofMachines(
            processing: false,
            readAtTick);
        machines["source"]!["path"] = "machine-material-transfer-proof";
        if (!afterTransfer)
        {
            var row = machines["value"]![0]!.AsObject();
            row["machine_input_probe_eligible_count"] = 0;
            row["loadable_inputs"] = new JsonArray();
        }

        return machines;
    }

    private static void SetMachineMaterialTransferProofInventory(
        JsonObject root,
        bool afterTransfer)
    {
        var envelope = root["state"]!["farm"]![
            "material_inventory_graph"]!.AsObject();
        var readAtTick = afterTransfer ? 32 : 30;
        envelope["source"] = JsonSerializer.SerializeToNode(new
        {
            kind = "test",
            path = "machine-material-transfer-proof"
        }, JsonDefaults.Options);
        envelope["adapter"] = "test";
        envelope["read_at_tick"] = readAtTick;
        envelope["confidence"] = 1;
        var graph = envelope["value"]!.AsObject();
        var playerId = graph["player_id"]!.DeepClone();
        var playerSlots = afterTransfer
            ? new object[] { MachineMaterialTransferProofSlot() }
            : Array.Empty<object>();
        var chestSlots = afterTransfer
            ? Array.Empty<object>()
            : new object[] { MachineMaterialTransferProofSlot() };
        graph["inventory_nodes"] = JsonSerializer.SerializeToNode(new object[]
        {
            new
            {
                node_id = "player:1",
                inventory_kind = "player_inventory",
                supply_state = "available",
                location_id = "Farm",
                owner_player_id = playerId,
                actor_use_authorized = true,
                capacity = 12,
                slots = playerSlots
            },
            new
            {
                node_id = "chest:Farm:4,5",
                inventory_kind = "chest",
                supply_state = "available",
                location_id = "Farm",
                tile_x = 4,
                tile_y = 5,
                owner_player_id = playerId,
                actor_use_authorized = true,
                capacity = 36,
                slots = chestSlots
            }
        }, JsonDefaults.Options);
        graph["access_points"] = JsonSerializer.SerializeToNode(new[]
        {
            new
            {
                access_point_id = "access:chest:Farm:4,5",
                node_id = "chest:Farm:4,5",
                access_kind = "placed_chest",
                location_id = "Farm",
                location_is_current = true,
                tile_x = 4,
                tile_y = 5,
                special_chest_type = "None",
                owner_player_id = playerId,
                is_player_chest = true,
                locked_by_other_player = false
            }
        }, JsonDefaults.Options);
        graph["quantity_rows"] = JsonSerializer.SerializeToNode(new[]
        {
            new
            {
                qualified_item_id = "(O)262",
                quality = 0,
                available_quantity = 1,
                ready_output_quantity = 0,
                in_process_quantity = 0,
                restricted_quantity = 0,
                source_slot_count = 1
            }
        }, JsonDefaults.Options);
        graph["physical_inventory_count"] = 2;
        graph["access_point_count"] = 1;
        graph["deduplicated_access_point_count"] = 1;
    }

    private static object MachineMaterialTransferProofSlot() => new
    {
        slot_index = 0,
        item_id = "262",
        qualified_item_id = "(O)262",
        runtime_type = "StardewValley.Object",
        context_tags = Array.Empty<string>(),
        context_tags_projection_status = "exact_item_get_context_tags",
        edibility = -300,
        edibility_projection_status = "exact_object_edibility",
        stack = 1,
        maximum_stack_size = 999,
        quality = 0,
        sale_price = 25
    };
}
