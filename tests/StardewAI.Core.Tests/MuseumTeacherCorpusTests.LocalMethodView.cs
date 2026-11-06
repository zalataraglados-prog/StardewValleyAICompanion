using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumTeacherCorpusTests
{
    [Fact]
    public void LocalViewPreservesTheEntireOriginalGraphAndAllSixMuseumRoles()
    {
        var graph = MuseumGraph(); var before = graph.ToJsonString();
        var direction = MuseumDirection(graph);
        direction["unknown_extension"] = "retained";
        before = graph.ToJsonString();
        var result = ProjectMuseum(graph, LocalMuseumCorpus());
        Assert.Equal(before, graph.ToJsonString());
        var projected = MuseumDirection(result);
        var metadata = result["museum_local_method_view"]!;
        Assert.True(JsonNode.DeepEquals(direction, metadata["global_direction_contract"]));
        Assert.True(JsonNode.DeepEquals(direction["blockers"], metadata["global_dependency_blockers"]));
        foreach (var other in graph["directions"]!.AsArray().Where(row => row!["direction_id"]!.GetValue<string>() != MuseumTeacherCorpus.Direction))
            Assert.True(JsonNode.DeepEquals(other, result["directions"]!.AsArray().Single(row =>
                row!["direction_id"]!.GetValue<string>() == other!["direction_id"]!.GetValue<string>())));
        foreach (var node in direction["nodes"]!.AsArray())
        {
            var copy = projected["nodes"]!.AsArray().Single(row => row!["id"]!.GetValue<string>() == node!["id"]!.GetValue<string>());
            if (node!["id"]!.GetValue<string>() != MuseumPrefix + "donation_recurrence") Assert.True(JsonNode.DeepEquals(node, copy));
            else foreach (var property in node["rule"]!.AsObject()) Assert.True(JsonNode.DeepEquals(property.Value, copy!["rule"]![property.Key]));
        }
        Assert.Equal(7, projected["nodes"]!.AsArray().Count);
        Assert.Equal("complete", projected["status"]!.GetValue<string>());
        Assert.Empty(projected["blockers"]!.AsArray());
        foreach (var flag in new[] { "prior_item_acquisition_verified", "global_rng_and_retry_bounds_verified", "global_shared_resource_schedule_verified", "full_root_verified", "formal_product_training_authorized" })
            Assert.False(metadata[flag]!.GetValue<bool>());
    }

    [Fact]
    public void SourceCertificateRetainsFull95AndExactNativeIdentityWithoutPolicyPermission()
    {
        var corpus = LocalMuseumCorpus();
        var view = ProjectMuseum(MuseumGraph(), corpus);
        var certificate = MuseumNode(MuseumDirection(view), "current_source_certificate");
        var rule = certificate["rule"]!;
        Assert.Equal(95, rule["source_frames"]![0]!["evidence"]!["items"]!.AsArray().Count);
        Assert.Equal(corpus.Rows[0].Evidence.Action.RequestNonce, rule["verified_routes"]![0]!["request_nonce"]!.GetValue<string>());
        Assert.Equal(corpus.Rows[0].Evidence.Action.BeforeStateHash, rule["verified_routes"]![0]!["source_state_hash"]!.GetValue<string>());
        Assert.Equal("museum.donate_items", rule["verified_routes"]![0]!["option_id"]!.GetValue<string>());
        Assert.False(rule["policy_training_authorized"]!.GetValue<bool>());
        Assert.False(rule["counterfactual_references_are_runtime_candidates"]!.GetValue<bool>());
        var fields = certificate["required_field_paths"]!.AsArray().Select(row => row!.GetValue<string>()).ToArray();
        Assert.Contains("quests.active_quests", fields);
        Assert.Contains("world_progress.full_shipment_progress", fields);
        Assert.Contains("farm.material_inventory_graph", fields);
        Assert.Contains("time.total_days", fields);
        Assert.Equal(view.ToJsonString(), ProjectMuseum(MuseumGraph(), corpus).ToJsonString());
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("stacked")]
    [InlineData("missing_acquisition")]
    [InlineData("denominator")]
    [InlineData("achievement")]
    [InlineData("reservation")]
    [InlineData("recurrence")]
    [InlineData("already_complete")]
    public void MuseumProjectionRejectsScopeOrOriginalNativeContractDrift(string mutation)
    {
        var graph = MuseumGraph(); var direction = MuseumDirection(graph);
        if (mutation == "stacked") graph["marriage_local_method_view"] = new JsonObject();
        if (mutation == "missing_acquisition") direction["nodes"]!.AsArray().Remove(MuseumNode(direction, "acquire_missing_item"));
        if (mutation == "denominator") MuseumNode(direction, "authoritative_requirement_set")["rule"]!["required_group_count"] = 94;
        if (mutation == "achievement") MuseumNode(direction, "achievement_received")["rule"]!["all"]![0]!["value"] = "26";
        if (mutation == "reservation") MuseumNode(direction, "reserve_missing_item")["rule"]!["unknown_reservation_evidence"] = "allow";
        if (mutation == "recurrence") MuseumNode(direction, "donation_recurrence")["rule"]!["continue_while"]!["value"] = true;
        if (mutation == "already_complete") direction["status"] = "complete";
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
            ProjectMuseum(graph, LocalMuseumCorpus(), mutation == "authority" ? "foreign" : "dependencies")).InnerException);
    }

    [Fact]
    public void CoverageReusesItsVerifiedCorpusPortInsteadOfDeepReadingNativeAgain()
    {
        var fixture = MuseumViewFixture(); var evidence = Evidence();
        ApplyMuseumView(fixture, evidence);
        var own = evidence[MuseumTeacherCorpus.CriterionId]!;
        Assert.Contains("train", ((System.Collections.IEnumerable)own.GetType().GetProperty("NativeOutcomePartitions")!.GetValue(own)!).Cast<string>());
        Assert.Equal("executable_frontier", fixture.Frontier.Methods[0].Status);
    }

    [Theory]
    [InlineData("full_root")]
    [InlineData("other_corpus")]
    [InlineData("original_graph")]
    public void CoverageRequiresAnIndependentlyRebuiltExactCorpusCertificate(string mutation)
    {
        var f = MuseumViewFixture();
        if (mutation == "original_graph")
        {
            var original = JsonNode.Parse(File.ReadAllText(f.Dependencies))!;
            original["changed_after_source"] = true;
            File.WriteAllText(f.Dependencies, original.ToJsonString());
        }
        else
        {
            var view = JsonNode.Parse(File.ReadAllText(f.View))!;
            if (mutation == "full_root") view["museum_local_method_view"]!["full_root_verified"] = true;
            else view["museum_local_method_view"]!["corpus_path"] = Path.Combine(Path.GetDirectoryName(f.CorpusPath)!, "other-corpus.json");
            File.WriteAllText(f.View, view.ToJsonString());
            f.Frontier.DependencyExpansionSha256 = StructuredPolicyCheckpointStore.HashFile(f.View);
        }
        var evidence = Evidence();
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ApplyMuseumView(f, evidence)).InnerException);
        Assert.All(evidence.Values.Cast<object>(), value => Assert.Empty((System.Collections.IEnumerable)value.GetType().GetProperty("TeacherSourceKinds")!.GetValue(value)!));
    }
}
