namespace StardewAI.Core.Tests;

public sealed partial class CropTeacherControlIntegrationSourceGuardTests
{
    [Fact]
    public void OptionalOwnedSeedScopeReusesOriginalCompilerWithDefaultGoalUnchanged()
    {
        var wrapper = Read("tools", "StardewAI.LiveTrainingLoop", "Program.CropTeacherSeedPurchase.cs");
        Assert.Contains("if (resourceSupport is null)", wrapper, StringComparison.Ordinal);
        Assert.Contains("beforeSnapshotPath, beforeSnapshot, stateHash, \"earn_money\")", wrapper, StringComparison.Ordinal);
        Assert.Contains("beforeSnapshotPath, beforeSnapshot, stateHash, \"daily.closed_loop\")", wrapper, StringComparison.Ordinal);
        Assert.Contains("PostJournaledPortfolioCommitAsync", wrapper, StringComparison.Ordinal);
        Assert.Contains("PostJournaledPortfolioSettlementAsync", wrapper, StringComparison.Ordinal);
        var compiler = Read("tools", "StardewAI.LiveTrainingLoop", "Program.CropTeacherControl.cs");
        Assert.Contains("CompileCropTeacherSeedQueueAsync(", compiler, StringComparison.Ordinal);
        Assert.Contains("goalIdOverride: goalId", compiler, StringComparison.Ordinal);
        var dispatch = Read("tools", "StardewAI.LiveTrainingLoop", "Program.CropTeacherDispatch.cs");
        Assert.Contains("ValidateUnmanagedDispatch", dispatch, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnedSeedValidationAndSettlementBracketOriginalNativeExecutionBeforeFeedback()
    {
        var source = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs");
        var validate = source.IndexOf("selectedCandidateId = ValidateCropTeacherSeedQueueBeforeDispatch(", StringComparison.Ordinal);
        var execute = source.IndexOf("var execution = cropManagedOperationForExecution is null", StringComparison.Ordinal);
        Assert.True(validate >= 0 && execute > validate);
        var settle = source.IndexOf("await cropSeedSupportForExecution.CompleteAsync(", execute, StringComparison.Ordinal);
        Assert.True(settle > execute);
        var feedback = source.IndexOf("var feedbackAvailable =", settle, StringComparison.Ordinal);
        Assert.True(validate >= 0 && execute > validate && settle > execute && feedback > settle);
        Assert.Contains("ReadString(execution, \"after_snapshot_path\"), selectedCandidateId)", source, StringComparison.Ordinal);
        var managed = Read("tools", "StardewAI.LiveTrainingLoop", "Program.CropTeacherManagedLoop.Execution.cs");
        Assert.Contains("if (owner is null) runtime.ValidateDispatchQueue(queuePath)", managed, StringComparison.Ordinal);
        Assert.Contains("RequireManagedCropDispatch(owner, operation)", managed, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(operation!.PreparedRuntime, runtime)", managed, StringComparison.Ordinal);
        var lease = managed.IndexOf("await owner.ExecuteSeedAsync(", StringComparison.Ordinal);
        Assert.True(lease >= 0);
        var managedValidate = managed.IndexOf("runtime.ValidateDispatchQueue(queuePath)", lease, StringComparison.Ordinal);
        var managedExecute = managed.IndexOf("await executeOriginal()", StringComparison.Ordinal);
        var managedSettle = managed.IndexOf("await runtime.CompleteAsync(", StringComparison.Ordinal);
        var managedAccept = managed.IndexOf("await owner.AcceptLiveAsync(", StringComparison.Ordinal);
        Assert.True(managedValidate > lease && managedExecute > managedValidate &&
            managedSettle > managedExecute && managedAccept > managedSettle);
        Assert.Contains("cropSeedSupportForExecution is not null && cropManagedOperationForExecution is null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnedPurchaseReusesTheOriginalSelectedCandidateContinuationOnly()
    {
        var source = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs");
        Assert.Contains("useOwnedSeedPurchaseContinuation = cropManagedOperationForExecution is not null &&", source, StringComparison.Ordinal);
        Assert.Contains("cropSeedSupportForExecution is CropTeacherSeedPurchaseRuntime", source, StringComparison.Ordinal);
        Assert.Contains("useDailyPlanDecisionLease || useOwnedSeedPurchaseContinuation", source, StringComparison.Ordinal);
        Assert.Contains("useDailyPlanDecisionLease = false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnedSeedPreservesTheFullResourceSnapshotContract()
    {
        var source = Read("tools", "StardewAI.LiveTrainingLoop", "Program.CropTeacherManagedLoop.Control.cs");
        Assert.Contains("SnapshotProfile = \"full\"", source, StringComparison.Ordinal);
        Assert.Contains("SnapshotUrlForProfile(options.BridgeSnapshotUrl, \"full\", forceRefresh: true)", source, StringComparison.Ordinal);
    }
}
