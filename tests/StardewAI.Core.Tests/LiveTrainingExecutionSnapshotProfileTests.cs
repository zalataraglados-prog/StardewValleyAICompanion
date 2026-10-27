using StardewAI.LiveTrainingLoop;
using StardewAI.Contracts.Goals;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingExecutionSnapshotProfileTests
{
    [Fact]
    public void FullProfileRemainsTheDefault()
    {
        var options = LiveTrainingOptions.Parse(Array.Empty<string>());

        Assert.Equal("full", options.ExecutionSnapshotProfile);
    }

    [Fact]
    public void ExplicitExecutionProfileIsParsed()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--execution-snapshot-profile",
            "social"
        });

        Assert.Equal("social", options.ExecutionSnapshotProfile);
    }

    [Fact]
    public void SnapshotFileProfileIsOptionalAndParsedSeparately()
    {
        Assert.Empty(LiveTrainingOptions.Parse(Array.Empty<string>())
            .SnapshotFileProfile);

        var options = LiveTrainingOptions.Parse(new[]
        {
            "--snapshot-file-profile",
            "farm_layout"
        });

        Assert.Equal("farm_layout", options.SnapshotFileProfile);
        Assert.Equal("full", options.ExecutionSnapshotProfile);
    }

    [Fact]
    public void EmptySnapshotFileProfileIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--snapshot-file-profile",
            " "
        }));
    }

    [Fact]
    public void EmptyExecutionProfileIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--execution-snapshot-profile",
            " "
        }));
    }

    [Fact]
    public void SnapshotArtifactsRemainPlainByDefault()
    {
        var options = LiveTrainingOptions.Parse(Array.Empty<string>());

        Assert.Equal(
            ContentAddressedJsonArtifactStore.PlainMode,
            options.SnapshotArtifactMode);
    }

    [Fact]
    public void ContentAddressedSnapshotArtifactsCanBeEnabled()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--snapshot-artifact-mode",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode
        });

        Assert.Equal(
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
            options.SnapshotArtifactMode);
    }

    [Fact]
    public void UnknownSnapshotArtifactModeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--snapshot-artifact-mode",
            "lossy"
        }));
    }

    [Fact]
    public void GoldenWalnutEvidenceCaptureRequiresContentAddressedArtifacts()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--run-id",
            "fresh-save-run",
            "--capture-golden-walnut-fresh-save-evidence"
        }));
    }

    [Fact]
    public void GoldenWalnutEvidenceCaptureHasDeterministicManifestIdentity()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--run-id",
            "fresh-save-run",
            "--snapshot-artifact-mode",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
            "--capture-golden-walnut-fresh-save-evidence"
        });

        Assert.True(options.CaptureGoldenWalnutFreshSaveEvidence);
        Assert.Equal(
            "golden-walnut-fresh-save:fresh-save-run",
            options.GoldenWalnutFreshSaveScheduleId);
        Assert.EndsWith(
            "golden-walnut-fresh-save-episode-manifest.json",
            options.GoldenWalnutFreshSaveEpisodeManifestPath,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(GrandpaEvaluationGoalDefinition.GoalId)]
    [InlineData(GrandpaEvaluationGoalDefinition.StrategicGoal)]
    [InlineData(GrandpaEvaluationGoalDefinition.MaximumScoreEvidenceGoal)]
    public void GoldenWalnutEvidenceCaptureAcceptsEveryMaximumScoreGoalAlias(
        string goalId)
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--run-id",
            "fresh-save-run",
            "--goal",
            goalId,
            "--snapshot-artifact-mode",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
            "--capture-golden-walnut-fresh-save-evidence"
        });

        Assert.Equal(goalId, options.Goal);
    }

    [Fact]
    public void GoldenWalnutEvidenceCaptureRejectsPartialSnapshotProfile()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--run-id",
            "fresh-save-run",
            "--snapshot-artifact-mode",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
            "--execution-snapshot-profile",
            "social",
            "--capture-golden-walnut-fresh-save-evidence"
        }));
    }

    [Fact]
    public void GoldenWalnutLongRunRequiresEvidenceCapture()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--require-golden-walnut-source-schedule-ready"
        }));
    }

    [Fact]
    public void GoldenWalnutLongRunRequiresDailyPlanAndDurableBoundary()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--run-id",
            "fresh-save-run",
            "--snapshot-artifact-mode",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
            "--capture-golden-walnut-fresh-save-evidence",
            "--require-golden-walnut-source-schedule-ready"
        }));
    }

    [Fact]
    public void GoldenWalnutLongRunHasDeterministicSchedulePath()
    {
        var authorityPath = FindRepositoryFile(
            "StardewValleyAICompanion.sln");
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--run-id",
            "fresh-save-run",
            "--snapshot-artifact-mode",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
            "--capture-golden-walnut-fresh-save-evidence",
            "--require-golden-walnut-source-schedule-ready",
            "--use-daily-plan",
            "--require-native-save-boundary",
            "--stage-one-collection-requirement-inventory",
            authorityPath,
            "--stage-one-collection-acquisition-lowering",
            authorityPath,
            "--master-angler-stage-one-window-index",
            authorityPath,
            "--route-timing-calibration",
            authorityPath
        });

        Assert.True(options.RequireGoldenWalnutSourceScheduleReady);
        Assert.EndsWith(
            "golden-walnut-fresh-save-source-schedule.json",
            options.GoldenWalnutFreshSaveSourceSchedulePath,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GoldenWalnutLongRunRequiresCollectionTeacherAuthorityArtifacts()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            LiveTrainingOptions.Parse(new[]
            {
                "--run-id",
                "fresh-save-run",
                "--snapshot-artifact-mode",
                ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
                "--capture-golden-walnut-fresh-save-evidence",
                "--require-golden-walnut-source-schedule-ready",
                "--use-daily-plan",
                "--require-native-save-boundary"
            }));

        Assert.Contains(
            "--stage-one-collection-requirement-inventory",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GoldenWalnutLongRunInjectsExistingCollectionTeacherBeforeFallback()
    {
        var program = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));
        var teacher = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneCollectionTeacher.cs"));

        Assert.Contains(
            "TryBuildStageOneCollectionTeacherQueueAsync",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "MasterAnglerTargetDateIntentBuilder.Build",
            teacher,
            StringComparison.Ordinal);
        Assert.Contains(
            "CurrentStageOneCollectionTeacherPreferenceBuilder.Build",
            teacher,
            StringComparison.Ordinal);
        Assert.Contains(
            "TeacherPreferenceQueueLoader.LoadAsync",
            teacher,
            StringComparison.Ordinal);
        Assert.Contains(
            "HasPriorityStoryEventInterrupt(snapshot)",
            teacher,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionTeacherKeepsItsSelectedCandidateLeaseAcrossRollingWaits()
    {
        var program = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));
        var execution = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs"));
        var teacherBranchStart = program.IndexOf(
            "if (collectionTeacher.Queue is not null)",
            StringComparison.Ordinal);
        var teacherBranchEnd = program.IndexOf(
            "stageOneCollectionTeacherQueueCount++;",
            teacherBranchStart,
            StringComparison.Ordinal);

        Assert.True(teacherBranchStart >= 0);
        Assert.True(teacherBranchEnd > teacherBranchStart);
        var teacherBranch = program[teacherBranchStart..teacherBranchEnd];
        Assert.Contains(
            "useDailyPlanDecisionLease = true;",
            teacherBranch,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "useDailyPlanDecisionLease = false;",
            teacherBranch,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildQueueFromSelectedCandidateAsync(",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "policy_model_invoked_for_step\"] = false",
            execution,
            StringComparison.Ordinal);
        Assert.Contains(
            "HasPriorityStoryEventInterrupt(currentBeforeSnapshot)",
            execution,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GoldenWalnutLongRunUsesMilestoneThenExistingEpisodeScheduleProof()
    {
        var program = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));
        var milestone = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.GoldenWalnutMilestone.cs"));

        Assert.Contains(
            "GoldenWalnutFreshSaveMilestoneEvaluator.Evaluate",
            milestone,
            StringComparison.Ordinal);
        Assert.Contains(
            "GoldenWalnutFreshSaveEpisodeScheduleBuilder.Write",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "cropTeacherFirstDayCompleted &&\n" +
            "        !options.RequireGoldenWalnutSourceScheduleReady",
            program.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AfterExecutionReadsTheConfiguredProfile()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.QueueInspection.cs"));

        Assert.Contains("options.ExecutionSnapshotProfile", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadFullSnapshotAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterExecutionForcesOneSameTickCaptureForPausedMenus()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.QueueInspection.cs"));
        var refresh = source.IndexOf(
            "var sameTickRefresh = await ReadExecutionSnapshotAsync(",
            StringComparison.Ordinal);
        var poll = source.IndexOf(
            "await Task.Delay(options.AfterSnapshotPollMs);",
            refresh,
            StringComparison.Ordinal);

        Assert.True(refresh >= 0);
        Assert.True(poll > refresh);
        Assert.Contains("same_tick_state_hash_changed", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InitialSnapshotFileCanUseItsOwnIngestProfile()
    {
        var program = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));
        var http = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.JsonHttp.cs"));

        Assert.Contains("options.SnapshotFileProfile", program,
            StringComparison.Ordinal);
        Assert.Contains("string? explicitProfile = null", http,
            StringComparison.Ordinal);
        Assert.Contains("Uri.EscapeDataString(explicitProfile)", http,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
