using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static class CropTeacherFirstDayStageKinds
{
    public const string AcquireSeed = "acquire_seed";
    public const string RouteToFarm = "route_to_farm";
    public const string PrepareSite = "prepare_site";
    public const string ExecuteCropBatch = "execute_crop_batch";
    public const string RouteHome = "route_home";
    public const string NativeSleep = "native_sleep";
    public const string Complete = "complete";
}

public sealed record CropTeacherFirstDayStep(
    string Stage,
    int TotalDay,
    string CommitmentId,
    string TargetLocationId,
    string HomeLocationId,
    string SeedQualifiedItemId,
    string ShopId,
    int MaxUnitPrice,
    int RemainingQuantity,
    MoneyRevenueCropNativeProductionActionBatch? ActionBatch,
    string Reason);

public static class CropTeacherFirstDayPlanner
{
    private const string FarmLocationId = "Farm";
    private const string PlantOptionId = "executor.plant_seed";
    private const string WaterOptionId = "executor.water_crop";
    private const string HarvestOptionId = "executor.harvest_crop";

    public static CropTeacherFirstDayStep Evaluate(
        MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request,
        SnapshotEnvelope snapshot,
        IReadOnlySet<string>? preparedCommitmentIds = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        preparedCommitmentIds ??= new HashSet<string>(StringComparer.Ordinal);

        ValidatePlan(plan, request);
        var firstDay = plan.Days.Single(day => day.TotalDay == plan.FirstTotalDay);
        var totalDay = ReadRequiredInt(snapshot, "time", "total_days");
        if (totalDay < plan.FirstTotalDay)
        {
            throw new InvalidDataException(
                "crop_teacher_snapshot_precedes_first_run_day");
        }
        if (totalDay > plan.FirstTotalDay)
        {
            return Step(
                CropTeacherFirstDayStageKinds.Complete,
                totalDay,
                reason: "first_native_day_boundary_crossed");
        }

        var currentLocation = ReadRequiredString(
            snapshot,
            "player",
            "location_id");
        var inventory = ReadRequiredArray(snapshot, "player", "inventory");
        var crops = ReadCropRows(snapshot);
        var pendingPlantBatches = firstDay.ActionBatches
            .Where(batch => batch.OptionId == PlantOptionId)
            .Select(batch => FilterPlantBatch(batch, crops))
            .Where(batch => batch.Tiles.Length > 0)
            .ToArray();

        var inventoryBySeed = ReadInventoryByQualifiedItemId(inventory);
        foreach (var seedGroup in pendingPlantBatches
                     .GroupBy(batch => batch.SeedQualifiedItemId)
                     .OrderBy(group => group.Min(batch => batch.BatchIndex)))
        {
            var remainingQuantity = seedGroup.Sum(batch => batch.Tiles.Length);
            inventoryBySeed.TryGetValue(seedGroup.Key, out var availableQuantity);
            if (availableQuantity >= remainingQuantity)
            {
                continue;
            }

            var firstPendingBatch = seedGroup.OrderBy(batch => batch.BatchIndex).First();
            var acquisition = plan.SeedAcquisitions
                .Where(value =>
                    value.CommitmentId == firstPendingBatch.CommitmentId &&
                    value.SeedQualifiedItemId == seedGroup.Key)
                .OrderBy(value => value.AcquisitionIndex)
                .FirstOrDefault() ?? plan.SeedAcquisitions
                .Where(value => value.SeedQualifiedItemId == seedGroup.Key)
                .OrderBy(value => value.AcquisitionIndex)
                .FirstOrDefault() ?? throw new InvalidDataException(
                    "crop_teacher_seed_shortfall_has_no_acquisition_intent");
            if (totalDay > acquisition.DeadlineTotalDay)
            {
                throw new InvalidDataException(
                    "crop_teacher_seed_acquisition_deadline_missed");
            }

            return new CropTeacherFirstDayStep(
                CropTeacherFirstDayStageKinds.AcquireSeed,
                totalDay,
                acquisition.CommitmentId,
                string.Empty,
                string.Empty,
                acquisition.SeedQualifiedItemId,
                acquisition.ShopId,
                acquisition.MaxUnitPrice,
                remainingQuantity - availableQuantity,
                null,
                "first_day_pending_plant_tiles_exceed_seed_inventory");
        }

        var cropWorkRequiresFarm = pendingPlantBatches.Length > 0;
        if (!cropWorkRequiresFarm)
        {
            cropWorkRequiresFarm = firstDay.ActionBatches
                .Where(batch => batch.OptionId == WaterOptionId)
                .Select(batch => FilterWaterBatch(batch, crops))
                .Any(batch => batch.Tiles.Length > 0);
        }
        if (cropWorkRequiresFarm &&
            !string.Equals(currentLocation, plan.LocationId, StringComparison.Ordinal))
        {
            return Step(
                CropTeacherFirstDayStageKinds.RouteToFarm,
                totalDay,
                targetLocationId: plan.LocationId,
                reason: "first_day_crop_work_requires_farm_location");
        }

        foreach (var commitmentId in firstDay.CaptureLayoutCommitmentIds)
        {
            if (pendingPlantBatches.Any(batch =>
                    batch.CommitmentId == commitmentId) &&
                !preparedCommitmentIds.Contains(commitmentId))
            {
                return Step(
                    CropTeacherFirstDayStageKinds.PrepareSite,
                    totalDay,
                    commitmentId: commitmentId,
                    targetLocationId: plan.LocationId,
                    reason: "planting_layout_requires_site_preparation");
            }
        }

        foreach (var batch in firstDay.ActionBatches.OrderBy(value => value.BatchIndex))
        {
            var pending = batch.OptionId switch
            {
                PlantOptionId => FilterPlantBatch(batch, crops),
                WaterOptionId => FilterWaterBatch(batch, crops),
                HarvestOptionId => throw new InvalidDataException(
                    "crop_teacher_first_day_harvest_requires_transition_ledger"),
                _ => throw new InvalidDataException(
                    "crop_teacher_first_day_action_not_supported:" + batch.OptionId)
            };
            if (pending.Tiles.Length > 0)
            {
                return new CropTeacherFirstDayStep(
                    CropTeacherFirstDayStageKinds.ExecuteCropBatch,
                    totalDay,
                    pending.CommitmentId,
                    pending.LocationId,
                    string.Empty,
                    pending.SeedQualifiedItemId,
                    string.Empty,
                    0,
                    pending.Tiles.Length,
                    pending,
                    "first_incomplete_authoritative_crop_batch");
            }
        }

        if (!firstDay.NativeSleepRequired)
        {
            return Step(
                CropTeacherFirstDayStageKinds.Complete,
                totalDay,
                reason: "first_native_day_has_no_sleep_boundary");
        }

        var homeContext = ReadRequiredObject(
            snapshot,
            "current_location",
            "home_context");
        var homeLocationId = ReadRequiredString(
            homeContext,
            "home_location_id",
            "crop_teacher_home_location_unavailable");
        var currentLocationIsHome = ReadRequiredBool(
            homeContext,
            "current_location_is_home",
            "crop_teacher_home_membership_unavailable");
        if (!currentLocationIsHome ||
            !string.Equals(currentLocation, homeLocationId, StringComparison.Ordinal))
        {
            return Step(
                CropTeacherFirstDayStageKinds.RouteHome,
                totalDay,
                targetLocationId: homeLocationId,
                homeLocationId: homeLocationId,
                reason: "first_day_crop_work_complete_route_to_native_sleep");
        }

        return Step(
            CropTeacherFirstDayStageKinds.NativeSleep,
            totalDay,
            targetLocationId: homeLocationId,
            homeLocationId: homeLocationId,
            reason: "first_day_crop_work_complete_at_home");
    }

