using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropProductionObligationPlanBuilder
{
    private static readonly string[] Seasons =
        { "spring", "summer", "fall", "winter" };

    public static MoneyRevenueCropProductionObligationPlan Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop-production obligation-plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlanRequest>(
                fullRequestPath,
                "Money Revenue crop-production obligation-plan request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_production_obligation_plan_request.v1" &&
                request.DeadlineTotalDayExclusive == 224 &&
                request.TargetTotalMoneyEarned == 1_000_000 &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation-plan request is invalid.");

        var snapshotPath = RequiredFile(
            request.SnapshotPath,
            "Money Revenue crop-production source snapshot");
        var ledgerPath = RequiredFile(
            request.StrategyLedgerPath,
            "Money Revenue crop-production strategy ledger");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Money Revenue crop-production source snapshot");
        ValidateSnapshot(snapshot);
        var ledgerState = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            snapshot);
        Require(ledgerState.Ledger.SourceStateHash == snapshot.StateHash,
            "Money Revenue crop-production ledger is not anchored to the source snapshot.");
        var supply = ReadMaterialSupply(snapshot, ledgerState.Ledger);

        return BuildPlan(
            fullRequestPath,
            CurrentTeacherFrontierSupport.HashFile(fullRequestPath),
            snapshotPath,
            CurrentTeacherFrontierSupport.HashFile(snapshotPath),
            ledgerPath,
            CurrentTeacherFrontierSupport.HashFile(ledgerPath),
            request,
            snapshot,
            ledgerState.Ledger,
            supply);
    }

    internal static MoneyRevenueCropProductionObligationPlan BuildPlan(
        string requestPath,
        string requestSha256,
        string snapshotPath,
        string snapshotSha256,
        string ledgerPath,
        string ledgerSha256,
        MoneyRevenueCropProductionObligationPlanRequest request,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        MaterialSupplyProjectionResult supply)
    {
        Require(supply.Status == "available" &&
                supply.BlockingReasons.Length == 0,
            "Money Revenue crop-production material supply is blocked: " +
            string.Join(",", supply.BlockingReasons));
        var rows = ledger.CropPlantingCommitments ??
            throw new InvalidDataException(
                "Money Revenue crop-production commitments are missing.");
        Require(rows.All(value => value is not null) &&
                rows.Select(value => value.CommitmentId)
                    .Distinct(StringComparer.Ordinal).Count() == rows.Length,
            "Money Revenue crop-production commitment IDs are not unique.");

        var active = rows.Where(value => value.Status ==
                StrategyCommitmentStatuses.Active)
            .OrderBy(value => value.PlantingTotalDay)
            .ThenBy(value => value.CommitmentId, StringComparer.Ordinal)
            .ToArray();
        var availableByItem = supply.Quantities.ToDictionary(
            value => value.QualifiedItemId,
            value => value.AvailableQuantity,
            StringComparer.Ordinal);
        var commitments = new List<MoneyRevenueCropProductionCommitmentPlan>();
        foreach (var commitment in active)
        {
            var projection = VerifyCommitment(
                snapshot,
                commitment,
                request.DeadlineTotalDayExclusive);
            var available = availableByItem.GetValueOrDefault(
                commitment.SeedQualifiedItemId);
            var reservationPurpose =
                "crop_seed_supply:" + commitment.CommitmentId;
            var existingReservations = ledger.MaterialReservations
                .Where(value =>
                    value.Status == StrategyCommitmentStatuses.Active &&
                    value.Purpose == reservationPurpose)
                .ToArray();
            Require(existingReservations.All(value =>
                    value.QualifiedItemId ==
                    commitment.SeedQualifiedItemId),
                "Money Revenue crop seed reservation item drifted: " +
                commitment.CommitmentId);
            var existingReserved = existingReservations.Aggregate(
                0,
                (total, value) => checked(total + value.Quantity));
            Require(existingReserved <= commitment.TileCount,
                "Money Revenue crop seed reservation exceeds its commitment: " +
                commitment.CommitmentId);
            var remainingNeed = checked(
                commitment.TileCount - existingReserved);
            var allocated = Math.Min(available, remainingNeed);
            availableByItem[commitment.SeedQualifiedItemId] =
                available - allocated;
            var shortfall = checked(remainingNeed - allocated);
            var requestIds = new List<string>
            {
                "planting-layout:" + commitment.CommitmentId,
                "seed-reservation:" + commitment.CommitmentId,
                "daily-capacity:" + commitment.CommitmentId,
                "native-crop-lifecycle:" + commitment.CommitmentId
            };
            if (shortfall > 0)
            {
                requestIds.Add(
                    "seed-acquisition:" + commitment.CommitmentId + ":" +
                    shortfall);
            }
            var waves = projection.HarvestTotalDays.Select((day, index) =>
            {
                var retentionKey =
                    "crop-output:" + commitment.CommitmentId + ":" + day;
                requestIds.Add("output-retention-terminal:" + retentionKey);
                return new MoneyRevenueCropHarvestWaveObligation(
                    index + 1,
                    day,
                    projection.MinimumUnitsPerWave,
                    checked((long)projection.MinimumUnitsPerWave *
                        commitment.HarvestUnitSalePrice!.Value),
                    retentionKey);
            }).ToArray();
            commitments.Add(new MoneyRevenueCropProductionCommitmentPlan(
                commitment.CommitmentId,
                commitment.SeedQualifiedItemId,
                commitment.HarvestItemQualifiedId,
                commitment.TileCount,
                commitment.PlantingTotalDay,
                commitment.PlantingSeason,
                commitment.BaseGrowDays,
                commitment.PlantingTotalDay,
                allocated,
                existingReserved,
                shortfall,
                SeedSupplyStatus(existingReserved, allocated, shortfall),
                projection.RequiredWateringTotalDays,
                waves,
                projection.MinimumTotalUnits,
                projection.MinimumGrossRevenue!.Value,
                requestIds.Order(StringComparer.Ordinal).ToArray()));
        }

        var pending = commitments.SelectMany(value =>
                value.PendingEvidenceRequestIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var minimumRevenue = commitments.Sum(value =>
            value.MinimumGrossRevenue);
        return new MoneyRevenueCropProductionObligationPlan
        {
            Status = commitments.Count == 0
                ? "crop_production_commitments_required"
                : "crop_production_evidence_collection_ready",
            RequestPath = Path.GetFullPath(requestPath),
            RequestSha256 = requestSha256,
            SnapshotPath = Path.GetFullPath(snapshotPath),
            SnapshotSha256 = snapshotSha256,
            StrategyLedgerPath = Path.GetFullPath(ledgerPath),
            StrategyLedgerSha256 = ledgerSha256,
            SourceStateHash = snapshot.StateHash,
            DeadlineTotalDayExclusive = request.DeadlineTotalDayExclusive,
            TargetTotalMoneyEarned = request.TargetTotalMoneyEarned,
            ActiveCommitmentCount = commitments.Count,
            MinimumGrossRevenue = minimumRevenue,
            MinimumGrossRevenueReachesTarget =
                minimumRevenue >= request.TargetTotalMoneyEarned,
            Commitments = commitments.ToArray(),
            PendingEvidenceRequestIds = pending,
            FutureCropProductionProofReady = false,
            FreshSaveRevenueScheduleReady = false,
            FormalProductTrainingAuthorized = false
        };
    }

    private static CropProductionObligationProjection VerifyCommitment(
        SnapshotEnvelope snapshot,
        CropPlantingCommitment commitment,
        int deadlineTotalDayExclusive)
    {
        Require(commitment.ProductionObligationSchemaVersion ==
                    "crop_production_obligation.v1" &&
                !string.IsNullOrWhiteSpace(commitment.CommitmentId) &&
                commitment.Revision > 0 &&
                !string.IsNullOrWhiteSpace(commitment.SourceDecisionId) &&
                !string.IsNullOrWhiteSpace(commitment.SourceStateHash) &&
                commitment.LocationContext == "outdoor_seasonal" &&
                commitment.TileCount > 0 &&
                commitment.PlantingYear >= 1 &&
                commitment.PlantingDayOfMonth is >= 1 and <= 28 &&
                commitment.HarvestContextTags is not null &&
                commitment.RequiredWateringTotalDays is not null &&
                commitment.HarvestTotalDays is not null &&
                commitment.LastInSeasonHarvestTotalDay <
                    deadlineTotalDayExclusive,
            "Money Revenue crop-production commitment metadata is invalid: " +
            commitment.CommitmentId);
        var crop = FindCrop(snapshot, commitment.SeedId);
        var seedId = ReadString(crop, "seed_id");
        var harvestItemId = ReadString(crop, "harvest_item_id");
        var harvestQualifiedId = ReadString(
            crop,
            "harvest_item_qualified_id");
        var seasons = ReadStringArray(crop, "seasons");
        var baseGrowDays = ReadInt(crop, "grow_days");
        var rawRegrowDays = ReadNullableInt(crop, "regrow_days");
        var regrowDays = rawRegrowDays.HasValue && rawRegrowDays.Value > 0
            ? rawRegrowDays
            : null;
        var minimumStack = ReadNullableInt(crop, "harvest_min_stack") ?? 0;
        var salePrice = ReadNullableInt(
            crop,
            "harvest_unit_sale_price");
        var needsWatering = ReadBool(crop, "needs_watering");
        Require(seedId == commitment.SeedId &&
                commitment.SeedQualifiedItemId == QualifyObjectId(seedId) &&
                harvestItemId == commitment.HarvestItemId &&
                harvestQualifiedId == commitment.HarvestItemQualifiedId &&
                ReadStringArray(crop, "harvest_context_tags").SequenceEqual(
                    commitment.HarvestContextTags ?? Array.Empty<string>(),
                    StringComparer.Ordinal) &&
                seasons.Contains(commitment.PlantingSeason,
                    StringComparer.OrdinalIgnoreCase) &&
                baseGrowDays > 0 &&
                baseGrowDays == commitment.BaseGrowDays &&
                regrowDays == commitment.RegrowDays &&
                minimumStack > 0 &&
                salePrice.HasValue && salePrice.Value > 0 &&
                salePrice == commitment.HarvestUnitSalePrice &&
                needsWatering == commitment.NeedsWatering,
            "Money Revenue crop-production commitment drifted from the crop catalog: " +
            commitment.CommitmentId);

        var currentYear = ReadStateInt(snapshot, "time", "year");
        var currentDay = ReadStateInt(snapshot, "time", "day");
        var currentTotalDay = ReadStateInt(snapshot, "time", "total_days");
        var currentSeasonIndex = SeasonIndex(
            ReadStateString(snapshot, "time", "season"));
        var plantingSeasonIndex = SeasonIndex(commitment.PlantingSeason);
        var worldOffset = checked(currentTotalDay -
            CalendarOrdinal(currentYear, currentSeasonIndex, currentDay));
        var plantingTotalDay = checked(
            CalendarOrdinal(
                commitment.PlantingYear,
                plantingSeasonIndex,
                commitment.PlantingDayOfMonth) + worldOffset);
        Require(plantingTotalDay >= currentTotalDay,
            "Money Revenue crop-production planting day predates the source snapshot: " +
            commitment.CommitmentId);
        var firstHarvest = checked(plantingTotalDay + baseGrowDays);
        var seasonEnd = checked(
            plantingTotalDay + (28 - commitment.PlantingDayOfMonth));
        Require(firstHarvest <= seasonEnd,
            "Money Revenue crop-production commitment cannot mature in season: " +
            commitment.CommitmentId);
        var lastHarvest = firstHarvest;
        if (regrowDays.HasValue)
        {
            lastHarvest = checked(firstHarvest +
                (seasonEnd - firstHarvest) / regrowDays.Value *
                regrowDays.Value);
        }
        var projection = CropProductionObligationProjector.Project(
            plantingTotalDay,
            firstHarvest,
            lastHarvest,
            regrowDays,
            commitment.TileCount,
            minimumStack,
            needsWatering,
            salePrice);
        var requiredWateringTotalDays =
            commitment.RequiredWateringTotalDays!;
        var harvestTotalDays = commitment.HarvestTotalDays!;
        Require(commitment.PlantingTotalDay == plantingTotalDay &&
                commitment.FirstHarvestTotalDay == firstHarvest &&
                commitment.LastInSeasonHarvestTotalDay == lastHarvest &&
                requiredWateringTotalDays.SequenceEqual(
                    projection.RequiredWateringTotalDays) &&
                harvestTotalDays.SequenceEqual(
                    projection.HarvestTotalDays) &&
                commitment.MinimumUnitsPerWave ==
                    projection.MinimumUnitsPerWave &&
                commitment.MinimumHarvestWaveCount ==
                    projection.MinimumHarvestWaveCount &&
                commitment.MinimumTotalUnits == projection.MinimumTotalUnits &&
                commitment.MinimumGrossRevenue ==
                    projection.MinimumGrossRevenue,
            "Money Revenue crop-production obligation projection drifted: " +
            commitment.CommitmentId);
        return projection;
    }

    private static string SeedSupplyStatus(
        int existingReserved,
        int allocated,
        int shortfall) => shortfall > 0
            ? "future_seed_acquisition_required"
            : existingReserved > 0 && allocated == 0
                ? "existing_exact_reservation_satisfies_commitment"
                : existingReserved > 0
                    ? "existing_reservation_plus_unreserved_supply_can_cover_commitment"
                    : "current_unreserved_supply_can_cover_exact_reservation";

    private static MaterialSupplyProjectionResult ReadMaterialSupply(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger)
    {
        var value = ReadStateValue(snapshot, "farm", "material_inventory_graph");
        var graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
                value.GetRawText(),
                JsonDefaults.Options)
            ?? throw new InvalidDataException(
                "Money Revenue crop-production material graph is empty.");
        Require(graph.SchemaVersion == "material_inventory_graph.v1" &&
                graph.Status == "available" &&
                graph.DefaultSharedResourcePolicy ==
                    "deny_without_explicit_authorization",
            "Money Revenue crop-production material graph is invalid.");
        return new MaterialSupplyProjection().Project(
            graph,
            ledger.MaterialReservations);
    }

    private static JsonElement FindCrop(
        SnapshotEnvelope snapshot,
        string seedId)
    {
        var catalog = ReadStateValue(snapshot, "farm", "crop_catalog");
        Require(catalog.ValueKind == JsonValueKind.Array,
            "Money Revenue crop catalog is not an array.");
        var matches = catalog.EnumerateArray().Where(value =>
                value.ValueKind == JsonValueKind.Object &&
                ReadString(value, "seed_id") == seedId)
            .ToArray();
        Require(matches.Length == 1,
            "Money Revenue crop seed identity is not unique: " + seedId);
        return matches[0];
    }

    private static void ValidateSnapshot(SnapshotEnvelope snapshot)
    {
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
                !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            "Money Revenue crop-production snapshot identity is invalid.");
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
                "Money Revenue crop-production field is unavailable: " +
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
            "Money Revenue crop-production string field is empty: " +
            section + "." + field);

    private static int SeasonIndex(string season)
    {
        var result = Array.FindIndex(Seasons, value =>
            value.Equals(season, StringComparison.OrdinalIgnoreCase));
        Require(result >= 0,
            "Money Revenue crop-production season is invalid: " + season);
        return result;
    }

    private static int CalendarOrdinal(int year, int seasonIndex, int day) =>
        checked((year - 1) * 112 + seasonIndex * 28 + day - 1);

    private static string QualifyObjectId(string itemId) =>
        itemId.StartsWith('(') ? itemId : "(O)" + itemId;

    private static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var number)
            ? number
            : 0;

    private static int? ReadNullableInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    private static bool ReadBool(JsonElement row, string property)
    {
        Require(row.TryGetProperty(property, out var value) &&
                value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Money Revenue crop-production boolean is unavailable: " +
            property);
        return value.GetBoolean();
    }

    private static string[] ReadStringArray(
        JsonElement row,
        string property) => row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item =>
                    item.ValueKind == JsonValueKind.String
                        ? item.GetString() ?? string.Empty
                        : string.Empty)
                .ToArray()
            : Array.Empty<string>();

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
