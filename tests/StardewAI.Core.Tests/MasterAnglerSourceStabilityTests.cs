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
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    // v89 431/434: the same missing fish has equivalent Town:5 / Forest:9
    // windows. Defer departure rather than travelling and losing the local
    // continuation when the native cast predicate is still false at 11:30.
    [Theory]
    [InlineData("Town", "Town:5")]
    [InlineData("Forest", "Forest:9")]
    public void SourceStabilityDefersRemoteRouteAndTeacherBindingBeforeOpening(
        string location,
        string sourceKey)
    {
        using var fixture = CreateSourceStabilityFixture(location, sourceKey);
        var remote = SourceStabilitySnapshot(location, sourceKey, remote: true);
        var routeAvailability = EvaluateSourceStability(remote, fixture.Parameters);
        var route = Assert.Single(Assert.Single(routeAvailability.Options).EventCandidates);
        Assert.True(route.Available, string.Join(";", route.BlockReasons));
        Assert.Equal("route_connector_tile", route.Kind);
        Assert.False(route.AllowedNow);
        Assert.True(route.AllowedToday);
        Assert.Equal("master_angler_route_deferred_until_source_window", route.AvailabilityClass);
        Assert.Equal(1800, route.NextOpenTime);
        Assert.Equal(1800, route.EffectiveOpenTime);
        Assert.Equal(2600, route.ClosesAt);
        Assert.Equal(390 * 60, route.WaitCost);
        Assert.True(MasterAnglerWindowIntentValidator.TryValidate(
            remote, fixture.Parameters, out _, out var reason), reason);
        Assert.Contains(route.Parameters, parameter =>
            parameter.Name == "master_angler_route_window_status" &&
            parameter.Value == "deferred_until_authoritative_source_window");
        Assert.Contains(route.Parameters, parameter =>
            parameter.Name == "master_angler_route_not_before_time" && parameter.Value == "1800");
        var deferredAvailable = int.Parse(route.Parameters.Single(parameter =>
            parameter.Name ==
                "master_angler_deferred_departure_available_minutes").Value);
        var deferredRequired = int.Parse(route.Parameters.Single(parameter =>
            parameter.Name ==
                "master_angler_deferred_departure_required_minutes").Value);
        Assert.True(deferredAvailable >= deferredRequired);

        var ranked = Assert.Single(RankSourceStability(routeAvailability));
        Assert.Equal("deferred", ranked.TimelineStatus);
        Assert.Equal(1800, ranked.ScheduledStartTime);
        Assert.Equal(390 * 60, ranked.ScheduledWaitCost);
        var frontier = BuildSourceStabilityFrontier(fixture, remote, routeAvailability);
        Assert.Equal("no_current_matching_candidate", frontier.Status);
        Assert.Equal(1, frontier.CurrentIntentCount);
        Assert.Empty(frontier.CandidateBindings);
        Assert.Empty(frontier.SupportingPrerequisiteBindings);
        Assert.False(frontier.TrainingLabelEligible);
        Assert.False(frontier.EmitsNegativeLabelsForUnavailableRoutes);
        var rejection = Assert.Single(frontier.RejectedIntentCandidates);
        Assert.Equal(route.CandidateId, rejection.CandidateId);
        Assert.Equal("master_angler_route_deferred_until_source_window", rejection.Reason);

        // Even if the shared daily plan has no other work, it must wait, not
        // dispatch the remote connector early. No new local wait path is assumed.
        var plan = new DailyPlanCompiler().Compile(new[] { ranked }, remote.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, remote);
        Assert.Equal("pending", queue.Status);
        var wait = Assert.Single(queue.Items);
        Assert.Equal("executor.wait_ticks", wait.OptionId);
        Assert.Empty(wait.BlockingReasons);
    }

    [Fact]
    public void SourceStabilityDefersFeasibleMultiConnectorRouteUntilOpening()
    {
        using var fixture = CreateSourceStabilityFixture("Beach", "Beach:0");
        var remote = SourceStabilitySnapshot(
            "Beach",
            "Beach:0",
            remote: true,
            remoteViaLongTownRoute: true);

        var route = Assert.Single(Assert.Single(
            EvaluateSourceStability(remote, fixture.Parameters).Options)
            .EventCandidates);

        Assert.True(route.Available, string.Join(";", route.BlockReasons));
        Assert.False(route.AllowedNow);
        Assert.True(route.AllowedToday);
        Assert.Equal(1800, route.NextOpenTime);
        Assert.Contains(route.Parameters, parameter =>
            parameter.Name == "master_angler_full_route_edge_count" &&
            parameter.Value == "2");
        var deferredAvailable = int.Parse(route.Parameters.Single(parameter =>
            parameter.Name ==
                "master_angler_deferred_departure_available_minutes").Value);
        var deferredRequired = int.Parse(route.Parameters.Single(parameter =>
            parameter.Name ==
                "master_angler_deferred_departure_required_minutes").Value);
        Assert.True(deferredAvailable >= deferredRequired);
    }

    [Fact]
    public void SourceStabilityRejectsDeferredRouteThatCannotFinishAfterOpening()
    {
        using var fixture = CreateSourceStabilityFixture(
            "Beach",
            "Beach:0",
            startTime: 2500);
        var remote = SourceStabilitySnapshot(
            "Beach",
            "Beach:0",
            remote: true,
            remoteViaLongTownRoute: true);

        var route = Assert.Single(Assert.Single(
            EvaluateSourceStability(remote, fixture.Parameters).Options)
            .EventCandidates);

        Assert.False(route.Available);
        Assert.False(route.AllowedNow);
        Assert.False(route.AllowedToday);
        Assert.Equal(
            "master_angler_deferred_route_time_budget_blocked",
            route.AvailabilityClass);
        Assert.Contains(
            "master_angler_deferred_departure_would_miss_source_window",
            route.BlockReasons);
        var deferredAvailable = int.Parse(route.Parameters.Single(parameter =>
            parameter.Name ==
                "master_angler_deferred_departure_available_minutes").Value);
        var deferredRequired = int.Parse(route.Parameters.Single(parameter =>
            parameter.Name ==
                "master_angler_deferred_departure_required_minutes").Value);
        Assert.True(deferredAvailable < deferredRequired);
    }

    [Fact]
    public void SourceStabilityKeepsOneStableDeferralBeforeOpening()
    {
        using var fixture = CreateSourceStabilityFixture("Town", "Town:5");
        var candidates = new[] { 1130, 1200, 1750 }
            .Select(currentTime => Assert.Single(Assert.Single(
                EvaluateSourceStability(
                    SourceStabilitySnapshot(
                        "Town",
                        "Town:5",
                        remote: true,
                        currentTime: currentTime),
                    fixture.Parameters).Options).EventCandidates))
            .ToArray();

        Assert.All(candidates, route =>
        {
            Assert.True(route.Available, string.Join(";", route.BlockReasons));
            Assert.False(route.AllowedNow);
            Assert.True(route.AllowedToday);
            Assert.Equal(1800, route.NextOpenTime);
            Assert.Equal(
                "master_angler_route_deferred_until_source_window",
                route.AvailabilityClass);
        });
        Assert.Single(candidates.Select(route => route.CandidateId).Distinct());
    }

    [Theory]
    [InlineData("Town", "Town:5")]
    [InlineData("Forest", "Forest:9")]
    public void SourceStabilityReopensExactRouteAndTerminalAtWindowOpening(
        string location,
        string sourceKey)
    {
        using var fixture = CreateSourceStabilityFixture(location, sourceKey);
        var remote = SourceStabilitySnapshot(location, sourceKey, remote: true, currentTime: 1800);
        var availability = EvaluateSourceStability(remote, fixture.Parameters);
        var ranked = Assert.Single(RankSourceStability(availability));
        Assert.True(ranked.Available);
        Assert.True(ranked.AllowedNow);
        Assert.Equal("ready_now", ranked.TimelineStatus);
        Assert.Equal(1800, ranked.ScheduledStartTime);
        Assert.Equal(0, ranked.ScheduledWaitCost);
        var frontier = BuildSourceStabilityFrontier(fixture, remote, availability);
        var binding = Assert.Single(frontier.CandidateBindings);
        Assert.True(frontier.TrainingLabelEligible);
        Assert.Equal(location, binding.TargetLocation);
        Assert.Equal(sourceKey, binding.SourceKey);
        Assert.Equal("authoritative_window_rolling_route_step", binding.BindingKind);
        Assert.Empty(frontier.RejectedIntentCandidates);

        var plan = new DailyPlanCompiler().Compile(new[] { ranked }, remote.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, remote);
        var connector = Assert.Single(queue.Items);
        Assert.Equal("executor.traverse_connector", connector.OptionId);
        Assert.Empty(connector.BlockingReasons);
        var continuation = Assert.IsType<JsonObject>(QueueReplanFilter.ReadObjectiveContinuation(
            JsonSerializer.SerializeToNode(connector, JsonOptions)!.AsObject()));
        Assert.Equal(location, continuation["master_angler_target_location"]!.GetValue<string>());
        Assert.Equal(sourceKey, continuation["master_angler_source_key"]!.GetValue<string>());
        var reboundParameters = continuation.Where(property => property.Key != "kind")
            .Select(property => Parameter("continuation." + property.Key,
                property.Value!.GetValue<string>())).ToArray();
        var arrived = SourceStabilitySnapshot(location, sourceKey, currentTime: 1800);
        var terminalAvailability = EvaluateSourceStability(arrived, reboundParameters);
        var terminals = RankSourceStability(terminalAvailability);
        Assert.NotEmpty(terminals);
        Assert.All(terminals, terminal =>
        {
            Assert.Equal("catch_fish", terminal.Kind);
            Assert.True(terminal.Available);
            Assert.Equal("ready_now", terminal.TimelineStatus);
        });
        var retained = QueueReplanFilter.FilterRankedCandidates(
            JsonSerializer.SerializeToNode(terminals, JsonOptions)!.AsArray(), continuation);
        Assert.Equal(terminals.Length, retained.Count);
        var terminalPlan = new DailyPlanCompiler().Compile(terminals, arrived.StateHash);
        var terminalQueue = new ActionQueueCompiler().Compile(terminalPlan, arrived);
        var catchItem = Assert.Single(terminalQueue.Items);
        Assert.Equal("executor.catch_fish", catchItem.OptionId);
        Assert.Equal("pending", catchItem.Status);
        Assert.Empty(catchItem.BlockingReasons);
        var terminalFrontier = BuildSourceStabilityFrontier(fixture, arrived, terminalAvailability);
        Assert.NotEmpty(terminalFrontier.CandidateBindings);
        Assert.All(terminalFrontier.CandidateBindings, terminal =>
        {
            Assert.Equal(location, terminal.TargetLocation);
            Assert.Equal(sourceKey, terminal.SourceKey);
            Assert.Equal("authoritative_window_terminal_attempt", terminal.BindingKind);
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
    [InlineData(1130, "sun", "Town:5", false, true, "")]
    [InlineData(1800, "sun", "Town:5", false, true, "")]
    [InlineData(2600, "sun", "Town:5", false, false, "master_angler_window_intent_snapshot_time_mismatch")]
    [InlineData(1130, "rain", "Town:5", false, false, "master_angler_target_weather_window_mismatch")]
    [InlineData(1130, "sun", "Town:6", false, false, "master_angler_window_intent_not_an_exact_artifact_window")]
    [InlineData(1130, "sun", "Town:5", true, true, "")]
    [InlineData(2600, "sun", "Town:5", true, false, "master_angler_window_intent_snapshot_time_mismatch")]
    [InlineData(1130, "rain", "Town:5", true, false, "master_angler_target_weather_window_mismatch")]
    [InlineData(1130, "sun", "Town:6", true, false, "master_angler_window_intent_not_an_exact_artifact_window")]
    public void SourceStabilityRequiresFreshExactWindowValidation(
        int currentTime,
        string weather,
        string sourceKey,
        bool remote,
        bool expectedValid,
        string expectedReason)
    {
        using var fixture = CreateSourceStabilityFixture("Town", "Town:5");
        var snapshot = SourceStabilitySnapshot(
            "Town", "Town:5", remote: remote, currentTime: currentTime, weather: weather);
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

    private static Fixture CreateSourceStabilityFixture(
        string location,
        string sourceKey,
        int startTime = 1800)
    {
        var fixture = CreateFixture("Beach:0");
        var artifact = JsonNode.Parse(File.ReadAllText(fixture.WindowIndexPath))!.AsObject();
        var species = artifact["species"]![0]!;
        species["qualified_item_id"] = "(O)132";
        var window = species["windows"]![0]!;
        window["location_id"] = location;
        window["source_key"] = sourceKey;
        window["weather_modes"] = new JsonArray("sun");
        window["time_windows"]![0]!["start_time"] = startTime;
        AddSourceStabilityAuthority(artifact);
        File.WriteAllText(fixture.WindowIndexPath, artifact.ToJsonString(JsonOptions));
        var hash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(fixture.WindowIndexPath))).ToLowerInvariant();
        var parameters = fixture.Parameters.Select(parameter => Parameter(parameter.Name,
            parameter.Name switch
            {
                "master_angler_target_qualified_item_id" => "(O)132",
                "master_angler_target_location" => location,
                "master_angler_source_key" => sourceKey,
                "master_angler_effective_start_time" =>
                    startTime.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
        string weather = "sun",
        bool remoteViaLongTownRoute = false)
    {
        // Reuse the established full-route/fishing fixture. The native time
        // predicate is false before 18:00; no cast eligibility is invented.
        var snapshot = BuildSnapshot(
            targetCaught: false,
            currentLocation: remote ? "Farm" : "Beach",
            currentTime: currentTime,
            remoteViaLongTownRoute: remoteViaLongTownRoute,
            runtimeRuleEligible: currentTime >= 1800 && currentTime < 2600,
            currentWeather: weather);
        var json = JsonSerializer.Serialize(snapshot.State, JsonOptions)
            .Replace("Beach", location, StringComparison.Ordinal)
            .Replace("(O)145", "(O)132", StringComparison.Ordinal)
            .Replace("\"145\"", "\"132\"", StringComparison.Ordinal)
            .Replace(location + "#0:", location + "#" + sourceKey.Split(':')[1] + ":",
                StringComparison.Ordinal);
        snapshot.State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["farm_type_key"] = Field(JsonValue.Create("Standard")!)
        }, JsonOptions);
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

    private static PolicyEventCandidatePrediction[] RankSourceStability(
        OptionAvailabilityEnvelope availability) => new EventCandidateRanker().Rank(
            new BaselineTrainingReport(), availability, "goal.fishing.complete_master_angler");

    private static void AddSourceStabilityAuthority(JsonObject index)
    {
        // Synthetic authority inputs, not a native catalog or production label.
        // Keep these extra artifacts outside the existing two-file fixture root.
        var root = Path.Combine(Path.GetTempPath(), "opencode", "master-angler-frontier-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var inventoryPath = Path.Combine(root, "inventory.json");
        var catalogPath = Path.Combine(root, "catalog.json");
        const string goal = "grandpa.maximum_21";
        var species = index["species"]!.AsArray().Select(node =>
            node!["qualified_item_id"]!.GetValue<string>()).ToArray();
        var groups = species.Select(id => new GoalRequirementGroup
        {
            RequirementId = "master_angler:" + id,
            SelectionRule = "all_required",
            RequiredAlternativeCount = 1,
            Alternatives = new[] { new GoalRequirementAlternative
            {
                ItemId = id[3..], QualifiedItemId = id, MatchKind = "item_id", Amount = 1
            } }
        }).ToArray();
        WriteSourceStabilityArtifact(inventoryPath, new AuthoritativeRequirementInventoryReport
        {
            Status = "complete", GoalId = goal,
            DenominatorComplete = true, AcquisitionRoutesComplete = true,
            RequirementSets = new[] { new GoalRequirementSet
            {
                RequirementSetId = "master_angler", RequiredGroupCount = 72,
                AcquisitionRoutesComplete = true, Groups = groups
            } }
        });
        WriteSourceStabilityArtifact(Path.Combine(root, "lowering.json"),
            new AcquisitionRouteOptionLoweringReport
            {
                Status = "complete", GoalId = goal,
                RequirementInventorySha256 = SourceStabilityHash(inventoryPath),
                DependencyAxisInventoryComplete = true,
                RequiredDownstreamDependencyAxes = StageOneCollectionRouteDependencyAxes.Required.ToArray(),
                RequirementSets = new[] { new AcquisitionRequirementSetLowering(
                    "master_angler", 72, 72, 72, groups.Select(group =>
                        new AcquisitionRequirementGroupLowering(group.RequirementId,
                            1, 1, 1, true, true, Array.Empty<string>(),
                            new[] { new AcquisitionRequirementAlternativeLowering(
                                group.Alternatives[0].ItemId, group.Alternatives[0].QualifiedItemId,
                                "fixture", "item_id", 1, 0, true, true,
                                new[] { new AcquisitionRequirementRouteLowering(
                                    "location_rule", "fixture", "fixture", "fixture",
                                    "fixture", "fixture",
                                    StageOneCollectionRouteDependencyAxes.Required.ToArray(),
                                    new[] { "fishing.catch_fish" }, Array.Empty<string>(), true, true) })
                            })).ToArray()) }
            });
        WriteSourceStabilityArtifact(catalogPath, new MasterAnglerOpportunityCatalogReport
        {
            Status = "complete", GoalId = goal, GameVersion = "1.6.15",
            NativeDenominatorCount = 72, SourceInventoryComplete = true,
            StaticCalendarConstraintComplete = true,
            LocationRuleSpawnChanceInputInventoryComplete = true,
            RequirementInventorySha256 = SourceStabilityHash(inventoryPath),
            Species = species.Select(id => new MasterAnglerSpeciesOpportunity
            { ItemId = id[3..], QualifiedItemId = id }).ToArray()
        });
        index["goal_id"] = goal;
        index["catalog_path"] = catalogPath;
        index["catalog_sha256"] = SourceStabilityHash(catalogPath);
    }

    private static CurrentMasterAnglerTeacherFrontier BuildSourceStabilityFrontier(
        Fixture fixture, SnapshotEnvelope snapshot, OptionAvailabilityEnvelope availability)
    {
        var index = JsonNode.Parse(File.ReadAllText(fixture.WindowIndexPath))!;
        var root = Path.GetDirectoryName(index["catalog_path"]!.GetValue<string>())!;
        var snapshotPath = Path.Combine(root, "snapshot.json");
        var rankingPath = Path.Combine(root, "ranking.json");
        var intentsPath = Path.Combine(root, "intents.json");
        WriteSourceStabilityArtifact(snapshotPath, snapshot);
        WriteSourceStabilityArtifact(rankingPath, new AvailabilityAwarePolicyPredictionEnvelope
        {
            Availability = availability, RankedEventCandidates = RankSourceStability(availability)
        });
        var intents = MasterAnglerTargetDateIntentBuilder.Build(
            fixture.WindowIndexPath, snapshotPath, fixture.TimingCalibrationPath);
        Assert.Single(intents.Candidates);
        WriteSourceStabilityArtifact(intentsPath, intents);
        return CurrentMasterAnglerTeacherFrontierBuilder.Build(
            Path.Combine(root, "inventory.json"), Path.Combine(root, "lowering.json"),
            rankingPath, snapshotPath, intentsPath);
    }

    private static void WriteSourceStabilityArtifact<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

    private static string SourceStabilityHash(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
