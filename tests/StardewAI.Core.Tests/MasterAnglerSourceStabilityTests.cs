using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    // v89 431/434: the same missing fish has equivalent Town:5 / Forest:9
    // windows, but arrival at 11:30 is not invalidation of an 18:00 source.
    [Theory]
    [InlineData("Town", "Town:5")]
    [InlineData("Forest", "Forest:9")]
    public void SourceStabilityPreservesChosenSourceAfterArrivalBeforeOpening(
        string location,
        string sourceKey)
    {
        using var fixture = CreateSourceStabilityFixture(location, sourceKey);
        var remote = SourceStabilitySnapshot(location, sourceKey, remote: true);
        var routeAvailability = EvaluateSourceStability(remote, fixture.Parameters);
        var route = Assert.Single(Assert.Single(routeAvailability.Options).EventCandidates);
        Assert.True(route.Available, string.Join(";", route.BlockReasons));
        Assert.Equal("route_connector_tile", route.Kind);
        var routePlan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(), routeAvailability,
                "goal.fishing.complete_master_angler"), remote.StateHash);
        var routeQueue = new ActionQueueCompiler().Compile(routePlan, remote);
        var routeItem = Assert.Single(routeQueue.Items);
        Assert.Equal("executor.traverse_connector", routeItem.OptionId);
        Assert.Empty(routeItem.BlockingReasons);
        var continuation = Assert.IsType<JsonObject>(
            QueueReplanFilter.ReadObjectiveContinuation(
                JsonSerializer.SerializeToNode(routeItem, JsonOptions)!.AsObject()));

        var arrived = fixture.Before;
        var reboundParameters = continuation
            .Where(property => property.Key != "kind")
            .Select(property => Parameter(
                "continuation." + property.Key, property.Value!.GetValue<string>()))
            .ToArray();
        Assert.True(MasterAnglerWindowIntentValidator.TryValidate(
            arrived, reboundParameters, out _, out var reason), reason);
        var freshAvailability = EvaluateSourceStability(arrived, reboundParameters);
        var freshRanking = JsonSerializer.SerializeToNode(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(), freshAvailability,
                "goal.fishing.complete_master_angler"), JsonOptions)!.AsArray();
        var retained = QueueReplanFilter.FilterRankedCandidates(
            freshRanking, continuation);

        // A pre-window source must remain dispatchable through the existing
        // bounded wait/replan path, not disappear and invite a reverse route.
        Assert.NotEmpty(retained);
        Assert.All(retained, node =>
            Assert.True(node!["available"]!.GetValue<bool>()));
        Assert.All(retained, node =>
            Assert.NotEqual("route_connector_tile", node!["kind"]!.GetValue<string>()));
        var reboundPlan = new DailyPlanCompiler().Compile(
            JsonSerializer.Deserialize<PolicyEventCandidatePrediction[]>(
                retained.ToJsonString(), JsonOptions)!, arrived.StateHash);
        var reboundQueue = new ActionQueueCompiler().Compile(reboundPlan, arrived);
        Assert.Equal("pending", reboundQueue.Status);
        Assert.NotEmpty(reboundQueue.Items);
        Assert.All(reboundQueue.Items, item =>
        {
            Assert.Equal("pending", item.Status);
            Assert.Equal("executor.wait_ticks", item.OptionId);
            Assert.Empty(item.BlockingReasons);
            var refreshed = Assert.IsType<JsonObject>(
                QueueReplanFilter.ReadObjectiveContinuation(
                    JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject()));
            Assert.Equal(location, refreshed["master_angler_target_location"]!.GetValue<string>());
            Assert.Equal(sourceKey, refreshed["master_angler_source_key"]!.GetValue<string>());
        });
    }

    [Theory]
    [InlineData("Forest", "Forest:9")]
    [InlineData("Town", "Town:6")]
    public void SourceStabilityRejectsSameFishAtDifferentLocationOrSourceKey(
        string otherLocation,
        string otherSourceKey)
    {
        using var fixture = CreateSourceStabilityFixture("Town", "Town:5");
        var availability = EvaluateSourceStability(
            SourceStabilitySnapshot("Town", "Town:5", remote: true), fixture.Parameters);
        var route = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        var candidate = JsonSerializer.SerializeToNode(
            Assert.Single(new EventCandidateRanker().Rank(
                new BaselineTrainingReport(), availability,
                "goal.fishing.complete_master_angler")), JsonOptions)!.AsObject();
        var original = candidate.ToJsonString();
        var continuation = new JsonObject
        {
            ["kind"] = "master_angler",
            ["option_id"] = "fishing.catch_fish"
        };
        foreach (var parameter in fixture.Parameters)
            continuation[parameter.Name] = parameter.Value;
        var other = candidate.DeepClone().AsObject();
        other["candidate_id"] = "same-fish-other-source";
        foreach (var parameter in other["parameters"]!.AsArray())
        {
            var name = parameter!["name"]!.GetValue<string>();
            if (name.EndsWith("master_angler_target_location", StringComparison.Ordinal))
                parameter["value"] = otherLocation;
            if (name.EndsWith("master_angler_source_key", StringComparison.Ordinal))
                parameter["value"] = otherSourceKey;
        }
        var candidates = new JsonArray(other, candidate);
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(
            candidates, continuation));

        Assert.Equal(route.CandidateId, selected!["candidate_id"]!.GetValue<string>());
        // Identity filtering is control-only: it neither rewrites learner
        // metadata nor manufactures preference eligibility/provenance fields.
        Assert.Equal(original, selected.ToJsonString());
        Assert.Equal(original, candidate.ToJsonString());
        Assert.Equal(2, candidates.Count);
        Assert.Null(selected["teacher_preference_label_eligible"]);
        Assert.Null(selected["preference_provenance_class"]);
    }

    [Theory]
    [InlineData(1130, "sun", "Town:5", true, "")]
    [InlineData(1800, "sun", "Town:5", true, "")]
    [InlineData(2600, "sun", "Town:5", false, "master_angler_window_intent_snapshot_time_mismatch")]
    [InlineData(1130, "rain", "Town:5", false, "master_angler_target_weather_window_mismatch")]
    [InlineData(1130, "sun", "Town:6", false, "master_angler_window_intent_not_an_exact_artifact_window")]
    public void SourceStabilityRequiresFreshExactWindowValidation(
        int currentTime,
        string weather,
        string sourceKey,
        bool expectedValid,
        string expectedReason)
    {
        using var fixture = CreateSourceStabilityFixture("Town", "Town:5");
        var snapshot = SourceStabilitySnapshot(
            "Town", "Town:5", currentTime: currentTime, weather: weather);
        var parameters = fixture.Parameters.Select(parameter => Parameter(
            parameter.Name,
            parameter.Name == "master_angler_source_key" ? sourceKey : parameter.Value))
            .ToArray();

        Assert.Equal(expectedValid, MasterAnglerWindowIntentValidator.TryValidate(
            snapshot, parameters, out var intent, out var reason));
        Assert.Equal(expectedReason, reason);
        if (expectedValid)
        {
            Assert.Equal("Town", intent.TargetLocation);
            Assert.Equal("Town:5", intent.SourceKey);
            Assert.Equal(1800, intent.EffectiveStartTime);
        }
        else
        {
            var blocked = Assert.Single(Assert.Single(
                EvaluateSourceStability(snapshot, parameters).Options).EventCandidates);
            Assert.False(blocked.Available);
            Assert.Contains(expectedReason, blocked.BlockReasons);
        }
    }

    private static Fixture CreateSourceStabilityFixture(string location, string sourceKey)
    {
        var fixture = CreateFixture("Beach:0");
        var artifact = JsonNode.Parse(File.ReadAllText(fixture.WindowIndexPath))!.AsObject();
        var species = artifact["species"]![0]!;
        species["qualified_item_id"] = "(O)132";
        var window = species["windows"]![0]!;
        window["location_id"] = location;
        window["source_key"] = sourceKey;
        window["weather_modes"] = new JsonArray("sun");
        window["time_windows"]![0]!["start_time"] = 1800;
        File.WriteAllText(fixture.WindowIndexPath, artifact.ToJsonString(JsonOptions));
        var hash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(fixture.WindowIndexPath))).ToLowerInvariant();
        var parameters = fixture.Parameters.Select(parameter => Parameter(parameter.Name,
            parameter.Name switch
            {
                "master_angler_target_qualified_item_id" => "(O)132",
                "master_angler_target_location" => location,
                "master_angler_source_key" => sourceKey,
                "master_angler_effective_start_time" => "1800",
                "master_angler_window_index_sha256" => hash,
                _ => parameter.Value
            })).ToArray();
        return fixture with
        {
            Before = SourceStabilitySnapshot(location, sourceKey),
            Parameters = parameters
        };
    }

    private static SnapshotEnvelope SourceStabilitySnapshot(
        string location,
        string sourceKey,
        bool remote = false,
        int currentTime = 1130,
        string weather = "sun")
    {
        // Reuse the established full-route/fishing fixture. The native time
        // predicate is false before 18:00; no cast eligibility is invented.
        var snapshot = BuildSnapshot(
            targetCaught: false,
            currentLocation: remote ? "Farm" : "Beach",
            currentTime: currentTime,
            runtimeRuleEligible: currentTime >= 1800 && currentTime < 2600,
            currentWeather: weather);
        var json = JsonSerializer.Serialize(snapshot.State, JsonOptions)
            .Replace("Beach", location, StringComparison.Ordinal)
            .Replace("(O)145", "(O)132", StringComparison.Ordinal)
            .Replace("\"145\"", "\"132\"", StringComparison.Ordinal)
            .Replace(location + "#0:", location + "#" + sourceKey.Split(':')[1] + ":",
                StringComparison.Ordinal);
        snapshot.State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static OptionAvailabilityEnvelope EvaluateSourceStability(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }
        });
}
