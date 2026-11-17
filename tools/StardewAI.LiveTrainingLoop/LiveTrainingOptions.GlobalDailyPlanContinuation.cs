public sealed partial class LiveTrainingOptions
{
    public bool ContinueGlobalDailyPlanAfterCropTeacherRun { get; set; }

    public bool ShouldContinueAfterCropTeacherRun =>
        ContinueAfterCropTeacherRun || ContinueGlobalDailyPlanAfterCropTeacherRun;

    public void ValidateGlobalDailyPlanContinuationMode()
    {
        if (!ContinueGlobalDailyPlanAfterCropTeacherRun) return;
        const string mode = "--continue-global-daily-plan-after-crop-teacher-run";
        if (ContinueAfterCropTeacherRun || StandaloneCollectionTeacher ||
            RequireGoldenWalnutSourceScheduleReady || RequireGourmandContinuationReady)
            throw new ArgumentException(mode + " cannot be combined with collection-control continuation or collection-only admission modes.");
        if (!UseManagedCropTeacher)
            throw new ArgumentException(mode + " requires --use-managed-crop-teacher and its original native owner boundary.");
        ValidateManagedCropTeacherMode();
        if (ArtifactRetentionMode != "stop")
            throw new ArgumentException(mode + " requires --artifact-retention-mode stop to retain the original owner inputs.");
        if (DailyPlanCandidateOptionIds.Length != 0 || DailyPlanCandidateParameters.Count != 0 ||
            !string.IsNullOrWhiteSpace(DailyPlanCandidateKind) || !string.IsNullOrWhiteSpace(DailyPlanCandidateId))
            throw new ArgumentException(mode + " requires the original global daily-plan candidate denominator without candidate overrides.");
    }
}
