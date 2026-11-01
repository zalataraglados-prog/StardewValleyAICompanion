using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed record StageOneDailyMaintenancePreemptionDecision(
    bool ShouldPreempt,
    string Reason,
    int WateringCandidateCount);

public static class StageOneDailyMaintenancePreemptionPolicy
{
    public const string CandidateOptionId = "farm.maintain_crops";
    public const string CandidateKind = "water_crop_tile";
    public const string PreemptionReason =
        "current_location_crop_requires_watering_before_day_end";

    public static IReadOnlyList<string> CandidateOptionIds { get; } =
        new[] { CandidateOptionId };

    public static StageOneDailyMaintenancePreemptionDecision Evaluate(
        JsonObject snapshot)
    {
        var crops = snapshot["state"]?["current_location"]?["crops"]?["value"]
            as JsonArray;
        if (crops is null)
        {
            return new StageOneDailyMaintenancePreemptionDecision(
                false,
                string.Empty,
                0);
        }

        var wateringCandidateCount = crops.Count(node =>
            node is JsonObject crop &&
            crop["needs_watering"] is JsonValue value &&
            value.TryGetValue<bool>(out var needsWatering) &&
            needsWatering);
        return new StageOneDailyMaintenancePreemptionDecision(
            wateringCandidateCount > 0,
            wateringCandidateCount > 0 ? PreemptionReason : string.Empty,
            wateringCandidateCount);
    }
}
