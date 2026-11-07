using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyLocalDependencyViewBuilder
{
    internal static JsonObject ProjectVerifiedCorpus(JsonObject original, MoneyThresholdTeacherCorpus corpus, string corpusPath,
        string corpusHash, string dependenciesPath, string dependenciesHash)
    {
        Require(Text(original, "schema_version") == "goal_method_dependency_expansions.v1" &&
            Text(original, "goal_id") == GoalMethodTeacherCoverageGoalIds.Authoritative &&
            !original.Any(property => property.Key.EndsWith("_local_method_view", StringComparison.Ordinal)),
            "Money view requires the original unscoped graph, not stacked local views.");
        Require(corpus.SchemaVersion == MoneyThresholdTeacherCorpusVersions.ShopReplayCorpus &&
            corpus.DirectionId == DirectionId && corpus.MethodId == "grandpa.direct.earn_money" &&
            corpus.Rows.Length > 0 && !corpus.FormalProductTrainingAuthorized &&
            corpus.Rows.All(row => row.NativeTerminalOutcomeVerified && row.NativeOutcomeKind == MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode &&
                !row.AcquisitionRolloutRequired && row.CrossedThresholds.All(value => value.NativeTerminalOutcomeVerified)),
            "Money local method requires verified v5 immediate-shop native witnesses, not acquisition or training claims.");
        foreach (var target in GrandpaEvaluationGoalEvaluator.MoneyThresholds)
            Require(corpus.Rows.Any(row => row.CrossedThresholds.Any(value => value.Target == target.Target &&
                value.CriterionId == target.CriterionId && value.Points == target.Points &&
                value.TeacherComparison?.ShopReplayJoin is not null)),
                "Money local method is missing a criterion-local native/comparison witness.");
        var root = original.DeepClone().AsObject();
        var directions = root["directions"]!.AsArray();
        var ids = directions.Select(value => Text(value!, "direction_id")).ToArray();
        Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length && ids.Count(id => id == DirectionId) == 1 &&
            ids.All(id => GrandpaDirectionCatalog.Entries.Any(entry => entry.DirectionId == id)),
            "Money view requires unique known directions and exactly one original Money direction.");
        var direction = directions.Single(value => Text(value!, "direction_id") == DirectionId)!.AsObject();
        ValidateDirection(direction);
        root[MetadataKey] = new JsonObject
        {
            ["schema_version"] = "money_local_method_view.v1", ["game_version"] = "1.6.15",
            ["corpus_path"] = corpusPath, ["corpus_sha256"] = corpusHash,
            ["original_dependencies_path"] = dependenciesPath, ["original_dependencies_sha256"] = dependenciesHash,
            ["scope"] = "criterion_local_declared_held_stock_immediate_native_shop_sale",
            ["global_direction_contract"] = direction.DeepClone(), ["global_dependency_blockers"] = direction["blockers"]!.DeepClone(),
            ["prior_inventory_acquisition_verified"] = false, ["global_production_recurrence_verified"] = false,
            ["global_seed_robust_lower_bound_verified"] = false, ["global_shared_resource_schedule_verified"] = false,
            ["full_fresh_save_root_verified"] = false, ["formal_product_training_authorized"] = false
        };
        var fields = direction["nodes"]!.AsArray().SelectMany(node => node!["required_field_paths"]!.AsArray().Select(field => field!.GetValue<string>()))
            .Concat(new OptionRegistry().GetRequired("economy.sell_items").RequiredStateFactors)
            .Concat(new[] { "time.total_days", "player.total_money_earned" }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        direction["nodes"]!.AsArray().Add(new JsonObject
        {
            ["id"] = CertificateNodeId, ["kind"] = "state_gate", ["required_field_paths"] = JsonSerializer.SerializeToNode(fields),
            ["rule"] = new JsonObject
            {
                ["source_schema"] = corpus.SchemaVersion, ["source_path"] = corpusPath, ["source_sha256"] = corpusHash,
                ["source_frames"] = JsonSerializer.SerializeToNode(corpus.Rows, JsonDefaults.Options),
                ["prerequisite_scope"] = "exact declared held inventory, native quote, same-source ledger and complete comparison; not earlier acquisition or a new-save schedule",
                ["fresh_rebind_required_after_each_native_receipt"] = true,
                ["counterfactual_references_are_runtime_candidates"] = false, ["uncompared_candidates_are_negative_labels"] = false,
                ["policy_training_authorized"] = false
            }
        });
        direction["edges"]!.AsArray().Add(new JsonObject
        {
            ["from"] = CertificateNodeId, ["to"] = Prefix + "total_money_earned_one_million", ["kind"] = "binds_selected_current_source"
        });
        direction["status"] = "complete";
        direction["blockers"] = new JsonArray();
        return root;
    }

    private static void ValidateDirection(JsonObject direction)
    {
        Require(Text(direction, "status") == "in_progress" && direction["blockers"]?.AsArray().Count > 0 &&
            Text(direction, "completion_node_id") == Prefix + "total_money_earned_one_million", "Original Money completion contract drifted.");
        var nodes = direction["nodes"]!.AsArray().ToDictionary(value => Text(value!, "id"), value => value!.AsObject(), StringComparer.Ordinal);
        foreach (var (suffix, kind) in new[] { ("total_money_earned_one_million", "terminal_fact"), ("settled_milestone_ladder", "state_fact"),
            ("reserve_safe_positive_inventory", "state_gate"), ("acquire_fishable_inventory", "policy_option_transition"),
            ("acquire_spawned_foraging_inventory", "policy_option_transition"), ("acquire_animal_product_inventory", "policy_option_transition"),
            ("maintain_crop_production", "deterministic_transition"), ("collect_ready_machine_output", "policy_option_transition"),
            ("establish_positive_machine_capacity", "policy_option_transition"), ("load_positive_machine_input", "policy_option_transition"),
            ("sell_at_shop_immediate_settlement", "policy_option_transition"), ("deposit_shipping_pending_settlement", "policy_option_transition"),
            ("native_shipping_day_settlement", "deterministic_transition"), ("revenue_portfolio_recurrence", "recurrence_rule") })
            Require(nodes.TryGetValue(Prefix + suffix, out var node) && Text(node, "kind") == kind,
                "Money view cannot remove or reassign an original acquisition/production/settlement responsibility.");
        Require(!nodes.ContainsKey(CertificateNodeId), "Money source certificate is already present.");
        var terminal = nodes[Prefix + "total_money_earned_one_million"]["rule"]!;
        var ladder = nodes[Prefix + "settled_milestone_ladder"]["rule"]!;
        var reserve = nodes[Prefix + "reserve_safe_positive_inventory"]["rule"]!;
        var loop = nodes[Prefix + "revenue_portfolio_recurrence"]["rule"]!;
        Require(Text(terminal, "path") == "player.total_money_earned" && Text(terminal, "operator") == "greater_than_or_equal" &&
            terminal["value"]!.GetValue<uint>() == 1_000_000 && terminal["grandpa_points_at_terminal"]!.GetValue<int>() == 7 &&
            ladder["scored_thresholds"]!.AsArray().Select(value => value!.GetValue<uint>()).SequenceEqual(GrandpaEvaluationGoalEvaluator.MoneyThresholds.Select(value => value.Target)) &&
            ladder["points"]!.AsArray().Select(value => value!.GetValue<int>()).SequenceEqual(GrandpaEvaluationGoalEvaluator.MoneyThresholds.Select(value => value.Points)) &&
            ladder["current_cash_is_not_cumulative_earnings"]!.GetValue<bool>() &&
            Text(reserve, "missing_reservation_or_price_evidence") == "fail_closed" &&
            Text(nodes[Prefix + "sell_at_shop_immediate_settlement"], "option_id") == "economy.sell_items" &&
            Text(loop["continue_while"]!, "path") == "player.total_money_earned" && Text(loop["continue_while"]!, "operator") == "less_than" &&
            loop["continue_while"]!["value"]!.GetValue<uint>() == 1_000_000 && Text(loop, "admission_requirement") ==
                "at least one fresh-save route family has a seed-robust time lower-bound proof before this expansion may become complete",
            "Money view cannot weaken cumulative thresholds, reservations or the original global recurrence contract.");
    }
}
