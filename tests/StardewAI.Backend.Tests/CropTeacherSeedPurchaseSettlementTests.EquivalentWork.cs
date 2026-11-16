using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false, "valid")]
    [InlineData(true, "valid")]
    [InlineData(true, "aggregate_status")]
    [InlineData(true, "result_status")]
    [InlineData(true, "replan_flag")]
    public async Task EquivalentWorkUsesOriginalProgressPolicyWithoutPromotingNativePlay(bool product, string fault)
    {
        using var f = new EquivalentWorkFixture(product);
        await using var owner = await f.Files.Open(f.Options);
        f.Compile(owner.CurrentSnapshotPath);
        var before = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        var operation = await owner.AdmitNativeSharedSourceYieldAsync(f.Selection, f.ReceiptPath, f.AfterPath, f.RuntimePath);
        Task<JsonObject> Execute() => InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Runtime.Client,
            f.Runtime.LiveOptions, owner, operation, f.Selection.QueuePath, (Func<Task<JsonObject>>)(() => f.Emit(before, fault)));
        if (fault != "valid")
        {
            await Assert.ThrowsAsync<InvalidDataException>(Execute);
            Assert.Same(operation, owner.Pending);
            Assert.False(File.Exists(Path.Combine(f.RuntimePath, "shared-source-native-result.json")));
            return;
        }
        await Execute();
        Assert.Null(owner.Pending);
        var row = Assert.Single((await owner.ReadJointProductionTraceAsync()).SharedSourceReturns);
        var transition = row.NativeTransition with { NativeSharedSource = row.SourceReference };
        var scope = MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithNativeWork(transition);
        Assert.Equal(0, scope.VerifiedNativeStepCount); Assert.Equal(1, scope.SimulatedEquivalentStepCount);
        Assert.False(scope.CropProductionCreditAuthorized); Assert.False(scope.NativeCropLifecycleVerified);
        Assert.False(scope.TeacherPreferenceLabelEligible); Assert.False(scope.FormalProductTrainingAuthorized);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition));
        var acceptedPath = Path.Combine(f.Options.EvidenceDirectory, "operation-000001-accepted.json");
        var accepted = JsonNode.Parse(File.ReadAllText(acceptedPath))!;
        Assert.Equal(0, accepted["shared_verified_native_step_count"]!.GetValue<int>());
        Assert.Equal(1, accepted["shared_simulated_equivalent_step_count"]!.GetValue<int>());
        Assert.False(accepted["native_perfect_play_verified"]!.GetValue<bool>());
        var oldRead = typeof(CropTeacherSharedSourceReturn).Assembly.GetType("StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader")!
            .GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => oldRead.Invoke(null,
            [f.Selection.QueuePath, f.ResultPath, f.AfterPath])).InnerException);
        Assert.Equal("simulated_equivalent", JsonNode.Parse(File.ReadAllText(f.ResultPath))!["primitive_verification_status"]!.GetValue<string>());
        var commits = f.Runtime.Handler.Commits;
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open(f.Options);
        await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        Assert.Equal(commits, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("applied", "verified", "executor.move_to_tile", true)]
    [InlineData("applied", "simulated_equivalent", "executor.play_prairie_king", true)]
    [InlineData("applied", "simulated_equivalent", "executor.play_junimo_kart", true)]
    [InlineData("applied", "simulated_equivalent", "executor.move_to_tile", false)]
    [InlineData("applied", "simulated_equivalent", "executor.cook_recipe", false)]
    [InlineData("blocked", "simulated_equivalent", "executor.play_prairie_king", false)]
    [InlineData("applied", "unknown", "executor.play_prairie_king", false)]
    [InlineData(null, null, null, false)]
    public void SharedProgressClassificationIsExactlyTheOriginalLoopPolicy(string? status, string? verification, string? option, bool expected)
    {
        var raw = new JsonObject { ["status"] = status, ["primitive_verification_status"] = verification, ["option_id"] = option };
        Assert.Equal(expected, TrainingExecutionVerification.IsAcceptedProgress(status, verification, option));
        Assert.Equal(expected, QueueReplanFilter.IsTrainingVerifiedExecution(raw));
        Assert.Equal(status != "applied" || verification != "verified",
            TrainingExecutionVerification.RequiresProductReplan(false, status, verification));
        Assert.True(TrainingExecutionVerification.RequiresProductReplan(true, status, verification));
    }
}
