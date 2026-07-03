using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Events
{
    public sealed class GameEvent
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "event.v1";

        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("game_tick")]
        public long GameTick { get; set; }

        [JsonPropertyName("real_timestamp")]
        public string RealTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("after")]
        public JsonElement? After { get; set; }
    }
}
