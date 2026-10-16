using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropPlantingLayoutProofBuilder
{
    public static MoneyRevenueCropPlantingLayoutProof Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop planting-layout proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropPlantingLayoutProofRequest>(
                fullRequestPath,
                "Money Revenue crop planting-layout proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_planting_layout_proof_request.v1" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                !string.IsNullOrWhiteSpace(request.LocationId) &&
                request.Tiles is not null &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop planting-layout proof request is invalid.");

        var planPath = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production obligation plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planPath,
                "Money Revenue crop-production obligation plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            plan.RequestPath);
        Require(BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan) &&
                !plan.FutureCropProductionProofReady &&
                !plan.FreshSaveRevenueScheduleReady &&
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");
        var commitment = plan.Commitments.SingleOrDefault(value =>
            value.CommitmentId == request.CommitmentId);
        Require(commitment is not null,
            "Money Revenue crop planting-layout commitment is not in the plan.");
        var evidenceRequestId = "planting-layout:" + request.CommitmentId;
        Require(commitment!.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal) &&
                plan.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal),
            "Money Revenue crop planting-layout request is outside the plan denominator.");

        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            RequiredFile(
                plan.SnapshotPath,
                "Money Revenue crop-production source snapshot"),
            "Money Revenue crop-production source snapshot");
        ValidateSnapshot(sourceSnapshot, "source");
        Require(CurrentTeacherFrontierSupport.HashFile(plan.SnapshotPath) ==
                    plan.SnapshotSha256 &&
                sourceSnapshot.StateHash == plan.SourceStateHash,
            "Money Revenue crop-production source snapshot drifted.");

        var plantingSnapshotPath = RequiredFile(
            request.PlantingSnapshotPath,
            "Money Revenue crop planting-day snapshot");
        var plantingSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            plantingSnapshotPath,
            "Money Revenue crop planting-day snapshot");
        ValidateSnapshot(plantingSnapshot, "planting-day");
        Require(SameActor(sourceSnapshot, plantingSnapshot),
            "Money Revenue crop planting-day snapshot changed save or player identity.");
        Require(ReadStateInt(plantingSnapshot, "time", "total_days") ==
                    commitment.PlantingTotalDay &&
                ReadStateString(plantingSnapshot, "player", "location_id") ==
                    request.LocationId,
            "Money Revenue crop planting-day or player location is incorrect.");

        var context = ReadStateValue(
            plantingSnapshot,
            "current_location",
            "planting_context");
        Require(context.ValueKind == JsonValueKind.Object &&
                ReadString(context, "location_id") == request.LocationId &&
                ReadString(context, "season") == commitment.PlantingSeason &&
                ReadRequiredBool(context, "is_outdoors") &&
                ReadRequiredBool(context, "is_farm") &&
                !ReadRequiredBool(context, "is_greenhouse"),
            "Money Revenue crop planting context is not the required outdoor Farm context.");
        Require(context.TryGetProperty("hoe_dirt_tiles", out var tilesValue) &&
                tilesValue.ValueKind == JsonValueKind.Array,
            "Money Revenue crop planting context has no HoeDirt tile array.");
        var hoeDirtTiles = context.GetProperty("hoe_dirt_tiles");

        var requestedTiles = (request.Tiles ??
                throw new InvalidDataException(
                    "Money Revenue crop planting layout tiles are missing."))
            .OrderBy(value => value.TileY)
            .ThenBy(value => value.TileX)
            .ToArray();
        Require(requestedTiles.Length == commitment.TileCount &&
                requestedTiles.Distinct().Count() == requestedTiles.Length,
            "Money Revenue crop planting layout does not contain the exact unique tile count.");
        var baseGrowDays = commitment.BaseGrowDays;
        Require(baseGrowDays > 0,
            "Money Revenue crop planting commitment has no first harvest duration.");
        var seedId = UnqualifyObjectId(commitment.SeedQualifiedItemId);
        foreach (var tile in requestedTiles)
        {
            var matches = hoeDirtTiles.EnumerateArray().Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "tile_x") == tile.TileX &&
                    ReadInt(row, "tile_y") == tile.TileY)
                .ToArray();
            Require(matches.Length == 1,
                "Money Revenue crop planting tile is missing or duplicated: " +
                TileName(tile));
            VerifyTile(matches[0], tile, seedId, baseGrowDays);
        }

        return new MoneyRevenueCropPlantingLayoutProof
        {
            Status = "verified_planting_layout",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = CurrentTeacherFrontierSupport.HashFile(
                planPath),
            EvidenceRequestId = evidenceRequestId,
            CommitmentId = commitment.CommitmentId,
            PlantingSnapshotPath = plantingSnapshotPath,
            PlantingSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                plantingSnapshotPath),
            SourceStateHash = sourceSnapshot.StateHash,
            PlantingStateHash = plantingSnapshot.StateHash,
            PlantingTotalDay = commitment.PlantingTotalDay,
            LocationId = request.LocationId,
            SeedQualifiedItemId = commitment.SeedQualifiedItemId,
            TileCount = requestedTiles.Length,
            Tiles = requestedTiles,
            PlantingLayoutVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static void VerifyTile(
        JsonElement row,
        MoneyRevenueCropPlantingTile tile,
        string seedId,
        int baseGrowDays)
    {
        Require(!ReadRequiredBool(row, "has_crop") &&
                !ReadRequiredBool(row, "is_garden_pot") &&
                row.TryGetProperty(
                    "occupied_object_qualified_id",
                    out var occupied) &&
                (occupied.ValueKind == JsonValueKind.Null ||
                 occupied.ValueKind == JsonValueKind.String &&
                 string.IsNullOrEmpty(occupied.GetString())),
            "Money Revenue crop planting tile is occupied or is not empty outdoor HoeDirt: " +
            TileName(tile));
        Require(row.TryGetProperty("seed_results", out var seedResultsValue) &&
                seedResultsValue.ValueKind == JsonValueKind.Array,
            "Money Revenue crop planting tile has no seed-result array: " +
            TileName(tile));
        var seedResults = row.GetProperty("seed_results");
        var matches = seedResults.EnumerateArray().Where(result =>
                result.ValueKind == JsonValueKind.Object &&
                ReadString(result, "seed_id") == seedId)
            .ToArray();
        var adjustedGrowDays = matches.Length == 1
            ? ReadInt(
                matches[0],
                "adjusted_grow_days_with_paddy_if_eligible")
            : int.MinValue;
        Require(matches.Length == 1 &&
                ReadRequiredBool(matches[0], "hard_rule_allows_planting") &&
                ReadRequiredBool(
                    matches[0],
                    "can_mature_before_season_end_with_paddy_if_eligible") &&
                adjustedGrowDays > 0 &&
                adjustedGrowDays <= baseGrowDays,
            "Money Revenue crop planting tile cannot meet the conservative native growth schedule: " +
            TileName(tile));
    }

    private static bool SameActor(
        SnapshotEnvelope source,
        SnapshotEnvelope planting) =>
        source.SaveId.Status == FieldStatus.Available &&
        planting.SaveId.Status == FieldStatus.Available &&
        source.PlayerId.Status == FieldStatus.Available &&
        planting.PlayerId.Status == FieldStatus.Available &&
        !string.IsNullOrWhiteSpace(source.SaveId.Value) &&
        !string.IsNullOrWhiteSpace(source.PlayerId.Value) &&
        source.SaveId.Value == planting.SaveId.Value &&
        source.PlayerId.Value == planting.PlayerId.Value;

    private static void ValidateSnapshot(
        SnapshotEnvelope snapshot,
        string label)
    {
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
                !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            "Money Revenue crop " + label + " snapshot identity is invalid.");
    }

    private static JsonElement ReadStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            sectionValue.ValueKind != JsonValueKind.Object ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not
                (FieldStatus.Available or FieldStatus.Derived) ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                "Money Revenue crop planting-layout field is unavailable: " +
                section + "." + field);
        }
        return value;
    }

    private static int ReadStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadStateValue(snapshot, section, field).GetInt32();

    private static string ReadStateString(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadStateValue(snapshot, section, field).GetString() ??
        throw new InvalidDataException(
            "Money Revenue crop planting-layout string field is empty: " +
            section + "." + field);

    private static bool ReadRequiredBool(JsonElement row, string property)
    {
        Require(row.TryGetProperty(property, out var value) &&
                value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Money Revenue crop planting-layout boolean is unavailable: " +
            property);
        return value.GetBoolean();
    }

    private static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var number)
            ? number
            : int.MinValue;

    private static string UnqualifyObjectId(string qualifiedItemId)
    {
        Require(qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) &&
                qualifiedItemId.Length > 3,
            "Money Revenue crop seed qualified item ID is invalid.");
        return qualifiedItemId[3..];
    }

    private static string TileName(MoneyRevenueCropPlantingTile tile) =>
        tile.TileX + "," + tile.TileY;

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
