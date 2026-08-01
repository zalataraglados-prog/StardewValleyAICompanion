using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingHorizonObservationIntegrationTests
{
    [Fact]
    public void YearThreeBoundaryWritesClosedHorizonsExactlyOnce()
    {
        var root = TestRoot();
        var afterPath = Path.Combine(root, "after.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(afterPath, Snapshot(3, "spring", 1, 600, 21, "available").ToJsonString());
        var options = new LiveTrainingOptions { Root = root, RunId = "horizon.integration" };
        var execution = new JsonObject
        {
            ["status"] = "applied",
            ["primitive_verification_status"] = "verified",
            ["after_snapshot_fresh"] = true,
            ["state_hash_changed"] = true,
            ["after_snapshot_path"] = afterPath,
            ["after_state_hash"] = "hash.year3"
        };

        Assert.Equal(4, InvokeAppender(options, Snapshot(2, "winter", 28, 2600), execution));
        Assert.Equal(0, InvokeAppender(options, Snapshot(2, "winter", 28, 2600), execution));

        var rows = File.ReadAllLines(options.PolicyHorizonObservationPath)
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal(
            new[] { "day", "grandpa_21", "season", "year" },
            rows.Select(row => row["horizon"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        var grandpa = Assert.Single(rows, row => row["horizon"]!.GetValue<string>() == "grandpa_21");
        Assert.Equal(21, grandpa["grandpa_score"]!.GetValue<int>());
        Assert.Equal(3, grandpa["year"]!.GetValue<int>());
        Assert.All(
            rows.Where(row => row["horizon"]!.GetValue<string>() != "grandpa_21"),
            row => Assert.Equal(2, row["year"]!.GetValue<int>()));
    }

    [Fact]
    public void UnavailableGrandpaScoreDoesNotCreateTerminalLabel()
    {
        var root = TestRoot();
        var afterPath = Path.Combine(root, "after.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(afterPath, Snapshot(3, "spring", 1, 600, 21, "unavailable").ToJsonString());
        var options = new LiveTrainingOptions { Root = root, RunId = "horizon.unavailable" };
        var execution = new JsonObject
        {
            ["status"] = "applied",
            ["primitive_verification_status"] = "verified",
            ["after_snapshot_fresh"] = true,
            ["state_hash_changed"] = true,
            ["after_snapshot_path"] = afterPath,
            ["after_state_hash"] = "hash.unavailable"
        };

        Assert.Equal(3, InvokeAppender(options, Snapshot(2, "winter", 28, 2600), execution));
        Assert.DoesNotContain(
            File.ReadAllLines(options.PolicyHorizonObservationPath),
            line => line.Contains("\"horizon\":\"grandpa_21\"", StringComparison.Ordinal));
    }

    [Fact]
    public void UnverifiedExecutionCannotCloseAHorizon()
    {
        var root = TestRoot();
        var afterPath = Path.Combine(root, "after.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(afterPath, Snapshot(2, "spring", 2, 600).ToJsonString());
        var options = new LiveTrainingOptions { Root = root, RunId = "horizon.blocked" };
        var execution = new JsonObject
        {
            ["status"] = "blocked",
            ["primitive_verification_status"] = "blocked",
            ["after_snapshot_fresh"] = true,
            ["state_hash_changed"] = true,
            ["after_snapshot_path"] = afterPath,
            ["after_state_hash"] = "hash.blocked"
        };

        Assert.Equal(0, InvokeAppender(options, Snapshot(2, "spring", 1, 2600), execution));
        Assert.False(File.Exists(options.PolicyHorizonObservationPath));
    }

    private static int InvokeAppender(
        LiveTrainingOptions options,
        JsonObject beforeSnapshot,
        JsonObject execution)
    {
        var programType = typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!;
        var method = programType.GetMethod(
            "AppendClosedHorizonObservations",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("LiveTrainingLoop horizon appender was not found.");
        return Assert.IsType<int>(method.Invoke(null, new object[] { options, beforeSnapshot, execution }));
    }

    private static JsonObject Snapshot(
        int year,
        string season,
        int day,
        int time,
        int? grandpaScore = null,
        string grandpaStatus = "available")
    {
        var farm = new JsonObject();
        if (grandpaScore.HasValue)
            farm["grandpa_score"] = Field(grandpaScore.Value, grandpaStatus);
        return new JsonObject
        {
            ["save_id"] = Field("save.horizon", "available"),
            ["state"] = new JsonObject
            {
                ["time"] = new JsonObject
                {
                    ["year"] = Field(year, "available"),
                    ["season"] = Field(season, "available"),
                    ["day"] = Field(day, "available"),
                    ["time"] = Field(time, "available")
                },
                ["farm"] = farm
            }
        };
    }

    private static JsonObject Field<T>(T value, string status) => new()
    {
        ["status"] = status,
        ["value"] = JsonValue.Create(value)
    };

    private static string TestRoot() =>
        Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
}