    private static void ValidatePlan(
        MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request)
    {
        if (plan.SchemaVersion != "money_revenue_crop_native_production_run_plan.v3" ||
            plan.Status != "native_crop_production_run_ready" ||
            !plan.NativeProductionRunReady ||
            plan.FormalProductTrainingAuthorized ||
            request.SchemaVersion !=
                "money_revenue_crop_native_production_run_plan_request.v2" ||
            request.FormalProductTrainingAuthorized ||
            plan.FirstTotalDay < 0 ||
            plan.FirstTotalDay != plan.SourceTotalDay ||
            plan.MaxQueueItemCount is < 1 or > 8 ||
            request.MaxQueueItemCount != plan.MaxQueueItemCount ||
            !string.Equals(plan.LocationId, FarmLocationId, StringComparison.Ordinal) ||
            plan.Days.Count(day => day.TotalDay == plan.FirstTotalDay) != 1)
        {
            throw new InvalidDataException("crop_teacher_native_run_plan_invalid");
        }

        var layouts = request.LayoutIntents
            .GroupBy(value => value.CommitmentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var batch in plan.Days.SelectMany(day => day.ActionBatches))
        {
            if (!layouts.TryGetValue(batch.CommitmentId, out var matches) ||
                matches.Length != 1 ||
                !string.Equals(matches[0].LocationId, batch.LocationId, StringComparison.Ordinal) ||
                batch.Tiles.Length is < 1 or > 8 ||
                batch.Tiles.Distinct().Count() != batch.Tiles.Length ||
                batch.Tiles.Any(tile => !matches[0].Tiles.Contains(tile)))
            {
                throw new InvalidDataException(
                    "crop_teacher_native_batch_layout_binding_invalid");
            }
        }
    }

