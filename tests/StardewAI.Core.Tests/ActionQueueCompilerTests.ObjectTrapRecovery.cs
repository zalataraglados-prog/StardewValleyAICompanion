using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompileObjectTrapRecoveryDelegatesToSafeMachineRemoval()
    {
        var snapshot = MachineRemovalSnapshot(safe: true);
        var request = Request(snapshot.StateHash, "recovery.escape_object_trap");
        request.Actions[0].Parameters = Array.Empty<SmallModelActionParameter>();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("remove_machine", step.StepType);
        Assert.Contains(
            "intent=object_trap_recovery:Farm:61,15->60,15",
            step.Target,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine_recovery[(BC)13]=debris_or_native_auto_collected_inventory",
            step.ExpectedEffect,
            StringComparison.Ordinal);
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            row => row.Name == "execution_option_id" && row.Value == "executor.remove_machine");
        Assert.DoesNotContain(
            item.NormalizedCommand.Parameters,
            row => row.Name == "native_null_tool_dispatch");
    }

    [Fact]
    public void ObjectTrapRecoveryCandidatePublishesCompilerSelectedTarget()
    {
        var snapshot = MachineRemovalSnapshot(safe: true);

        var option = new StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "recovery.escape_object_trap" },
                includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("recovery_escape_object_trap", candidate.Kind);
        Assert.Equal(60, candidate.TileX);
        Assert.Equal(15, candidate.TileY);
        Assert.Contains(
            candidate.Parameters,
            row => row.Name == "execution_option_id" &&
                row.Value == "executor.remove_machine");

        var planCandidate = new StardewAI.Contracts.Training.PolicyEventCandidatePrediction
        {
            CandidateId = candidate.CandidateId,
            OptionId = "recovery.escape_object_trap",
            Kind = candidate.Kind,
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = candidate.LocationId,
            TileX = candidate.TileX,
            TileY = candidate.TileY,
            ExpectedEffect = candidate.ExpectedEffect,
            EstimatedTicks = candidate.EstimatedTicks,
            Parameters = candidate.Parameters
        };
        var plan = new StardewAI.Core.Training.DailyPlanCompiler()
            .Compile(new[] { planCandidate }, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("escape_object_trap", step.Kind);
        Assert.Equal(60, step.TargetTileX);
        Assert.Equal(15, step.TargetTileY);
    }

    [Fact]
    public void CompileObjectTrapRecoveryRejectsUnsafeMachineProjection()
    {
        var snapshot = MachineRemovalSnapshot(safe: false);
        var request = Request(snapshot.StateHash, "recovery.escape_object_trap");
        request.Actions[0].Parameters = ObjectTrapParameters();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "remove_machine_safety_projection_blocked",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileObjectTrapRecoveryRejectsSnapshotWithoutExactFourWayTrap()
    {
        var snapshot = MachineRemovalSnapshot(safe: true, trapped: false);
        var request = Request(snapshot.StateHash, "recovery.escape_object_trap");
        request.Actions[0].Parameters = ObjectTrapParameters();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "object_trap_four_cardinal_non_passable_objects_not_observed",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void ObjectTrapRecoveryHasNoSecondRemovalOrNullToolRuntime()
    {
        var root = FindObjectTrapRepositoryRoot();
        var compiler = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.Core",
            "Execution",
            "ActionQueueCompiler.ObjectTrapRecovery.cs"));
        var bridge = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.ObjectTrapRecovery.cs"));
        var runtime = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));

        Assert.Contains("CompileRemoveMachineStep", compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(null)", compiler, StringComparison.Ordinal);
        Assert.Contains("destructive_native_fallback_enabled = false", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "pending.Request.OptionId == \"recovery.escape_object_trap\"",
            runtime,
            StringComparison.Ordinal);
    }

    private static SmallModelActionParameter[] ObjectTrapParameters() =>
        new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "60" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" }
        };

    private static string FindObjectTrapRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Unable to locate StardewAI repository root.");
    }
}
