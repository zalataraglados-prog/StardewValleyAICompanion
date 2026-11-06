using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class FriendshipTeacherCorpusTests
{
    private const string FriendshipPrefix = "dependency.raise_friendships.";

    [Fact]
    public void FriendshipLocalViewPreservesAllGlobalRulesAndOtherRootsWithoutMutatingTheOriginal()
    {
        var original = OriginalFriendshipGraph();
        original["unmodeled_extension"] = new JsonObject { ["retained"] = true };
        var before = original.ToJsonString();
        var source = FriendshipDirection(original);
        var corpus = LocalCorpus();
        var result = ProjectFriendshipView(original, corpus);
        Assert.Equal(before, original.ToJsonString());
        var local = FriendshipDirection(result);
        Assert.Equal("complete", local["status"]!.GetValue<string>());
        Assert.Empty(local["blockers"]!.AsArray());
        var metadata = result["friendship_local_method_view"]!;
        Assert.True(JsonNode.DeepEquals(source, metadata["global_direction_contract"]));
        Assert.True(JsonNode.DeepEquals(source["blockers"], metadata["global_dependency_blockers"]));
        Assert.True(JsonNode.DeepEquals(FriendshipNode(source, "portfolio_recurrence")["rule"], metadata["global_recurrence_contract"]));
        foreach (var name in new[] { "prior_friendship_acquisition_verified", "prior_story_progress_verified", "full_root_verified", "formal_product_training_authorized" })
            Assert.False(metadata[name]!.GetValue<bool>());
        foreach (var direction in original["directions"]!.AsArray().Where(row => row!["direction_id"]!.GetValue<string>() != "raise_friendships"))
            Assert.True(JsonNode.DeepEquals(direction, result["directions"]!.AsArray().Single(other =>
                other!["direction_id"]!.GetValue<string>() == direction!["direction_id"]!.GetValue<string>())));
        foreach (var node in source["nodes"]!.AsArray().Where(node => node!["id"]!.GetValue<string>() != FriendshipPrefix + "portfolio_recurrence"))
            Assert.True(JsonNode.DeepEquals(node, local["nodes"]!.AsArray().Single(other => other!["id"]!.GetValue<string>() == node!["id"]!.GetValue<string>())));
        Assert.True(JsonNode.DeepEquals(original["unmodeled_extension"], result["unmodeled_extension"]));
        var certificate = FriendshipNode(local, "current_source_certificate");
        Assert.Equal(2, certificate["rule"]!["source_frames"]!.AsArray().Count);
        Assert.All(certificate["rule"]!["source_frames"]!.AsArray(), frame =>
        {
            Assert.Equal(2, frame!["queue_item_ids"]!.AsArray().Count);
            Assert.Equal(2, frame["request_nonces"]!.AsArray().Count);
        });
        Assert.Equal("corpus-hash", certificate["rule"]!["source_sha256"]!.GetValue<string>());
        Assert.True(certificate["rule"]!["fresh_rebind_required_after_each_native_receipt"]!.GetValue<bool>());
        Assert.False(certificate["rule"]!["uncompared_candidates_are_negative_labels"]!.GetValue<bool>());
        var fields = certificate["required_field_paths"]!.AsArray().Select(field => field!.GetValue<string>()).ToArray();
        Assert.Contains("npcs.grandpa_friendship_progress", fields);
        Assert.Contains("locations.collision_grid", fields);
        Assert.Contains("player.movement_timing_context", fields);
        Assert.Contains("locations.social_route_date_evidence", fields);
        Assert.Equal(new CurrentFriendshipTeacherPreference().ComparisonPolicyId,
            FriendshipNode(local, "portfolio_recurrence")["rule"]!["teacher_choice"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("goal")]
    [InlineData("own_scope")]
    [InlineData("skills_scope")]
    [InlineData("missing_root")]
    [InlineData("duplicate_root")]
    [InlineData("already_complete")]
    [InlineData("discarded_blockers")]
    [InlineData("missing_settlement")]
    [InlineData("points")]
    [InlineData("maximum")]
    [InlineData("romance")]
    [InlineData("count")]
    [InlineData("operator")]
    [InlineData("population")]
    [InlineData("portfolio")]
    [InlineData("loop")]
    [InlineData("deadline")]
    [InlineData("arithmetic")]
    public void FriendshipProjectionRejectsScopeOrNativeContractDrift(string mutation)
    {
        var graph = OriginalFriendshipGraph();
        var direction = FriendshipDirection(graph);
        var terminal = FriendshipNode(direction, "ten_native_villagers_at_1975")["rule"]!;
        switch (mutation)
        {
            case "schema": graph["schema_version"] = "other"; break;
            case "goal": graph["goal_id"] = "other"; break;
            case "own_scope": graph["friendship_local_method_view"] = new JsonObject(); break;
            case "skills_scope": graph["skill_local_method_view"] = new JsonObject(); break;
            case "missing_root": graph["directions"]!.AsArray().Remove(direction); break;
            case "duplicate_root": graph["directions"]!.AsArray().Add(direction.DeepClone()); break;
            case "already_complete": direction["status"] = "complete"; break;
            case "discarded_blockers": direction["blockers"] = new JsonArray(); break;
            case "missing_settlement": direction["nodes"]!.AsArray().Remove(FriendshipNode(direction, "native_day_settlement")); break;
            case "points": terminal["threshold_points"] = 2000; break;
            case "maximum": terminal["maximum_points"] = 1000000; break;
            case "romance": terminal["romance_only"] = true; break;
            case "count": terminal["qualifying_count"]!["value"] = 9; break;
            case "operator": terminal["qualifying_count"]!["operator"] = "eq"; break;
            case "population": FriendshipNode(direction, "native_eligible_population")["rule"]!["population_owner"] = "friendship dictionary"; break;
            case "portfolio": FriendshipNode(direction, "portfolio_deficits")["rule"]!["portfolio_size"] = 5; break;
            case "loop": FriendshipNode(direction, "portfolio_recurrence")["rule"]!["continue_while"]!["value"] = 5; break;
            case "deadline": FriendshipNode(direction, "deadline_budget")["rule"]!["deadline"] = "Year 4"; break;
            case "arithmetic": FriendshipNode(direction, "deadline_budget")["rule"]!["arithmetic_is_not_route_proof"] = false; break;
        }
        var error = Assert.Throws<TargetInvocationException>(() => ProjectFriendshipView(graph, LocalCorpus()));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Theory]
    [InlineData("friendships_5")]
    [InlineData("friendships_10")]
    public void FriendshipViewRequiresIndependentNativeWitnessesForBothThresholds(string removed)
    {
        var corpus = LocalCorpus();
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ProjectFriendshipView(OriginalFriendshipGraph(),
            corpus with { Rows = corpus.Rows.Where(row => row.CriterionId != removed).ToArray() })).InnerException);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("state")]
    [InlineData("candidate")]
    [InlineData("option")]
    public void FriendshipProjectionCannotAttachAForeignSourceRoute(string mutation)
    {
        var corpus = LocalCorpus();
        var routes = LocalRoutes(corpus);
        var key = mutation switch { "source" => "source_id", "state" => "source_state_hash", "candidate" => "selected_candidate_id", _ => "option_id" };
        routes[0]![key] = mutation == "option" ? "fishing.collect_crab_pots" : "foreign";
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
            ProjectFriendshipView(OriginalFriendshipGraph(), corpus, routes)).InnerException);
    }

    private static FriendshipTeacherCorpus LocalCorpus() => Corpus(Row("friendships_5", "train", "five"), Row("friendships_10", "train", "ten"));

    private static JsonArray LocalRoutes(FriendshipTeacherCorpus corpus) => new(corpus.Rows.Select(row => (JsonNode)new JsonObject
    {
        ["source_id"] = row.SourceId, ["option_id"] = "social.talk_npc", ["selected_candidate_id"] = row.SelectedCandidateId,
        ["source_state_hash"] = row.BeforeStateHash
    }).ToArray());

    private static JsonObject ProjectFriendshipView(JsonObject graph, FriendshipTeacherCorpus corpus, JsonArray? routes = null) =>
        (JsonObject)typeof(FriendshipLocalDependencyViewBuilder).GetMethod("Project", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { graph, corpus, "corpus", "corpus-hash", "dependencies", "dependencies-hash", routes ?? LocalRoutes(corpus) })!;

    private static JsonObject FriendshipDirection(JsonObject graph) => graph["directions"]!.AsArray()
        .Single(row => row!["direction_id"]!.GetValue<string>() == "raise_friendships")!.AsObject();

    private static JsonObject FriendshipNode(JsonObject direction, string suffix) => direction["nodes"]!.AsArray()
        .Single(node => node!["id"]!.GetValue<string>() == FriendshipPrefix + suffix)!.AsObject();

    private static JsonObject OriginalFriendshipGraph()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "experiments", "goal-method-dependency-expansions.v1.json");
            if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        }
        throw new FileNotFoundException("Original dependency graph not found.");
    }
}
