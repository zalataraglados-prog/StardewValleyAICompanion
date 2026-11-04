using System;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Contracts.Strategy;

public sealed class MaterialTransferPreparationRequest
{
    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("intent")]
    public MaterialTransferIntent Intent { get; set; } = new();

    [JsonPropertyName("material_relocations")]
    public ReservationPortfolioMaterialRelocation[] MaterialRelocations { get; set; } =
        Array.Empty<ReservationPortfolioMaterialRelocation>();
}

public sealed class PreparedMaterialTransfer
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "prepared_material_transfer.v1";

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_game_tick")]
    public long SourceGameTick { get; set; }

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("material_reservations_sha256")]
    public string MaterialReservationsSha256 { get; set; } = string.Empty;

    [JsonPropertyName("intent")]
    public MaterialTransferIntent Intent { get; set; } = new();

    [JsonPropertyName("material_relocations")]
    public ReservationPortfolioMaterialRelocation[] MaterialRelocations { get; set; } =
        Array.Empty<ReservationPortfolioMaterialRelocation>();

    [JsonPropertyName("before_inventory_graph")]
    public MaterialInventoryGraph BeforeInventoryGraph { get; set; } = new();

    [JsonPropertyName("reservation_projection")]
    public MaterialTransferReservationProjection ReservationProjection { get; set; } = new();

    [JsonPropertyName("binding_sha256")]
    public string BindingSha256 { get; set; } = string.Empty;
}
