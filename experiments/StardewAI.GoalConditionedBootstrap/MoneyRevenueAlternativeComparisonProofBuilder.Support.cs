namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueAlternativeComparisonProofBuilder
{
    private static void ValidateManifest(
        MoneyRevenueAlternativeComparisonProofManifest manifest)
    {
        var alternatives = manifest.Alternatives ??
            throw new InvalidDataException(
                "Money revenue comparison manifest has no alternatives.");
        Require(manifest.SchemaVersion ==
                    "money_revenue_alternative_comparison_proof_manifest.v1" &&
                !manifest.FormalProductTrainingAuthorized &&
                !string.IsNullOrWhiteSpace(manifest.CriterionId) &&
                alternatives.Length >= 2 &&
                alternatives.All(row => row is not null &&
                    !string.IsNullOrWhiteSpace(row.AlternativeId)) &&
                alternatives.Select(row => row.AlternativeId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    alternatives.Length,
            "Money revenue comparison proof manifest is invalid.");
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private static T Read<T>(string path, string label) =>
        CurrentTeacherFrontierSupport.Read<T>(path, label);

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
