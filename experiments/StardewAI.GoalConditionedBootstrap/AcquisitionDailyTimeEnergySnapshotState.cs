using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionDailyTimeEnergySnapshotState
{
    private AcquisitionDailyTimeEnergySnapshotState(
        AcquisitionLocationRouteSnapshotState routeState,
        int targetTotalDay,
        double? availableEnergy,
        string[] energyBlockingReasons)
    {
        RouteState = routeState;
        TargetTotalDay = targetTotalDay;
        AvailableEnergy = availableEnergy;
        EnergyBlockingReasons = energyBlockingReasons;
    }

    public AcquisitionLocationRouteSnapshotState RouteState { get; }

    public int TargetTotalDay { get; }

    public double? AvailableEnergy { get; }

    public string[] EnergyBlockingReasons { get; }

    public static AcquisitionDailyTimeEnergySnapshotState Read(
        JsonElement snapshot,
        string expectedGameVersion,
        int targetTotalDay,
        string timingCalibrationPath)
    {
        var routeState = AcquisitionLocationRouteSnapshotState.Read(
            snapshot,
            expectedGameVersion,
            targetTotalDay,
            timingCalibrationPath);
        var reasons = new List<string>();
        double? energy = null;
        if (!TryAvailableNumber(snapshot, "player", "energy", out var parsed) ||
            parsed < 0d)
        {
            reasons.Add("player_energy_evidence_missing_or_invalid");
        }
        else
        {
            energy = parsed;
        }

        return new AcquisitionDailyTimeEnergySnapshotState(
            routeState,
            targetTotalDay,
            energy,
            reasons.ToArray());
    }

    private static bool TryAvailableNumber(
        JsonElement snapshot,
        string sectionName,
        string fieldName,
        out double value)
    {
        value = 0d;
        return snapshot.TryGetProperty("state", out var state) &&
            state.ValueKind == JsonValueKind.Object &&
            state.TryGetProperty(sectionName, out var section) &&
            section.ValueKind == JsonValueKind.Object &&
            section.TryGetProperty(fieldName, out var field) &&
            field.ValueKind == JsonValueKind.Object &&
            field.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            string.Equals(status.GetString(), "available", StringComparison.Ordinal) &&
            field.TryGetProperty("value", out var raw) &&
            raw.ValueKind == JsonValueKind.Number &&
            raw.TryGetDouble(out value) &&
            double.IsFinite(value);
    }
}
