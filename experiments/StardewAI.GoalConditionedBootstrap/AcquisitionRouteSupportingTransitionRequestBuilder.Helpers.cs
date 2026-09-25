using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    private static int? ReadPositiveIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name) =>
        CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
            candidate,
            name,
            out var value) && value > 0
                ? value
                : null;

    private static int? ReadNonNegativeIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name) =>
        CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
            candidate,
            name,
            out var value) && value >= 0
                ? value
                : null;

    private static bool TryStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out int value)
    {
        value = 0;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out var fieldValue) &&
            fieldValue.TryGetInt32(out value);
    }
}
