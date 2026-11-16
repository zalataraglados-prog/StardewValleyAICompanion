using System;

namespace StardewAI.Contracts.Training
{
    public static class TrainingExecutionVerification
    {
        // Original loop progress policy; equivalent execution is never relabeled as native play.
        public static bool IsAcceptedProgress(string? status, string? verificationStatus, string? optionId) =>
            string.Equals(status, "applied", StringComparison.Ordinal) &&
            (string.Equals(verificationStatus, "verified", StringComparison.Ordinal) ||
             string.Equals(verificationStatus, "simulated_equivalent", StringComparison.Ordinal) &&
             (string.Equals(optionId, "executor.play_junimo_kart", StringComparison.Ordinal) ||
              string.Equals(optionId, "executor.play_prairie_king", StringComparison.Ordinal)));

        public static bool RequiresProductReplan(bool requestStateHashDrift, string? status, string? verificationStatus) =>
            requestStateHashDrift || !string.Equals(status, "applied", StringComparison.Ordinal) ||
            !string.Equals(verificationStatus, "verified", StringComparison.Ordinal);
    }
}
