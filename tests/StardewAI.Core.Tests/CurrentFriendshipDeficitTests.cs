using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CurrentFriendshipDeficitTests
{
    private readonly CurrentFriendshipDeficitBuilder builder = new();

    [Fact]
    public void ProjectsCurrentNativeFactsWithoutInventingAbsentPointsOrEvidence()
    {
        var snapshot = Snapshot(1974, 1975, null);
        var before = JsonSerializer.Serialize(snapshot);
        var result = builder.Build(snapshot);

        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(snapshot.StateHash, result.SourceStateHash);
        Assert.Equal(117, result.GameTick);
        Assert.Equal(3, result.EligibleVillagerCount);
        Assert.Equal(1, result.QualifyingVillagerCount);
        Assert.Equal(4, result.VillagersMissingForFive);
        Assert.Equal(9, result.VillagersMissingForTen);
        Assert.Equal(new long?[] { 1, 0, null },
            result.Villagers.Select(row => row.PointsMissingToScoringRange));
        Assert.Null(result.Villagers[2].FriendshipPoints);
        Assert.False(result.Villagers[2].FriendshipRowExists);
        Assert.All(result.Villagers, row => Assert.True(row.UniqueNpcIdentity));
        Assert.Empty(result.BlockingReasons);
        Assert.Empty(result.DuplicateNpcNames);
        Assert.False(result.TrainingLabelEligible);
        Assert.False(result.NativeOutcomeVerified);
        Assert.Equal(before, JsonSerializer.Serialize(snapshot));
    }

    [Theory]
    [InlineData(0, 5, 10)]
    [InlineData(4, 1, 6)]
    [InlineData(5, 0, 5)]
    [InlineData(9, 0, 1)]
    [InlineData(10, 0, 0)]
    [InlineData(11, 0, 0)]
    public void CountsEachCriterionIndependently(int count, int fiveMissing, int tenMissing)
    {
        var result = builder.Build(Snapshot(Enumerable.Repeat<int?>(1975, count).ToArray()));
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(count, result.QualifyingVillagerCount);
        Assert.Equal(fiveMissing, result.VillagersMissingForFive);
        Assert.Equal(tenMissing, result.VillagersMissingForTen);
    }

    [Theory]
    [InlineData(int.MinValue, false, 2147485623L)]
    [InlineData(-1, false, 1976L)]
    [InlineData(0, false, 1975L)]
    [InlineData(1974, false, 1L)]
    [InlineData(1975, true, 0L)]
    [InlineData(999999, true, 0L)]
    [InlineData(1000000, false, null)]
    [InlineData(int.MaxValue, false, null)]
    public void UsesInclusiveNativeRangeAndDoesNotOverflow(int points, bool qualifies, long? missing)
    {
        var result = builder.Build(Snapshot(points));
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(qualifies, result.Villagers.Single().Qualifies);
        Assert.Equal(missing, result.Villagers.Single().PointsMissingToScoringRange);
    }

    [Fact]
    public void PreservesNativeInstancesButMarksAmbiguousActionIdentity()
    {
        var snapshot = Mutate(progress =>
        {
            progress["eligible_villager_rows"]![1]!["npc_name"] = "Npc0";
            progress["eligible_villager_rows"]![2]!["npc_name"] = "Npc0";
        }, 1975, 1975, 1975);
        var result = builder.Build(snapshot);
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(3, result.QualifyingVillagerCount);
        Assert.Equal(new[] { 0, 1, 2 }, result.Villagers.Select(row => row.NativeIterationIndex));
        Assert.Equal(new[] { "Npc0" }, result.DuplicateNpcNames);
        Assert.All(result.Villagers, row => Assert.False(row.UniqueNpcIdentity));
    }

    [Fact]
    public void ConflictingRepeatedNamesCannotComeFromOneNativeFarmerDictionary()
    {
        AssertBlocked(Mutate(progress =>
            progress["eligible_villager_rows"]![1]!["npc_name"] = "Npc0", 1975, 1976),
            "friendship_deficit_duplicate_identity_conflict");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("unavailable")]
    [InlineData("missing")]
    public void UnreadableProjectionIsNotZero(string status)
    {
        var snapshot = Snapshot(1975);
        var section = JsonNode.Parse(snapshot.State["npcs"].GetRawText())!;
        section["grandpa_friendship_progress"]!["status"] = status;
        SetSection(snapshot, section);
        AssertBlocked(snapshot, "friendship_deficit_source_unreadable");
    }

    [Theory]
    [InlineData("threshold_points", "2000")]
    [InlineData("maximum_points", "1000000")]
    [InlineData("romance_only", "true")]
    [InlineData("romance_only", "\"false\"")]
    [InlineData("projection_status", "\"partial\"")]
    [InlineData("eligible_villager_count", "0")]
    [InlineData("qualifying_count", "-1")]
    [InlineData("qualifying_count", "null")]
    [InlineData("eligible_villager_rows", "{}")]
    public void RejectsIncompleteOrDifferentScoringDefinition(string field, string json)
    {
        AssertBlocked(Mutate(progress => progress[field] = JsonNode.Parse(json), 1975),
            "friendship_deficit_source_shape_invalid");
    }

    [Theory]
    [InlineData("native_iteration_index", "1")]
    [InlineData("native_iteration_index", "-1")]
    [InlineData("npc_name", "\" \"")]
    [InlineData("is_villager", "false")]
    [InlineData("event_actor", "true")]
    [InlineData("friendship_row_exists", "false")]
    [InlineData("friendship_points", "null")]
    [InlineData("friendship_points", "\"1975\"")]
    [InlineData("friendship_points", "2147483648")]
    [InlineData("qualifies", "false")]
    [InlineData("qualifies", "null")]
    public void RejectsMalformedOrContradictoryNativeRow(string field, string json)
    {
        AssertBlocked(Mutate(progress =>
            progress["eligible_villager_rows"]![0]![field] = JsonNode.Parse(json), 1975),
            "friendship_deficit_villager_row_invalid");
    }

    [Fact]
    public void MissingPointsAreNotTheExplicitAbsentRecordNull()
    {
        AssertBlocked(Mutate(progress =>
            progress["eligible_villager_rows"]![0]!.AsObject().Remove("friendship_points"), (int?)null),
            "friendship_deficit_villager_row_invalid");
        AssertBlocked(Mutate(progress => progress.AsObject().Remove("qualifying_count"), 1975),
            "friendship_deficit_source_shape_invalid");
        AssertBlocked(Mutate(progress => progress["qualifying_count"] = 0, 1975),
            "friendship_deficit_qualifying_count_inconsistent");
    }

    [Theory]
    [InlineData("\"friendship_points\":1975", "friendship_deficit_villager_row_invalid")]
    [InlineData("\"qualifying_count\":1", "friendship_deficit_source_shape_invalid")]
    [InlineData("\"status\":\"available\"", "friendship_deficit_source_unreadable")]
    public void RejectsDuplicateConsumedJsonFields(string property, string reason)
    {
        var snapshot = Snapshot(1975);
        var json = snapshot.State["npcs"].GetRawText().Replace(property,
            property + "," + property, StringComparison.Ordinal);
        snapshot.State["npcs"] = JsonDocument.Parse(json).RootElement.Clone();
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        AssertBlocked(snapshot, reason);
    }

    [Fact]
    public void RequiresOriginalVersionHashAndFrameButNotUnrelatedForecastFields()
    {
        var snapshot = Snapshot(1975);
        snapshot.GameVersion = "1.6.16";
        AssertBlocked(snapshot, "friendship_deficit_snapshot_version_or_tick_unsupported");
        snapshot = Snapshot(1975);
        snapshot.GameTick = -1;
        AssertBlocked(snapshot, "friendship_deficit_snapshot_version_or_tick_unsupported");
        snapshot = Snapshot(1975);
        snapshot.SchemaVersion = "snapshot.v2";
        AssertBlocked(snapshot, "friendship_deficit_snapshot_version_or_tick_unsupported");
        snapshot = Snapshot(1975);
        snapshot.StateHash = new string('0', 64);
        AssertBlocked(snapshot, "friendship_deficit_state_hash_mismatch");
        snapshot = Snapshot(1975);
        snapshot.State.Remove("npcs");
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        AssertBlocked(snapshot, "friendship_deficit_source_unreadable");
        // No date forecasts, marriage flags, routes or preference labels are needed for current facts.
        Assert.Equal("ready_current_state_only", builder.Build(Snapshot(1975)).Status);
    }

    private void AssertBlocked(SnapshotEnvelope snapshot, string reason)
    {
        var result = builder.Build(snapshot);
        Assert.Equal("blocked", result.Status);
        Assert.Equal(new[] { reason }, result.BlockingReasons);
        Assert.Null(result.EligibleVillagerCount);
        Assert.Null(result.QualifyingVillagerCount);
        Assert.Null(result.VillagersMissingForFive);
        Assert.Null(result.VillagersMissingForTen);
        Assert.Empty(result.Villagers);
        Assert.False(result.TrainingLabelEligible);
        Assert.False(result.NativeOutcomeVerified);
    }

    private static SnapshotEnvelope Mutate(Action<JsonNode> mutation, params int?[] points)
    {
        var snapshot = Snapshot(points);
        var section = JsonNode.Parse(snapshot.State["npcs"].GetRawText())!;
        mutation(section["grandpa_friendship_progress"]!["value"]!);
        SetSection(snapshot, section);
        return snapshot;
    }

    private static void SetSection(SnapshotEnvelope snapshot, JsonNode section)
    {
        snapshot.State["npcs"] = JsonSerializer.SerializeToElement(section);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }

    private static SnapshotEnvelope Snapshot(params int?[] points)
    {
        var rows = points.Select((value, index) => new
        {
            native_iteration_index = index, npc_name = "Npc" + index,
            is_villager = true, event_actor = false,
            friendship_row_exists = value.HasValue, friendship_points = value,
            qualifies = value is >= 1975 and <= 999999
        }).ToArray();
        var snapshot = new SnapshotEnvelope { GameVersion = "1.6.15", GameTick = 117 };
        snapshot.State["npcs"] = JsonSerializer.SerializeToElement(new
        {
            grandpa_friendship_progress = new
            {
                status = "available",
                value = new
                {
                    threshold_points = 1975, maximum_points = 999999, romance_only = false,
                    projection_status = "complete_live_native_iteration",
                    eligible_villager_count = rows.Length,
                    qualifying_count = rows.Count(row => row.qualifies), eligible_villager_rows = rows
                }
            }
        });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
