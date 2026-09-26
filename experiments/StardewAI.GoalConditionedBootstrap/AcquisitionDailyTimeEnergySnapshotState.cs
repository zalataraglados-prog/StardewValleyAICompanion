using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionDailyTimeEnergySnapshotState
{
    private AcquisitionDailyTimeEnergySnapshotState(
        AcquisitionLocationRouteSnapshotState routeState,
        AcquisitionProcessingLeadTimeSnapshotState processingState,
        int targetTotalDay,
        double? availableEnergy,
        string[] energyBlockingReasons,
        int? emptyInventorySlots,
        string[] inventoryCapacityBlockingReasons)
    {
        RouteState = routeState;
        ProcessingState = processingState;
        TargetTotalDay = targetTotalDay;
        AvailableEnergy = availableEnergy;
        EnergyBlockingReasons = energyBlockingReasons;
        EmptyInventorySlots = emptyInventorySlots;
        InventoryCapacityBlockingReasons = inventoryCapacityBlockingReasons;
    }

    public AcquisitionLocationRouteSnapshotState RouteState { get; }

    public AcquisitionProcessingLeadTimeSnapshotState ProcessingState { get; }

    public int TargetTotalDay { get; }

    public double? AvailableEnergy { get; }

    public string[] EnergyBlockingReasons { get; }

    public int? EmptyInventorySlots { get; }

    public string[] InventoryCapacityBlockingReasons { get; }

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
        var processingState = AcquisitionProcessingLeadTimeSnapshotState.Read(
            snapshot);
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

        var inventoryReasons = new List<string>();
        int? emptyInventorySlots = null;
        var state = snapshot.GetProperty("state");
        if (!AcquisitionLocationRouteSnapshotState.TryFieldValue(
                state,
                "player",
                "inventory_capacity",
                out var capacity) ||
            capacity.ValueKind != JsonValueKind.Object)
        {
            inventoryReasons.Add("player_inventory_capacity_evidence_missing");
        }
        else
        {
            var maxItems = AcquisitionLocationRouteSnapshotState.ReadInt(
                capacity,
                "max_items");
            var occupied = AcquisitionLocationRouteSnapshotState.ReadInt(
                capacity,
                "occupied_item_stacks");
            var empty = AcquisitionLocationRouteSnapshotState.ReadInt(
                capacity,
                "empty_slots");
            var hasEmpty = AcquisitionLocationRouteSnapshotState.ReadBool(
                capacity,
                "has_empty_slot");
            if (!maxItems.HasValue || maxItems < 0 ||
                !occupied.HasValue || occupied < 0 ||
                !empty.HasValue || empty < 0 ||
                !hasEmpty.HasValue ||
                occupied + empty != maxItems ||
                hasEmpty != (empty > 0))
            {
                inventoryReasons.Add(
                    "player_inventory_capacity_evidence_inconsistent");
            }
            else
            {
                emptyInventorySlots = empty;
            }
        }

        return new AcquisitionDailyTimeEnergySnapshotState(
            routeState,
            processingState,
            targetTotalDay,
            energy,
            reasons.ToArray(),
            emptyInventorySlots,
            inventoryReasons.ToArray());
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
