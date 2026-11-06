using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MarriageTeacherCorpusTests
{
    private const string MarriagePrefix = "dependency.marriage_and_house_upgrade.";

    [Fact]
    public void ExecutableMarriageCannotUseADependencyViewWhoseHashChanged()
    {
        var frontier = Frontier();
        frontier.Methods[0] = frontier.Methods[0] with { Status = "executable_frontier" };
        frontier.DependencyExpansionSha256 = "foreign";
        var path = Path.Combine(Path.GetTempPath(), "stardewai-marriage-view-hash-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{}");
        var error = Assert.Throws<TargetInvocationException>(() => typeof(GoalMethodTeacherCoverageBuilder)
            .GetMethod("ApplyMarriageCorpus", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { LocalMarriageCorpus(), GoalMethodTeacherCoverageSourceKinds.MarriageJointCorpus, frontier, Evidence(), "corpus", path }));
        Assert.Contains("changed after frontier", Assert.IsType<InvalidDataException>(error.InnerException).Message);
    }

    [Fact]
    public void MarriageCertificateCannotBorrowAnotherCorpusWithoutReplayingIt()
    {
        var path = Path.Combine(Path.GetTempPath(), "stardewai-marriage-view-corpus-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, new JsonObject { ["marriage_local_method_view"] = new JsonObject { ["corpus_path"] = "foreign-corpus" } }.ToJsonString());
        var error = Assert.Throws<TargetInvocationException>(() => typeof(MarriageLocalDependencyViewBuilder)
            .GetMethod("VerifyForCoverage", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { LocalMarriageCorpus(), "original-corpus", path }));
        Assert.Contains("another corpus", Assert.IsType<InvalidDataException>(error.InnerException).Message);
    }

    [Fact]
    public void MarriageViewRetainsAllGlobalObligationsAndSeparateDirectDependencyControlRoles()
    {
        var original = OriginalMarriageGraph();
        original["unmodeled_extension"] = new JsonObject { ["retained"] = true };
        var before = original.ToJsonString();
        var source = MarriageDirection(original);
        var corpus = LocalMarriageCorpus();
        var result = ProjectMarriage(original, corpus);
        Assert.Equal(before, original.ToJsonString());
        var local = MarriageDirection(result);
        Assert.Equal("complete", local["status"]!.GetValue<string>());
        Assert.Empty(local["blockers"]!.AsArray());
        var metadata = result["marriage_local_method_view"]!;
        Assert.True(JsonNode.DeepEquals(source, metadata["global_direction_contract"]));
        Assert.True(JsonNode.DeepEquals(source["blockers"], metadata["global_dependency_blockers"]));
        Assert.True(JsonNode.DeepEquals(MarriageNode(source, "joint_recurrence")["rule"], metadata["global_recurrence_contract"]));
        foreach (var flag in new[] { "prior_house_acquisition_verified", "prior_relationship_acquisition_verified", "prior_item_acquisition_verified", "full_root_verified", "formal_product_training_authorized" })
            Assert.False(metadata[flag]!.GetValue<bool>());
        foreach (var direction in original["directions"]!.AsArray().Where(row => row!["direction_id"]!.GetValue<string>() != MarriageTeacherCorpus.CurrentDirectionId))
            Assert.True(JsonNode.DeepEquals(direction, result["directions"]!.AsArray().Single(other =>
                other!["direction_id"]!.GetValue<string>() == direction!["direction_id"]!.GetValue<string>())));
        foreach (var node in source["nodes"]!.AsArray().Where(node => node!["id"]!.GetValue<string>() != MarriagePrefix + "joint_recurrence"))
            Assert.True(JsonNode.DeepEquals(node, local["nodes"]!.AsArray().Single(other => other!["id"]!.GetValue<string>() == node!["id"]!.GetValue<string>())));
        Assert.True(JsonNode.DeepEquals(original["unmodeled_extension"], result["unmodeled_extension"]));
        var certificate = MarriageNode(local, "current_source_certificate");
        var rule = certificate["rule"]!;
        Assert.Equal("corpus-hash", rule["source_sha256"]!.GetValue<string>());
        Assert.Equal(5, rule["source_frames"]![0]!["queue_chain"]!.AsArray().Count);
        Assert.Equal(2, rule["source_frames"]![0]!["preparation_decisions"]!.AsArray().Count);
        Assert.Equal("social.advance_partnership", rule["verified_routes"]![0]!["option_id"]!.GetValue<string>());
        Assert.All(rule["verified_routes"]![0]!["preparation_bindings"]!.AsArray(), binding =>
        {
            Assert.Equal("social.gift_npc", binding!["option_id"]!.GetValue<string>());
            Assert.Equal(MarriagePrefix + "gift_partner", binding["dependency_node_id"]!.GetValue<string>());
        });
        Assert.True(rule["fresh_rebind_required_after_each_native_receipt"]!.GetValue<bool>());
        foreach (var flag in new[] { "mechanical_controls_are_preference_labels", "uncompared_candidates_are_negative_labels", "gift_dependencies_are_direct_marriage_routes" })
            Assert.False(rule[flag]!.GetValue<bool>());
        var fields = certificate["required_field_paths"]!.AsArray().Select(field => field!.GetValue<string>()).ToArray();
        foreach (var field in new[] { "player.farmhouse_upgrade_level", "player.days_until_farmhouse_upgrade", "world_progress.marriage_house",
            "npcs.friendships", "player.inventory", "player.story_event", "player.movement_timing_context", "time.total_days" })
            Assert.Contains(field, fields);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("scope")]
    [InlineData("missing_settlement")]
    [InlineData("discarded_blockers")]
    [InlineData("terminal")]
    [InlineData("house_target")]
    [InlineData("state_kind")]
    [InlineData("direct_role")]
    [InlineData("dependency_role")]
    [InlineData("settlement_owner")]
    [InlineData("refresh")]
    [InlineData("loop")]
    public void MarriageViewCannotWeakenTheOriginalJointOrOwnershipContracts(string mutation)
    {
        var graph = OriginalMarriageGraph();
        var direction = MarriageDirection(graph);
        switch (mutation)
        {
            case "schema": graph["schema_version"] = "other"; break;
            case "scope": graph["friendship_local_method_view"] = new JsonObject(); break;
            case "missing_settlement": direction["nodes"]!.AsArray().Remove(MarriageNode(direction, "native_day_settlement")); break;
            case "discarded_blockers": direction["blockers"] = new JsonArray(); break;
            case "terminal": MarriageNode(direction, "joint_terminal")["rule"]!["all"]![1]!["value"] = 1; break;
            case "house_target": MarriageNode(direction, "house_state")["rule"]!["target_level"] = 1; break;
            case "state_kind": MarriageNode(direction, "house_state")["kind"] = "policy_option_transition"; break;
            case "direct_role": MarriageNode(direction, "advance_partnership")["option_id"] = "social.gift_npc"; break;
            case "dependency_role": MarriageNode(direction, "gift_partner")["kind"] = "state_fact"; break;
            case "settlement_owner": MarriageNode(direction, "native_day_settlement")["rule"]!["execution_owner"] = "policy_label"; break;
            case "refresh": MarriageNode(direction, "native_day_settlement")["rule"]!["refresh_snapshot_after_transition"] = false; break;
            case "loop": MarriageNode(direction, "joint_recurrence")["rule"]!["continue_while"] = "only house remains false"; break;
        }
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ProjectMarriage(graph, LocalMarriageCorpus())).InnerException);
    }

    [Theory]
    [InlineData("direct_gift")]
    [InlineData("source")]
    [InlineData("candidate")]
    [InlineData("queue")]
    [InlineData("premise")]
    [InlineData("preparation_option")]
    [InlineData("preparation_role")]
    [InlineData("preparation_identity")]
    [InlineData("omitted_preparation")]
    [InlineData("no_native")]
    [InlineData("no_proposal")]
    public void MarriageViewCannotAttachAForeignRouteOrTurnPreparationsIntoDirectActions(string mutation)
    {
        var corpus = LocalMarriageCorpus();
        var routes = MarriageRoutes(corpus);
        switch (mutation)
        {
            case "direct_gift": routes[0]!["option_id"] = "social.gift_npc"; break;
            case "source": routes[0]!["source_id"] = "foreign"; break;
            case "candidate": routes[0]!["selected_candidate_id"] = "foreign"; break;
            case "queue": routes[0]!["queue_id"] = "foreign"; break;
            case "premise": routes[0]!["declared_native_prerequisites"]!["native_receipt_sha256"] = "foreign"; break;
            case "preparation_option": routes[0]!["preparation_bindings"]![0]!["option_id"] = "housing.advance_farmhouse"; break;
            case "preparation_role": routes[0]!["preparation_bindings"]![0]!["dependency_node_id"] = MarriagePrefix + "advance_partnership"; break;
            case "preparation_identity": routes[0]!["preparation_bindings"]![0]!["selected_candidate_id"] = "foreign"; break;
            case "omitted_preparation": routes[0]!["preparation_bindings"]!.AsArray().RemoveAt(1); break;
            case "no_native": corpus = corpus with { Runs = new[] { corpus.Runs[0] with { NativeOutcomeVerified = false } } }; break;
            case "no_proposal":
                var chain = JsonNode.Parse(corpus.Runs[0].QueueChain.GetRawText())!.AsArray();
                chain.RemoveAt(2);
                corpus = corpus with { Runs = new[] { corpus.Runs[0] with { QueueChain = JsonSerializer.SerializeToElement(chain) } } };
                break;
        }
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ProjectMarriage(OriginalMarriageGraph(), corpus, routes)).InnerException);
    }

    private static JsonObject ProjectMarriage(JsonObject graph, MarriageTeacherCorpus corpus, JsonArray? routes = null) =>
        (JsonObject)typeof(MarriageLocalDependencyViewBuilder).GetMethod("Project", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { graph, corpus, "corpus", "corpus-hash", "dependencies", "dependencies-hash", routes ?? MarriageRoutes(corpus) })!;
    private static JsonObject MarriageDirection(JsonObject graph) => graph["directions"]!.AsArray().Single(row =>
        row!["direction_id"]!.GetValue<string>() == MarriageTeacherCorpus.CurrentDirectionId)!.AsObject();
    private static JsonObject MarriageNode(JsonObject direction, string suffix) => direction["nodes"]!.AsArray().Single(node =>
        node!["id"]!.GetValue<string>() == MarriagePrefix + suffix)!.AsObject();
    private static JsonObject OriginalMarriageGraph()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "experiments", "goal-method-dependency-expansions.v1.json");
            if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        }
        throw new FileNotFoundException("Original Marriage dependency graph not found.");
    }
}