    private static MoneyRevenueCropNativeProductionActionBatch FilterPlantBatch(
        MoneyRevenueCropNativeProductionActionBatch batch,
        IReadOnlyDictionary<(int X, int Y), JsonElement> crops)
    {
        var expectedSeedId = NormalizeQualifiedObjectId(batch.SeedQualifiedItemId);
        if (string.IsNullOrWhiteSpace(expectedSeedId))
        {
            throw new InvalidDataException("crop_teacher_plant_seed_identity_missing");
        }

        var pending = new List<MoneyRevenueCropPlantingTile>();
        foreach (var tile in batch.Tiles)
        {
            if (!crops.TryGetValue((tile.TileX, tile.TileY), out var crop))
            {
                pending.Add(tile);
                continue;
            }
            if (!string.Equals(
                    ReadRequiredString(
                        crop,
                        "harvest_source_seed_id",
                        "crop_teacher_existing_crop_seed_identity_unavailable"),
                    expectedSeedId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "crop_teacher_existing_crop_seed_identity_mismatch");
            }
        }
        return CopyBatch(batch, pending);
    }

    private static MoneyRevenueCropNativeProductionActionBatch FilterWaterBatch(
        MoneyRevenueCropNativeProductionActionBatch batch,
        IReadOnlyDictionary<(int X, int Y), JsonElement> crops)
    {
        var pending = new List<MoneyRevenueCropPlantingTile>();
        foreach (var tile in batch.Tiles)
        {
            if (!crops.TryGetValue((tile.TileX, tile.TileY), out var crop))
            {
                throw new InvalidDataException(
                    "crop_teacher_water_target_crop_missing");
            }
            if (ReadRequiredBool(
                    crop,
                    "needs_watering",
                    "crop_teacher_water_requirement_unavailable"))
            {
                pending.Add(tile);
            }
        }
        return CopyBatch(batch, pending);
    }

    private static MoneyRevenueCropNativeProductionActionBatch CopyBatch(
        MoneyRevenueCropNativeProductionActionBatch batch,
        IEnumerable<MoneyRevenueCropPlantingTile> tiles) => new(
            batch.BatchIndex,
            batch.CommitmentId,
            batch.OptionId,
            batch.ExecutionCondition,
            batch.LocationId,
            batch.SeedQualifiedItemId,
            tiles.ToArray());

