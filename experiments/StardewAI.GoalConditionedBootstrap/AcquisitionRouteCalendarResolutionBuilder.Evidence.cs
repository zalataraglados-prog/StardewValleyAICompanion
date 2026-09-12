using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static RequirementSourceEvidence VerifyEvidence(
        AuthoritativeRequirementInventoryReport inventory,
        string sourceId,
        string label)
    {
        var matches = inventory.SourceEvidence
            .Where(evidence => evidence.SourceId == sourceId)
            .ToArray();
        Require(matches.Length == 1,
            label + " evidence must occur exactly once.");
        var evidence = matches[0];
        Require(File.Exists(evidence.Path) &&
                string.Equals(
                    CurrentTeacherFrontierSupport.HashFile(evidence.Path),
                    evidence.Sha256,
                    StringComparison.OrdinalIgnoreCase),
            label + " evidence is missing or stale.");
        return evidence;
    }

    private static JsonElement ReadPayloadEvidence(
        RequirementSourceEvidence evidence,
        string label)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(evidence.Path));
        Require(document.RootElement.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object,
            label + " payload is unavailable.");
        return payload.Clone();
    }
}
