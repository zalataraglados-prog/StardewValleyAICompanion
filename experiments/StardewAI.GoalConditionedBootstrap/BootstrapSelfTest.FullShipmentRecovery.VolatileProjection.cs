using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void AddRecoveryVolatileProjection(
        SnapshotEnvelope snapshot,
        int variant)
    {
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!
            .AsObject();
        farm["machines"] = JsonSerializer.SerializeToNode(Available(new[]
        {
            new
            {
                qualified_item_id = "(BC)163",
                held_item = "(O)348",
                machine_probe_cache_tick = 100 + variant
            }
        }));
        farm["material_inventory_graph"] = JsonSerializer.SerializeToNode(
            Available(new
            {
                inventory_nodes = new[]
                {
                    new
                    {
                        node_id = "player:123",
                        inventory_kind = "player_inventory",
                        location_id = variant == 1 ? "Farm" : "FarmHouse",
                        slots = new[]
                        {
                            new
                            {
                                qualified_item_id = "(O)388",
                                stack = 8
                            }
                        }
                    }
                },
                quantity_rows = new[]
                {
                    new
                    {
                        qualified_item_id = "(O)388",
                        available_quantity = 8
                    }
                }
            }));
        foreach (var bin in farm["shipping_bins"]!["value"]!
                     .AsArray().OfType<JsonObject>())
        {
            bin["distance_to_player"] = variant == 1 ? 1.5 : 68.5;
            bin["player_within_shipping_range"] = variant == 1;
            bin["interaction_stand_tile_x"] = variant == 1 ? 70 : 71;
            bin["interaction_stand_tile_y"] = variant == 1 ? 14 : 13;
            bin["interaction_stand_tile_blocked_reason"] = null;
            bin["stand_tiles"] = JsonSerializer.SerializeToNode(variant == 1
                ? new[] { new { x = 70, y = 14 }, new { x = 71, y = 13 } }
                : new[] { new { x = 71, y = 13 }, new { x = 70, y = 14 } });
        }
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);

        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!
            .AsObject();
        player["active_dialogue_events"] = JsonSerializer.SerializeToNode(
            Available(variant == 1
                ? new[] { "Introduction" }
                : new[] { "Introduction", "firstVisit_FarmHouse" }));
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);

        var quests = JsonNode.Parse(snapshot.State["quests"].GetRawText())!
            .AsObject();
        quests["adventure_guild_reward"] = JsonSerializer.SerializeToNode(
            Available(new
            {
                batch_fingerprint = "stable",
                pending_goal_count = 0,
                action_tile_x = variant == 1 ? 12 : 11,
                action_tile_y = 12,
                action_tile_index = variant == 1 ? 1356 : 1355,
                stand_tile_x = variant == 1 ? 12 : 10,
                stand_tile_y = variant == 1 ? 13 : 12,
                current_location_matches = false,
                menu_clear = true
            }));
        snapshot.State["quests"] = JsonSerializer.SerializeToElement(quests);

        var progress = JsonNode.Parse(
            snapshot.State["world_progress"].GetRawText())!.AsObject();
        progress["game_state_query_unlock_state"] =
            JsonSerializer.SerializeToNode(Available(new
            {
                players = new[]
                {
                    new
                    {
                        player_id = "123",
                        stats = new
                        {
                            TreesChopped = 1,
                            stepsTaken = variant == 1 ? 9 : 12
                        }
                    }
                },
                island_north_bridge_fixed = false
            }));
        progress["game_state_query_calendar_state"] =
            JsonSerializer.SerializeToNode(Available(new
            {
                current_total_day = 40,
                time_of_day = variant == 1 ? 600 : 620,
                days_played = 41,
                festival_date_keys = new[] { "spring13", "spring24" }
            }));
        progress["marriage_house"] = JsonSerializer.SerializeToNode(
            Available(new
            {
                married_or_roommate = false,
                farmhouse_upgrade_level = 1,
                robin_present_at_counter = variant == 2,
                home_renovations = new
                {
                    projection_status = "complete",
                    robin_present_at_service = variant == 2,
                    native_available_renovation_ids = Array.Empty<string>()
                }
            }));
        snapshot.State["world_progress"] =
            JsonSerializer.SerializeToElement(progress);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }
}
