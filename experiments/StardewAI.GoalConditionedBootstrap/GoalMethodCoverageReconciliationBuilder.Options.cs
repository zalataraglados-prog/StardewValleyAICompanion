using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodCoverageReconciliationBuilder
{
    private static IReadOnlyDictionary<string, OptionEvidence> LoadOptions(
        string optionMatrixPath,
        string expectedSha256)
    {
        var fullPath = Path.GetFullPath(optionMatrixPath);
        if (!File.Exists(fullPath) ||
            CurrentTeacherFrontierSupport.HashFile(fullPath) != expectedSha256)
        {
            throw new InvalidDataException(
                "Reconciliation option matrix differs from the rebuilt frontier.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
        var root = document.RootElement;
        if (RequiredString(root, "schema_version") !=
                "stardewai.option_governance_matrix.v3" ||
            !root.TryGetProperty("options", out var optionsElement) ||
            optionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Reconciliation option matrix is invalid.");
        }

        var options = new Dictionary<string, OptionEvidence>(
            StringComparer.Ordinal);
        foreach (var element in optionsElement.EnumerateArray())
        {
            var optionId = RequiredString(element, "optionId");
            var evidenceIds = new[]
                {
                    "readEvidenceIds",
                    "candidateEvidenceIds",
                    "compilerEvidenceIds",
                    "runtimeEvidenceIds",
                    "outputEvidenceIds"
                }
                .SelectMany(name => StringArray(element, name))
                .Concat(SplitEvidenceIds(
                    OptionalString(element, "runtimeEvidenceId")))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var gates = new[]
            {
                RequiredString(element, "readTrainingGate"),
                RequiredString(element, "candidateTrainingGate"),
                RequiredString(element, "compilerTrainingGate"),
                RequiredString(element, "runtimeTrainingGate"),
                RequiredString(element, "outputTrainingGate")
            };
            var option = new OptionEvidence(
                optionId,
                RequiredString(element, "trainingEligibility"),
                RequiredString(element, "runtimeStatus"),
                RequiredString(element, "productStatus"),
                GateReady(gates[0]),
                gates.All(GateReady),
                RequiredBoolean(element,
                    "internalExecutionPipelineSupported"),
                RequiredBoolean(element, "productExecutorSupported"),
                evidenceIds,
                StringArray(element, "trainingExclusionReasons"));
            if (!options.TryAdd(optionId, option))
            {
                throw new InvalidDataException(
                    "Reconciliation option matrix contains a duplicate option: " +
                    optionId);
            }
        }
        return options;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException(
                "Reconciliation option matrix is missing " + name + ".");
        }
        return property.GetString()!;
    }

    private static string OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static bool RequiredBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException(
                "Reconciliation option matrix is missing " + name + ".");
        }
        return property.GetBoolean();
    }

    private static string[] StringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Reconciliation option matrix is missing " + name + ".");
        }
        return property.EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> SplitEvidenceIds(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries);

    private static bool GateReady(string status) =>
        status is "RuntimeVerified" or "LongDurationVerified";

    private sealed record OptionEvidence(
        string OptionId,
        string TrainingEligibility,
        string RuntimeStatus,
        string ProductStatus,
        bool TransparentReadGateReady,
        bool FiveGateTrainingReady,
        bool InternalExecutionPipelineSupported,
        bool ProductExecutorSupported,
        string[] EvidenceIds,
        string[] TrainingExclusionReasons);
}
