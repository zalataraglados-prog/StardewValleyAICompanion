using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy
{
    public static class StrategyCommitmentStatuses
    {
        public const string Active = "active";
        public const string Cancelled = "cancelled";
        public const string Completed = "completed";
    }

    public sealed class StrategyCommitmentLedger
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "strategy_commitment_ledger.v1";

        [JsonPropertyName("ledger_id")]
        public string LedgerId { get; set; } = string.Empty;

        [JsonPropertyName("save_id")]
        public string SaveId { get; set; } = string.Empty;

        [JsonPropertyName("player_id")]
        public string PlayerId { get; set; } = string.Empty;

        [JsonPropertyName("revision")]
        public int Revision { get; set; }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("crop_planting_commitments")]
        public CropPlantingCommitment[] CropPlantingCommitments { get; set; } = Array.Empty<CropPlantingCommitment>();

        [JsonPropertyName("material_reservations")]
        public MaterialReservation[] MaterialReservations { get; set; } = Array.Empty<MaterialReservation>();

        [JsonPropertyName("history")]
        public StrategyCommitmentHistoryEntry[] History { get; set; } = Array.Empty<StrategyCommitmentHistoryEntry>();
    }

    public sealed class StrategyCommitmentHistoryEntry
    {
        [JsonPropertyName("ledger_revision")]
        public int LedgerRevision { get; set; }

        [JsonPropertyName("commitment_id")]
        public string CommitmentId { get; set; } = string.Empty;

        [JsonPropertyName("commitment_revision")]
        public int CommitmentRevision { get; set; }

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = string.Empty;

        [JsonPropertyName("source_decision_id")]
        public string SourceDecisionId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("recorded_at")]
        public string RecordedAt { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class CropPlantingCommitment
    {
        [JsonPropertyName("commitment_id")]
        public string CommitmentId { get; set; } = string.Empty;

        [JsonPropertyName("revision")]
        public int Revision { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = StrategyCommitmentStatuses.Active;

        [JsonPropertyName("source_decision_id")]
        public string SourceDecisionId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("location_context")]
        public string LocationContext { get; set; } = "outdoor_seasonal";

        [JsonPropertyName("seed_id")]
        public string SeedId { get; set; } = string.Empty;

        [JsonPropertyName("harvest_item_id")]
        public string HarvestItemId { get; set; } = string.Empty;

        [JsonPropertyName("harvest_item_qualified_id")]
        public string HarvestItemQualifiedId { get; set; } = string.Empty;

        [JsonPropertyName("harvest_context_tags")]
        public string[] HarvestContextTags { get; set; } = Array.Empty<string>();

        [JsonPropertyName("tile_count")]
        public int TileCount { get; set; }

        [JsonPropertyName("planting_year")]
        public int PlantingYear { get; set; }

        [JsonPropertyName("planting_season")]
        public string PlantingSeason { get; set; } = string.Empty;

        [JsonPropertyName("planting_day_of_month")]
        public int PlantingDayOfMonth { get; set; }

        [JsonPropertyName("planting_total_day")]
        public int PlantingTotalDay { get; set; }

        [JsonPropertyName("base_grow_days")]
        public int BaseGrowDays { get; set; }

        [JsonPropertyName("first_harvest_total_day")]
        public int FirstHarvestTotalDay { get; set; }

        [JsonPropertyName("regrow_days")]
        public int? RegrowDays { get; set; }

        [JsonPropertyName("last_in_season_harvest_total_day")]
        public int LastInSeasonHarvestTotalDay { get; set; }

        [JsonPropertyName("minimum_units_per_wave")]
        public int MinimumUnitsPerWave { get; set; }

        [JsonPropertyName("projection_status")]
        public string ProjectionStatus { get; set; } = "conservative_native_base_growth";

        [JsonPropertyName("projection_condition")]
        public string ProjectionCondition { get; set; } = "outdoor_crop_planted_on_committed_date_and_receives_each_required_daily_growth_update_without_speed_modifiers";

        [JsonPropertyName("cancel_reason")]
        public string CancelReason { get; set; } = string.Empty;
    }

    public sealed class CropPlantingCommitmentUpsertRequest
    {
        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("expected_ledger_revision")]
        public int ExpectedLedgerRevision { get; set; }

        [JsonPropertyName("commitment_id")]
        public string CommitmentId { get; set; } = string.Empty;

        [JsonPropertyName("source_decision_id")]
        public string SourceDecisionId { get; set; } = string.Empty;

        [JsonPropertyName("seed_id")]
        public string SeedId { get; set; } = string.Empty;

        [JsonPropertyName("tile_count")]
        public int TileCount { get; set; }

        [JsonPropertyName("planting_year")]
        public int PlantingYear { get; set; }

        [JsonPropertyName("planting_season")]
        public string PlantingSeason { get; set; } = string.Empty;

        [JsonPropertyName("planting_day_of_month")]
        public int PlantingDayOfMonth { get; set; }

        [JsonPropertyName("location_context")]
        public string LocationContext { get; set; } = "outdoor_seasonal";
    }

    public sealed class StrategyCommitmentCancelRequest
    {
        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("expected_ledger_revision")]
        public int ExpectedLedgerRevision { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class StrategyCommitmentMutationResult
    {
        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        [JsonPropertyName("errors")]
        public string[] Errors { get; set; } = Array.Empty<string>();

        [JsonPropertyName("ledger")]
        public StrategyCommitmentLedger? Ledger { get; set; }
    }
}
