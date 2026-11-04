using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class RustyKeyPortfolioFixture
    {
        public RustyKeyRollingSupplyProposal RunRolling(
            Func<AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext?>? contextFactory = null,
            string routeTimingCalibrationPath = "") =>
            RustyKeyRollingSupplyProposalBuilder.Build(Inventory, Snapshot, Ledger,
                "fixture-inventory-sha", "fixture-snapshot-sha", "fixture-ledger-sha", contextFactory ?? (() => Context), routeTimingCalibrationPath);

        public void ChangeRoute(int index, Action<JsonObject> change)
        {
            var node = JsonSerializer.SerializeToNode(Opportunity.Routes[index], JsonDefaults.Options)!.AsObject();
            change(node);
            Opportunity.Routes[index] = node.Deserialize<AcquisitionRouteTargetDateOpportunityCost>(JsonDefaults.Options)!;
        }

        public void EnableHeldDonation(int index = 59)
        {
            var item = Inventory.RequirementSets.Single().Groups[index].Alternatives.Single();
            var world = JsonSerializer.SerializeToNode(Snapshot.State["world_progress"])!;
            var museum = world["museum"]!["value"]!.AsObject();
            var donated = museum["donated_count"]!.GetValue<int>();
            museum["museum_location_id"] = "ArchaeologyHouse";
            museum["museum_is_current_location"] = true;
            museum["museum_mutex_locked"] = false;
            museum["gunther_action_tile_x"] = 10;
            museum["gunther_action_tile_y"] = 9;
            museum["gunther_action_raw"] = "Gunther";
            museum["free_donation_tile_x"] = 4;
            museum["free_donation_tile_y"] = 4;
            museum["free_donation_tile_count"] = 1;
            museum["pending_reward_ids"] = new JsonArray();
            museum["complete_collection_achievement_received"] = false;
            museum["field_guide_quest_present"] = false;
            museum["field_guide_quest_completed"] = false;
            museum["rusty_key_reward_claimed"] = false;
            museum["rusty_key_prerequisite_event_seen"] = false;
            museum["rusty_key_event_seen"] = false;
            museum["has_rusty_key"] = false;
            var threshold = donated == 59;
            museum["donation_candidates"] = JsonSerializer.SerializeToNode(new[] { new
            {
                slot_index = 0, item_id = item.ItemId, qualified_item_id = item.QualifiedItemId,
                display_name = "synthetic museum item", runtime_type = "StardewValley.Object", stack_before = 2, stack_after = 1,
                donated_count_before = donated, donated_count_after = donated + 1, completes_collection = false,
                reaches_rusty_key_threshold = threshold, expected_complete_collection_achievement_after = false,
                field_guide_quest_present_before = false, field_guide_quest_completed_before = false,
                expected_field_guide_quest_completed_after = false, pending_reward_ids_before = Array.Empty<string>(),
                pending_reward_ids_after = Array.Empty<string>(), newly_pending_reward_ids = Array.Empty<string>(),
                auto_applied_reward_ids = threshold ? new[] { "museum60" } : Array.Empty<string>(),
                auto_applied_reward_actions = threshold ? new[] { "MarkEventSeen Host 295672" } : Array.Empty<string>(),
                reward_projection_status = "ready", action_status = "ready"
            } });
            Snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(world);
            Snapshot.State["player"] = JsonSerializer.SerializeToElement(new
            {
                location_id = new { value = "ArchaeologyHouse", status = "available" },
                tile_x = new { value = 8, status = "available" }, tile_y = new { value = 9, status = "available" },
                inventory = new { value = new[] { new { slot_index = 0, qualified_item_id = item.QualifiedItemId, stack = 2 } }, status = "available" }
            });
            Snapshot.State["locations"] = JsonSerializer.SerializeToElement(new
            {
                collision_grid = new { value = new { location_id = "ArchaeologyHouse", width = 64, height = 64, notable_tiles = Array.Empty<object>() }, status = "available" }
            });
            Snapshot.State["menus"] = JsonSerializer.SerializeToElement(new
            {
                active_menu = new { value = new { is_open = false, type = "none" }, status = "available" }
            });
            var time = JsonSerializer.SerializeToNode(Snapshot.State["time"])!;
            time["time"] = JsonSerializer.SerializeToNode(new { value = 1000, status = "available" });
            Snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
            var farm = JsonSerializer.SerializeToNode(Snapshot.State["farm"])!;
            var slot = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]![0]!;
            slot["item_id"] = item.ItemId;
            slot["qualified_item_id"] = item.QualifiedItemId;
            slot["stack"] = 2;
            Snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
            foreach (var section in Snapshot.State.ToArray())
            {
                var fields = JsonSerializer.SerializeToNode(section.Value)!.AsObject();
                foreach (var field in fields.Select(pair => pair.Value).OfType<JsonObject>()
                             .Where(field => field.ContainsKey("status") && field.ContainsKey("value")))
                {
                    field["confidence"] = 1;
                    field["adapter"] = "test";
                    field["read_at_tick"] = 1;
                    field["source"] = JsonSerializer.SerializeToNode(new { kind = "game_object", path = "synthetic_rolling_fixture" });
                }
                Snapshot.State[section.Key] = JsonSerializer.SerializeToElement(fields);
            }
            Rehash();
        }
    }
}
