using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    [Fact]
    public void AuthoritativeRemoteIntentCompilesOneExistingRouteConnector()
    {
        var fixture = CreateFixture("Beach:0");
        try
        {
            var remote = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Farm");
            var availability = new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    remote,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    });

            var route = Assert.Single(
                Assert.Single(availability.Options).EventCandidates);
            Assert.True(route.Available, string.Join(";", route.BlockReasons));
            Assert.Equal("route_connector_tile", route.Kind);
            Assert.Contains(route.Parameters, parameter =>
                parameter.Name == "continuation.master_angler_target_location" &&
                parameter.Value == "Beach");

            var ranked = new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                "goal.fishing.complete_master_angler");
            var plan = new DailyPlanCompiler().Compile(
                ranked,
                remote.StateHash);
            Assert.Equal("traverse_connector", Assert.Single(plan.Steps).Kind);
            var queue = new ActionQueueCompiler().Compile(plan, remote);
            var queueItem = Assert.Single(queue.Items);
            Assert.Equal("executor.traverse_connector", queueItem.OptionId);
            Assert.Empty(queueItem.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeRemoteIntentRejectsRouteWhenTargetWeatherDoesNotMatch()
    {
        var fixture = CreateFixture(
            "Beach:0",
            weatherModes: new[] { "rain", "storm" });
        try
        {
            var remote = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Farm",
                currentWeather: "sun");
            var blocked = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    remote,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.False(blocked.Available);
            Assert.Equal("catch_fish", blocked.Kind);
            Assert.Contains(
                "master_angler_target_weather_window_mismatch",
                blocked.BlockReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeRemoteIntentRoutesWhenTargetWeatherMatches()
    {
        var fixture = CreateFixture(
            "Beach:0",
            weatherModes: new[] { "rain", "storm" });
        try
        {
            var remote = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Farm",
                currentWeather: "rain");
            var route = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    remote,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.True(route.Available, string.Join(";", route.BlockReasons));
            Assert.Equal("route_connector_tile", route.Kind);
            Assert.Equal("master_angler_rolling_route", route.AvailabilityClass);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeRemoteIntentDoesNotRouteWhileInventoryIsFull()
    {
        var fixture = CreateFixture("Beach:0");
        try
        {
            var remote = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Farm",
                inventoryFull: true);
            var blocked = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    remote,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.False(blocked.Available);
            Assert.Equal("catch_fish", blocked.Kind);
            Assert.Contains(
                "fishing_inventory_full_requires_storage_transfer",
                blocked.BlockReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void ConcurrentWindowsKeepDistinctCandidateIdentities()
    {
        var first = CreateFixture("Beach:0");
        var second = CreateFixture("Beach:1");
        try
        {
            var remote = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Farm");
            var availability = new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    remote,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = first.Parameters
                        },
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = second.Parameters
                        }
                    });

            var candidates = availability.Options
                .SelectMany(value => value.EventCandidates)
                .Where(value => value.Available)
                .ToArray();
            Assert.Equal(2, candidates.Length);
            Assert.Equal(
                candidates.Length,
                candidates.Select(value => value.CandidateId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.Contains(candidates, value =>
                value.CandidateId.Contains("Beach:0", StringComparison.Ordinal));
            Assert.Contains(candidates, value =>
                value.CandidateId.Contains("Beach:1", StringComparison.Ordinal));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeRemoteIntentRejectsFullRouteThatMissesWindow()
    {
        var fixture = CreateFixture("Beach:0");
        try
        {
            var remote = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Farm",
                currentTime: 2400,
                remoteViaLongTownRoute: true);
            var route = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    remote,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.False(route.Available);
            Assert.Contains(
                "master_angler_current_step_would_miss_window",
                route.BlockReasons);
            Assert.Contains(route.Parameters, parameter =>
                parameter.Name ==
                    "master_angler_full_route_edge_count" &&
                parameter.Value == "2");
            Assert.Contains(route.Parameters, parameter =>
                parameter.Name ==
                    "master_angler_full_route_remaining_minutes" &&
                int.Parse(parameter.Value) > 90);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeRemoteIntentRequiresFullProfileRouteEvidence()
    {
        var fixture = CreateFixture("Beach:0");
        try
        {
            var remote = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Farm",
                includeFullRouteEvidence: false);
            var blocked = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    remote,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.False(blocked.Available);
            Assert.Contains(
                "master_angler_full_route_transparent_evidence_missing",
                blocked.BlockReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeRemoteIntentRecomputesRemainingRouteAfterTransition()
    {
        var fixture = CreateFixture("Beach:0");
        try
        {
            var afterFirstTransition = BuildSnapshot(
                targetCaught: false,
                currentLocation: "Town");
            var route = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    afterFirstTransition,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.True(route.Available, string.Join(";", route.BlockReasons));
            Assert.Contains(route.Parameters, parameter =>
                parameter.Name == "master_angler_full_route_edge_count" &&
                parameter.Value == "1");
        }
        finally
        {
            fixture.Dispose();
        }
    }
}
