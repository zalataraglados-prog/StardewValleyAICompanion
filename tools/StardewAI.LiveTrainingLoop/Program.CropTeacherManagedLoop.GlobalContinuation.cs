using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropGlobalContinuation?> ContinueAfterManagedCropRunAsync(LiveTrainingOptions options, int iteration,
        CropTeacherOwnerLifetime lifetime, CropTeacherManagedSeedOperation? operation)
    {
        if (!options.UseManagedCropTeacher || !options.ShouldContinueAfterCropTeacherRun ||
            lifetime.Current is not { } owner || lifetime.TerminalSelection is not { } selection ||
            operation?.IsOvernight != true)
            return null;

        lifetime.RequireLiveContinuation();
        selection.ValidateInputsUnchanged();
        var boundary = await owner.ReadTerminalGlobalContinuationAsync(operation,
            selection.CreateTarget(owner.AcceptedTerminalDepositCount), lifetime.TerminalProduction!).ConfigureAwait(false);
        var path = Path.Combine(owner.EvidenceDirectory, "global-continuation-" + iteration.ToString("D4") + ".json");
        var continuation = ReadManagedGlobalSaveBoundary(options, path, boundary);
        await WritePortfolioJournalFileAsync(path, boundary.ToJsonString(JsonOptions)).ConfigureAwait(false);
        AppendProgress(options, "crop_teacher_managed_global_continuation", iteration,
            ReadString(boundary, "after_state_hash"), string.Empty,
            "boundary=" + path + " original_global_planner_resumes=true terminal_settlement_verified=false" +
            " semantic_saved_content_verified=false formal_product_training_authorized=false" +
            (options.ContinueGlobalDailyPlanAfterCropTeacherRun ? " continuation_mode=global_daily_plan" : string.Empty));
        await lifetime.ReleaseForGlobalContinuationAsync(owner).ConfigureAwait(false);
        return continuation;
    }
}
