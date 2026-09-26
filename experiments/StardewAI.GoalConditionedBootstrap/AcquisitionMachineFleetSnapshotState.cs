using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionMachineFleetSnapshotState
{
    private readonly IReadOnlyDictionary<string, AcquisitionMachineRouteState>
        rowsByLocationTile;

    private AcquisitionMachineFleetSnapshotState(
        bool evidenceAvailable,
        AcquisitionMachineRouteState[] rows,
        string[] blockingReasons)
    {
        EvidenceAvailable = evidenceAvailable;
        Rows = rows;
        BlockingReasons = blockingReasons;
        rowsByLocationTile = rows.ToDictionary(
            row => MachineKey(row.LocationId, row.TileX, row.TileY),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool EvidenceAvailable { get; }

    public AcquisitionMachineRouteState[] Rows { get; }

    public string[] BlockingReasons { get; }

    public IEnumerable<AcquisitionMachineRouteState> RowsFor(
        string machineQualifiedItemId) => Rows.Where(row => string.Equals(
            row.QualifiedItemId,
            machineQualifiedItemId,
            StringComparison.Ordinal));

    public bool TryGet(
        string locationId,
        int tileX,
        int tileY,
        out AcquisitionMachineRouteState machine) =>
        rowsByLocationTile.TryGetValue(
            MachineKey(locationId, tileX, tileY),
            out machine!);

    public static AcquisitionMachineFleetSnapshotState Read(
        JsonElement state)
    {
        if (!AcquisitionLocationRouteSnapshotState.TryFieldValue(
                state,
                "farm",
                "machines",
                out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return Blocked("machine_fleet_missing_or_unavailable");
        }

        var rawRows = value.EnumerateArray().ToArray();
        if (rawRows.Length == 0)
        {
            return new AcquisitionMachineFleetSnapshotState(
                true,
                Array.Empty<AcquisitionMachineRouteState>(),
                Array.Empty<string>());
        }

        var rows = new List<AcquisitionMachineRouteState>(rawRows.Length);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rawRows)
        {
            if (row.ValueKind != JsonValueKind.Object ||
                ReadInt(row, "machine_row_count_total") != rawRows.Length ||
                ReadString(row, "machine_row_snapshot_status") !=
                    "complete_no_row_truncation" ||
                ReadInt(row, "machine_input_probe_eligible_count") is not
                    int probeEligibleCount ||
                probeEligibleCount < 0)
            {
                return Blocked("machine_fleet_row_completeness_invalid");
            }

            var locationId = ReadString(row, "location_id");
            var qualifiedItemId = ReadString(row, "qualified_item_id");
            var tileX = ReadInt(row, "tile_x");
            var tileY = ReadInt(row, "tile_y");
            var locationIsPlayerControlled = ReadBool(
                row,
                "location_is_player_controlled");
            var ownerPlayerId = ReadLong(row, "owner_player_id");
            var readyForHarvest = ReadBool(row, "ready_for_harvest");
            var minutesUntilReady = ReadInt(row, "minutes_until_ready");
            var machineHasInput = ReadBool(row, "machine_has_input");
            var machineHasOutput = ReadBool(row, "machine_has_output");
            if (string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !qualifiedItemId.StartsWith("(", StringComparison.Ordinal) ||
                !tileX.HasValue || !tileY.HasValue ||
                tileX < 0 || tileY < 0 ||
                !locationIsPlayerControlled.HasValue ||
                !ownerPlayerId.HasValue ||
                !readyForHarvest.HasValue ||
                !minutesUntilReady.HasValue ||
                !machineHasInput.HasValue ||
                !machineHasOutput.HasValue)
            {
                return Blocked("machine_fleet_row_identity_or_state_invalid");
            }

            var key = MachineKey(locationId, tileX.Value, tileY.Value);
            if (!keys.Add(key))
                return Blocked("machine_fleet_location_tile_duplicate");
            rows.Add(new AcquisitionMachineRouteState(
                locationId,
                tileX.Value,
                tileY.Value,
                qualifiedItemId,
                locationIsPlayerControlled.Value,
                ownerPlayerId.Value,
                readyForHarvest.Value,
                minutesUntilReady.Value,
                machineHasInput.Value,
                machineHasOutput.Value));
        }

        return new AcquisitionMachineFleetSnapshotState(
            true,
            rows.ToArray(),
            Array.Empty<string>());
    }

    private static AcquisitionMachineFleetSnapshotState Blocked(
        string reason) => new(
            false,
            Array.Empty<AcquisitionMachineRouteState>(),
            new[] { reason });

    private static string MachineKey(
        string locationId,
        int tileX,
        int tileY) => locationId + ":" + tileX + "," + tileY;

    private static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int? ReadInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : null;

    private static long? ReadLong(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var result)
            ? result
            : null;

    private static bool? ReadBool(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}

internal sealed record AcquisitionMachineRouteState(
    string LocationId,
    int TileX,
    int TileY,
    string QualifiedItemId,
    bool LocationIsPlayerControlled,
    long OwnerPlayerId,
    bool ReadyForHarvest,
    int MinutesUntilReady,
    bool MachineHasInput,
    bool MachineHasOutput)
{
    public string CapacityState => ReadyForHarvest
        ? "ready_output"
        : MinutesUntilReady > 0
            ? "processing"
            : "idle";
}
