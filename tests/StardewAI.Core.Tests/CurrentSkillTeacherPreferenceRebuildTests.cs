using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class CurrentSkillTeacherPreferenceRebuildTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public void AlreadyCompleteFixtureIsCurrentStateOnlyAndRebuildable()
    {
        var (source, output) = Files();
        File.WriteAllText(source, JsonSerializer.Serialize(CurrentSkillExperienceDeficitTests.Snapshot(10, 15000), Json));
        var report = CurrentSkillTeacherPreferenceBuilder.Build(source);
        Assert.Equal("goal_already_satisfied_current_state", report.Status);
        Assert.False(report.TeacherPreferenceLabelEligible);
        Assert.False(report.NativeOutcomeVerified);
        Assert.False(report.FormalProductTrainingAuthorized);
        File.WriteAllText(output, JsonSerializer.Serialize(report, Json));
        var replay = CurrentSkillTeacherPreferenceBuilder.Verify(output);
        Assert.Equal(JsonSerializer.Serialize(report, Json), JsonSerializer.Serialize(replay, Json));
    }

    [Theory]
    [InlineData("teacherPreferenceLabelEligible")]
    [InlineData("formalProductTrainingAuthorized")]
    [InlineData("nativeOutcomeVerified")]
    [InlineData("usesLearnerRankOrScore")]
    public void IndependentRebuildRejectsPromotedOrLearnerClaims(string property)
    {
        var (source, output) = Files();
        File.WriteAllText(source, JsonSerializer.Serialize(CurrentSkillExperienceDeficitTests.Snapshot(10, 15000), Json));
        var raw = JsonSerializer.SerializeToNode(CurrentSkillTeacherPreferenceBuilder.Build(source), Json)!;
        raw[property] = true;
        File.WriteAllText(output, raw.ToJsonString(Json));
        Assert.Throws<InvalidDataException>(() => CurrentSkillTeacherPreferenceBuilder.Verify(output));
    }

    [Fact]
    public void MissingTimeIsAnExplicitBlockedResultNotAssumedOpen()
    {
        var (source, _) = Files();
        File.WriteAllText(source, JsonSerializer.Serialize(CurrentSkillExperienceDeficitTests.Snapshot(), Json));
        var report = CurrentSkillTeacherPreferenceBuilder.Build(source);
        Assert.Equal("blocked", report.Status);
        Assert.Contains(report.BlockingReasons, reason => reason.Contains("snapshot_field_unavailable:time.time", StringComparison.Ordinal));
        Assert.Empty(report.PairwisePreferences);
        Assert.False(report.TeacherPreferenceLabelEligible);
    }

    [Theory]
    [InlineData(500, 30)]
    [InlineData(660, 30)]
    [InlineData(1900, 30)]
    [InlineData(2600, 30)]
    [InlineData(600, -1)]
    public void RecoveryOrInvalidCurrentBudgetsStopBeforeCandidateProduction(int time, double energy)
    {
        var (source, _) = Files();
        var snapshot = CurrentSkillExperienceDeficitTests.Snapshot();
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["energy"] = JsonSerializer.SerializeToNode(new { status = "available", value = energy });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.State["time"] = JsonSerializer.SerializeToElement(new
            { time = new { status = "available", value = time } });
        snapshot.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(snapshot.State);
        File.WriteAllText(source, JsonSerializer.Serialize(snapshot, Json));
        var report = CurrentSkillTeacherPreferenceBuilder.Build(source);
        Assert.Equal("blocked", report.Status);
        Assert.Contains("skill_teacher_current_time_or_energy_unavailable_or_recovery_due", report.BlockingReasons);
        Assert.Empty(report.Candidates);
        Assert.False(report.TeacherPreferenceLabelEligible);
    }

    private static (string Source, string Output) Files()
    {
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-SkillTeacher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return (Path.Combine(root, "snapshot.json"), Path.Combine(root, "preference.json"));
    }
}
