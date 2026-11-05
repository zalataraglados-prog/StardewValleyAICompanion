using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed class SkillPolicyStateFeatureTests
{
    [Theory]
    [InlineData(6, 3300, 1500)]
    [InlineData(9, 10000, 5000)]
    [InlineData(10, 15000, null)]
    public void BothPolicyConsumersGetExactFiveSkillStateAndExplicitCaps(int level, int xp, int? next)
    {
        var snapshot = CurrentSkillExperienceDeficitTests.Snapshot(level, xp);
        var world = new WorldModelProjector().Project(snapshot, "goal.skills.raise_skill_levels", "training_singleplayer");
        var features = new PolicyStateFeatureProjector().Project(world, snapshot);
        foreach (var skill in new[] { "farming", "fishing", "foraging", "mining", "combat" })
        {
            var prefix = "player.skills." + skill + ".";
            Assert.Equal(xp, features.Numeric.Single(row => row.Name == prefix + "experience").Value);
            Assert.Equal(level, features.Numeric.Single(row => row.Name == prefix + "unmodified_level").Value);
            Assert.Equal(level == 10, features.Boolean.Single(row => row.Name == prefix + "at_vanilla_level_cap").Value);
            if (next.HasValue) Assert.Equal(next.Value, features.Numeric.Single(row => row.Name == prefix + "experience_to_next_level").Value);
            else Assert.DoesNotContain(features.Numeric, row => row.Name == prefix + "experience_to_next_level");
        }
        Assert.DoesNotContain(features.Numeric, row => row.Name.Contains("skills.luck", StringComparison.Ordinal));
        Assert.Equal(features.Numeric.Length, features.Numeric.Select(row => row.Name).Distinct().Count());
    }

    [Fact]
    public void OtherGoalsRetainTheOriginalFeatureBytes()
    {
        var snapshot = CurrentSkillExperienceDeficitTests.Snapshot();
        var world = new WorldModelProjector().Project(snapshot, "goal.collection.master_angler", "training_singleplayer");
        var projector = new PolicyStateFeatureProjector();
        Assert.Equal(JsonSerializer.Serialize(projector.Project(world)), JsonSerializer.Serialize(projector.Project(world, snapshot)));
    }

    [Theory]
    [InlineData("unreadable")]
    [InlineData("hash")]
    [InlineData("world")]
    public void UnknownOrCrossStateSkillsNeverBecomeZeroPolicyProgress(string mutation)
    {
        var snapshot = CurrentSkillExperienceDeficitTests.Snapshot();
        var world = new WorldModelProjector().Project(snapshot, "goal.skills.raise_skill_levels", "training_singleplayer");
        if (mutation == "unreadable")
        {
            var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
            player["skills_detail"]!["status"] = "unavailable";
            snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            world.StateHash = snapshot.StateHash;
        }
        if (mutation == "hash") snapshot.StateHash = world.StateHash = "wrong";
        if (mutation == "world") world.StateHash = "other";
        if (mutation == "world")
            Assert.Throws<ArgumentException>(() => new PolicyStateFeatureProjector().Project(world, snapshot));
        else
            Assert.Throws<InvalidOperationException>(() => new PolicyStateFeatureProjector().Project(world, snapshot));
    }
}
