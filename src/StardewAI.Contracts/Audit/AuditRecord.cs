using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Audit
{
    public sealed class AuditRecord
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "audit.v1";

        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("game_tick")]
        public long GameTick { get; set; }

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Details { get; set; }
    }
}
