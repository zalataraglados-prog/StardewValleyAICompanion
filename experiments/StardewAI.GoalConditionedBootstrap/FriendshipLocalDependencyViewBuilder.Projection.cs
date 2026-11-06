using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FriendshipLocalDependencyViewBuilder
{
    private static JsonObject Project(JsonObject original, FriendshipTeacherCorpus corpus, string corpusPath,
        string corpusHash, string dependenciesPath, string dependenciesHash, JsonArray routes)
    {
        Require(original["schema_version"]?.GetValue<string>() == "goal_method_dependency_expansions.v1" &&
            original["goal_id"]?.GetValue<string>() == GoalMethodTeacherCoverageGoalIds.Authoritative &&
            !original.Any(property => property.Key.EndsWith("_local_method_view", StringComparison.Ordinal)),
            "Friendship view requires the original unscoped authoritative graph, not stacked local views.");
        Require(corpus.CriterionCoverage.All(row => row.TeacherComparisonPartitions.Intersect(row.NativeOutcomePartitions).Any()) &&
            !corpus.FullRootVerified && !corpus.PriorFriendshipAcquisitionVerified && !corpus.PriorStoryProgressVerified &&
            !corpus.FormalProductTrainingAuthorized,
            "Friendship local method requires genuine independent/native witnesses for both thresholds, not prior acquisition.");
        var directions = original["directions"]!.AsArray();
        var ids = directions.Select(row => row!["direction_id"]!.GetValue<string>()).ToArray();
        Require(ids.Count(id => id == DirectionId) == 1 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length &&
            ids.All(id => GrandpaDirectionCatalog.Entries.Any(entry => entry.DirectionId == id)),
            "Friendship view requires unique known dependency directions and its original target.");
        var root = original.DeepClone().AsObject();
        var direction = root["directions"]!.AsArray().Single(row => row!["direction_id"]!.GetValue<string>() == DirectionId)!.AsObject();
        var byId = ValidateDirection(direction);
        var recurrence = byId[Prefix + "portfolio_recurrence"]["rule"]!.AsObject();
        root[MetadataKey] = new JsonObject
        {
            ["schema_version"] = "friendship_local_method_view.v1", ["game_version"] = corpus.GameVersion,
            ["corpus_path"] = corpusPath, ["corpus_sha256"] = corpusHash,
            ["original_dependencies_path"] = dependenciesPath, ["original_dependencies_sha256"] = dependenciesHash,
            ["scope"] = "criterion_local_conditional_current_social_contact_sources",
            ["global_direction_contract"] = direction.DeepClone(),
            ["global_dependency_blockers"] = direction["blockers"]!.DeepClone(),
            ["global_recurrence_contract"] = recurrence.DeepClone(),
            ["prior_friendship_acquisition_verified"] = false, ["prior_story_progress_verified"] = false,
            ["full_root_verified"] = false, ["formal_product_training_authorized"] = false
        };
        Require(routes.Count == corpus.Rows.Length && routes.Select(route => route!["source_id"]!.GetValue<string>()).Order()
            .SequenceEqual(corpus.Rows.Select(row => row.SourceId).Order()) && routes.All(route =>
                GrandpaDirectionCatalog.Entries.Single(entry => entry.DirectionId == DirectionId).PermittedOptionIds
                    .Contains(route!["option_id"]!.GetValue<string>()) &&
                corpus.Rows.Any(row => row.SourceId == route!["source_id"]!.GetValue<string>() &&
                    row.SelectedCandidateId == route["selected_candidate_id"]!.GetValue<string>() &&
                    row.BeforeStateHash == route["source_state_hash"]!.GetValue<string>())),
            "Friendship source-route frame identities are incomplete.");
        var registry = new OptionRegistry();
        var requiredFields = new[] { "npcs.grandpa_friendship_progress", "player.energy", "player.inventory", "time.time",
                "time.total_days", "player.movement_timing_context", "locations.social_route_date_evidence" }
            .Concat(routes.SelectMany(route => registry.GetRequired(route!["option_id"]!.GetValue<string>()).RequiredStateFactors))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        direction["nodes"]!.AsArray().Add(new JsonObject
        {
            ["id"] = CertificateNodeId, ["kind"] = "state_gate",
            ["required_field_paths"] = JsonSerializer.SerializeToNode(requiredFields),
            ["rule"] = new JsonObject
            {
                ["source_schema"] = corpus.SchemaVersion, ["source_path"] = corpusPath, ["source_sha256"] = corpusHash,
                ["verified_routes"] = routes.DeepClone(), ["source_frames"] = JsonSerializer.SerializeToNode(corpus.Rows, JsonDefaults.Options),
                ["prerequisite_scope"] = "exact replayed current decision frames, explicit pairs and original lawful full queue; not prior acquisition or future access",
                ["fresh_rebind_required_after_each_native_receipt"] = true,
                ["uncompared_candidates_are_negative_labels"] = false
            }
        });
        direction["edges"]!.AsArray().Add(new JsonObject
        {
            ["from"] = CertificateNodeId, ["to"] = Prefix + "portfolio_recurrence", ["kind"] = "binds_selected_current_source"
        });
        recurrence["acceptance_scope"] = "criterion-local conditional current contacts; complete new-save ten-villager access, decay, calendar and cross-root budgets remain global work";
        recurrence["teacher_choice"] = new CurrentFriendshipTeacherPreference().ComparisonPolicyId;
        recurrence["admission_requirement"] = "fresh complete lawful compilation and replayed explicit current pairs; unknown windows, unproved tails or incomparable candidates never acquire negative labels";
        recurrence["progress_measure"] = "observed native qualifying counts and 1975-point deficits, never declared setup friendship/story acquisition";
        direction["status"] = "complete";
        direction["blockers"] = new JsonArray();
        return root;
    }

    private static Dictionary<string, JsonObject> ValidateDirection(JsonObject direction)
    {
        Require(direction["status"]?.GetValue<string>() == "in_progress" && direction["blockers"]?.AsArray().Count > 0 &&
            direction["completion_node_id"]?.GetValue<string>() == Prefix + "ten_native_villagers_at_1975",
            "Original Friendship completion contract drifted.");
        var byId = direction["nodes"]!.AsArray().ToDictionary(node => node!["id"]!.GetValue<string>(), node => node!.AsObject(), StringComparer.Ordinal);
        var required = new[] { "ten_native_villagers_at_1975", "native_eligible_population", "portfolio_deficits", "current_access_gate",
            "talk_once_today", "give_positive_safe_gift", "native_day_settlement", "deadline_budget", "portfolio_recurrence" };
        Require(required.All(id => byId.ContainsKey(Prefix + id)) && !byId.ContainsKey(CertificateNodeId),
            "Original Friendship population/access/settlement/deadline/recurrence nodes are incomplete or already scoped.");
        var terminal = byId[Prefix + "ten_native_villagers_at_1975"]["rule"]!;
        var population = byId[Prefix + "native_eligible_population"]["rule"]!;
        Require(terminal["projection_status"]!.GetValue<string>() == "complete_live_native_iteration" &&
            terminal["threshold_points"]!.GetValue<int>() == 1975 && terminal["maximum_points"]!.GetValue<int>() == 999999 &&
            !terminal["romance_only"]!.GetValue<bool>() && terminal["qualifying_count"]!["operator"]!.GetValue<string>() == "greater_than_or_equal" &&
            terminal["qualifying_count"]!["value"]!.GetValue<int>() == 10 && terminal["grandpa_points_at_terminal"]!.GetValue<int>() == 2 &&
            population["population_owner"]!.GetValue<string>() == "Utility.ForEachVillager(includeEventActors:false)" &&
            population["count_owner"]!.GetValue<string>() == "Utility.getNumberOfFriendsWithinThisRange(player,1975,999999,romanceOnly:false)" &&
            population["raw_friendship_rows_are_not_score_population"]!.GetValue<bool>() &&
            byId[Prefix + "portfolio_deficits"]["rule"]!["portfolio_size"]!.GetValue<int>() == 10,
            "Friendship local view cannot weaken the original native population/range/count rule.");
        var loop = byId[Prefix + "portfolio_recurrence"]["rule"]!["continue_while"]!;
        Require(loop["path"]!.GetValue<string>() == "npcs.grandpa_friendship_progress.qualifying_count" &&
            loop["operator"]!.GetValue<string>() == "less_than" && loop["value"]!.GetValue<int>() == 10 &&
            byId[Prefix + "deadline_budget"]["rule"]!["deadline"]!.GetValue<string>() == "Year 3 initial evaluation" &&
            byId[Prefix + "deadline_budget"]["rule"]!["arithmetic_is_not_route_proof"]!.GetValue<bool>(),
            "Friendship local view cannot weaken the original recurrence/deadline contract.");
        return byId;
    }
}
