using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class SkillLocalDependencyViewBuilder
{
    public const string DirectionId = "raise_skill_levels";
    public const string CertificateNodeId = "dependency.raise_skill_levels.current_source_certificate";
    private const string MetadataKey = "skill_local_method_view";
    private const string Prefix = "dependency.raise_skill_levels.";

    public static JsonObject Build(string corpusPath, string dependenciesPath) =>
        Rebuild(SkillTeacherCorpusBuilder.Verify(RequiredFile(corpusPath)), corpusPath, dependenciesPath);

    public static JsonObject Verify(string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Skills local view metadata is missing.");
        var rebuilt = Build(RequiredString(metadata, "corpus_path"), RequiredString(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Skills local dependency view differs from its original sources.");
        return rebuilt;
    }

    internal static void VerifyForCoverage(SkillTeacherCorpus verifiedCorpus, string corpusPath, string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Executable Skills method lacks its local source certificate.");
        Require(Path.GetFullPath(RequiredString(metadata, "corpus_path")).Equals(Path.GetFullPath(corpusPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Skills method certificate belongs to another corpus.");
        // Coverage already replayed this corpus; do not repeat its native verification.
        var rebuilt = Rebuild(verifiedCorpus, corpusPath, RequiredString(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Executable Skills method is not the independently rebuilt local view.");
    }

    private static JsonObject Rebuild(SkillTeacherCorpus corpus, string corpusPath, string dependenciesPath)
    {
        corpusPath = RequiredFile(corpusPath);
        dependenciesPath = RequiredFile(dependenciesPath);
        var routes = corpus.Rows.Select(source =>
        {
            Require(CurrentTeacherFrontierSupport.HashFile(source.TeacherRowPath) == source.TeacherRowSha256,
                "Skills source row changed after replay.");
            var row = ReadTyped<PolicyDecisionTrajectoryEnvelope>(source.TeacherRowPath, PolicyTrajectoryVersionPins.TrajectorySchema);
            Require(GrandpaDirectionCatalog.Entries.Single(entry => entry.DirectionId == DirectionId)
                .PermittedOptionIds.Contains(row.Selection.OptionId), "Skills source route is not in the original direction catalog.");
            return new { source_id = source.SourceId, option_id = row.Selection.OptionId };
        }).ToArray();
        return Project(ReadObject(dependenciesPath), corpus, corpusPath, CurrentTeacherFrontierSupport.HashFile(corpusPath),
            dependenciesPath, CurrentTeacherFrontierSupport.HashFile(dependenciesPath), JsonSerializer.SerializeToNode(routes)!.AsArray());
    }

    private static JsonObject Project(JsonObject original, SkillTeacherCorpus corpus, string corpusPath,
        string corpusHash, string dependenciesPath, string dependenciesHash, JsonArray routes)
    {
        Require(original["schema_version"]?.GetValue<string>() == "goal_method_dependency_expansions.v1" &&
            original["goal_id"]?.GetValue<string>() == GoalMethodTeacherCoverageGoalIds.Authoritative && original[MetadataKey] is null,
            "Skills view requires an original authoritative dependency graph, not another local view.");
        Require(corpus.CriterionCoverage.All(row => row.TeacherComparisonPartitions.Intersect(row.NativeOutcomePartitions).Any()) &&
            !corpus.FullRootVerified && !corpus.PriorSkillAcquisitionVerified && !corpus.FormalProductTrainingAuthorized,
            "Skills local method requires genuine independent/native witnesses for both thresholds, not complete prior acquisition.");
        var root = original.DeepClone().AsObject();
        var matching = root["directions"]!.AsArray().Where(value => value?["direction_id"]?.GetValue<string>() == DirectionId).ToArray();
        Require(matching.Length == 1, "Skills dependency direction is missing or duplicated.");
        var direction = matching[0]!.AsObject();
        Require(direction["status"]?.GetValue<string>() == "in_progress" && direction["blockers"]?.AsArray().Count > 0 &&
            direction["completion_node_id"]?.GetValue<string>() == Prefix + "native_scoring_level_25",
            "Original Skills completion contract drifted.");
        var nodes = direction["nodes"]!.AsArray();
        var byId = nodes.ToDictionary(node => node!["id"]!.GetValue<string>(), node => node!.AsObject(), StringComparer.Ordinal);
        var required = new[] { "five_unmodified_skill_state", "native_xp_threshold_ladder", "native_scoring_level_15",
            "native_scoring_level_25", "acquire_farming_xp_from_animals", "maintain_crops_for_farming_xp", "acquire_fishing_xp",
            "acquire_foraging_xp", "acquire_foraging_xp_from_wild_tree", "acquire_mining_xp", "read_exact_xp_book",
            "acquire_combat_xp", "skill_portfolio_recurrence" };
        Require(required.All(id => byId.ContainsKey(Prefix + id)) && !byId.ContainsKey(CertificateNodeId),
            "Original Skills acquisition/recurrence nodes are incomplete or already scoped.");
        var skillIds = new[] { "farming", "fishing", "foraging", "mining", "combat" };
        var state = byId[Prefix + "five_unmodified_skill_state"]["rule"]!.AsObject();
        var terminal = byId[Prefix + "native_scoring_level_25"]["rule"]!.AsObject();
        var ladder = byId[Prefix + "native_xp_threshold_ladder"]["rule"]!.AsObject();
        Require(state["included_skill_ids"]!.AsArray().Select(value => value!.GetValue<string>()).Order()
                .SequenceEqual(skillIds.Order()) && state["vanilla_luck_level"]!.GetValue<int>() == 0 &&
            state["excluded_skill_ids"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(new[] { "luck" }) &&
            state["level_source"]!.GetValue<string>() == "player.skills_detail.skills[].unmodified_level" &&
            !state["temporary_buffs_count"]!.GetValue<bool>() && !state["profession_or_mastery_count"]!.GetValue<bool>() &&
            ladder["per_skill_cap"]!.GetValue<int>() == 10 && ladder["next_threshold_source"]!.GetValue<string>() == "Farmer.getBaseExperienceForLevel" &&
            terminal["required_unmodified_levels"]!.AsObject().Count == 5 &&
            skillIds.All(id => terminal["required_unmodified_levels"]![id]!.GetValue<int>() == 10),
            "Skills local view cannot weaken the original five-skill native scoring contract.");
        foreach (var level in new[] { 15, 25 })
        {
            var rule = byId[Prefix + "native_scoring_level_" + level]["rule"]!;
            Require(rule["path"]!.GetValue<string>() == "player.level" && rule["operator"]!.GetValue<string>() == "greater_than_or_equal" &&
                rule["value"]!.GetValue<int>() == level && rule["equivalent_five_skill_sum"]!.GetValue<int>() == level * 2,
                "Skills scoring threshold contract drifted.");
        }
        var recurrence = byId[Prefix + "skill_portfolio_recurrence"]["rule"]!.AsObject();
        var metadata = new JsonObject
        {
            ["schema_version"] = "skill_local_method_view.v1", ["game_version"] = NativeSkillExperienceRules.GameVersion,
            ["corpus_path"] = corpusPath, ["corpus_sha256"] = corpusHash,
            ["original_dependencies_path"] = dependenciesPath, ["original_dependencies_sha256"] = dependenciesHash,
            ["scope"] = "criterion_local_conditional_current_positive_xp_sources",
            ["global_dependency_blockers"] = direction["blockers"]!.DeepClone(),
            ["global_recurrence_contract"] = recurrence.DeepClone(),
            ["prior_skill_acquisition_verified"] = false, ["full_root_verified"] = false,
            ["formal_product_training_authorized"] = false
        };
        root[MetadataKey] = metadata;
        var registry = new OptionRegistry();
        Require(routes.Count == corpus.Rows.Length && routes.Select(route => route!["source_id"]!.GetValue<string>()).Order()
            .SequenceEqual(corpus.Rows.Select(row => row.SourceId).Order()), "Skills source-route identities are incomplete.");
        var requiredFields = new[] { "player.skills_detail", "player.energy", "player.inventory", "time.time" }
            .Concat(routes.SelectMany(route => registry.GetRequired(route!["option_id"]!.GetValue<string>()).RequiredStateFactors))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        nodes.Add(new JsonObject
        {
            ["id"] = CertificateNodeId, ["kind"] = "state_gate",
            ["required_field_paths"] = JsonSerializer.SerializeToNode(requiredFields),
            ["rule"] = new JsonObject
            {
                ["source_schema"] = "skill_teacher_corpus.v1", ["source_path"] = corpusPath, ["source_sha256"] = corpusHash,
                ["verified_routes"] = routes.DeepClone(), ["source_frames"] = JsonSerializer.SerializeToNode(corpus.Rows, JsonDefaults.Options),
                ["prerequisite_scope"] = "exact replayed decision frames and original lawful queue, not prior acquisition or future availability",
                ["fresh_rebind_required_after_each_native_receipt"] = true
            }
        });
        direction["edges"]!.AsArray().Add(new JsonObject
        {
            ["from"] = CertificateNodeId, ["to"] = Prefix + "skill_portfolio_recurrence", ["kind"] = "binds_selected_current_source"
        });
        recurrence["acceptance_scope"] = "criterion-local conditional method; full new-save five-skill acquisition and cross-root budgets remain global integration work";
        recurrence["teacher_choice"] = new CurrentSkillTeacherPreference().SelectionPolicyId;
        recurrence["admission_requirement"] = "fresh original lawful positive-XP compilation and independently replayed current source; no admitted route or ambiguous comparison remains blocked";
        recurrence["progress_measure"] = "observed native base XP and scoring-level deficits, never buffs, Mastery or declared setup acquisition";
        direction["status"] = "complete";
        direction["blockers"] = new JsonArray();
        return root;
    }

    private static JsonObject ReadObject(string path) => JsonNode.Parse(
        CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(path)))!.AsObject();

    private static string RequiredString(JsonObject value, string name) => value[name]?.GetValue<string>() ??
        throw new InvalidDataException("Skills local view is missing " + name + ".");

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
