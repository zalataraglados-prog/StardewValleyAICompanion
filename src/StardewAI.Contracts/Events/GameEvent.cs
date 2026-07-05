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

        [JsonPropertyName("event_sequence")]
        public long EventSequence { get; set; }

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("game_tick")]
        public long GameTick { get; set; }

        [JsonPropertyName("real_timestamp")]
        public string RealTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("in_game_time")]
        public int? InGameTime { get; set; }

        [JsonPropertyName("state_hash_before")]
        public string StateHashBefore { get; set; } = string.Empty;

        [JsonPropertyName("state_hash_after")]
        public string StateHashAfter { get; set; } = string.Empty;

        [JsonPropertyName("previous_event_hash")]
        public string PreviousEventHash { get; set; } = string.Empty;

        [JsonPropertyName("event_hash")]
        public string EventHash { get; set; } = string.Empty;

        [JsonPropertyName("changed_fields")]
        public string[] ChangedFields { get; set; } = new string[0];

        [JsonPropertyName("before")]
        public JsonElement? Before { get; set; }

        [JsonPropertyName("after")]
        public JsonElement? After { get; set; }
    }
}
