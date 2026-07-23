using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Capabilities
{
    public sealed class BinaryIdentity
    {
        [JsonPropertyName("assembly_name")]
        public string AssemblyName { get; set; } = string.Empty;

        [JsonPropertyName("assembly_version")]
        public string AssemblyVersion { get; set; } = string.Empty;

        [JsonPropertyName("mvid")]
        public string Mvid { get; set; } = string.Empty;

        [JsonPropertyName("byte_length")]
        public long? ByteLength { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;

        [JsonPropertyName("identity_status")]
        public string IdentityStatus { get; set; } = "unavailable";
    }

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

        [JsonPropertyName("bridge_version")]
        public string BridgeVersion { get; set; } = string.Empty;

        [JsonPropertyName("permission_mode")]
        public string PermissionMode { get; set; } = "observer";

        [JsonPropertyName("verified_versions")]
        public string[] VerifiedVersions { get; set; } = new string[0];

        [JsonPropertyName("compatibility_status")]
        public string CompatibilityStatus { get; set; } = "unverified";

        [JsonPropertyName("observed_game_version")]
        public string ObservedGameVersion { get; set; } = string.Empty;

        [JsonPropertyName("observed_smapi_version")]
        public string ObservedSmapiVersion { get; set; } = string.Empty;

        [JsonPropertyName("game_binary_identity")]
        public BinaryIdentity GameBinaryIdentity { get; set; } = new BinaryIdentity();

        [JsonPropertyName("smapi_binary_identity")]
        public BinaryIdentity SmapiBinaryIdentity { get; set; } = new BinaryIdentity();

        [JsonPropertyName("can_write_game_state")]
        public bool CanWriteGameState { get; set; }

        [JsonPropertyName("can_execute_commands")]
        public bool CanExecuteCommands { get; set; }

        [JsonPropertyName("capabilities")]
        public Capability[] Capabilities { get; set; } = new Capability[0];

        [JsonPropertyName("option_capability_schema_version")]
        public string OptionCapabilitySchemaVersion { get; set; } = OptionCapabilityRegistrySource.SchemaVersion;

        [JsonPropertyName("option_capabilities")]
        public OptionCapabilityDeclaration[] OptionCapabilities { get; set; } =
            new OptionCapabilityDeclaration[0];
    }
}
