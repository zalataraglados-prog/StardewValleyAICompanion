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
    public void ClockProgressOrDifferentObjectiveBreaksAlternation()
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
        var changedObjective = guard.Observe(
            RouteExecution("(O)145"),
            Snapshot("Forest", 15, 1820),
            Snapshot("Town", 15, 1820));

        Assert.False(progressed.Detected);
        Assert.Equal(0, progressed.ReverseTransitionStreak);
        Assert.False(changedObjective.Detected);
        Assert.Equal(0, changedObjective.ReverseTransitionStreak);
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
