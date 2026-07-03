using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Capabilities
{
    public sealed class Capability
    {
        [JsonPropertyName("capability_id")]
        public string CapabilityId { get; set; } = string.Empty;

        [JsonPropertyName("access_mode")]
        public string AccessMode { get; set; } = "read";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "unavailable";

        [JsonPropertyName("source")]
        public object Source { get; set; } = "unknown";

        [JsonPropertyName("limitations")]
        public object Limitations { get; set; } = new string[0];

        [JsonPropertyName("required_permission")]
        public string RequiredPermission { get; set; } = "observer";
    }

    public sealed class CapabilityManifest
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "capabilities.v1";

        [JsonPropertyName("capabilities")]
        public Capability[] Capabilities { get; set; } = new Capability[0];
    }
}
