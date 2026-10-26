using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class RouteOscillationGuardTests
{
    [Fact]
    public void AlternatingVerifiedRoutesForSameObjectiveStopBeforeThirdReverse()
    {
        var guard = new RouteOscillationGuard();
        var execution = RouteExecution("(O)143");

        Assert.False(guard.Observe(
            execution,
            Snapshot("Forest", 15, 1810),
            Snapshot("Town", 15, 1810)).Detected);
        Assert.False(guard.Observe(
            execution,
            Snapshot("Town", 15, 1810),
            Snapshot("Forest", 15, 1810)).Detected);
        var detected = guard.Observe(
            execution,
            Snapshot("Forest", 15, 1810),
            Snapshot("Town", 15, 1810));

        Assert.True(detected.Detected);
        Assert.Equal(2, detected.ReverseTransitionStreak);
        Assert.Equal("Forest", detected.FromLocation);
        Assert.Equal("Town", detected.ToLocation);
    }

    [Fact]
    public void ClockProgressBreaksAlternation()
    {
        var guard = new RouteOscillationGuard();
        guard.Observe(
            RouteExecution("(O)143"),
            Snapshot("Forest", 15, 1810),
            Snapshot("Town", 15, 1810));
        var progressed = guard.Observe(
            RouteExecution("(O)143"),
            Snapshot("Town", 15, 1820),
            Snapshot("Forest", 15, 1820));

        Assert.False(progressed.Detected);
        Assert.Equal(0, progressed.ReverseTransitionStreak);
    }

    [Fact]
    public void AlternatingVerifiedRoutesAcrossObjectivesStillStop()
    {
        var guard = new RouteOscillationGuard();

        Assert.False(guard.Observe(
            RouteExecution("(O)129"),
            Snapshot("Farm", 13, 1820),
            Snapshot("BusStop", 13, 1820)).Detected);
        var firstReverse = guard.Observe(
            RouteExecution("(O)Goby"),
            Snapshot("BusStop", 13, 1820),
            Snapshot("Farm", 13, 1820));
        var detected = guard.Observe(
            RouteExecution("(O)129"),
            Snapshot("Farm", 13, 1820),
            Snapshot("BusStop", 13, 1820));

        Assert.False(firstReverse.Detected);
        Assert.Equal(1, firstReverse.ReverseTransitionStreak);
        Assert.True(detected.Detected);
        Assert.Equal(2, detected.ReverseTransitionStreak);
        Assert.Contains("(O)129", detected.ObjectiveKey, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteWithoutObjectiveContinuationIsStillGuarded()
    {
        var guard = new RouteOscillationGuard();
        var execution = RouteExecution("(O)143");
        execution.Remove("objective_continuation");
        execution["option_id"] = "executor.traverse_connector";

        guard.Observe(
            execution,
            Snapshot("Farm", 15, 1810),
            Snapshot("BusStop", 15, 1810));
        guard.Observe(
            execution,
            Snapshot("BusStop", 15, 1810),
            Snapshot("Farm", 15, 1810));
        var detected = guard.Observe(
            execution,
            Snapshot("Farm", 15, 1810),
            Snapshot("BusStop", 15, 1810));

        Assert.True(detected.Detected);
        Assert.Equal("executor.traverse_connector", detected.ObjectiveKey);
    }

    [Fact]
    public void NonRouteExecutionResetsObservedTransition()
    {
        var guard = new RouteOscillationGuard();
        guard.Observe(
            RouteExecution("(O)143"),
            Snapshot("Forest", 15, 1810),
            Snapshot("Town", 15, 1810));
        var nonRoute = RouteExecution("(O)143");
        nonRoute["primitive_kind"] = "catch_fish";

        guard.Observe(
            nonRoute,
            Snapshot("Town", 15, 1810),
            Snapshot("Town", 15, 1810));
        var afterReset = guard.Observe(
            RouteExecution("(O)143"),
            Snapshot("Town", 15, 1810),
            Snapshot("Forest", 15, 1810));

        Assert.False(afterReset.Detected);
        Assert.Equal(0, afterReset.ReverseTransitionStreak);
    }

    private static JsonObject RouteExecution(string targetId) => new()
    {
        ["status"] = "applied",
        ["primitive_kind"] = "traverse_connector",
        ["primitive_verification_status"] = "verified",
        ["after_snapshot_fresh"] = true,
        ["objective_continuation"] = new JsonObject
        {
            ["kind"] = "master_angler",
            ["option_id"] = "fishing.catch_fish",
            ["master_angler_target_qualified_item_id"] = targetId
        }
    };

    private static JsonObject Snapshot(
        string location,
        int totalDay,
        int time) => new()
    {
        ["state"] = new JsonObject
        {
            ["player"] = new JsonObject
            {
                ["location_id"] = Field(location)
            },
            ["time"] = new JsonObject
            {
                ["total_days"] = Field(totalDay),
                ["time"] = Field(time)
            }
        }
    };

    private static JsonObject Field(JsonNode? value) => new()
    {
        ["value"] = value
    };
}
