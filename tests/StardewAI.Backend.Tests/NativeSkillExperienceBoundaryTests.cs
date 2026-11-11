using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeSkillExperienceBoundaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(374)]
    [InlineData(15000)]
    public void SharedSkillVerifierPreservesOriginalForagingContractAndSnapshots(int experience)
    {
        var f = NativeWildTreeClearanceBoundaryTests.Fixture(experience);
        var original = JsonSerializer.Serialize(new[] { f.Before, f.After });
        var result = Read(f.Before, f.After, "foraging", 2, experience, 16);
        Assert.Equal(experience, result.Before.Skills.Single(row => row.SkillIndex == 2).Experience);
        Assert.Equal(experience + 16, result.After.Skills.Single(row => row.SkillIndex == 2).Experience);
        Assert.Equal(original, JsonSerializer.Serialize(new[] { f.Before, f.After }));
    }

    [Theory]
    [InlineData("before-xp")]
    [InlineData("delta")]
    [InlineData("negative-before")]
    [InlineData("negative-delta")]
    [InlineData("overflow")]
    [InlineData("skill-id")]
    [InlineData("skill-index")]
    [InlineData("luck-index")]
    [InlineData("buff")]
    [InlineData("pending-missing")]
    [InlineData("pending-index")]
    [InlineData("unrelated-skill")]
    [InlineData("profession")]
    [InlineData("unknown-row")]
    [InlineData("unknown-envelope")]
    [InlineData("state-hash")]
    public void SharedSkillVerifierRejectsIncorrectContractOrUnrelatedMutation(string change)
    {
        var f = NativeWildTreeClearanceBoundaryTests.Fixture(90);
        var xp = 90; var delta = 16; var id = "foraging"; var index = 2;
        switch (change)
        {
            case "before-xp": xp = 91; break;
            case "delta": delta = 17; break;
            case "negative-before": xp = -1; break;
            case "negative-delta": delta = -1; break;
            case "overflow": xp = int.MaxValue; delta = 1; break;
            case "skill-id": id = "farming"; break;
            case "skill-index": index = 0; break;
            case "luck-index": index = 5; break;
            case "buff": Edit(row => { row["skills"]![2]!["effective_level"] = 2; row["skills"]![2]!["temporary_buff_delta"] = 1; }); break;
            case "pending-missing": Edit(row => row["new_levels"] = new JsonArray()); break;
            case "pending-index": Edit(row => row["new_levels"]![0]!["skill_index"] = 0); break;
            case "unrelated-skill": Edit(row => row["skills"]![0]!["experience"] = 101); break;
            case "profession": Edit(row => row["profession_ids"]!.AsArray().Add(12)); break;
            case "unknown-row": Edit(row => row["skills"]![2]!["future_persistent"] = "changed"); break;
            case "unknown-envelope":
                var player = JsonNode.Parse(f.After.State["player"].GetRawText())!;
                player["skills_detail"]!["future_semantic"] = "changed";
                f.After.State["player"] = JsonSerializer.SerializeToElement(player); break;
        }
        f.After.StateHash = change == "state-hash" ? "forged" : SnapshotHash.ComputeStateHash(f.After.State);
        Assert.Throws<InvalidDataException>(() => Read(f.Before, f.After, id, index, xp, delta));
        void Edit(Action<JsonNode> edit)
        {
            var player = JsonNode.Parse(f.After.State["player"].GetRawText())!;
            edit(player["skills_detail"]!["value"]!);
            f.After.State["player"] = JsonSerializer.SerializeToElement(player);
        }
    }

    private static (CurrentSkillExperienceDeficit Before, CurrentSkillExperienceDeficit After) Read(
        SnapshotEnvelope before, SnapshotEnvelope after, string id, int index, int xp, int delta)
    {
        var type = typeof(CropTeacherWateringMaintenance).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!;
        try { return ((CurrentSkillExperienceDeficit, CurrentSkillExperienceDeficit))type.GetMethod("VerifySkillExperienceDelta",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [before, after, id, index, xp, delta, "Test skill"] )!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
}
