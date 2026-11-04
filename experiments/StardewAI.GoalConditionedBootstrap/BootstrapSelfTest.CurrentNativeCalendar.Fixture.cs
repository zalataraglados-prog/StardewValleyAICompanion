using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private AcquisitionRouteExecutionBindingInputs PrepareCurrentNativeGeode()
        {
            var inputs = PrepareControlExecution();
            const string sourceId = "geode:535:0:random:0";
            var inventory = JsonNode.Parse(File.ReadAllText(inventoryPath))!;
            var groups = inventory["requirement_sets"]!.AsArray().Single(set => set!["requirement_set_id"]!.GetValue<string>() == "museum_collection")!["groups"]!.AsArray();
            groups[groups.IndexOf(groups.Single(group => group!["requirement_id"]!.GetValue<string>() == "museum:item:96"))] =
                JsonSerializer.SerializeToNode(CollectionRequirementGroup("museum:item:538", "all_required", 1,
                    CollectionAlternative("538", "(O)538", "Synthetic mineral source", "item_id", 1, 0, "native_geode_drop", sourceId)), JsonDefaults.Options);
            var route = groups.Single(group => group!["requirement_id"]!.GetValue<string>() == "museum:item:538")!["alternatives"]![0]!["acquisition_routes"]![0]!;
            route["source_asset"] = "Data/Objects";
            route["source_path"] = "payload.535.GeodeDrops[0].RandomItemId[0]";
            Write(inventoryPath, inventory);
            var lowering = JsonNode.Parse(File.ReadAllText(loweringPath))!;
            var loweredGroups = lowering["requirement_sets"]!.AsArray().Single(set => set!["requirement_set_id"]!.GetValue<string>() == "museum_collection")!["groups"]!.AsArray();
            loweredGroups[loweredGroups.IndexOf(loweredGroups.Single(group => group!["requirement_id"]!.GetValue<string>() == "museum:item:96"))] =
                JsonSerializer.SerializeToNode(CollectionLoweredGroup("museum:item:538", 1,
                    CollectionLoweredAlternative("538", "(O)538", "Synthetic mineral source", "item_id", 1, 0,
                        "native_geode_drop", sourceId, "processing.crack_geode")), JsonDefaults.Options);
            var loweredRoute = loweredGroups.Single(group => group!["requirement_id"]!.GetValue<string>() == "museum:item:538")!["alternatives"]![0]!["routes"]![0]!;
            loweredRoute["source_asset"] = route["source_asset"]!.DeepClone();
            loweredRoute["source_path"] = route["source_path"]!.DeepClone();
            lowering["requirement_inventory_sha256"] = HashFile(inventoryPath);
            Write(loweringPath, lowering);
            var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!;
            catalog["requirement_inventory_sha256"] = HashFile(inventoryPath);
            Write(catalogPath, catalog);
            Write(windowsPath, MasterAnglerStageOneWindowIndexBuilder.Build(catalogPath, 3));

            var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(inputs.BeforeSnapshotPath, "Current geode fixture");
            var player = JsonSerializer.SerializeToNode(snapshot.State["player"])!;
            player["location_id"] = JsonSerializer.SerializeToNode(NativeField("Blacksmith", "test"));
            player["tile_x"] = JsonSerializer.SerializeToNode(NativeField(3, "test"));
            player["tile_y"] = JsonSerializer.SerializeToNode(NativeField(4, "test"));
            player["geode_processing"] = JsonSerializer.SerializeToNode(NativeField(CurrentNativeGeodeProjection(), "test"));
            player["inventory"] = JsonSerializer.SerializeToNode(NativeField(new[] { new { slot_index = 0, qualified_item_id = "(O)535", stack = 2, quality = 0 } }, "test"));
            snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
            var menus = JsonSerializer.SerializeToNode(snapshot.State["menus"])!;
            menus["active_menu"] = JsonSerializer.SerializeToNode(NativeField(new { is_open = false, type = "none" }, "test"));
            snapshot.State["menus"] = JsonSerializer.SerializeToElement(menus);
            var locations = JsonSerializer.SerializeToNode(snapshot.State["locations"])!;
            locations["collision_grid"] = JsonSerializer.SerializeToNode(NativeField(new { location_id = "Blacksmith", width = 80, height = 80, notable_tiles = Array.Empty<object>() }, "test"));
            var dateEvidence = locations["social_route_date_evidence"]!["value"]!;
            var maps = dateEvidence["locations"]!.AsArray();
            maps.Add(JsonSerializer.SerializeToNode(Location("Blacksmith", mapWidth: 80, mapHeight: 80), JsonDefaults.Options));
            dateEvidence["location_count"] = maps.Count;
            snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
            var world = JsonSerializer.SerializeToNode(snapshot.State["world_progress"])!;
            var museum = world["museum"]!["value"]!;
            var target = museum["donatable_items"]!.AsArray().Single(row => row!["item_id"]!.GetValue<string>() == "96")!;
            target["item_id"] = "538"; target["qualified_item_id"] = "(O)538";
            museum["missing_item_ids"] = JsonSerializer.SerializeToNode(museum["donatable_items"]!.AsArray()
                .Where(row => !row!["donated"]!.GetValue<bool>()).Select(row => row!["item_id"]!.GetValue<string>()).ToArray());
            snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(world);
            var farm = JsonSerializer.SerializeToNode(snapshot.State["farm"])!;
            var slot = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]![0]!;
            slot["qualified_item_id"] = "(O)535"; slot["item_id"] = "535"; slot["stack"] = 2;
            snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            Write(inputs.BeforeSnapshotPath, snapshot);
            WriteEmptyStrategyLedger(inputs.StrategyLedgerPath, snapshot.StateHash);
            Write(Path.Combine(root, "runtime-inputs.json"), inputs);
            return inputs;
        }
    }

    private static object CurrentNativeGeodeProjection() => new
    {
        schema_version = "geode_processing.v1", projection_status = "complete_locked_base_1.6.15",
        projection_fingerprint = new string('g', 64), location_id = "Blacksmith", base_service_status = "ready",
        price_gold = 25, money_before = 500, free_inventory_slots = 5, geodes_cracked_before = 42,
        mystery_boxes_opened_before = 11, golden_coconut_cracked_before = false, golden_walnuts_before = 17,
        golden_walnuts_found_before = 29, archaeology_found_count = 0,
        predictor_context = new { save_id_half = 12345L, player_id_half = 678L, season = "spring", deepest_mine_level = 90,
            skill_1_unmodified_level = 8, farming_mastery_unlocked = false, qi_beans_rule_active = false,
            got_mystery_book_mail = true, artifact_found_mail = false, geode_found_mail = true },
        inventory_inputs = new[] { new { slot_index = 0, qualified_item_id = "(O)535", item_id = "535", display_name = "Geode",
            stack_before = 2, quality = 0, locked_base_1_6_15 = true, output_capacity_allowed = true, kind = "exact", status = "available",
            expected_output = new { qualified_item_id = "(O)538", stack = 1, quality = 0, set_flag_on_pickup = "", inventory_persists = true,
                pickup_effect_kind = "mineral_discovery", expected_mail_additions = Array.Empty<string>() },
            accepted_outputs = new[] { new { qualified_item_id = "(O)538", stack = 1, quality = 0, set_flag_on_pickup = "", inventory_persists = true,
                pickup_effect_kind = "mineral_discovery", expected_mail_additions = Array.Empty<string>() } },
            authoritative_route_sources = new[] { new { route_kind = "native_geode_drop", source_id = "geode:535:0:random:0", qualified_item_id = "(O)538" } },
            expected_mail_additions = Array.Empty<string>(), reason = "seeded_exact" } },
        counter_action_tiles = new[] { new { tile_x = 3, tile_y = 3, action_raw = "Blacksmith", action_token = "Blacksmith" } },
        native_contract = "shared_route->Blacksmith_checkAction->answerDialogue(Process)->GeodeMenu_inventory_click->GeodeMenu_geodeSpot_click->2700ms_native_animation->inventory_receipt"
    };
}
