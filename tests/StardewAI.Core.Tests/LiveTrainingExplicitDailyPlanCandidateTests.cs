namespace StardewAI.Core.Tests;

public sealed class LiveTrainingExplicitDailyPlanCandidateTests
{
    [Fact]
    public void ParsesExplicitDailyPlanCandidateParameters()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--daily-plan-candidate-options", "inventory.transfer_item",
            "--daily-plan-candidate-parameter", "source_node_id=chest:Farm:60,15",
            "--daily-plan-candidate-parameter", "quantity=2"
        });

        Assert.Equal(new[] { "inventory.transfer_item" }, options.DailyPlanCandidateOptionIds);
        Assert.Collection(
            options.DailyPlanCandidateParameters,
            parameter =>
            {
                Assert.Equal("source_node_id", parameter.Name);
                Assert.Equal("chest:Farm:60,15", parameter.Value);
            },
            parameter =>
            {
                Assert.Equal("quantity", parameter.Name);
                Assert.Equal("2", parameter.Value);
            });
    }

    [Fact]
    public void RejectsExplicitParametersWithoutExactlyOneOption()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--daily-plan-candidate-parameter", "quantity=2"
        }));

        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--daily-plan-candidate-options", "inventory.transfer_item,farm.process_machines",
            "--daily-plan-candidate-parameter", "quantity=2"
        }));
    }

    [Fact]
    public void ParsesGenericObjectiveCompletionStopCondition()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--stop-after-objective-complete"
        });

        Assert.True(options.StopAfterObjectiveComplete);
        Assert.False(options.StopAfterSocialObjectiveComplete);
    }

    [Fact]
    public void MainLoopUsesGenericObjectiveCompletionForExitAndReport()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));

        Assert.Contains(
            "execution[\"objective_continuation_completed\"]",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "!options.StopAfterObjectiveComplete || !objectiveCompleted",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ObjectiveCompleted = objectiveCompleted",
            source,
            StringComparison.Ordinal);

        var queueSource = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.QueueBuilding.cs"));
        Assert.Contains(
            "ranking[\"objective_continuation_filter\"]",
            queueSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "typed_objective_identity_match;fail_closed_no_objective_switch",
            queueSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RankingRequestUsesTypedCandidateInsteadOfBareOptionId()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.QueueBuilding.cs"));

        Assert.Contains("options.DailyPlanCandidateParameters.Count > 0", source, StringComparison.Ordinal);
        Assert.Contains("parameters = options.DailyPlanCandidateParameters", source, StringComparison.Ordinal);
        Assert.Contains("explicitCandidates.Length == 0", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
