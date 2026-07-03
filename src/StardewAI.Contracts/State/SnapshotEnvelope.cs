using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State
{
    public sealed class SnapshotEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "snapshot.v1";

        [JsonPropertyName("bridge_version")]
        public string BridgeVersion { get; set; } = "unknown";

        [JsonPropertyName("game_tick")]
        public long GameTick { get; set; }

        [JsonPropertyName("real_timestamp")]
        public string RealTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("completeness")]
        public string Completeness { get; set; } = "partial";

        [JsonPropertyName("unavailable_fields")]
        public string[] UnavailableFields { get; set; } = new string[0];

        [JsonPropertyName("state")]
        public Dictionary<string, JsonElement> State { get; set; } = new Dictionary<string, JsonElement>();
    }
}
