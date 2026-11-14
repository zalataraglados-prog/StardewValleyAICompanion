using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeObservedRefillReusesOriginalClearanceProtocolWithoutPrivateTreePredictions()
    {
        using var f = new NativeRefillProductFixture(repair: true, nativeObserved: true);
        await f.Build();
        var compilation = f.Trace.Approach.Open();
        Assert.Single(compilation.ClearanceQueueItemIds);
        var trace = CropTeacherRefillApproachNativeTrace.OpenNativeObservedEffects(compilation, f.ReceiptPath,
            f.AfterPath, f.RunId, PolicyTrajectoryVersionPins.ProductExecutor);
        Assert.True(trace.NativeOutcomeVerified); Assert.True(trace.NativeEngineEffectsObserved);
        Assert.Equal(3, trace.NativeOutcomes.Count);
        Assert.Empty(trace.Steps); Assert.Empty(trace.ResourceEvidence);
        Assert.False(trace.SelectedResourceEffectsVerified); Assert.False(trace.ResourceCreditAuthorized);
        Assert.False(trace.FormalProductTrainingAuthorized);
        Assert.Throws<InvalidDataException>(() => CropTeacherRefillApproachNativeTrace.OpenNativeWithSelectedResourceEffects(
            compilation, f.ReceiptPath, f.AfterPath, f.RunId, PolicyTrajectoryVersionPins.ProductExecutor));
        var prep = OpenNativeObservedRefill(f);
        var scope = prep.VerifyTransition(f.Transition(prep, PolicyTrajectoryVersionPins.ProductExecutor));
        Assert.True(scope.RefillApproachVerified); Assert.True(scope.RefillNativeEffectsObserved);
        Assert.Equal(0, scope.AppliedTileCount); Assert.False(scope.NativeCropLifecycleVerified);
        var shared = f.Trace.Approach.Watering.Shared;
        var row = f.Transition(prep, PolicyTrajectoryVersionPins.ProductExecutor) with
        {
            NativeRefillPreparation = new(shared.Inputs.Plan.RequestPath, shared.Source.LedgerPath,
                shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-native.observed", f.Trace.Approach.ModelPath, "native_engine_observed")
        };
        var loaded = LifecycleLoad(new[] { row });
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.clear_obstacle"));
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.move_to_tile"));
        trace.ValidateInputsUnchanged(); scope.ValidateInputsUnchanged();
    }

    [Fact]
    public async Task NativeObservedRefillIsAnExplicitDurableOwnerModeThroughMainPreparationRebindAndReopen()
    {
        using var f = new NativeRefillProductFixture(repair: true, nativeObserved: true);
        var shared = f.Trace.Approach.Watering.Shared;
        shared.Runtime.LiveOptions.UseProductExecutor = true;
        var options = shared.Files.Options with { ExecutorVersion = PolicyTrajectoryVersionPins.ProductExecutor,
            UseSelectedRefillResourceEffects = true, UseNativeRefillOutcomeAuthority = true };
        await using var owner = await shared.Files.Open(options);
        var operation = await PrepareOwnerSelectedRefill(f.Trace, owner);
        Assert.False(shared.Runtime.LiveOptions.ObserveNativeOutputs);
        Assert.False(shared.Runtime.LiveOptions.ObserveNativeClearanceToolInputs);
        Assert.Equal("refill_approach_native", ReadOwnerSelectionIntent(shared)["maintenance_control_kind"]!.GetValue<string>());
        var boundary = ReadOwnerSelectionIntent(shared)["maintenance_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)!;
        var calls = 0;
        await owner.ExecuteOriginalWateringAsync(operation, shared.Runtime.Client, async () =>
        {
            calls++;
            await f.Build(retainCompilation: true);
            foreach (var resultPath in f.Results)
            {
                var result = JsonNode.Parse(File.ReadAllText(resultPath))!;
                result["mechanical_compiled_queue_path"] = boundary.QueuePath;
                Fixture.Save(resultPath, result);
            }
            var after = ReadSiteSnapshot(f.AfterPath);
            shared.Runtime.Store.Snapshots[after.StateHash] = after;
            var receipt = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!;
            receipt["after_snapshot_path"] = boundary.AfterSnapshotPath;
            Fixture.Save(boundary.ExecutionReceiptPath, receipt);
            Fixture.Save(boundary.AfterSnapshotPath, after);
            return new JsonObject { ["execution_path"] = boundary.ExecutionReceiptPath, ["after_snapshot_path"] = boundary.AfterSnapshotPath };
        });
        await owner.RunWateringMaintenanceAsync(operation, shared.Runtime.Client, (diagnostic, request) =>
            InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", shared.Runtime.Client,
                shared.Runtime.LiveOptions, diagnostic, request), shared.Files.Post);
        Assert.Null(owner.Pending); Assert.Equal(1, calls); Assert.Equal(1, shared.Runtime.Handler.Commits);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(options.EvidenceDirectory, "operation-000001-accepted.json")))!;
        Assert.True(accepted["native_refill_observed_outcome_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.Null(accepted["native_refill_resource_effects_verified"]);
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        await owner.DisposeAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => shared.Files.Open(options with { UseNativeRefillOutcomeAuthority = false }));
        await using var reopened = await shared.Files.Open(options);
        await reopened.RestoreHeadAsync(shared.Runtime.Client, shared.Files.Recovery, shared.Files.Post);
        Assert.Equal(boundary.AfterSnapshotPath, reopened.CurrentSnapshotPath);
        Assert.Equal(1, calls); Assert.Equal(1, shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task NativeObservedRefillCannotUpgradeALegacyOwnerOrApplyToOrdinaryWatering()
    {
        using var f = new NativeRefillProductFixture(repair: true, nativeObserved: true);
        var shared = f.Trace.Approach.Watering.Shared;
        await f.Trace.Approach.Compile();
        var options = shared.Files.Options with { UseSelectedRefillResourceEffects = true };
        await using var owner = await shared.Files.Open(options);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalWateringAsync(() => Task.FromResult(
            new CropTeacherOriginalWateringCompilation(f.Trace.Approach.Watering.Step, f.Trace.Approach.ModelPath,
                f.Trace.Approach.QueuePath, f.ReceiptPath, f.AfterPath, shared.Files.Native)
                { IsRefillApproach = true, UseSelectedRefillResourceEffects = true, UseNativeRefillOutcomeAuthority = true })));
        Assert.Null(owner.Pending); Assert.Equal(0, shared.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => shared.Files.Open(options with
            { UseSelectedRefillResourceEffects = false, UseNativeRefillOutcomeAuthority = true }));
    }

    private static CropTeacherWateringMaintenance.Preparation OpenNativeObservedRefill(NativeRefillProductFixture f)
    {
        var approach = f.Trace.Approach; var shared = approach.Watering.Shared;
        return CropTeacherWateringMaintenance.Preparation.OpenNativeObservedRefillApproach(shared.Inputs.Plan.RequestPath,
            shared.Source.SourcePath, shared.Source.LedgerPath, shared.Inputs.Plan.CoveredCommitmentIds,
            Id, "refill-native.observed", approach.ModelPath, approach.QueuePath);
    }
}
