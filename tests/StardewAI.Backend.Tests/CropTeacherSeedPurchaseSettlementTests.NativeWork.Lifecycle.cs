using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeSharedWorkPreservesTheWholeCropTrajectoryWithoutCreditingForeignMovement()
    {
        var f = new LifecycleControlsFixture(combinedRefill: true); await f.Build();
        var rows = f.Rows.ToArray();
        var index = Array.FindIndex(rows, row => row.NativeRefillPreparation is not null);
        var original = rows[index]; var binding = original.NativeRefillPreparation!;
        // Set the declared aggregate's version to its original Product wire before binding.
        var aggregate = JsonNode.Parse(File.ReadAllText(original.ExecutionReceiptPath))!;
        aggregate["executor_version"] = original.ExecutorVersion; Fixture.Save(original.ExecutionReceiptPath, aggregate);
        var shared = original with { TransitionKind = "shared_source_return", NativeRefillPreparation = null };
        const string step = "declared.native-work.whole-lifecycle";
        var scope = CropTeacherSharedSourceReturn.OpenNativeWork(binding.RunRequestPath, shared.BeforeSnapshotPath,
            binding.LedgerPath, binding.CoveredCommitmentIds, shared, step);
        rows[index] = shared with { NativeSharedSource = new(binding.RunRequestPath, binding.LedgerPath,
            binding.CoveredCommitmentIds, step, scope.InputHashes) { AppliedNativeWorkQueue = true } };
        Assert.Empty((Array)f.ValidateJoint(rows, appliedNativeWork: true));
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.ValidateJoint(rows)).InnerException);
        var loaded = rows.Select(row => LifecycleInvoke("LoadNativeWorkObservedTransition", row)).ToArray();
        var values = Array.CreateInstance(loaded[0].GetType(), loaded.Length);
        for (var item = 0; item < loaded.Length; item++) values.SetValue(loaded[item], item);
        Assert.Equal(6, LifecycleInvoke("CountOption", values, "executor.plant_seed"));
        Assert.Equal(6, LifecycleInvoke("CountOption", values, "executor.harvest_crop"));
        Assert.Equal(0, LifecycleInvoke("CountOption", values, "executor.move_to_tile"));
        Assert.False(scope.CropProductionCreditAuthorized);
        foreach (var bad in new[] { rows.Where((_, position) => position != index).ToArray(),
            rows.Take(index).Concat(new[] { rows[index], rows[index] }).Concat(rows.Skip(index + 1)).ToArray() })
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
                f.ValidateJoint(bad, appliedNativeWork: true)).InnerException);
    }
}
