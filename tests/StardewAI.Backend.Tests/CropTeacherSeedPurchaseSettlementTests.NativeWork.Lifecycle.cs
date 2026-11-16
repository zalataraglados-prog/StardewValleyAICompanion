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

        // Exercise the public request/rebuild entry too, not only its private leaf loader.
        var capacityRequest = LifecycleRead<MoneyRevenueCropDailyCapacityProofRequest>(f.CapacityRequestPath);
        capacityRequest.NativeDayTransitions = rows;
        var days = rows.GroupBy(row => row.TotalDay).ToDictionary(group => group.Key, group =>
            (group.FirstOrDefault(row => row.TransitionKind == "crop_actions") ??
             group.FirstOrDefault(row => row.TransitionKind == "supporting_route_to_sleep") ?? group.First()).BeforeSnapshotPath);
        capacityRequest.DailySnapshotPaths = capacityRequest.DailySnapshotPaths.Select(path =>
            days[LifecycleRead<StardewAI.Contracts.State.SnapshotEnvelope>(path).State["time"]
                .GetProperty("total_days").GetProperty("value").GetInt32()]).ToArray();
        var capacityPath = Path.Combine(f.Root, "native-work-capacity-request.json");
        Fixture.Save(capacityPath, capacityRequest);
        var capacityProofPath = Path.Combine(f.Root, "native-work-capacity-proof.json");
        Fixture.Save(capacityProofPath, MoneyRevenueCropDailyCapacityProofBuilder.BuildNativeObserved(capacityPath));
        var request = f.OriginalRequest;
        request.SchemaVersion = "money_revenue_crop_native_lifecycle_proof_request.v9";
        request.Transitions = rows; request.DailyCapacityProofPath = capacityProofPath;
        var requestPath = Path.Combine(f.Root, "native-work-lifecycle-request.json"); Fixture.Save(requestPath, request);
        var proof = MoneyRevenueCropNativeLifecycleProofBuilder.Build(requestPath);
        Assert.True(proof.NativeCropLifecycleVerified); Assert.False(proof.FormalProductTrainingAuthorized);
        Assert.Equal(6, proof.VerifiedHarvestActionCount);
        var proofPath = Path.Combine(f.Root, "native-work-lifecycle-proof.json"); Fixture.Save(proofPath, proof);
        var retentionPath = Path.Combine(f.Root, "native-work-retention-request.json");
        Fixture.Save(retentionPath, new MoneyRevenueCropOutputRetentionProofRequest
        {
            SchemaVersion = "money_revenue_crop_output_retention_proof_request.v7", CommitmentId = Id,
            WaveIndex = LifecycleRead<MoneyRevenueCropProductionObligationPlan>(request.CropProductionPlanPath)
                .Commitments.Single(row => row.CommitmentId == Id).HarvestWaves.Single().WaveIndex,
            CropProductionPlanPath = request.CropProductionPlanPath, NativeLifecycleProofPath = proofPath,
            RetainThroughSnapshotPath = rows[^1].AfterSnapshotPath
        });
        var retention = MoneyRevenueCropOutputRetentionProofBuilder.Build(retentionPath);
        Assert.Equal("money_revenue_crop_output_retention_proof.v6", retention.SchemaVersion);
        Assert.True(MoneyRevenueCropOutputRetentionProofBuilder.HasProducerRetention(retention));
        Assert.False(MoneyRevenueCropOutputRetentionProofBuilder.HasReservedProducerRetention(retention));
        Assert.Equal(6, retention.ProducerRetention!.RetainedQuantity);
        Assert.False(retention.FormalProductTrainingAuthorized);
    }
}
