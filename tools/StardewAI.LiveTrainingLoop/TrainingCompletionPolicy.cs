namespace StardewAI.LiveTrainingLoop;

public enum LiveTrainingPhase
{
    Primary,
    NativeSaveBoundary,
    Complete,
    Incomplete
}

public sealed record LiveTrainingPhaseDecision(
    LiveTrainingPhase Phase,
    string StopReason = "");

public static class TrainingCompletionPolicy
{
    public static LiveTrainingPhaseDecision Decide(
        int primaryAttemptsStarted,
        int maxPrimaryAttempts,
        int saveBoundaryAttemptsStarted,
        int maxSaveBoundaryAttempts,
        bool hasExplicitPrimaryTarget,
        bool explicitPrimaryTargetMet,
        bool requireNativeSaveBoundary,
        bool nativeSaveBoundaryVerified)
    {
        var primaryComplete = hasExplicitPrimaryTarget
            ? explicitPrimaryTargetMet
            : primaryAttemptsStarted >= maxPrimaryAttempts;
        if (!primaryComplete)
        {
            return primaryAttemptsStarted >= maxPrimaryAttempts
                ? new LiveTrainingPhaseDecision(
                    LiveTrainingPhase.Incomplete,
                    "primary_training_target_not_met")
                : new LiveTrainingPhaseDecision(LiveTrainingPhase.Primary);
        }

        if (!requireNativeSaveBoundary || nativeSaveBoundaryVerified)
        {
            return new LiveTrainingPhaseDecision(LiveTrainingPhase.Complete);
        }

        return saveBoundaryAttemptsStarted >= maxSaveBoundaryAttempts
            ? new LiveTrainingPhaseDecision(
                LiveTrainingPhase.Incomplete,
                "native_save_boundary_not_verified")
            : new LiveTrainingPhaseDecision(LiveTrainingPhase.NativeSaveBoundary);
    }
}
