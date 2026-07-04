using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State
{
    public sealed class InstalledModRef
    {
        public InstalledModRef()
        {
        }

        public InstalledModRef(string id, string name, string version)
        {
            Id = id;
            Name = name;
            Version = version;
        }

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    public sealed class SnapshotEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "snapshot.v1";

        [JsonPropertyName("bridge_version")]
        public string BridgeVersion { get; set; } = string.Empty;

        [JsonPropertyName("game_version")]
        public string GameVersion { get; set; } = string.Empty;

        [JsonPropertyName("smapi_version")]
        public string SmapiVersion { get; set; } = string.Empty;

        [JsonPropertyName("installed_mods")]
        public InstalledModRef[] InstalledMods { get; set; } = new InstalledModRef[0];

        [JsonPropertyName("save_id")]
        public FieldEnvelope<string?> SaveId { get; set; } = new FieldEnvelope<string?>();

        [JsonPropertyName("player_id")]
        public FieldEnvelope<string?> PlayerId { get; set; } = new FieldEnvelope<string?>();

        [JsonPropertyName("game_tick")]
        public long GameTick { get; set; }

        [JsonPropertyName("in_game_time")]
        public FieldEnvelope<int?> InGameTime { get; set; } = new FieldEnvelope<int?>();

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
