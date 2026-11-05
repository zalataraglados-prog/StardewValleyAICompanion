using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class SkillLocalDependencyViewTests
{
    private const string Prefix = "dependency.raise_skill_levels.";

    [Fact]
    public void ProjectionOnlyPreservesGlobalResponsibilitiesAndEveryOtherRoot()
    {
        var original = Graph();
        var before = original.ToJsonString();
        var direction = Skills(original);
        var originalRule = Node(direction, "skill_portfolio_recurrence")["rule"]!;
        var result = Project(original, Corpus());
        Assert.Equal(before, original.ToJsonString());
        var local = Skills(result);
        Assert.Equal("complete", local["status"]!.GetValue<string>());
        Assert.Empty(local["blockers"]!.AsArray());
        var metadata = result["skill_local_method_view"]!;
        Assert.True(JsonNode.DeepEquals(direction["blockers"], metadata["global_dependency_blockers"]));
        Assert.True(JsonNode.DeepEquals(originalRule, metadata["global_recurrence_contract"]));
        Assert.False(metadata["prior_skill_acquisition_verified"]!.GetValue<bool>());
        Assert.False(metadata["full_root_verified"]!.GetValue<bool>());
        Assert.False(metadata["formal_product_training_authorized"]!.GetValue<bool>());
        foreach (var row in original["directions"]!.AsArray().Where(row => row!["direction_id"]!.GetValue<string>() != "raise_skill_levels"))
            Assert.True(JsonNode.DeepEquals(row, result["directions"]!.AsArray().Single(other =>
                other!["direction_id"]!.GetValue<string>() == row!["direction_id"]!.GetValue<string>())));
        foreach (var node in direction["nodes"]!.AsArray().Where(node => node!["id"]!.GetValue<string>() != Prefix + "skill_portfolio_recurrence"))
            Assert.True(JsonNode.DeepEquals(node, local["nodes"]!.AsArray().Single(other => other!["id"]!.GetValue<string>() == node!["id"]!.GetValue<string>())));
        var certificate = local["nodes"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == SkillLocalDependencyViewBuilder.CertificateNodeId)!;
        Assert.Equal(2, certificate["rule"]!["source_frames"]!.AsArray().Count);
        Assert.Equal("corpus-hash", certificate["rule"]!["source_sha256"]!.GetValue<string>());
        Assert.True(certificate["rule"]!["fresh_rebind_required_after_each_native_receipt"]!.GetValue<bool>());
        var fields = certificate["required_field_paths"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert.Contains("player.skills_detail", fields);
        Assert.Contains("current_location.objects", fields);
        Assert.Contains("locations.collision_grid", fields);
        Assert.Contains("menus.active_menu", fields);
        Assert.Contains("current_skill_same_effect_xp_cost_dominance", Node(local, "skill_portfolio_recurrence")["rule"]!["teacher_choice"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("goal")]
    [InlineData("nested_view")]
    [InlineData("missing_direction")]
    [InlineData("duplicate_direction")]
    [InlineData("already_complete")]
    [InlineData("discarded_blockers")]
    [InlineData("missing_acquisition")]
    [InlineData("buffs")]
    [InlineData("luck")]
    [InlineData("cap")]
    [InlineData("level15")]
    [InlineData("level25_operator")]
    [InlineData("level25_path")]
    [InlineData("required_level")]
    public void ProjectionOnlyRejectsScopeAndNativeContractDrift(string mode)
    {
        var graph = Graph();
        var direction = Skills(graph);
        switch (mode)
        {
            case "schema": graph["schema_version"] = "other"; break;
            case "goal": graph["goal_id"] = "other"; break;
            case "nested_view": graph["skill_local_method_view"] = new JsonObject(); break;
            case "missing_direction": graph["directions"]!.AsArray().Remove(direction); break;
            case "duplicate_direction": graph["directions"]!.AsArray().Add(direction.DeepClone()); break;
            case "already_complete": direction["status"] = "complete"; break;
            case "discarded_blockers": direction["blockers"] = new JsonArray(); break;
            case "missing_acquisition": direction["nodes"]!.AsArray().Remove(Node(direction, "acquire_combat_xp")); break;
            case "buffs": Node(direction, "five_unmodified_skill_state")["rule"]!["temporary_buffs_count"] = true; break;
            case "luck": Node(direction, "five_unmodified_skill_state")["rule"]!["vanilla_luck_level"] = 1; break;
            case "cap": Node(direction, "native_xp_threshold_ladder")["rule"]!["per_skill_cap"] = 11; break;
            case "level15": Node(direction, "native_scoring_level_15")["rule"]!["value"] = 14; break;
            case "level25_operator": Node(direction, "native_scoring_level_25")["rule"]!["operator"] = "eq"; break;
            case "level25_path": Node(direction, "native_scoring_level_25")["rule"]!["path"] = "player.effective_level"; break;
            case "required_level": Node(direction, "native_scoring_level_25")["rule"]!["required_unmodified_levels"]!["combat"] = 9; break;
        }
        var error = Assert.Throws<TargetInvocationException>(() => Project(graph, Corpus()));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Theory]
    [InlineData("player_level_15")]
    [InlineData("player_level_25")]
    public void ProjectionOnlyCannotCloseBothThresholdsWithOneMissingWitness(string removed)
    {
        var corpus = Corpus();
        var error = Assert.Throws<TargetInvocationException>(() => Project(Graph(), corpus with
        {
            Rows = corpus.Rows.Where(row => row.CriterionId != removed).ToArray()
        }));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static JsonObject Project(JsonObject graph, SkillTeacherCorpus corpus) => (JsonObject)typeof(SkillLocalDependencyViewBuilder)
        .GetMethod("Project", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
            new object[] { graph, corpus, "corpus", "corpus-hash", "dependencies", "dependencies-hash",
                new JsonArray(corpus.Rows.Select(row => (JsonNode)new JsonObject
                {
                    ["source_id"] = row.SourceId, ["option_id"] = "fishing.collect_crab_pots"
                }).ToArray()) })!;

    private static JsonObject Skills(JsonObject graph) => graph["directions"]!.AsArray()
        .Single(row => row!["direction_id"]!.GetValue<string>() == "raise_skill_levels")!.AsObject();

    private static JsonObject Node(JsonObject direction, string suffix) => direction["nodes"]!.AsArray()
        .Single(node => node!["id"]!.GetValue<string>() == Prefix + suffix)!.AsObject();

    private static SkillTeacherCorpus Corpus() => new("request", "hash", "fixture", "knowledge",
        new[] { "player_level_15", "player_level_25" }.Select(id => new SkillTeacherCorpusRow(id, "row", "hash", "native", "hash",
            id, id, id, "save", "player", "split", "train", id, id, id, id, id, 1, true)).ToArray());

    private static JsonObject Graph()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "experiments", "goal-method-dependency-expansions.v1.json");
            if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        }
        throw new FileNotFoundException("Original dependency graph not found.");
    }
}
