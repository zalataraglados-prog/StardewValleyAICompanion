using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class AcquisitionProcessingLeadTimeSnapshotState
{
    private readonly SnapshotArrayState farmCrops;
    private readonly SnapshotArrayState currentLocationCrops;

    private AcquisitionProcessingLeadTimeSnapshotState(
        string currentLocationId,
        SnapshotArrayState farmCrops,
        SnapshotArrayState currentLocationCrops,
        JsonElement crabPotNetwork)
    {
        CurrentLocationId = currentLocationId;
        this.farmCrops = farmCrops;
        this.currentLocationCrops = currentLocationCrops;
        CrabPotNetwork = crabPotNetwork;
    }

    public string CurrentLocationId { get; }

    public JsonElement CrabPotNetwork { get; }

    public static AcquisitionProcessingLeadTimeSnapshotState Read(
        JsonElement snapshot)
    {
        var state = RequiredObject(snapshot, "state");
        var currentLocationId = RequiredAvailableString(
            state,
            "player",
            "location_id");
        var farmCrops = ReadArrayState(
            state,
            "farm",
            "crops",
            "state.farm.crops.value");
        var currentCrops = ReadArrayState(
            state,
            "current_location",
            "crops",
            "state.current_location.crops.value");
        var crabPots = TryAvailableValue(
                state,
                "player",
                "crab_pot_network",
                out var crabPotValue)
            ? crabPotValue.Clone()
            : default;
        return new AcquisitionProcessingLeadTimeSnapshotState(
            currentLocationId,
            farmCrops,
            currentCrops,
            crabPots);
    }

    public LiveCropLookup FindCrops(
        string locationId,
        string qualifiedItemId)
    {
        var source = string.Equals(
                locationId,
                "Farm",
                StringComparison.OrdinalIgnoreCase)
            ? farmCrops
            : string.Equals(
                locationId,
                CurrentLocationId,
                StringComparison.OrdinalIgnoreCase)
                ? currentLocationCrops
                : SnapshotArrayState.Unavailable(
                    "target_location_live_crop_state_not_loaded:" +
                    locationId);
        if (!source.Available)
        {
            return new(
                false,
                Array.Empty<LiveCropState>(),
                source.Reasons,
                Array.Empty<string>());
        }

        var rows = new List<LiveCropState>();
        var reasons = new List<string>();
        var seenTiles = new HashSet<(int X, int Y)>();
        foreach (var row in source.Rows)
        {
            if (!TryReadCrop(row, locationId, out var crop, out var reason) ||
                !seenTiles.Add((crop!.TileX, crop.TileY)))
            {
                reasons.Add(reason.Length > 0
                    ? reason
                    : "duplicate_live_crop_tile:" + locationId);
                continue;
            }
            if (crop.QualifiedItemId == qualifiedItemId)
                rows.Add(crop);
        }
        return reasons.Count == 0
            ? new(
                true,
                rows.ToArray(),
                Array.Empty<string>(),
                new[] { source.EvidencePath + "[]" })
            : new(
                false,
                Array.Empty<LiveCropState>(),
                reasons.Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                Array.Empty<string>());
    }
}
