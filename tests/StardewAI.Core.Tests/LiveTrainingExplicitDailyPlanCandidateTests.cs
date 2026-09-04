namespace StardewAI.Core.Tests;

public sealed class LiveTrainingExplicitDailyPlanCandidateTests
{
    [Fact]
    public void ParsesExplicitDailyPlanCandidateParameters()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--daily-plan-candidate-options", "inventory.transfer_item",
            "--daily-plan-explicit-confirmation",
            "--daily-plan-invocation-source", "PlayerCommand",
            "--daily-plan-candidate-parameter", "source_node_id=chest:Farm:60,15",
            "--daily-plan-candidate-parameter", "quantity=2"
        });

        Assert.Equal(new[] { "inventory.transfer_item" }, options.DailyPlanCandidateOptionIds);
        Assert.True(options.DailyPlanExplicitConfirmationGranted);
        Assert.Equal(StardewAI.Contracts.Options.OptionInvocationSource.PlayerCommand, options.DailyPlanInvocationSource);
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
    public void ParsesNativeSaveBoundaryControls()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--require-native-save-boundary",
            "--save-isolation-path", "test-saves",
            "--save-slot", "Farm_123",
            "--save-boundary-max-attempts", "7",
            "--use-daily-plan"
        });

        Assert.True(options.RequireNativeSaveBoundary);
        Assert.Equal("Farm_123", options.SaveSlot);
        Assert.Equal(7, options.SaveBoundaryMaxAttempts);
    }

    [Fact]
    public void MainLoopUsesGenericObjectiveCompletionForExitAndReport()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));

        Assert.Contains(
            "objectiveCompleted |= execution[",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"objective_continuation_completed\"]",
            source,
            StringComparison.Ordinal);
        Assert.Contains("TrainingCompletionPolicy.Decide", source, StringComparison.Ordinal);
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

        Assert.Contains("candidateParameters.Count > 0", source, StringComparison.Ordinal);
        Assert.Contains("parameters = candidateParameters", source, StringComparison.Ordinal);
        Assert.Contains("explicit_confirmation_granted = options.DailyPlanExplicitConfirmationGranted", source, StringComparison.Ordinal);
        Assert.Contains("invocation_source = options.DailyPlanInvocationSource", source, StringComparison.Ordinal);
        Assert.True(
            source.Split("explicit_confirmation_granted = options.DailyPlanExplicitConfirmationGranted", StringSplitOptions.None).Length >= 3,
            "Initial and continuation candidates must both retain explicit confirmation.");
        Assert.True(
            source.Split("invocation_source = options.DailyPlanInvocationSource", StringSplitOptions.None).Length >= 3,
            "Initial and continuation candidates must both retain the invocation source.");
        var initialSelectionStart = source.IndexOf(
            "BuildQueueFromDailyPlanAsync(",
            StringComparison.Ordinal);
        var selectedCandidateCompileStart = source.IndexOf(
            "BuildQueueFromSelectedCandidateAsync(",
            initialSelectionStart,
            StringComparison.Ordinal);
        Assert.True(initialSelectionStart >= 0);
        Assert.True(selectedCandidateCompileStart > initialSelectionStart);
        Assert.DoesNotContain(
            "ContinuationRequestParameters(",
            source[initialSelectionStart..selectedCandidateCompileStart],
            StringComparison.Ordinal);
        Assert.Contains("ContinuationRequestParameters(effectiveContinuation)", source, StringComparison.Ordinal);
        Assert.Contains("\"continuation.\" + property.Key", source, StringComparison.Ordinal);
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
