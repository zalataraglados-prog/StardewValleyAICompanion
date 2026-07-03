using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State
{
    public sealed class FieldEnvelope<T>
    {
        [JsonPropertyName("value")]
        public T? Value { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "unavailable";

        [JsonPropertyName("source")]
        public SourceRef Source { get; set; } = new SourceRef();

        [JsonPropertyName("adapter")]
        public string Adapter { get; set; } = "unknown";

        [JsonPropertyName("read_at_tick")]
        public long ReadAtTick { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reason { get; set; }

        [JsonPropertyName("derivation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DerivationRef? Derivation { get; set; }
    }

    public sealed class SourceRef
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "unavailable";

        [JsonPropertyName("path")]
        public string Path { get; set; } = "unknown";
    }

    public sealed class DerivationRef
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = "unknown";

        [JsonPropertyName("inputs")]
        public string[] Inputs { get; set; } = new string[0];
    }
}
