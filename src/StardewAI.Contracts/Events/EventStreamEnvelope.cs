using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Events
{
    public sealed class EventStreamEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "event_stream.v2";

        [JsonPropertyName("latest_snapshot_hash")]
        public string LatestSnapshotHash { get; set; } = string.Empty;

        [JsonPropertyName("latest_event_sequence")]
        public long LatestEventSequence { get; set; }

        [JsonPropertyName("latest_event_hash")]
        public string LatestEventHash { get; set; } = string.Empty;

        [JsonPropertyName("events")]
        public GameEvent[] Events { get; set; } = new GameEvent[0];

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("next_after_sequence")]
        public long? NextAfterSequence { get; set; }

        [JsonPropertyName("chain_status")]
        public string ChainStatus { get; set; } = "ok";
    }
}
