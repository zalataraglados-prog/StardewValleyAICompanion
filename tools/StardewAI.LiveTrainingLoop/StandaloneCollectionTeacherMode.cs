using StardewAI.Contracts.Execution;

namespace StardewAI.LiveTrainingLoop;

public static class StandaloneCollectionTeacherMode
{
    public static void Validate(LiveTrainingOptions options)
    {
        if (!options.StandaloneCollectionTeacher) return;
        const string mode = "--standalone-collection-teacher";
        if (!options.UseDailyPlan || !options.SkipTraining || !options.RequireExecutorFeedback ||
            (!options.UseProductExecutor && !options.UseRuntimeTestHarnessExecutor) ||
            options.TargetExecutionMode != ExecutionTargetProfiles.TrainingSingleplayer)
            throw new ArgumentException(mode + " requires --use-daily-plan, --skip-training and native training_singleplayer feedback.");
        if (!string.IsNullOrWhiteSpace(options.ManifestPath) || options.RequireStructuredPolicy ||
            !string.IsNullOrWhiteSpace(options.PolicyCheckpointPath) ||
            !string.IsNullOrWhiteSpace(options.PolicyInitializationCheckpointPath))
            throw new ArgumentException(mode + " cannot resume a training manifest or use a learner checkpoint.");
        if (options.UseTeacherPreferenceQueue || options.UsePrecompiledQueue || options.UseParameterizedAction ||
            options.UsePlanOutput || !string.IsNullOrWhiteSpace(options.ExecutorOptionId) ||
            options.DailyPlanCandidateOptionIds.Length != 0 || options.DailyPlanCandidateParameters.Count != 0 ||
            !string.IsNullOrWhiteSpace(options.DailyPlanCandidateKind) || !string.IsNullOrWhiteSpace(options.DailyPlanCandidateId))
            throw new ArgumentException(mode + " requires the original complete candidate denominator without queue or candidate overrides.");
        if (options.CaptureGoldenWalnutFreshSaveEvidence || options.RequireGoldenWalnutSourceScheduleReady ||
            options.RequireGourmandContinuationReady || options.ContinueAfterCropTeacherRun ||
            !string.IsNullOrWhiteSpace(options.ResumeCropTeacherTransitionLedgerPath))
            throw new ArgumentException(mode + " is a local source mode, not a global fresh-save or crop continuation.");
        if (string.IsNullOrWhiteSpace(options.RunId) || !Directory.Exists(options.SaveIsolationPath))
            throw new ArgumentException(mode + " requires a run identity and an existing isolated save directory.");
        foreach (var artifact in new[]
        {
            ("--stage-one-collection-requirement-inventory", options.StageOneCollectionRequirementInventoryPath),
            ("--stage-one-collection-acquisition-lowering", options.StageOneCollectionAcquisitionLoweringPath),
            ("--master-angler-stage-one-window-index", options.MasterAnglerStageOneWindowIndexPath),
            ("--route-timing-calibration", options.RouteTimingCalibrationPath)
        })
            if (string.IsNullOrWhiteSpace(artifact.Item2) || !File.Exists(artifact.Item2))
                throw new ArgumentException(mode + " requires an existing authority artifact: " + artifact.Item1);
    }
}
