using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageLocalDependencyViewBuilder
{
    private static JsonObject Project(JsonObject original, MarriageTeacherCorpus corpus, string corpusPath,
        string corpusHash, string dependenciesPath, string dependenciesHash, JsonArray routes)
    {
        Require(original["schema_version"]?.GetValue<string>() == "goal_method_dependency_expansions.v1" &&
            original["goal_id"]?.GetValue<string>() == GoalMethodTeacherCoverageGoalIds.Authoritative &&
            !original.Any(property => property.Key.EndsWith("_local_method_view", StringComparison.Ordinal)),
            "Marriage view requires the original unscoped authoritative graph, not stacked local views.");
        Require(corpus.Runs.Length > 0 && corpus.CriterionCoverage.All(row =>
                row.TeacherComparisonPartitions.Intersect(row.NativeOutcomePartitions).Any()) &&
            !corpus.PriorHouseAcquisitionVerified && !corpus.PriorRelationshipAcquisitionVerified && !corpus.FullRootVerified &&
            !corpus.FormalProductTrainingAuthorized && !corpus.MechanicalControlsArePreferenceLabels,
            "Marriage local method requires genuine linked preparation/native witnesses, not prior acquisition or a full root claim.");
        var ids = original["directions"]!.AsArray().Select(row => Text(row!, "direction_id")).ToArray();
        Require(ids.Count(id => id == DirectionId) == 1 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length &&
            ids.All(id => GrandpaDirectionCatalog.Entries.Any(entry => entry.DirectionId == id)),
            "Marriage view requires unique known dependency directions and its original target.");
        var root = original.DeepClone().AsObject();
        var direction = root["directions"]!.AsArray().Single(row => Text(row!, "direction_id") == DirectionId)!.AsObject();
        var nodes = ValidateDirection(direction);
        ValidateBindings(corpus, routes);
        var recurrence = nodes[Prefix + "joint_recurrence"]["rule"]!.AsObject();
        root[MetadataKey] = new JsonObject
        {
            ["schema_version"] = "marriage_local_method_view.v1", ["game_version"] = corpus.GameVersion,
            ["corpus_path"] = corpusPath, ["corpus_sha256"] = corpusHash,
            ["original_dependencies_path"] = dependenciesPath, ["original_dependencies_sha256"] = dependenciesHash,
            ["scope"] = "criterion_local_conditional_current_partner_and_native_house_joint_sources",
            ["global_direction_contract"] = direction.DeepClone(), ["global_dependency_blockers"] = direction["blockers"]!.DeepClone(),
            ["global_recurrence_contract"] = recurrence.DeepClone(), ["prior_house_acquisition_verified"] = false,
            ["prior_relationship_acquisition_verified"] = false, ["prior_item_acquisition_verified"] = false,
            ["full_root_verified"] = false, ["formal_product_training_authorized"] = false
        };
        var registry = new OptionRegistry();
        var fields = direction["nodes"]!.AsArray().SelectMany(node => node!["required_field_paths"]!.AsArray().Select(field => field!.GetValue<string>()))
            .Concat(new[] { "player.movement_timing_context", "locations.social_route_date_evidence", "time.time", "time.total_days" })
            .Concat(new[] { "social.gift_npc", "social.advance_partnership", "recovery.stabilize_day", "story.advance_event" }
                .SelectMany(option => registry.GetRequired(option).RequiredStateFactors))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        direction["nodes"]!.AsArray().Add(new JsonObject
        {
            ["id"] = CertificateNodeId, ["kind"] = "state_gate", ["required_field_paths"] = JsonSerializer.SerializeToNode(fields),
            ["rule"] = new JsonObject
            {
                ["source_schema"] = corpus.SchemaVersion, ["source_path"] = corpusPath, ["source_sha256"] = corpusHash,
                ["verified_routes"] = routes.DeepClone(), ["source_frames"] = JsonSerializer.SerializeToNode(corpus.Runs, JsonDefaults.Options),
                ["prerequisite_scope"] = "exact original gift dependencies and native proposal/control chain with declared initial house, relationship and items; not prior acquisition",
                ["fresh_rebind_required_after_each_native_receipt"] = true,
                ["mechanical_controls_are_preference_labels"] = false, ["uncompared_candidates_are_negative_labels"] = false,
                ["gift_dependencies_are_direct_marriage_routes"] = false
            }
        });
        direction["edges"]!.AsArray().Add(new JsonObject
        {
            ["from"] = CertificateNodeId, ["to"] = Prefix + "joint_recurrence", ["kind"] = "binds_selected_current_joint_source"
        });
        recurrence["acceptance_scope"] = "criterion-local conditional current-partner gift to native wedding/house outcome; full new-save acquisition and cross-root commitments remain global work";
        recurrence["teacher_choice"] = new CurrentMarriageTeacherPreference().ComparisonPolicyId;
        recurrence["admission_requirement"] = "complete replayed joint source, all independent preparation pairs, native calendar and full original lawful queues; fresh rebind after every receipt";
        recurrence["progress_measure"] = "observed joint criterion at native wedding setup plus completed event/return; gift points alone and declared prerequisites are not earned marriage/house acquisition";
        direction["status"] = "complete";
        direction["blockers"] = new JsonArray();
        return root;
    }

    private static Dictionary<string, JsonObject> ValidateDirection(JsonObject direction)
    {
        Require(direction["status"]?.GetValue<string>() == "in_progress" && direction["blockers"]?.AsArray().Count > 0 &&
            Text(direction, "completion_node_id") == Prefix + "joint_terminal",
            "Original Marriage completion contract drifted.");
        var nodes = direction["nodes"]!.AsArray().ToDictionary(node => Text(node!, "id"), node => node!.AsObject(), StringComparer.Ordinal);
        var required = new[] { "joint_terminal", "house_state", "purchase_house_upgrade", "raise_partner_friendship", "talk_partner",
            "gift_partner", "acquire_partnership_item", "advance_partnership", "native_day_settlement", "joint_recurrence" };
        Require(required.All(id => nodes.ContainsKey(Prefix + id)) && !nodes.ContainsKey(CertificateNodeId),
            "Original Marriage house/partner/item/settlement/recurrence nodes are incomplete or already scoped.");
        foreach (var (id, kind) in new[] { ("joint_terminal", "terminal_fact"), ("house_state", "state_fact"),
            ("raise_partner_friendship", "recurrence_rule"), ("acquire_partnership_item", "recurrence_rule"), ("joint_recurrence", "recurrence_rule") })
            Require(Text(nodes[Prefix + id], "kind") == kind, "Marriage view cannot replace the original state or recurrence roles.");
        var terminal = nodes[Prefix + "joint_terminal"]["rule"]!["all"]!.AsArray();
        Require(terminal.Count == 2 && Text(terminal[0]!, "path") == "player.married_or_roommate" && Text(terminal[0]!, "operator") == "equals" &&
            terminal[0]!["value"]!.GetValue<bool>() && Text(terminal[1]!, "path") == "player.farmhouse_upgrade_level" &&
            Text(terminal[1]!, "operator") == "greater_than_or_equal" && terminal[1]!["value"]!.GetValue<int>() == 2 &&
            nodes[Prefix + "house_state"]["rule"]!["target_level"]!.GetValue<int>() == 2,
            "Marriage view cannot weaken the original native joint rule or farmhouse target.");
        foreach (var (id, kind, option) in new[]
        {
            ("purchase_house_upgrade", "policy_option_transition", "housing.advance_farmhouse"),
            ("advance_partnership", "policy_option_transition", "social.advance_partnership"),
            ("talk_partner", "policy_option_transition", "social.talk_npc"),
            ("gift_partner", "policy_option_transition", "social.gift_npc"),
            ("native_day_settlement", "deterministic_transition", "recovery.stabilize_day")
        })
            Require(Text(nodes[Prefix + id], "kind") == kind && Text(nodes[Prefix + id], "option_id") == option,
                "Marriage view cannot reassign direct, dependency or deterministic ownership.");
        var settlement = nodes[Prefix + "native_day_settlement"]["rule"]!;
        Require(Text(settlement, "execution_owner") == "deterministic_day_compiler_not_policy_label" &&
            settlement["refresh_snapshot_after_transition"]!.GetValue<bool>() &&
            Text(nodes[Prefix + "joint_recurrence"]["rule"]!, "continue_while") == "either terminal branch remains false",
            "Marriage view cannot weaken the original native settlement or joint recurrence contract.");
        return nodes;
    }
}
