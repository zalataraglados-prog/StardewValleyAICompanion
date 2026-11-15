using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeSharedMailWorkRetainsOneDailyChoiceThroughApproachOpenCloseAndReopen()
    {
        var f = new NativeMailWorkFixture(); using var host = f.Runtime;
        await using var owner = await f.Files.Open();
        f.Compile(owner.CurrentSnapshotPath);
        f.BindDecision(await SaveDaily(owner, f.Selection));
        var original = f.Selection;
        var plan = LifecycleRead<SmallModelPlanEnvelope>(original.DecisionPlanPath);
        var selectionType = typeof(LiveTrainingOptions).Assembly.GetType("Program")!.GetNestedType("CropTeacherDailySelection", BindingFlags.NonPublic)!;
        var liveSelection = Activator.CreateInstance(selectionType, LifecycleRead<SnapshotEnvelope>(original.DecisionSnapshotPath)
            .State["time"].GetProperty("total_days").GetProperty("value").GetInt32(), plan.GoalId,
            SelectedQueueDecisionLease.CreateFromPlan(plan, JsonNode.Parse(File.ReadAllText(original.DecisionRankingPath))!.AsObject()),
            new HashSet<string>(StringComparer.Ordinal), original.DecisionPlanPath, original.DecisionRankingPath)!;
        selectionType.GetProperty("SourceSnapshotPath")!.SetValue(liveSelection, original.DecisionSnapshotPath);
        var choices = new List<string>();
        foreach (var option in new[] { "executor.move_to_tile", "executor.interact", "executor.close_menu" })
        {
            Assert.Equal(option, Assert.Single(f.Queue.Items).OptionId);
            var before = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
            var operation = await owner.AdmitNativeSharedSourceYieldAsync(f.Selection, f.ReceiptPath, f.AfterPath, f.RuntimePath);
            Assert.True(operation.IsSharedSourceReturn);
            var claimCalls = 0;
            await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Runtime.Client,
                f.Runtime.LiveOptions, owner, operation, f.Selection.QueuePath, (Func<Task<JsonObject>>)(async () =>
                { claimCalls++; return await f.Emit(before); }));
            Assert.Equal(1, claimCalls); Assert.Null(owner.Pending);
            var trace = await owner.ReadJointProductionTraceAsync();
            Assert.Empty(trace.CropTrace.Transitions);
            var returned = trace.SharedSourceReturns.Last();
            Assert.True(returned.SourceReference!.AppliedNativeWorkQueue);
            var transition = returned.NativeTransition with { NativeSharedSource = returned.SourceReference };
            Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition));
            var scope = MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithNativeWork(transition);
            Assert.False(scope.CropProductionCreditAuthorized); Assert.Equal(1, scope.VerifiedNativeStepCount);
            var completedSelection = InvokeLive<object>("CompleteCropTeacherSharedSelection", owner, operation, liveSelection);
            if (option == "executor.close_menu")
                Assert.Single(SelectionProperty<IReadOnlySet<string>>(completedSelection, "CompletedForeignCandidateIds")!);
            else
            {
                Assert.Same(liveSelection, completedSelection);
                Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition with
                    { NativeSharedSource = returned.SourceReference with { AppliedNativeWorkQueue = false } }));
            }
            liveSelection = completedSelection;
            choices.Add(transition.SelectedCandidateId);
            var daily = (await ReadDaily(owner))!;
            Assert.Equal(original.DecisionReference, daily.GetType().GetProperty("Reference")!.GetValue(daily));
            if (option == "executor.close_menu") Assert.Single(DailyCompleted(daily));
            else
            {
                Assert.Empty(DailyCompleted(daily));
                f.Compile(owner.CurrentSnapshotPath);
            }
        }
        Assert.Equal(3, choices.Distinct(StringComparer.Ordinal).Count());
        var accepted = Assert.Single(DailyCompleted((await ReadDaily(owner))!));
        Assert.Contains("approach", accepted);
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.Equal(accepted, Assert.Single(DailyCompleted((await ReadDaily(reopened))!)));
        Assert.Equal(3, (await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns.Count);
        Assert.Equal(3, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSharedWorkUsesOriginalRuntimeOrProductWireAndCannotDowngradeItsSourceConsumer(bool product)
    {
        var f = new SharedYieldFixture(product); using var host = f.Native.Files.F.Runtime;
        f.Native.Files.F.Runtime.LiveOptions.UseProductExecutor = product;
        var options = f.Native.Files.F.Files.Options with { ExecutorVersion = product ?
            PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor };
        await using var owner = await f.Native.Files.F.Files.Open(options);
        var operation = await owner.AdmitNativeSharedSourceYieldAsync(f.Selection, f.Native.ReceiptPath,
            f.Native.AfterPath, f.Native.Files.F.Files.Native);
        await DispatchShared(f, owner, operation, async () =>
        {
            await f.Native.Build();
            var receipt = JsonNode.Parse(File.ReadAllText(f.Native.ReceiptPath))!;
            receipt["executor_version"] = options.ExecutorVersion; Fixture.Save(f.Native.ReceiptPath, receipt);
            return new JsonObject { ["execution_path"] = f.Native.ReceiptPath, ["after_snapshot_path"] = f.Native.AfterPath };
        });
        var row = Assert.Single((await owner.ReadJointProductionTraceAsync()).SharedSourceReturns);
        var transition = row.NativeTransition with { NativeSharedSource = row.SourceReference };
        Assert.True(MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithNativeWork(transition).AppliedNativeWorkQueue);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithSharedInventoryEffects(transition));
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open(options);
        await reopened.RestoreHeadAsync(f.Native.Files.F.Runtime.Client, f.Native.Files.F.Files.Recovery, f.Native.Files.F.Files.Post);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
    }

    [Theory]
    [InlineData("missing_objective")]
    [InlineData("wrong_option")]
    [InlineData("incomplete_queue")]
    [InlineData("failed_step")]
    [InlineData("inconsistent_success")]
    public async Task NativeSharedPendingWorkRejectsUnverifiedOrUnboundOutcomesWithoutReclaim(string fault)
    {
        var f = new NativeMailWorkFixture(); using var host = f.Runtime;
        await using var owner = await f.Files.Open(); f.Compile(owner.CurrentSnapshotPath);
        var operation = await owner.AdmitNativeSharedSourceYieldAsync(f.Selection, f.ReceiptPath, f.AfterPath, f.RuntimePath);
        await owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client);
        await f.Emit(LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath));
        var raw = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!;
        if (fault == "missing_objective") raw["objective_continuation"] = null;
        if (fault == "wrong_option") raw["objective_continuation"]!["option_id"] = "another.option";
        if (fault == "incomplete_queue") raw["executed_item_count"] = 0;
        if (fault == "failed_step") raw["step_results"]![0]!["primitive_verification_status"] = "failed";
        if (fault == "inconsistent_success") raw["success"] = true;
        Fixture.Save(f.ReceiptPath, raw);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.VerifySharedSourceExecutionAsync(operation));
        Assert.Same(operation, owner.Pending); Assert.Equal(0, f.Runtime.Handler.Commits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client));
    }

    [Fact]
    public async Task NativeSharedProductWorkCannotRelabelARuntimeRequestWire()
    {
        var f = new SiteTillNativeFixture(); using var host = f.Files.F.Runtime;
        await f.Build();
        var raw = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!;
        raw["executor_version"] = PolicyTrajectoryVersionPins.ProductExecutor; Fixture.Save(f.ReceiptPath, raw);
        var transition = f.Transition with { TransitionKind = "shared_source_return", ExecutorVersion = PolicyTrajectoryVersionPins.ProductExecutor };
        Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.OpenNativeWork(f.Files.F.Inputs.Plan.RequestPath,
            f.Files.F.Source.SourcePath, f.Files.F.Source.LedgerPath, f.Files.F.Inputs.Plan.CoveredCommitmentIds,
            transition, "declared.native-work.wrong-wire"));
    }
}
