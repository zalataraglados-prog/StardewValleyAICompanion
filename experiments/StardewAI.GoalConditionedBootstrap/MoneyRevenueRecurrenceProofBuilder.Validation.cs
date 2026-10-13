namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueRecurrenceProofBuilder
{
    private static void ValidateManifest(
        MoneyRevenueRecurrenceProofManifest manifest)
    {
        var transitions = manifest.Transitions ??
            Array.Empty<MoneyRevenueRecurrenceTransitionProof>();
        Require(manifest.SchemaVersion ==
                    "money_revenue_recurrence_proof_manifest.v1" &&
                !manifest.FormalProductTrainingAuthorized &&
                transitions.Length > 0 &&
                transitions.All(proof => proof is not null &&
                    !string.IsNullOrWhiteSpace(proof.TransitionId)) &&
                transitions.Select(proof => proof.TransitionId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    transitions.Length,
            "Money revenue recurrence proof manifest is invalid.");
    }

    private static bool ShippingFieldsEmpty(
        MoneyRevenueRecurrenceTransitionProof proof) =>
        string.IsNullOrWhiteSpace(proof.QueuePath) &&
        string.IsNullOrWhiteSpace(proof.BeforeSnapshotPath) &&
        string.IsNullOrWhiteSpace(proof.ExecutionReceiptPath) &&
        string.IsNullOrWhiteSpace(proof.AfterSnapshotPath) &&
        string.IsNullOrWhiteSpace(proof.RunId) &&
        string.IsNullOrWhiteSpace(proof.ExecutorVersion) &&
        string.IsNullOrWhiteSpace(proof.SelectedCandidateId);

    private static bool ShippingFieldsComplete(
        MoneyRevenueRecurrenceTransitionProof proof) =>
        !string.IsNullOrWhiteSpace(proof.QueuePath) &&
        !string.IsNullOrWhiteSpace(proof.BeforeSnapshotPath) &&
        !string.IsNullOrWhiteSpace(proof.ExecutionReceiptPath) &&
        !string.IsNullOrWhiteSpace(proof.AfterSnapshotPath) &&
        !string.IsNullOrWhiteSpace(proof.RunId) &&
        !string.IsNullOrWhiteSpace(proof.ExecutorVersion) &&
        !string.IsNullOrWhiteSpace(proof.SelectedCandidateId);

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
