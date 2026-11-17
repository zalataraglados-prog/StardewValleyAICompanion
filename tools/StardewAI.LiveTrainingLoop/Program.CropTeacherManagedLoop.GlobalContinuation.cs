using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<string?> ContinueAfterManagedCropRunAsync(LiveTrainingOptions options, int iteration,
        CropTeacherOwnerLifetime lifetime, CropTeacherManagedSeedOperation? operation)
    {
        if (!options.UseManagedCropTeacher || !options.ContinueAfterCropTeacherRun ||
            lifetime.Current is not { } owner || lifetime.TerminalSelection is not { } selection ||
            operation?.IsOvernight != true)
            return null;

        lifetime.RequireLiveContinuation();
        selection.ValidateInputsUnchanged();
        var boundary = await owner.ReadTerminalGlobalContinuationAsync(operation,
            selection.CreateTarget(owner.AcceptedTerminalDepositCount), lifetime.TerminalProduction!).ConfigureAwait(false);
        var path = Path.Combine(owner.EvidenceDirectory, "global-continuation-" + iteration.ToString("D4") + ".json");
        await WritePortfolioJournalFileAsync(path, boundary.ToJsonString(JsonOptions)).ConfigureAwait(false);
        AppendProgress(options, "crop_teacher_managed_global_continuation", iteration,
            ReadString(boundary, "after_state_hash"), string.Empty,
            "boundary=" + path + " original_global_planner_resumes=true terminal_settlement_verified=false" +
            " semantic_saved_content_verified=false formal_product_training_authorized=false");
        await lifetime.ReleaseForGlobalContinuationAsync(owner).ConfigureAwait(false);
        return path;
    }
}
