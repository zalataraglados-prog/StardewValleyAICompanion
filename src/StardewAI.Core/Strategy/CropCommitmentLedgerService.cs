using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy
{
    public sealed class CropCommitmentLedgerService
    {
        private static readonly string[] Seasons = { "spring", "summer", "fall", "winter" };

        public StrategyCommitmentMutationResult Upsert(
            StrategyCommitmentLedger? current,
            SnapshotEnvelope snapshot,
            CropPlantingCommitmentUpsertRequest request,
            string updatedAt)
        {
            var errors = ValidateCommon(current, snapshot, request.StateHash, request.ExpectedLedgerRevision);
            if (string.IsNullOrWhiteSpace(request.CommitmentId))
            {
                errors.Add("commitment_id_required");
            }
            if (string.IsNullOrWhiteSpace(request.SourceDecisionId))
            {
                errors.Add("source_decision_id_required");
            }
            if (request.TileCount <= 0 || request.TileCount > 100000)
            {
                errors.Add("tile_count_out_of_supported_range");
            }
            if (request.PlantingYear < 1 || request.PlantingDayOfMonth < 1 || request.PlantingDayOfMonth > 28)
            {
                errors.Add("planting_calendar_date_invalid");
            }
            if (!string.Equals(request.LocationContext, "outdoor_seasonal", StringComparison.Ordinal))
            {
                errors.Add("only_outdoor_seasonal_commitments_supported");
            }

            var seasonIndex = Array.FindIndex(Seasons, season =>
                string.Equals(season, request.PlantingSeason, StringComparison.OrdinalIgnoreCase));
            if (seasonIndex < 0)
            {
                errors.Add("planting_season_invalid");
            }

            var crop = FindCrop(snapshot, request.SeedId);
            if (!crop.HasValue)
            {
                errors.Add("seed_id_not_found_in_live_crop_catalog");
            }
            else if (!CropSupportsSeason(crop.Value, request.PlantingSeason))
            {
                errors.Add("crop_not_plantable_in_committed_season");
            }

            var currentTotalDay = ReadStateFieldIntOptional(snapshot, "time", "total_days");
            var currentYear = ReadStateFieldIntOptional(snapshot, "time", "year");
            var currentDay = ReadStateFieldIntOptional(snapshot, "time", "day");
            var currentSeason = ReadStateFieldString(snapshot, "time", "season");
            var currentSeasonIndex = Array.FindIndex(Seasons, season =>
                string.Equals(season, currentSeason, StringComparison.OrdinalIgnoreCase));
            if (!currentTotalDay.HasValue || !currentYear.HasValue || !currentDay.HasValue || currentSeasonIndex < 0)
            {
                errors.Add("live_calendar_anchor_incomplete");
            }

            var plantingTotalDay = 0;
            var firstHarvestTotalDay = 0;
            var lastHarvestTotalDay = 0;
            var baseGrowDays = crop.HasValue ? ReadInt(crop.Value, "grow_days") : 0;
            var regrowDays = crop.HasValue ? NullableReadInt(crop.Value, "regrow_days") : null;
            if (crop.HasValue && baseGrowDays <= 0)
            {
                errors.Add("native_crop_base_grow_days_unavailable");
            }
            if (errors.Count == 0)
            {
                var currentOrdinal = CalendarOrdinal(currentYear!.Value, currentSeasonIndex, currentDay!.Value);
                var worldDateOffset = currentTotalDay!.Value - currentOrdinal;
                plantingTotalDay = CalendarOrdinal(request.PlantingYear, seasonIndex, request.PlantingDayOfMonth) + worldDateOffset;
                firstHarvestTotalDay = plantingTotalDay + baseGrowDays;
                var seasonEndTotalDay = plantingTotalDay + (28 - request.PlantingDayOfMonth);
                if (plantingTotalDay < currentTotalDay.Value)
                {
                    errors.Add("planting_date_is_in_the_past");
                }
                if (firstHarvestTotalDay > seasonEndTotalDay)
                {
                    errors.Add("crop_will_not_reach_first_harvest_in_committed_season");
                }
                lastHarvestTotalDay = firstHarvestTotalDay;
                if (regrowDays.HasValue && regrowDays.Value > 0 && firstHarvestTotalDay <= seasonEndTotalDay)
                {
                    lastHarvestTotalDay += (seasonEndTotalDay - firstHarvestTotalDay) / regrowDays.Value * regrowDays.Value;
                }
            }

            if (errors.Count > 0)
            {
                return Rejected(current, errors);
            }

            var ledger = CloneOrCreate(current, snapshot, updatedAt);
            var existing = ledger.CropPlantingCommitments.FirstOrDefault(row =>
                string.Equals(row.CommitmentId, request.CommitmentId, StringComparison.Ordinal));
            var commitment = new CropPlantingCommitment
            {
                CommitmentId = request.CommitmentId,
                Revision = (existing?.Revision ?? 0) + 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = request.SourceDecisionId,
                SourceStateHash = snapshot.StateHash,
                LocationContext = "outdoor_seasonal",
                SeedId = ReadString(crop!.Value, "seed_id"),
                HarvestItemId = ReadString(crop.Value, "harvest_item_id"),
                HarvestItemQualifiedId = ReadString(crop.Value, "harvest_item_qualified_id"),
                HarvestContextTags = ReadStringArray(crop.Value, "harvest_context_tags"),
                TileCount = request.TileCount,
                PlantingYear = request.PlantingYear,
                PlantingSeason = Seasons[seasonIndex],
                PlantingDayOfMonth = request.PlantingDayOfMonth,
                PlantingTotalDay = plantingTotalDay,
                BaseGrowDays = baseGrowDays,
                FirstHarvestTotalDay = firstHarvestTotalDay,
                RegrowDays = regrowDays.HasValue && regrowDays.Value > 0 ? regrowDays : null,
                LastInSeasonHarvestTotalDay = lastHarvestTotalDay,
                MinimumUnitsPerWave = checked(request.TileCount * Math.Max(1, ReadInt(crop.Value, "harvest_min_stack", 1))),
                ProjectionStatus = "conservative_native_base_growth",
                ProjectionCondition = "outdoor_crop_planted_on_committed_date_and_receives_each_required_daily_growth_update_without_speed_modifiers"
            };
            ledger.CropPlantingCommitments = ledger.CropPlantingCommitments
                .Where(row => !string.Equals(row.CommitmentId, commitment.CommitmentId, StringComparison.Ordinal))
                .Append(commitment)
                .OrderBy(row => row.CommitmentId, StringComparer.Ordinal)
                .ToArray();
            AdvanceLedger(ledger, snapshot, updatedAt);
            AppendHistory(ledger, commitment, "upsert", updatedAt, string.Empty);
            return Accepted(ledger);
        }

        public StrategyCommitmentMutationResult Cancel(
            StrategyCommitmentLedger? current,
            SnapshotEnvelope snapshot,
            string commitmentId,
            StrategyCommitmentCancelRequest request,
            string updatedAt)
        {
            var errors = ValidateCommon(current, snapshot, request.StateHash, request.ExpectedLedgerRevision);
            var existing = current?.CropPlantingCommitments.FirstOrDefault(row =>
                string.Equals(row.CommitmentId, commitmentId, StringComparison.Ordinal));
            if (existing is null)
            {
                errors.Add("commitment_not_found");
            }
            else if (!string.Equals(existing.Status, StrategyCommitmentStatuses.Active, StringComparison.Ordinal))
            {
                errors.Add("only_active_commitment_can_be_cancelled");
            }
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                errors.Add("cancel_reason_required");
            }
            if (errors.Count > 0)
            {
                return Rejected(current, errors);
            }

            var ledger = CloneOrCreate(current, snapshot, updatedAt);
            ledger.CropPlantingCommitments = ledger.CropPlantingCommitments.Select(row =>
                string.Equals(row.CommitmentId, commitmentId, StringComparison.Ordinal)
                    ? Clone(row, StrategyCommitmentStatuses.Cancelled, row.Revision + 1, request.Reason)
                    : row).ToArray();
            AdvanceLedger(ledger, snapshot, updatedAt);
            AppendHistory(
                ledger,
                ledger.CropPlantingCommitments.Single(row => string.Equals(row.CommitmentId, commitmentId, StringComparison.Ordinal)),
                "cancel",
                updatedAt,
                request.Reason);
            return Accepted(ledger);
        }

        public StrategyCommitmentLedger ReconcileCompleted(
            StrategyCommitmentLedger current,
            SnapshotEnvelope snapshot,
            string updatedAt)
        {
            var totalDay = ReadStateFieldIntOptional(snapshot, "time", "total_days");
            if (!totalDay.HasValue || !current.CropPlantingCommitments.Any(row =>
                string.Equals(row.Status, StrategyCommitmentStatuses.Active, StringComparison.Ordinal) &&
                totalDay.Value > row.LastInSeasonHarvestTotalDay))
            {
                return current;
            }

            var ledger = CloneOrCreate(current, snapshot, updatedAt);
            var completedIds = ledger.CropPlantingCommitments
                .Where(row => string.Equals(row.Status, StrategyCommitmentStatuses.Active, StringComparison.Ordinal) &&
                    totalDay.Value > row.LastInSeasonHarvestTotalDay)
                .Select(row => row.CommitmentId)
                .ToHashSet(StringComparer.Ordinal);
            ledger.CropPlantingCommitments = ledger.CropPlantingCommitments.Select(row =>
                string.Equals(row.Status, StrategyCommitmentStatuses.Active, StringComparison.Ordinal) &&
                totalDay.Value > row.LastInSeasonHarvestTotalDay
                    ? Clone(row, StrategyCommitmentStatuses.Completed, row.Revision + 1, string.Empty)
                    : row).ToArray();
            AdvanceLedger(ledger, snapshot, updatedAt);
            foreach (var commitment in ledger.CropPlantingCommitments.Where(row => completedIds.Contains(row.CommitmentId)))
            {
                AppendHistory(ledger, commitment, "complete", updatedAt, "last_committed_harvest_window_elapsed");
            }
            return ledger;
        }

        private static List<string> ValidateCommon(
            StrategyCommitmentLedger? current,
            SnapshotEnvelope snapshot,
            string stateHash,
            int expectedRevision)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(stateHash) || !string.Equals(stateHash, snapshot.StateHash, StringComparison.Ordinal))
            {
                errors.Add("state_hash_mismatch");
            }
            if (expectedRevision != (current?.Revision ?? 0))
            {
                errors.Add("ledger_revision_conflict");
            }
            if (string.IsNullOrWhiteSpace(SaveId(snapshot)) || string.IsNullOrWhiteSpace(PlayerId(snapshot)))
            {
                errors.Add("snapshot_identity_unavailable");
            }
            if (current is not null &&
                (!string.Equals(current.SaveId, SaveId(snapshot), StringComparison.Ordinal) ||
                 !string.Equals(current.PlayerId, PlayerId(snapshot), StringComparison.Ordinal)))
            {
                errors.Add("ledger_identity_mismatch");
            }
            return errors;
        }

        private static JsonElement? FindCrop(SnapshotEnvelope snapshot, string seedId)
        {
            var catalog = ReadStateFieldValue(snapshot, "farm", "crop_catalog");
            if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var row in catalog.Value.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    (string.Equals(ReadString(row, "seed_id"), seedId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals("(O)" + ReadString(row, "seed_id"), seedId, StringComparison.OrdinalIgnoreCase)))
                {
                    return row;
                }
            }
            return null;
        }

        private static bool CropSupportsSeason(JsonElement crop, string season) =>
            crop.TryGetProperty("seasons", out var seasons) && seasons.ValueKind == JsonValueKind.Array &&
            seasons.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.String &&
                string.Equals(row.GetString(), season, StringComparison.OrdinalIgnoreCase));

        private static int CalendarOrdinal(int year, int seasonIndex, int day) =>
            (year - 1) * 112 + seasonIndex * 28 + day - 1;

        private static string SaveId(SnapshotEnvelope snapshot) =>
            snapshot.SaveId.Value ?? ReadStateFieldString(snapshot, "identity", "save_id");

        private static string PlayerId(SnapshotEnvelope snapshot) =>
            snapshot.PlayerId.Value ?? ReadStateFieldString(snapshot, "identity", "player_id");

        private static StrategyCommitmentLedger CloneOrCreate(
            StrategyCommitmentLedger? current,
            SnapshotEnvelope snapshot,
            string updatedAt) => new()
            {
                LedgerId = current?.LedgerId ?? "strategy-ledger:" + SaveId(snapshot) + ":" + PlayerId(snapshot),
                SaveId = SaveId(snapshot),
                PlayerId = PlayerId(snapshot),
                Revision = current?.Revision ?? 0,
                UpdatedAt = updatedAt,
                SourceStateHash = snapshot.StateHash,
                CropPlantingCommitments = current?.CropPlantingCommitments.Select(row => Clone(row, row.Status, row.Revision, row.CancelReason)).ToArray()
                    ?? Array.Empty<CropPlantingCommitment>(),
                History = current?.History.Select(CloneHistory).ToArray() ?? Array.Empty<StrategyCommitmentHistoryEntry>()
            };

        private static CropPlantingCommitment Clone(CropPlantingCommitment row, string status, int revision, string cancelReason) => new()
        {
            CommitmentId = row.CommitmentId,
            Revision = revision,
            Status = status,
            SourceDecisionId = row.SourceDecisionId,
            SourceStateHash = row.SourceStateHash,
            LocationContext = row.LocationContext,
            SeedId = row.SeedId,
            HarvestItemId = row.HarvestItemId,
            HarvestItemQualifiedId = row.HarvestItemQualifiedId,
            HarvestContextTags = (row.HarvestContextTags ?? Array.Empty<string>()).ToArray(),
            TileCount = row.TileCount,
            PlantingYear = row.PlantingYear,
            PlantingSeason = row.PlantingSeason,
            PlantingDayOfMonth = row.PlantingDayOfMonth,
            PlantingTotalDay = row.PlantingTotalDay,
            BaseGrowDays = row.BaseGrowDays,
            FirstHarvestTotalDay = row.FirstHarvestTotalDay,
            RegrowDays = row.RegrowDays,
            LastInSeasonHarvestTotalDay = row.LastInSeasonHarvestTotalDay,
            MinimumUnitsPerWave = row.MinimumUnitsPerWave,
            ProjectionStatus = row.ProjectionStatus,
            ProjectionCondition = row.ProjectionCondition,
            CancelReason = cancelReason
        };

        private static void AdvanceLedger(StrategyCommitmentLedger ledger, SnapshotEnvelope snapshot, string updatedAt)
        {
            ledger.Revision++;
            ledger.SourceStateHash = snapshot.StateHash;
            ledger.UpdatedAt = updatedAt;
        }

        private static void AppendHistory(
            StrategyCommitmentLedger ledger,
            CropPlantingCommitment commitment,
            string operation,
            string recordedAt,
            string reason)
        {
            ledger.History = ledger.History.Append(new StrategyCommitmentHistoryEntry
            {
                LedgerRevision = ledger.Revision,
                CommitmentId = commitment.CommitmentId,
                CommitmentRevision = commitment.Revision,
                Operation = operation,
                SourceDecisionId = commitment.SourceDecisionId,
                SourceStateHash = ledger.SourceStateHash,
                RecordedAt = recordedAt,
                Reason = reason
            }).ToArray();
        }

        private static StrategyCommitmentHistoryEntry CloneHistory(StrategyCommitmentHistoryEntry row) => new()
        {
            LedgerRevision = row.LedgerRevision,
            CommitmentId = row.CommitmentId,
            CommitmentRevision = row.CommitmentRevision,
            Operation = row.Operation,
            SourceDecisionId = row.SourceDecisionId,
            SourceStateHash = row.SourceStateHash,
            RecordedAt = row.RecordedAt,
            Reason = row.Reason
        };

        private static string[] ReadStringArray(JsonElement row, string property) =>
            row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => item.Length > 0)
                    .ToArray()
                : Array.Empty<string>();

        private static StrategyCommitmentMutationResult Accepted(StrategyCommitmentLedger ledger) => new()
        {
            Accepted = true,
            Ledger = ledger
        };

        private static StrategyCommitmentMutationResult Rejected(StrategyCommitmentLedger? ledger, IEnumerable<string> errors) => new()
        {
            Accepted = false,
            Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
            Ledger = ledger
        };
    }
}
