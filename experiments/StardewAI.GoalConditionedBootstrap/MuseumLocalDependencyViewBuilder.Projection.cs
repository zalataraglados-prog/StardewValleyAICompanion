using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumLocalDependencyViewBuilder
{
    private static JsonObject Project(JsonObject original, MuseumTeacherCorpus corpus, string corpusPath,
        string corpusHash, string dependenciesPath, string dependenciesHash)
    {
        Require(original["schema_version"]?.GetValue<string>() == "goal_method_dependency_expansions.v1" &&
            original["goal_id"]?.GetValue<string>() == GoalMethodTeacherCoverageGoalIds.Authoritative &&
            !original.Any(property => property.Key.EndsWith("_local_method_view", StringComparison.Ordinal)),
            "Museum view requires the original unscoped graph, not stacked local views.");
        Require(corpus.Rows.Length > 0 && corpus.OriginalDependencyExpansionSha256 == dependenciesHash &&
            corpus.TeacherComparisonPartitions.Intersect(corpus.NativeOutcomePartitions).Any() &&
            !corpus.FullRootVerified && !corpus.FormalProductTrainingAuthorized,
            "Museum local method requires verified conditional witnesses and their exact original dependency authority.");
        var directions = original["directions"]!.AsArray();
        var ids = directions.Select(row => Text(row!, "direction_id")).ToArray();
        Require(ids.Count(id => id == DirectionId) == 1 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length &&
            ids.All(id => GrandpaDirectionCatalog.Entries.Any(entry => entry.DirectionId == id)),
            "Museum view requires unique known dependency directions and its original target.");
        var root = original.DeepClone().AsObject();
        var direction = root["directions"]!.AsArray().Single(row => Text(row!, "direction_id") == DirectionId)!.AsObject();
        var nodes = ValidateDirection(direction);
        var recurrence = nodes[Prefix + "donation_recurrence"]["rule"]!.AsObject();
        root[MetadataKey] = new JsonObject
        {
            ["schema_version"] = "museum_local_method_view.v1", ["game_version"] = "1.6.15",
            ["corpus_path"] = corpusPath, ["corpus_sha256"] = corpusHash,
            ["original_dependencies_path"] = dependenciesPath, ["original_dependencies_sha256"] = dependenciesHash,
            ["scope"] = "criterion_local_conditional_held_final_item_to_native_full95_and_achievement5",
            ["global_direction_contract"] = direction.DeepClone(), ["global_dependency_blockers"] = direction["blockers"]!.DeepClone(),
            ["global_recurrence_contract"] = recurrence.DeepClone(), ["prior_item_acquisition_verified"] = false,
            ["global_rng_and_retry_bounds_verified"] = false, ["global_shared_resource_schedule_verified"] = false,
            ["full_root_verified"] = false, ["formal_product_training_authorized"] = false
        };
        var fields = direction["nodes"]!.AsArray().SelectMany(node => node!["required_field_paths"]!.AsArray().Select(field => field!.GetValue<string>()))
            .Concat(new OptionRegistry().GetRequired("museum.donate_items").RequiredStateFactors)
            .Concat(new[] { "time.time", "time.total_days" }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var routes = corpus.Rows.Select(row => new
        {
            source_id = row.SourceId, option_id = "museum.donate_items", selected_candidate_id = row.Evidence.PreferredCandidateId,
            source_state_hash = row.Evidence.Action.BeforeStateHash, run_id = row.Evidence.RunId, queue_id = row.Evidence.Action.QueueId,
            queue_item_id = row.Evidence.Action.QueueItemId, request_nonce = row.Evidence.Action.RequestNonce, native_join_sha256 = row.JoinSha256,
            qualified_item_id = row.Evidence.QualifiedItemId, inventory_slot_index = row.Evidence.InventorySlotIndex,
            decision_total_day = row.Evidence.DecisionTotalDay, stack_before = row.Evidence.StackBefore, stack_after = row.Evidence.StackAfter
        }).ToArray();
        direction["nodes"]!.AsArray().Add(new JsonObject
        {
            ["id"] = CertificateNodeId, ["kind"] = "state_gate", ["required_field_paths"] = JsonSerializer.SerializeToNode(fields),
            ["rule"] = new JsonObject
            {
                ["source_schema"] = corpus.SchemaVersion, ["source_path"] = corpusPath, ["source_sha256"] = corpusHash,
                ["verified_routes"] = JsonSerializer.SerializeToNode(routes, JsonDefaults.Options),
                ["source_frames"] = JsonSerializer.SerializeToNode(corpus.Rows, JsonDefaults.Options),
                ["prerequisite_scope"] = "exact verified 94-donated frame and owned reserve-safe last item; not prior acquisition, future RNG or a new-save schedule",
                ["comparison_policy_id"] = MuseumTerminalTeacherComparisonBuilder.PolicyId,
                ["comparison_horizon_ticks"] = MuseumTerminalTeacherComparisonBuilder.HorizonTicks,
                ["fresh_rebind_required_after_each_native_receipt"] = true,
                ["counterfactual_references_are_runtime_candidates"] = false,
                ["uncompared_candidates_are_negative_labels"] = false, ["policy_training_authorized"] = false
            }
        });
        direction["edges"]!.AsArray().Add(new JsonObject
        {
            ["from"] = CertificateNodeId, ["to"] = Prefix + "donation_recurrence", ["kind"] = "binds_selected_current_source"
        });
        recurrence["acceptance_scope"] = "criterion-local conditional held final donation; full95 new-save acquisition, RNG retries, reservations and global Year3 budgets remain global work";
        recurrence["teacher_choice"] = MuseumTerminalTeacherComparisonBuilder.PolicyId;
        recurrence["admission_requirement"] = "complete rebuilt full95 corpus, exact current independent comparison, native compilation and lawful source; fresh rebind after each receipt";
        recurrence["progress_measure"] = "native full95 and achievement5 transition, never declared initial museum/item acquisition";
        direction["status"] = "complete";
        direction["blockers"] = new JsonArray();
        return root;
    }

    private static Dictionary<string, JsonObject> ValidateDirection(JsonObject direction)
    {
        Require(Text(direction, "status") == "in_progress" && direction["blockers"]?.AsArray().Count > 0 &&
            Text(direction, "completion_node_id") == Prefix + "achievement_received", "Original Museum completion contract drifted.");
        var nodes = direction["nodes"]!.AsArray().ToDictionary(node => Text(node!, "id"), node => node!.AsObject(), StringComparer.Ordinal);
        foreach (var (id, kind) in new[] { ("achievement_received", "terminal_fact"), ("authoritative_requirement_set", "state_fact"),
            ("reserve_missing_item", "state_gate"), ("acquire_missing_item", "recurrence_rule"),
            ("donate_missing_item", "policy_option_transition"), ("donation_recurrence", "recurrence_rule") })
            Require(nodes.TryGetValue(Prefix + id, out var node) && Text(node, "kind") == kind,
                "Museum view cannot remove or reassign original acquisition/reservation/donation/recurrence roles.");
        Require(!nodes.ContainsKey(CertificateNodeId), "Museum source certificate is already present.");
        var terminal = nodes[Prefix + "achievement_received"]["rule"]!["all"]!.AsArray();
        var inventory = nodes[Prefix + "authoritative_requirement_set"]["rule"]!;
        var reserve = nodes[Prefix + "reserve_missing_item"]["rule"]!;
        var loop = nodes[Prefix + "donation_recurrence"]["rule"]!["continue_while"]!;
        Require(terminal.Count == 2 && Text(terminal[0]!, "path") == "world_progress.achievements" && Text(terminal[0]!, "operator") == "contains" &&
            Text(terminal[0]!, "value") == "5" && Text(terminal[1]!, "path") == "world_progress.museum.collection_complete" &&
            Text(terminal[1]!, "operator") == "equals" && terminal[1]!["value"]!.GetValue<bool>() &&
            Text(inventory, "requirement_set_id") == "museum_collection" && inventory["required_group_count"]!.GetValue<int>() == 95 &&
            Text(inventory, "selection") == "all_required" && Text(reserve, "unknown_reservation_evidence") == "fail_closed" &&
            Text(nodes[Prefix + "donate_missing_item"], "option_id") == "museum.donate_items" &&
            Text(loop, "path") == "world_progress.museum.collection_complete" && Text(loop, "operator") == "equals" && !loop["value"]!.GetValue<bool>(),
            "Museum view cannot weaken the original full95/achievement5, reservation or recurrence contract.");
        return nodes;
    }
}