    private static Dictionary<(int X, int Y), JsonElement> ReadCropRows(
        SnapshotEnvelope snapshot)
    {
        var rows = ReadRequiredArray(snapshot, "farm", "crops");
        var result = new Dictionary<(int X, int Y), JsonElement>();
        foreach (var row in rows.EnumerateArray())
        {
            var locationId = ReadOptionalString(row, "location_id");
            if (locationId.Length > 0 && locationId != FarmLocationId)
            {
                continue;
            }
            var key = (
                ReadRequiredInt(row, "tile_x", "crop_teacher_crop_tile_x_unavailable"),
                ReadRequiredInt(row, "tile_y", "crop_teacher_crop_tile_y_unavailable"));
            if (!result.TryAdd(key, row))
            {
                throw new InvalidDataException("crop_teacher_crop_tile_duplicated");
            }
        }
        return result;
    }

    private static Dictionary<string, int> ReadInventoryByQualifiedItemId(
        JsonElement inventory)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in inventory.EnumerateArray())
        {
            if (ReadOptionalBool(row, "is_empty") == true)
            {
                continue;
            }
            var qualifiedItemId = ReadOptionalString(row, "qualified_item_id");
            if (qualifiedItemId.Length == 0)
            {
                continue;
            }
            var stack = ReadRequiredInt(
                row,
                "stack",
                "crop_teacher_inventory_stack_unavailable");
            if (stack < 0)
            {
                throw new InvalidDataException("crop_teacher_inventory_stack_invalid");
            }
            result[qualifiedItemId] = checked(
                result.GetValueOrDefault(qualifiedItemId) + stack);
        }
        return result;
    }

    private static JsonElement ReadRequiredArray(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadRequiredField(snapshot, section, field);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"crop_teacher_snapshot_field_not_array:{section}.{field}");
        }
        return value;
    }

    private static JsonElement ReadRequiredObject(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadRequiredField(snapshot, section, field);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"crop_teacher_snapshot_field_not_object:{section}.{field}");
        }
        return value;
    }

    private static JsonElement ReadRequiredField(
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
            status.GetString() is not ("available" or "derived") ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"crop_teacher_snapshot_field_unavailable:{section}.{field}");
        }
        return value;
    }

    private static int ReadRequiredInt(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadRequiredField(snapshot, section, field);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException(
                $"crop_teacher_snapshot_field_not_int:{section}.{field}");
        }
        return result;
    }

    private static string ReadRequiredString(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadRequiredField(snapshot, section, field);
        return ReadRequiredString(
            value,
            string.Empty,
            $"crop_teacher_snapshot_field_not_string:{section}.{field}");
    }

    private static int ReadRequiredInt(
        JsonElement value,
        string property,
        string error)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.Number ||
            !propertyValue.TryGetInt32(out var result))
        {
            throw new InvalidDataException(error);
        }
        return result;
    }

    private static string ReadRequiredString(
        JsonElement value,
        string property,
        string error)
    {
        JsonElement propertyValue;
        if (property.Length == 0)
        {
            propertyValue = value;
        }
        else if (value.ValueKind != JsonValueKind.Object ||
                 !value.TryGetProperty(property, out propertyValue))
        {
            throw new InvalidDataException(error);
        }
        if (propertyValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(propertyValue.GetString()))
        {
            throw new InvalidDataException(error);
        }
        return propertyValue.GetString()!;
    }

    private static bool ReadRequiredBool(
        JsonElement value,
        string property,
        string error)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException(error);
        }
        return propertyValue.GetBoolean();
    }

    private static string ReadOptionalString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var propertyValue) &&
        propertyValue.ValueKind == JsonValueKind.String
            ? propertyValue.GetString() ?? string.Empty
            : string.Empty;

    private static bool? ReadOptionalBool(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var propertyValue) &&
        propertyValue.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? propertyValue.GetBoolean()
            : null;

    private static string NormalizeQualifiedObjectId(string qualifiedItemId) =>
        qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal)
            ? qualifiedItemId[3..]
            : qualifiedItemId;

    private static CropTeacherFirstDayStep Step(
        string stage,
        int totalDay,
        string commitmentId = "",
        string targetLocationId = "",
        string homeLocationId = "",
        string reason = "") => new(
            stage,
            totalDay,
            commitmentId,
            targetLocationId,
            homeLocationId,
            string.Empty,
            string.Empty,
            0,
            0,
            null,
            reason);
}
