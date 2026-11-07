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
        var validate = source.IndexOf("cropSeedSupportForExecution.ValidateDispatchQueue(queuePath)", StringComparison.Ordinal);
        var execute = source.IndexOf("var execution = options.UseRuntimeTestHarnessExecutor", StringComparison.Ordinal);
        var settle = source.IndexOf("await cropSeedSupportForExecution.CompleteAsync(", execute, StringComparison.Ordinal);
        var feedback = source.IndexOf("var feedbackAvailable =", settle, StringComparison.Ordinal);
        Assert.True(validate >= 0 && execute > validate && settle > execute && feedback > settle);
        Assert.Contains("ReadString(execution, \"after_snapshot_path\"), selectedCandidateId)", source, StringComparison.Ordinal);
    }
}
