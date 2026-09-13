using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class AcquisitionProcessingLeadTimeSnapshotState
{
    private static bool TryReadCrop(
        JsonElement row,
        string expectedLocationId,
        out LiveCropState? crop,
        out string reason)
    {
        crop = null;
        reason = string.Empty;
        var locationId = ReadString(row, "location_id");
        if (row.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(locationId) ||
            !string.Equals(
                locationId,
                expectedLocationId,
                StringComparison.OrdinalIgnoreCase) ||
            !TryReadInt(row, "tile_x", out var tileX) ||
            !TryReadInt(row, "tile_y", out var tileY) ||
            !TryReadBool(row, "dead", out var dead) ||
            !TryReadBool(row, "ready_for_harvest", out var ready) ||
            !TryReadNullableNonNegativeInt(
                row,
                "days_until_next_harvest_if_watered",
                out var days))
        {
            reason = "live_crop_row_invalid:" + expectedLocationId;
            return false;
        }
        var qualifiedItemId = ReadString(
            row,
            "harvest_item_qualified_id");
        var projectionStatus = ReadString(
            row,
            "harvest_item_projection_status");
        if (string.IsNullOrWhiteSpace(qualifiedItemId) ||
            string.IsNullOrWhiteSpace(projectionStatus))
        {
            reason = "live_crop_harvest_identity_unavailable:" +
                expectedLocationId;
            return false;
        }
        crop = new LiveCropState(
            tileX,
            tileY,
            qualifiedItemId,
            projectionStatus,
            dead,
            ready,
            days);
        return true;
    }
}

internal sealed record LiveCropLookup(
    bool EvidenceAvailable,
    LiveCropState[] Rows,
    string[] BlockingReasons,
    string[] EvidencePaths);

internal sealed record LiveCropState(
    int TileX,
    int TileY,
    string QualifiedItemId,
    string ProjectionStatus,
    bool Dead,
    bool ReadyForHarvest,
    int? DaysUntilNextHarvestIfWatered);
