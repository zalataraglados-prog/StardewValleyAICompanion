using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class MaterialOutputCustodyInitialPosition
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "material_output_custody_initial_position.v1";
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;
    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }
    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("reservation_revision")]
    public int ReservationRevision { get; set; }
    [JsonPropertyName("binding_sha256")]
    public string BindingSha256 { get; set; } = string.Empty;
}
