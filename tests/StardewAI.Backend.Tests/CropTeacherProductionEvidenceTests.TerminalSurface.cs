using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Execution;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    // Explicit protocol-fixture fields, not observed native gameplay or production data.
    private static void AddTerminalControlSurface(SnapshotEnvelope snapshot, bool openMenu)
    {
        snapshot.InGameTime.Status = "available";
        snapshot.InGameTime.Value = snapshot.State["time"].GetProperty("time").GetProperty("value").GetInt32();
        var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!;
        time["time"]!["read_at_tick"] = snapshot.GameTick;
        snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        farm["shipping_bins"]!["value"]![0]!["interaction_stand_tile_blocked_reason"] = null;
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["movement_timing_context"] = JsonSerializer.SerializeToNode(new
        {
            status = "available", value = new
            {
                projection_status = "exact_current_player_native_cardinal_movement_context",
                scope = "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
                runtime_calibration_compatible = true, route_timing_ready_now = false,
                native_control_gate_open = true, player_can_move_now = false, player_using_tool_now = false,
                immobilizing_buff_19_active = false, current_event_active = false, event_up = false,
                game_paused = false, dialogue_up = false, global_fade = false, freeze_controls = false,
                is_warping = false, player_freeze_pause = 0
            }
        });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.State["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = new
            {
                is_open = openMenu, type = openMenu ? "DialogueBox" : "none", is_sleep_prompt = false,
                event_up = false, dialogue_is_question = false, dialogue_response_count = 0,
                dialogue_transitioning = false, dialogue_character_present = false,
                dialogue_speaker_name = (string?)null, last_question_key = ""
            } }
        });
        SynchronizeDeclaredTerminalStock(snapshot, enrich: true);
    }

    internal static void SynchronizeDeclaredTerminalStock(SnapshotEnvelope snapshot, bool enrich = false)
    {
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        var inventory = player["inventory"]!["value"]!.AsArray();
        if (enrich)
            foreach (var row in inventory.OfType<JsonObject>().Where(row => row["is_empty"]?.GetValue<bool>() != true))
            {
                row["is_empty"] = false;
                row["display_name"] ??= "Declared terminal item";
                row["runtime_type"] ??= "StardewValley.Object";
                row["context_tags"] ??= new JsonArray();
                row["maximum_stack_size"] ??= 999;
                row["sale_price"] ??= 0;
            }
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        var graph = farm["material_inventory_graph"]!["value"]!;
        var node = graph["inventory_nodes"]!.AsArray().OfType<JsonObject>().Single(row => row["inventory_kind"]!.GetValue<string>() == "player_inventory");
        node["capacity"] = 12;
        node["location_id"] = player["location_id"]!["value"]!.DeepClone();
        node["slots"] = new JsonArray(inventory.OfType<JsonObject>().Where(row => row["is_empty"]?.GetValue<bool>() == false)
            .Select(row => JsonSerializer.SerializeToNode(row.Deserialize<MaterialInventorySlot>(JsonOptions), JsonOptions)).ToArray());
        var occupied = node["slots"]!.AsArray().Count;
        player["inventory_capacity"] = JsonSerializer.SerializeToNode(new { status = "available", value = new
            { max_items = 12, occupied_item_stacks = occupied, empty_slots = 12 - occupied, has_empty_slot = occupied < 12 } });
        graph["quantity_rows"] = new JsonArray(graph["inventory_nodes"]!.AsArray().OfType<JsonObject>()
            .SelectMany(value => value["slots"]!.AsArray().OfType<JsonObject>().Select(slot => (Node: value, Slot: slot)))
            .Where(row => row.Slot["stack"]!.GetValue<int>() > 0)
            .GroupBy(row => (Id: row.Slot["qualified_item_id"]!.GetValue<string>(), Quality: row.Slot["quality"]!.GetValue<int>()))
            .OrderBy(group => group.Key.Id, StringComparer.Ordinal).ThenBy(group => group.Key.Quality)
            .Select(group => JsonSerializer.SerializeToNode(new { qualified_item_id = group.Key.Id, quality = group.Key.Quality,
                available_quantity = group.Where(row => row.Node["actor_use_authorized"]!.GetValue<bool>()).Sum(row => row.Slot["stack"]!.GetValue<int>()),
                restricted_quantity = group.Where(row => !row.Node["actor_use_authorized"]!.GetValue<bool>()).Sum(row => row.Slot["stack"]!.GetValue<int>()),
                ready_output_quantity = 0, in_process_quantity = 0, source_slot_count = group.Count() })).ToArray());
        foreach (var bin in farm["shipping_bins"]!["value"]!.AsArray().OfType<JsonObject>())
        {
            var contents = bin["contents"]!.AsArray().OfType<JsonObject>().ToArray();
            bin["contents_total_count"] = contents.Sum(row => row["count"]!.GetValue<int>());
            bin["contents_distinct_item_count"] = contents.Select(row => row["qualified_item_id"]!.GetValue<string>()).Distinct().Count();
            bin["contents_distinct_settlement_variant_count"] = contents.Length;
            bin["contents_truncated"] = false;
            bin["pending_settlement_timing"] = "next_native_day_transition";
            var text = string.Concat(contents.OrderBy(row => row["qualified_item_id"]!.GetValue<string>(), StringComparer.Ordinal)
                .ThenBy(row => row["quality"]!.GetValue<int>()).ThenBy(row => row["unit_sell_to_store_price"]!.GetValue<int>())
                .Select(row => $"{row["qualified_item_id"]!.GetValue<string>()}|{row["quality"]!.GetValue<int>()}|{row["unit_sell_to_store_price"]!.GetValue<int>()}|{row["count"]!.GetValue<int>()}\n"));
            bin["contents_signature"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
    }
}
