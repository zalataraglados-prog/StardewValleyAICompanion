using System.Reflection;
using System.Security.Cryptography;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeLifecycleConsumesPreWaterInterBatchAndFinalControlsWithoutCropCredit()
    {
        var f = new LifecycleControlsFixture(); await f.Build();
        var paths = f.Rows.SelectMany(row => new[] { row.QueuePath, row.ExecutionReceiptPath }).ToArray();
        var hashes = paths.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
        Assert.Empty((Array)f.Validate());
        var loaded = LifecycleLoad(f.Rows);
        var controls = f.Rows.Count(row => row.SelectedCandidateId == StardewAI.Core.Training.NativeControlSettleRecovery.CandidateId);
        Assert.Equal(4, controls);
        Assert.Equal(2, LifecycleInvoke("CountOption", loaded, "executor.plant_seed"));
        Assert.Equal(2, LifecycleInvoke("CountOption", loaded, "executor.harvest_crop"));
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.wait_ticks"));
        for (var i = 0; i < paths.Length; i++) Assert.Equal(hashes[i], SHA256.HashData(File.ReadAllBytes(paths[i])));
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.Validate(native: false)).InnerException);
        foreach (var fault in new[] { "duplicate", "missing", "reordered", "misbinding" })
        {
            var rows = f.Rows.ToList(); var index = rows.FindIndex(row => row.SelectedCandidateId == StardewAI.Core.Training.NativeControlSettleRecovery.CandidateId);
            if (fault == "duplicate") rows.Insert(index, rows[index]);
            if (fault == "missing") rows.RemoveAt(index + 1);
            if (fault == "reordered") (rows[index], rows[index + 1]) = (rows[index + 1], rows[index]);
            if (fault == "misbinding") rows[index] = rows[index] with { SelectedCandidateId = "unrelated" };
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.Validate(rows)).InnerException);
        }
    }

    [Fact]
    public async Task NativeLifecycleReopensOriginalProductRefillPreparationWithoutProductionCredit()
    {
        using var f = new NativeRefillProductFixture(); await f.Build();
        var approach = f.Trace.Approach; var shared = approach.Watering.Shared;
        var preparation = f.OpenPreparation(selected: false, native: true);
        var row = f.Transition(preparation, StardewAI.Contracts.Training.PolicyTrajectoryVersionPins.ProductExecutor) with
        {
            NativeRefillPreparation = new(shared.Inputs.Plan.RequestPath, shared.Source.LedgerPath,
                shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-approach.original", approach.ModelPath, "native_resource_neutral")
        };
        var hashes = new[] { row.QueuePath, row.ExecutionReceiptPath }.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
        var loaded = LifecycleLoad(new[] { row });
        Assert.True((bool)loaded.GetValue(0)!.GetType().GetProperty("NativeNonProduction")!.GetValue(loaded.GetValue(0))!);
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.move_to_tile"));
        Assert.Equal(hashes[0], SHA256.HashData(File.ReadAllBytes(row.QueuePath)));
        Assert.Equal(hashes[1], SHA256.HashData(File.ReadAllBytes(row.ExecutionReceiptPath)));
        foreach (var bad in new[] { row with { SelectedCandidateId = "unrelated" }, row with
                 { NativeRefillPreparation = row.NativeRefillPreparation with { CommitmentId = "unrelated" } } })
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => LifecycleLoad(new[] { bad })).InnerException);
        Assert.DoesNotContain("native_refill_preparation", System.Text.Json.JsonSerializer.Serialize(row with { NativeRefillPreparation = null }, JsonOptions));
    }
}
