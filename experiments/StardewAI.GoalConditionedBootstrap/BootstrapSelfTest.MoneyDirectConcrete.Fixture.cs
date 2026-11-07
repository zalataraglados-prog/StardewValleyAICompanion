using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static string MoneyDirectConcreteFixture(string root)
    {
        var snapshot = MoneyActiveShopSnapshot();
        SetActiveShopTestField(snapshot, "time", "time", 1000);
        SetActiveShopTestField(snapshot, "player", "inventory", new[]
        {
            new { slot_index = 0, item_id = "24", qualified_item_id = "(O)24", display_name = "Fixture A",
                is_empty = false, category = -75, stack = 1, quality = 0, sell_to_store_price = 20,
                protected_from_auto_sell = false, auto_sell_protection_reasons = Array.Empty<string>(), context_tags = Array.Empty<string>() },
            new { slot_index = 1, item_id = "25", qualified_item_id = "(O)25", display_name = "Fixture B",
                is_empty = false, category = -75, stack = 1, quality = 0, sell_to_store_price = 19,
                protected_from_auto_sell = false, auto_sell_protection_reasons = Array.Empty<string>(), context_tags = Array.Empty<string>() }
        });
        SetActiveShopTestField(snapshot, "menus", "sell_context", new
        {
            kind = "shop_sell_context", shop_id = "Blacksmith", currency = 0, read_only = false,
            safety_timer = -6, held_item_present = false, storage_shop = false, sell_percentage = 1,
            custom_on_sell_present = false, can_buyback = true, categories_to_sell = new[] { -75 },
            tag_groups_to_sell = Array.Empty<string[]>(), buy_back_item_count = 0, buy_back_resell_tomorrow_count = 0
        });
        SetActiveShopTestField(snapshot, "locations", "shops", new { shops = Array.Empty<object>() });
        SetActiveShopTestField(snapshot, "locations", "route_graph", new { nodes = Array.Empty<object>(), edges = Array.Empty<object>() });
        foreach (var section in snapshot.State.Keys.ToArray())
        {
            var fields = JsonNode.Parse(snapshot.State[section].GetRawText())!.AsObject();
            foreach (var field in fields.Where(row => row.Value is JsonObject envelope && envelope.ContainsKey("status")))
            {
                var envelope = field.Value!.AsObject();
                envelope["source"] = new JsonObject { ["kind"] = "game_object", ["path"] = "self_test.direct_concrete." + section + "." + field.Key };
                envelope["adapter"] = "vanilla_1_6";
                envelope["read_at_tick"] = snapshot.GameTick;
            }
            snapshot.State[section] = JsonSerializer.SerializeToElement(fields);
        }
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var sourcePath = Path.Combine(root, "source.json");
        var dependencyPath = Path.Combine(root, "dependencies.json");
        Write(sourcePath, snapshot);
        File.WriteAllText(dependencyPath, """
            {"schema_version":"goal_method_dependency_expansions.v1","directions":[{
              "direction_id":"earn_money","status":"in_progress","completion_node_id":"complete",
              "blockers":["fresh_save_schedule_pending"],
              "nodes":[{"id":"complete","kind":"terminal_fact"},
                {"id":"milestone","kind":"state_fact"},
                {"id":"sale","kind":"policy_option_transition","option_id":"economy.sell_items",
                 "candidate_kinds":["sell_shop_item"],"required_field_paths":["menus.sell_context","player.inventory"]}],
              "edges":[{"from":"sale","to":"milestone","kind":"advances_immediately"},
                {"from":"milestone","to":"complete","kind":"terminates_at"}]}]}
            """);
        var candidatePath = Path.Combine(root, "candidate-catalog.json");
        var catalog = MoneyRevenueCandidateDenominatorBuilder.Build(sourcePath, dependencyPath);
        Require(catalog.CurrentComparisonDenominatorReady && catalog.ComparisonCandidateCount == 2,
            "Direct-concrete fixture did not pass the original native economic evaluator.");
        Write(candidatePath, catalog);
        var alternativePath = Path.Combine(root, "alternative-catalog.json");
        var alternatives = MoneyRevenueAlternativeDenominatorBuilder.Build(candidatePath);
        Require(alternatives.CurrentAlternativeDenominatorAuthoritative && alternatives.AlternativeCount == 2 &&
                alternatives.Alternatives.All(row => !row.RequiresFreshTerminalCandidateRebind),
            "Direct-concrete fixture did not rebuild two authoritative direct alternatives.");
        Write(alternativePath, alternatives);
        return alternativePath;
    }
}
