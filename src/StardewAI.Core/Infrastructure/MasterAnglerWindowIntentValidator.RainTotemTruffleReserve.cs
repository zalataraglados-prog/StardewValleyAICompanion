using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static partial class MasterAnglerWindowIntentValidator
{
    public const string TruffleOilReserveStageParameter =
        "master_angler_truffle_oil_reserve_stage";
    public const string TruffleOilReserveInventoryOilParameter =
        "master_angler_truffle_oil_inventory_quantity";
    public const string TruffleOilReserveInventoryTruffleParameter =
        "master_angler_truffle_inventory_quantity";
    public const string TruffleOilReserveOilMakerCountParameter =
        "master_angler_truffle_oil_maker_count";
    public const string TruffleOilReserveProjectedOutputParameter =
        "master_angler_truffle_oil_projected_output_quantity";
    public const string TruffleOilReservePigCountParameter =
        "master_angler_truffle_pig_count";
    public const string TruffleOilReserveAdultPigCountParameter =
        "master_angler_truffle_adult_pig_count";
    public const string TruffleOilReserveMaturityDaysParameter =
        "master_angler_truffle_minimum_pig_days_to_maturity";
    public const string TruffleOilReserveDoorOpenParameter =
        "master_angler_truffle_target_animal_door_open";
    public const string TruffleOilReserveBuildingTypeParameter =
        "master_angler_truffle_target_building_type";
    public const string TruffleOilReserveBuildingLocationParameter =
        "master_angler_truffle_target_building_location_id";
    public const string TruffleOilReserveBuildingXParameter =
        "master_angler_truffle_target_building_tile_x";
    public const string TruffleOilReserveBuildingYParameter =
        "master_angler_truffle_target_building_tile_y";
    public const string TruffleOilReserveLeadDaysParameter =
        "master_angler_truffle_required_lead_days";
    public const string TruffleOilReserveOpportunityDaysParameter =
        "master_angler_truffle_eligible_opportunity_days";

    private const string TruffleOilReserveValidationStatus =
        "authoritative_future_rain_window_truffle_oil_capacity_reserve";
    private const string TruffleOilQualifiedItemId = "(O)432";
    private const string TruffleQualifiedItemId = "(O)430";
    private const string OilMakerQualifiedItemId = "(BC)19";
    private const string PigTypeId = "Pig";
    private const int PigDaysToMature = 10;

    private sealed record TruffleReserveStage(
        string Stage,
        int RequiredLeadDays,
        int ProductionReadyOffsetDays,
        string TargetBuildingType,
        string TargetBuildingLocationId,
        int TargetBuildingTileX,
        int TargetBuildingTileY,
        bool TargetAnimalDoorOpen);

    private static bool TryBuildRainTotemTruffleOilReserveIntent(
        SnapshotEnvelope snapshot,
        ParsedArtifact artifact,
        string indexPath,
        string indexHash,
        out SmallModelActionParameter[] parameters,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        parameters = [];
        if (!TryProjectRainTotemTruffleOilReserve(
                snapshot,
                artifact,
                indexPath,
                indexHash,
                out validation,
                out rejectionReason))
            return false;

        parameters = CanonicalRainTotemTruffleOilReserveParameters(validation);
        if (TryValidateRainTotemTruffleOilReserveIntent(
                snapshot,
                parameters,
                out validation,
                out rejectionReason))
            return true;
        parameters = [];
        return false;
    }

    private static bool TryValidateRainTotemTruffleOilReserveIntent(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        validation = new();
        rejectionReason = string.Empty;
        if (!TryRead(parameters, WeatherReserveStatusParameter, out var status) ||
            !TryReadInt(parameters, WeatherReserveTargetDayParameter, out var targetDay) ||
            !TryReadInt(parameters, WeatherReserveUseDayParameter, out var useDay) ||
            !TryRead(parameters, WeatherReserveTargetIdsParameter, out var targetIdsJson) ||
            !TryRead(parameters, WeatherReserveAffectedContextParameter, out var affectedContext) ||
            !TryRead(parameters, WeatherReserveWindowIndexPathParameter, out var indexPath) ||
            !TryRead(parameters, WeatherReserveWindowIndexSha256Parameter, out var expectedHash) ||
            !TryRead(parameters, WeatherReserveMaterialIdParameter, out var materialId) ||
            !TryReadInt(parameters, WeatherReserveRequiredQuantityParameter, out var requiredQuantity) ||
            !TryRead(parameters, TruffleOilReserveStageParameter, out var stage) ||
            !TryReadInt(parameters, TruffleOilReserveInventoryOilParameter, out var inventoryOil) ||
            !TryReadInt(parameters, TruffleOilReserveInventoryTruffleParameter, out var inventoryTruffle) ||
            !TryReadInt(parameters, TruffleOilReserveOilMakerCountParameter, out var oilMakerCount) ||
            !TryReadInt(parameters, TruffleOilReserveProjectedOutputParameter, out var projectedOutput) ||
            !TryReadInt(parameters, TruffleOilReservePigCountParameter, out var pigCount) ||
            !TryReadInt(parameters, TruffleOilReserveAdultPigCountParameter, out var adultPigCount) ||
            !TryReadInt(parameters, TruffleOilReserveMaturityDaysParameter, out var maturityDays) ||
            !TryRead(parameters, TruffleOilReserveDoorOpenParameter, out var doorOpenText) ||
            !bool.TryParse(doorOpenText, out var doorOpen) ||
            !TryRead(parameters, TruffleOilReserveBuildingTypeParameter, out var buildingType) ||
            !TryRead(parameters, TruffleOilReserveBuildingLocationParameter, out var buildingLocation) ||
            !TryReadInt(parameters, TruffleOilReserveBuildingXParameter, out var buildingX) ||
            !TryReadInt(parameters, TruffleOilReserveBuildingYParameter, out var buildingY) ||
            !TryReadInt(parameters, TruffleOilReserveLeadDaysParameter, out var leadDays) ||
            !TryReadInt(parameters, TruffleOilReserveOpportunityDaysParameter, out var opportunityDays) ||
            !TryRead(parameters, WeatherReserveReplanRequiredParameter, out var replanRequired) ||
            !TryReadQualifiedItemIds(targetIdsJson, out var targetIds))
        {
            rejectionReason = "master_angler_truffle_oil_reserve_contract_incomplete_or_duplicated";
            return false;
        }
        if (!string.Equals(status, TruffleOilReserveValidationStatus, StringComparison.Ordinal) ||
            !string.Equals(materialId, TruffleOilQualifiedItemId, StringComparison.Ordinal) ||
            requiredQuantity != 1 || inventoryOil < 0 || inventoryTruffle < 0 ||
            oilMakerCount < 0 || projectedOutput < 0 || pigCount < 0 ||
            adultPigCount < 0 || adultPigCount > pigCount || maturityDays < 0 ||
            leadDays < 1 || opportunityDays < 1 || !IsSha256(expectedHash) ||
            !string.Equals(replanRequired, "true", StringComparison.Ordinal))
        {
            rejectionReason = "master_angler_truffle_oil_reserve_contract_invalid";
            return false;
        }
        if (!TryLoadArtifact(indexPath, expectedHash, out var artifact, out rejectionReason) ||
            !TryProjectRainTotemTruffleOilReserve(
                snapshot,
                artifact,
                Path.GetFullPath(indexPath),
                expectedHash,
                out var projected,
                out rejectionReason))
            return false;

        if (targetDay != projected.TargetTotalDay || useDay != projected.UseTotalDay ||
            !targetIds.SequenceEqual(projected.TargetQualifiedItemIds, StringComparer.Ordinal) ||
            !string.Equals(affectedContext, projected.AffectedLocationContextId, StringComparison.Ordinal) ||
            !string.Equals(stage, projected.ReserveStage, StringComparison.Ordinal) ||
            inventoryOil != projected.InventoryTruffleOilQuantity ||
            inventoryTruffle != projected.InventoryTruffleQuantity ||
            oilMakerCount != projected.ExistingOilMakerCount ||
            projectedOutput != projected.ExistingOilMakerProjectedOutputQuantity ||
            pigCount != projected.PigCount || adultPigCount != projected.AdultPigCount ||
            maturityDays != projected.MinimumPigDaysToMaturity ||
            doorOpen != projected.TargetAnimalDoorOpen ||
            !string.Equals(buildingType, projected.TargetBuildingType, StringComparison.Ordinal) ||
            !string.Equals(buildingLocation, projected.TargetBuildingLocationId, StringComparison.Ordinal) ||
            buildingX != projected.TargetBuildingTileX || buildingY != projected.TargetBuildingTileY ||
            leadDays != projected.RequiredLeadDays ||
            opportunityDays != projected.EligibleTruffleOpportunityDays)
        {
            rejectionReason = "master_angler_truffle_oil_reserve_projection_drifted";
            return false;
        }
        validation = projected;
        return true;
    }

    private static SmallModelActionParameter[] CanonicalRainTotemTruffleOilReserveParameters(
        MasterAnglerRainTotemReserveIntentValidation value) =>
    [
        Parameter(WeatherReserveStatusParameter, TruffleOilReserveValidationStatus),
        Parameter(WeatherReserveTargetDayParameter, value.TargetTotalDay.ToString(CultureInfo.InvariantCulture)),
        Parameter(WeatherReserveUseDayParameter, value.UseTotalDay.ToString(CultureInfo.InvariantCulture)),
        Parameter(WeatherReserveTargetIdsParameter, JsonSerializer.Serialize(value.TargetQualifiedItemIds)),
        Parameter(WeatherReserveAffectedContextParameter, value.AffectedLocationContextId),
        Parameter(WeatherReserveWindowIndexPathParameter, value.WindowIndexPath),
        Parameter(WeatherReserveWindowIndexSha256Parameter, value.WindowIndexSha256),
        Parameter(WeatherReserveMaterialIdParameter, TruffleOilQualifiedItemId),
        Parameter(WeatherReserveRequiredQuantityParameter, "1"),
        Parameter(TruffleOilReserveStageParameter, value.ReserveStage),
        Parameter(TruffleOilReserveInventoryOilParameter, value.InventoryTruffleOilQuantity.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveInventoryTruffleParameter, value.InventoryTruffleQuantity.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveOilMakerCountParameter, value.ExistingOilMakerCount.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveProjectedOutputParameter, value.ExistingOilMakerProjectedOutputQuantity.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReservePigCountParameter, value.PigCount.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveAdultPigCountParameter, value.AdultPigCount.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveMaturityDaysParameter, value.MinimumPigDaysToMaturity.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveDoorOpenParameter, value.TargetAnimalDoorOpen.ToString().ToLowerInvariant()),
        Parameter(TruffleOilReserveBuildingTypeParameter, value.TargetBuildingType),
        Parameter(TruffleOilReserveBuildingLocationParameter, value.TargetBuildingLocationId),
        Parameter(TruffleOilReserveBuildingXParameter, value.TargetBuildingTileX.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveBuildingYParameter, value.TargetBuildingTileY.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveLeadDaysParameter, value.RequiredLeadDays.ToString(CultureInfo.InvariantCulture)),
        Parameter(TruffleOilReserveOpportunityDaysParameter, value.EligibleTruffleOpportunityDays.ToString(CultureInfo.InvariantCulture)),
        Parameter(WeatherReserveReplanRequiredParameter, "true")
    ];

    private static bool TryProjectRainTotemTruffleOilReserve(
        SnapshotEnvelope snapshot,
        ParsedArtifact artifact,
        string indexPath,
        string indexHash,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        validation = new();
        var inventoryOil = InventoryQuantity(snapshot, TruffleOilQualifiedItemId);
        if (inventoryOil >= 1)
        {
            rejectionReason = "master_angler_truffle_oil_inventory_already_sufficient";
            return false;
        }
        if (!TryProjectTruffleReserveStage(snapshot, out var stage, out var facts, out rejectionReason))
            return false;

        FutureRainTotemReserveWindow? window = null;
        var opportunityDays = 0;
        var minimumLead = stage.RequiredLeadDays;
        while (minimumLead < artifact.DeadlineTotalDayExclusive)
        {
            if (!TryProjectRainTotemReserveWindow(
                    snapshot,
                    artifact,
                    indexPath,
                    indexHash,
                    minimumLead,
                    out var candidate,
                    out rejectionReason))
                return false;
            opportunityDays = CountNonWinterTruffleOpportunityDays(
                candidate.CurrentTotalDay + stage.ProductionReadyOffsetDays + 1,
                candidate.UseTotalDay);
            if (opportunityDays > 0)
            {
                window = candidate;
                break;
            }
            minimumLead = candidate.DaysToUse + 1;
        }
        if (window is null)
        {
            rejectionReason = "master_angler_truffle_oil_no_non_winter_production_opportunity";
            return false;
        }

        var projectedOutput = ProjectExistingOilMakerOutputs(
            snapshot,
            window.CurrentTime,
            window.DaysToUse);
        if (projectedOutput > 0)
        {
            rejectionReason = "master_angler_truffle_oil_existing_machine_output_sufficient";
            return false;
        }

        validation = new MasterAnglerRainTotemReserveIntentValidation
        {
            ReserveKind = "truffle_oil_capacity",
            ReserveStage = stage.Stage,
            TargetTotalDay = window.TargetTotalDay,
            UseTotalDay = window.UseTotalDay,
            TargetQualifiedItemIds = window.TargetQualifiedItemIds,
            AffectedLocationContextId = window.AffectedLocationContextId,
            WindowIndexPath = window.WindowIndexPath,
            WindowIndexSha256 = window.WindowIndexSha256,
            InventoryTruffleOilQuantity = inventoryOil,
            InventoryTruffleQuantity = facts.InventoryTruffleQuantity,
            ExistingOilMakerCount = facts.OilMakerCount,
            ExistingOilMakerProjectedOutputQuantity = projectedOutput,
            PigCount = facts.PigCount,
            AdultPigCount = facts.AdultPigCount,
            MinimumPigDaysToMaturity = facts.MinimumPigDaysToMaturity,
            TargetAnimalDoorOpen = stage.TargetAnimalDoorOpen,
            TargetBuildingType = stage.TargetBuildingType,
            TargetBuildingLocationId = stage.TargetBuildingLocationId,
            TargetBuildingTileX = stage.TargetBuildingTileX,
            TargetBuildingTileY = stage.TargetBuildingTileY,
            RequiredLeadDays = stage.RequiredLeadDays,
            EligibleTruffleOpportunityDays = opportunityDays
        };
        rejectionReason = string.Empty;
        return true;
    }

    private sealed record TruffleReserveFacts(
        int InventoryTruffleQuantity,
        int OilMakerCount,
        int PigCount,
        int AdultPigCount,
        int MinimumPigDaysToMaturity);

    private static bool TryProjectTruffleReserveStage(
        SnapshotEnvelope snapshot,
        out TruffleReserveStage stage,
        out TruffleReserveFacts facts,
        out string rejectionReason)
    {
        stage = new("", 0, 0, "", "", -1, -1, false);
        rejectionReason = string.Empty;
        var inventoryTruffle = InventoryQuantity(snapshot, TruffleQualifiedItemId);
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        var machineRows = machines.HasValue && machines.Value.ValueKind == JsonValueKind.Array
            ? machines.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object).ToArray()
            : Array.Empty<JsonElement>();
        var oilMakerCount = machineRows.Count(row =>
            string.Equals(ReadString(row, "qualified_item_id"), OilMakerQualifiedItemId, StringComparison.Ordinal));
        var readyOil = machineRows.Any(row =>
            string.Equals(ReadString(row, "qualified_item_id"), OilMakerQualifiedItemId, StringComparison.Ordinal) &&
            ReadBool(row, "ready_for_harvest") == true &&
            row.TryGetProperty("held_item", out var held) &&
            string.Equals(ReadString(held, "qualified_item_id"), TruffleOilQualifiedItemId, StringComparison.Ordinal));
        var animals = ReadStateFieldValue(snapshot, "farm", "animals");
        var pigs = animals.HasValue && animals.Value.ValueKind == JsonValueKind.Array
            ? animals.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "type"), PigTypeId, StringComparison.Ordinal)).ToArray()
            : Array.Empty<JsonElement>();
        var adultPigs = pigs.Where(row => ReadBool(row, "is_adult") == true).ToArray();
        var minimumMaturity = pigs.Length == 0
            ? PigDaysToMature
            : pigs.Min(row => Math.Max(0, ReadInt(row, "days_to_mature") - ReadInt(row, "age")));
        facts = new(inventoryTruffle, oilMakerCount, pigs.Length, adultPigs.Length, minimumMaturity);

        if (readyOil)
        {
            stage = new(
                "collect_ready_truffle_oil",
                1,
                0,
                "not_applicable",
                "not_applicable",
                -1,
                -1,
                false);
            return true;
        }
        if (FarmHasSpawnedTruffle(snapshot))
        {
            stage = new(
                "collect_spawned_truffle",
                1,
                0,
                "not_applicable",
                "not_applicable",
                -1,
                -1,
                false);
            return true;
        }
        if (inventoryTruffle > 0)
        {
            stage = oilMakerCount > 0
                ? new(
                    "load_truffle_into_oil_maker",
                    1,
                    0,
                    "not_applicable",
                    "not_applicable",
                    -1,
                    -1,
                    false)
                : new(
                    "establish_oil_maker",
                    1,
                    0,
                    "not_applicable",
                    "not_applicable",
                    -1,
                    -1,
                    false);
            return true;
        }

        if (pigs.Length == 0)
        {
            return TryProjectMissingPigStage(snapshot, out stage, out rejectionReason);
        }
        var pig = adultPigs.FirstOrDefault();
        if (adultPigs.Length == 0)
        {
            if (oilMakerCount == 0)
            {
                stage = new(
                    "establish_oil_maker",
                    Math.Max(1, minimumMaturity + 1),
                    minimumMaturity,
                    ReadString(pigs[0], "management_home_building_type"),
                    "Farm",
                    SnapshotValueReader.ReadInt(
                        pigs[0],
                        "management_home_building_tile_x",
                        -1),
                    SnapshotValueReader.ReadInt(
                        pigs[0],
                        "management_home_building_tile_y",
                        -1),
                    false);
                return true;
            }
            stage = new("wait_for_pig_maturity", Math.Max(1, minimumMaturity + 1), minimumMaturity,
                ReadString(pigs[0], "management_home_building_type"), "Farm",
                SnapshotValueReader.ReadInt(pigs[0], "management_home_building_tile_x", -1),
                SnapshotValueReader.ReadInt(pigs[0], "management_home_building_tile_y", -1), false);
            return true;
        }

        var homeType = ReadString(pig, "management_home_building_type");
        var homeX = SnapshotValueReader.ReadInt(pig, "management_home_building_tile_x", -1);
        var homeY = SnapshotValueReader.ReadInt(pig, "management_home_building_tile_y", -1);
        var doorOpen = TryReadBuildingAnimalDoorOpen(snapshot, homeType, homeX, homeY, out var exactDoorOpen) && exactDoorOpen;
        if (!doorOpen)
        {
            stage = new("open_pig_home_animal_door", 1, 0, homeType, "Farm", homeX, homeY, false);
            return true;
        }
        if (oilMakerCount == 0)
        {
            stage = new("establish_oil_maker", 1, 0, homeType, "Farm", homeX, homeY, true);
            return true;
        }
        stage = new("await_native_pig_truffle", 1, 0, homeType, "Farm", homeX, homeY, true);
        return true;
    }

    private static bool TryProjectMissingPigStage(
        SnapshotEnvelope snapshot,
        out TruffleReserveStage stage,
        out string rejectionReason)
    {
        stage = new("", 0, 0, "", "", -1, -1, false);
        var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
        var rows = buildings.HasValue && buildings.Value.ValueKind == JsonValueKind.Array
            ? buildings.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object).ToArray()
            : Array.Empty<JsonElement>();
        var deluxe = rows.FirstOrDefault(row => ReadString(row, "type") == "Deluxe Barn" && ReadBool(row, "is_under_construction") != true);
        if (deluxe.ValueKind == JsonValueKind.Object)
        {
            if (!TryReadPigPurchaseHome(snapshot, out var location, out var x, out var y))
            {
                rejectionReason = "master_angler_truffle_oil_pig_purchase_projection_unavailable";
                return false;
            }
            stage = new("purchase_pig", PigDaysToMature + 1, PigDaysToMature,
                "Deluxe Barn", location, x, y, ReadBuildingDoorOpen(deluxe));
            rejectionReason = string.Empty;
            return true;
        }

        var big = rows.FirstOrDefault(row => ReadString(row, "type") == "Big Barn");
        if (big.ValueKind == JsonValueKind.Object)
        {
            var remaining = BuildingRemainingDays(big);
            if (remaining > 0)
            {
                stage = new("wait_for_barn_upgrade", remaining + PigDaysToMature + 1,
                    remaining + PigDaysToMature, "Deluxe Barn", "Farm",
                    SnapshotValueReader.ReadInt(big, "tile_x", -1),
                    SnapshotValueReader.ReadInt(big, "tile_y", -1), false);
                rejectionReason = string.Empty;
                return true;
            }
            return TryBuildingProjectStage(snapshot, "Deluxe Barn", "upgrade_deluxe_barn",
                PigDaysToMature + 1, out stage, out rejectionReason);
        }

        var barn = rows.FirstOrDefault(row => ReadString(row, "type") == "Barn");
        if (barn.ValueKind == JsonValueKind.Object)
        {
            var remaining = BuildingRemainingDays(barn);
            var bigUpgradeDays = UpgradeDefinitionDays(snapshot, "Big Barn");
            var deluxeDays = UpgradeDefinitionDays(snapshot, "Deluxe Barn");
            if (remaining > 0)
            {
                var pendingUpgrade = ReadString(barn, "upgrade_name");
                var afterCurrentProject = string.Equals(
                        pendingUpgrade,
                        "Big Barn",
                        StringComparison.Ordinal)
                    ? deluxeDays + PigDaysToMature + 1
                    : bigUpgradeDays + deluxeDays + PigDaysToMature + 1;
                stage = new("wait_for_barn_upgrade", remaining + afterCurrentProject,
                    remaining + afterCurrentProject - 1, "Big Barn", "Farm",
                    SnapshotValueReader.ReadInt(barn, "tile_x", -1),
                    SnapshotValueReader.ReadInt(barn, "tile_y", -1), false);
                rejectionReason = string.Empty;
                return true;
            }
            return TryBuildingProjectStage(snapshot, "Big Barn", "upgrade_big_barn",
                deluxeDays + PigDaysToMature + 1, out stage, out rejectionReason);
        }

        var barnDays = ConstructionRowDays(snapshot, "Barn");
        var bigDays = UpgradeDefinitionDays(snapshot, "Big Barn");
        var finalDays = UpgradeDefinitionDays(snapshot, "Deluxe Barn");
        if (barnDays <= 0 || bigDays <= 0 || finalDays <= 0)
        {
            rejectionReason = "master_angler_truffle_oil_barn_build_authority_unavailable";
            return false;
        }
        return TryBuildingProjectStage(snapshot, "Barn", "construct_barn",
            bigDays + finalDays + PigDaysToMature + 1, out stage, out rejectionReason);
    }

    private static bool TryBuildingProjectStage(
        SnapshotEnvelope snapshot,
        string targetType,
        string stageName,
        int downstreamLeadDays,
        out TruffleReserveStage stage,
        out string rejectionReason)
    {
        stage = new("", 0, 0, "", "", -1, -1, false);
        var catalog = ReadStateFieldValue(snapshot, "player", "building_construction_catalog");
        if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Object ||
            !catalog.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            rejectionReason = "master_angler_truffle_oil_building_catalog_unavailable";
            return false;
        }
        var row = rows.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(value, "building_type"), targetType, StringComparison.Ordinal) &&
            string.Equals(ReadString(value, "placement_location_id"), "Farm", StringComparison.Ordinal));
        if (row.ValueKind != JsonValueKind.Object)
        {
            rejectionReason = "master_angler_truffle_oil_exact_building_project_unavailable";
            return false;
        }
        var days = Math.Max(ReadInt(row, "build_days"), 1);
        stage = new(stageName, days + downstreamLeadDays, days + downstreamLeadDays - 1,
            targetType, "Farm", SnapshotValueReader.ReadInt(row, "placement_tile_x", -1),
            SnapshotValueReader.ReadInt(row, "placement_tile_y", -1), false);
        rejectionReason = string.Empty;
        return true;
    }

    private static int ConstructionRowDays(SnapshotEnvelope snapshot, string type)
    {
        var catalog = ReadStateFieldValue(snapshot, "player", "building_construction_catalog");
        if (!catalog.HasValue || !catalog.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return 0;
        var row = rows.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object && ReadString(value, "building_type") == type &&
            ReadString(value, "project_kind") == "construction" && ReadString(value, "placement_location_id") == "Farm");
        return row.ValueKind == JsonValueKind.Object ? Math.Max(ReadInt(row, "build_days"), 1) : 0;
    }

    private static int UpgradeDefinitionDays(SnapshotEnvelope snapshot, string targetType)
    {
        var catalog = ReadStateFieldValue(snapshot, "player", "building_construction_catalog");
        if (!catalog.HasValue || !catalog.Value.TryGetProperty("upgrade_definitions", out var definitions) || definitions.ValueKind != JsonValueKind.Array)
            return 0;
        var row = definitions.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object && ReadString(value, "target_building_type") == targetType);
        return row.ValueKind == JsonValueKind.Object ? Math.Max(ReadInt(row, "native_countdown_days"), 1) : 0;
    }

    private static int BuildingRemainingDays(JsonElement building) =>
        Math.Max(ReadInt(building, "days_of_construction_left"), ReadInt(building, "days_until_upgrade"));

    private static bool TryReadPigPurchaseHome(
        SnapshotEnvelope snapshot,
        out string location,
        out int x,
        out int y)
    {
        location = string.Empty;
        x = -1;
        y = -1;
        var catalog = ReadStateFieldValue(snapshot, "farm", "animal_purchase_catalog");
        if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var locationRow in catalog.Value.EnumerateArray())
        {
            if (!locationRow.TryGetProperty("stock", out var stock) || stock.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var pig in stock.EnumerateArray().Where(row =>
                         ReadString(row, "animal_type_id") == PigTypeId &&
                         ReadBool(row, "required_building_met") == true))
            {
                if (!pig.TryGetProperty("compatible_homes", out var homes) || homes.ValueKind != JsonValueKind.Array)
                    continue;
                var home = homes.EnumerateArray().FirstOrDefault(row =>
                    ReadString(row, "building_type") == "Deluxe Barn" &&
                    ReadBool(row, "compatible_with_all_possible_types") == true &&
                    ReadBool(row, "is_under_construction") != true && ReadInt(row, "available_slots") > 0);
                if (home.ValueKind != JsonValueKind.Object)
                    continue;
                location = ReadString(locationRow, "target_location_id");
                x = SnapshotValueReader.ReadInt(home, "building_tile_x", -1);
                y = SnapshotValueReader.ReadInt(home, "building_tile_y", -1);
                return !string.IsNullOrWhiteSpace(location) && x >= 0 && y >= 0;
            }
        }
        return false;
    }

    private static bool FarmHasSpawnedTruffle(SnapshotEnvelope snapshot)
    {
        var farmObjects = ReadStateFieldValue(snapshot, "farm", "objects");
        if (farmObjects.HasValue &&
            farmObjects.Value.ValueKind == JsonValueKind.Array &&
            farmObjects.Value.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadString(row, "qualified_item_id") == TruffleQualifiedItemId))
        {
            return true;
        }

        var currentObjects = ReadStateFieldValue(
            snapshot,
            "current_location",
            "objects");
        return currentObjects.HasValue &&
            currentObjects.Value.ValueKind == JsonValueKind.Array &&
            currentObjects.Value.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object && ReadBool(row, "is_spawned_object") == true &&
                ReadString(row, "qualified_item_id") == TruffleQualifiedItemId);
    }

    private static bool TryReadBuildingAnimalDoorOpen(
        SnapshotEnvelope snapshot,
        string type,
        int x,
        int y,
        out bool open)
    {
        open = false;
        var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
        if (!buildings.HasValue || buildings.Value.ValueKind != JsonValueKind.Array)
            return false;
        var row = buildings.Value.EnumerateArray().FirstOrDefault(value =>
            ReadString(value, "type") == type && ReadInt(value, "tile_x") == x && ReadInt(value, "tile_y") == y);
        if (row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty("animal_door", out var door) || door.ValueKind != JsonValueKind.Object ||
            ReadBool(door, "present") != true || !door.TryGetProperty("is_open", out var state) ||
            state.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        open = state.GetBoolean();
        return true;
    }

    private static bool ReadBuildingDoorOpen(JsonElement building) =>
        building.TryGetProperty("animal_door", out var door) &&
        door.ValueKind == JsonValueKind.Object && ReadBool(door, "is_open") == true;

    private static int ProjectExistingOilMakerOutputs(
        SnapshotEnvelope snapshot,
        int currentTime,
        int daysToUse)
    {
        if (!TryMinutesUntilMorning(currentTime, daysToUse, out var budget))
            return 0;
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            return 0;
        return machines.Value.EnumerateArray().Count(row =>
            row.ValueKind == JsonValueKind.Object &&
            ReadString(row, "qualified_item_id") == OilMakerQualifiedItemId &&
            row.TryGetProperty("held_item", out var held) &&
            ReadString(held, "qualified_item_id") == TruffleOilQualifiedItemId &&
            ReadInt(held, "stack") > 0 &&
            (ReadBool(row, "ready_for_harvest") == true ||
            SnapshotValueReader.ReadInt(row, "minutes_until_ready", int.MaxValue) <= budget));
    }

    private static int CountNonWinterTruffleOpportunityDays(int firstDay, int lastDay)
    {
        if (lastDay < firstDay)
            return 0;
        var count = 0;
        for (var day = Math.Max(0, firstDay); day <= lastDay; day++)
        {
            if (day % 112 / 28 != 3)
                count++;
        }
        return count;
    }
}
