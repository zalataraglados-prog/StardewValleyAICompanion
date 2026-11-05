using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CurrentSkillExperienceDeficitTests
{
    private readonly CurrentSkillExperienceDeficitBuilder builder = new();

    [Fact]
    public void ProjectsFiveExactDeficitsWithoutMutatingSnapshotOrClaimingEvidence()
    {
        var snapshot = Snapshot();
        var before = JsonSerializer.Serialize(snapshot);
        var result = builder.Build(snapshot);

        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(snapshot.StateHash, result.SourceStateHash);
        Assert.Equal(snapshot.GameTick, result.GameTick);
        Assert.Equal(15, result.ScoringLevel);
        Assert.Equal(0, result.BaseLevelsMissingForFirstCriterion);
        Assert.Equal(20, result.BaseLevelsMissingForSecondCriterion);
        Assert.Equal(58500, result.ExperienceMissingForFiveLevelTenSkills);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, result.Skills.Select(row => row.SkillIndex));
        Assert.All(result.Skills, row =>
        {
            Assert.Equal(1500, row.ExperienceToNextLevel);
            Assert.Equal(11700, row.ExperienceToLevelTen);
        });
        Assert.False(result.TrainingLabelEligible);
        Assert.False(result.NativeOutcomeVerified);
        Assert.Equal(before, JsonSerializer.Serialize(snapshot));
    }

    [Theory]
    [InlineData(0, 0, 100, 30, 75000)]
    [InlineData(9, 10000, 5000, 0, 25000)]
    [InlineData(10, 15000, null, 0, 0)]
    [InlineData(10, int.MaxValue, null, 0, 0)]
    public void HandlesNativeBoundaryAndPostCapXp(
        int level, int xp, int? nextDeficit, int firstMissing, int totalDeficit)
    {
        var result = builder.Build(Snapshot(level, xp));
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(firstMissing, result.BaseLevelsMissingForFirstCriterion);
        Assert.Equal(totalDeficit, result.ExperienceMissingForFiveLevelTenSkills);
        Assert.Equal(50 - level * 5, result.BaseLevelsMissingForSecondCriterion);
        Assert.All(result.Skills, row => Assert.Equal(nextDeficit, row.ExperienceToNextLevel));
    }

    [Theory]
    [InlineData(8, 2)]
    [InlineData(4, -2)]
    public void EffectiveBuffsDoNotChangeBaseDeficit(int effective, int buff)
    {
        var snapshot = Mutate(detail =>
        {
            detail["skills"]![0]!["effective_level"] = effective;
            detail["skills"]![0]!["temporary_buff_delta"] = buff;
        });
        var result = builder.Build(snapshot);
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(15, result.ScoringLevel);
        Assert.Equal(20, result.BaseLevelsMissingForSecondCriterion);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("unavailable")]
    [InlineData("missing")]
    public void UnreadableSourceIsNotZero(string status)
    {
        var snapshot = Snapshot();
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["skills_detail"]!["status"] = status;
        SetPlayer(snapshot, player);
        AssertBlocked(snapshot, "skill_deficit_source_unreadable");
    }

    [Theory]
    [InlineData("skill_index", "1")]
    [InlineData("skill_id", "\"luck\"")]
    [InlineData("experience", "-1")]
    [InlineData("experience", "3299")]
    [InlineData("experience", "4800")]
    [InlineData("experience", "\"3300\"")]
    [InlineData("unmodified_level", "11")]
    [InlineData("temporary_buff_delta", "1")]
    [InlineData("next_level", "8")]
    [InlineData("next_level_experience", "4801")]
    [InlineData("experience_to_next_level", "1499")]
    [InlineData("at_vanilla_level_cap", "true")]
    public void RejectsInconsistentOrModdedRow(string field, string json)
    {
        var snapshot = Mutate(detail => detail["skills"]![0]![field] = JsonNode.Parse(json));
        AssertBlocked(snapshot, "skill_deficit_skill_row_invalid_or_duplicate");
    }

    [Fact]
    public void RequiresAllFiveSkillsAndDormantLuck()
    {
        AssertBlocked(Mutate(detail => detail["skills"]!.AsArray().RemoveAt(5)),
            "skill_deficit_source_shape_invalid");
        AssertBlocked(Mutate(detail =>
        {
            detail["skills"]![5]!["experience"] = 1;
            detail["skills"]![5]!["experience_to_next_level"] = 99;
        }), "skill_deficit_nonvanilla_luck_state");
        AssertBlocked(Mutate(detail => detail["skills"]![0]!.AsObject().Remove("experience")),
            "skill_deficit_skill_row_invalid_or_duplicate");
    }

    [Fact]
    public void RejectsBadScoringCapVersionAndStateHash()
    {
        AssertBlocked(Mutate(detail => detail["scoring_level"] = 25),
            "skill_deficit_scoring_level_inconsistent");
        AssertBlocked(Mutate(detail => detail["vanilla_level_cap"] = 11),
            "skill_deficit_source_shape_invalid");
        var snapshot = Snapshot();
        snapshot.GameVersion = "1.6.16";
        AssertBlocked(snapshot, "skill_deficit_snapshot_version_or_tick_unsupported");
        snapshot = Snapshot();
        snapshot.StateHash = new string('0', 64);
        AssertBlocked(snapshot, "skill_deficit_state_hash_mismatch");
    }

    [Fact]
    public void OddBaseSumDoesNotRoundUpToSecondCriterion()
    {
        var snapshot = Snapshot(10, 15000);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["level"]!["value"] = 24;
        player["skills_detail"]!["value"]!["scoring_level"] = 24;
        var row = player["skills_detail"]!["value"]!["skills"]![0]!;
        row["unmodified_level"] = 9;
        row["effective_level"] = 9;
        row["experience"] = 10000;
        row["next_level"] = 10;
        row["next_level_experience"] = 15000;
        row["experience_to_next_level"] = 5000;
        row["at_vanilla_level_cap"] = false;
        SetPlayer(snapshot, player);

        var result = builder.Build(snapshot);
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(24, result.ScoringLevel);
        Assert.Equal(1, result.BaseLevelsMissingForSecondCriterion);
        Assert.Equal(5000, result.ExperienceMissingForFiveLevelTenSkills);
    }

    [Fact]
    public void RejectsDuplicateSkillIdentityAndDuplicateJsonField()
    {
        AssertBlocked(Mutate(detail =>
            detail["skills"]![1] = detail["skills"]![0]!.DeepClone()),
            "skill_deficit_skill_row_invalid_or_duplicate");
        var snapshot = Snapshot();
        var json = snapshot.State["player"].GetRawText().Replace(
            "\"experience\":3300", "\"experience\":3300,\"experience\":3300",
            StringComparison.Ordinal);
        snapshot.State["player"] = JsonDocument.Parse(json).RootElement.Clone();
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        AssertBlocked(snapshot, "skill_deficit_skill_row_invalid_or_duplicate");
    }

    [Fact]
    public void RequiresNativeCapNullsAndIgnoresInputOrdering()
    {
        var snapshot = Snapshot(10, 15000);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["skills_detail"]!["value"]!["skills"]![0]!["next_level"] = 11;
        SetPlayer(snapshot, player);
        AssertBlocked(snapshot, "skill_deficit_skill_row_invalid_or_duplicate");

        snapshot = Mutate(detail =>
        {
            var array = detail["skills"]!.AsArray();
            var reversed = new JsonArray(array.Reverse().Select(row => row!.DeepClone()).ToArray());
            detail["skills"] = reversed;
        });
        var result = builder.Build(snapshot);
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, result.Skills.Select(row => row.SkillIndex));
    }

    private void AssertBlocked(SnapshotEnvelope snapshot, string reason)
    {
        var result = builder.Build(snapshot);
        Assert.Equal("blocked", result.Status);
        Assert.Equal(new[] { reason }, result.BlockingReasons);
        Assert.Empty(result.Skills);
        Assert.Null(result.ScoringLevel);
        Assert.Null(result.ExperienceMissingForFiveLevelTenSkills);
        Assert.False(result.TrainingLabelEligible);
    }

    private static SnapshotEnvelope Mutate(Action<JsonNode> mutation)
    {
        var snapshot = Snapshot();
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        mutation(player["skills_detail"]!["value"]!);
        SetPlayer(snapshot, player);
        return snapshot;
    }

    private static void SetPlayer(SnapshotEnvelope snapshot, JsonNode player)
    {
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }

    private static SnapshotEnvelope Snapshot(int level = 6, int xp = 3300)
    {
        var thresholds = new[] { 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 };
        var ids = new[] { "farming", "fishing", "foraging", "mining", "combat", "luck" };
        var snapshot = new SnapshotEnvelope { GameVersion = "1.6.15", GameTick = 117 };
        var skills = Enumerable.Range(0, 6).Select(index =>
        {
            var currentLevel = index == 5 ? 0 : level;
            var experience = index == 5 ? 0 : xp;
            var threshold = currentLevel < 10 ? thresholds[currentLevel] : (int?)null;
            return new
            {
                skill_id = ids[index], skill_index = index, unmodified_level = currentLevel,
                effective_level = currentLevel, temporary_buff_delta = 0, experience,
                next_level = currentLevel < 10 ? currentLevel + 1 : (int?)null,
                next_level_experience = threshold,
                experience_to_next_level = threshold - experience,
                at_vanilla_level_cap = currentLevel == 10
            };
        }).ToArray();
        snapshot.State["player"] = JsonSerializer.SerializeToElement(new
        {
            level = new { status = "available", value = level * 5 / 2 },
            skills_detail = new
            {
                status = "available",
                value = new { scoring_level = level * 5 / 2, vanilla_level_cap = 10, skills }
            }
        });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
