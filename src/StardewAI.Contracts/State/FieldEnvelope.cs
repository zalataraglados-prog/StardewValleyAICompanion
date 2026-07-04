using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State
{
    public static class FieldStatus
    {
        public const string Available = "available";
        public const string Derived = "derived";
        public const string Unavailable = "unavailable";
        public const string Stale = "stale";
        public const string Error = "error";

        public static bool IsKnown(string? status)
        {
            return status == Available ||
                status == Derived ||
                status == Unavailable ||
                status == Stale ||
                status == Error;
        }
    }

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

    public static class FieldEnvelopeValidator
    {
        public static bool IsReadableStatus(string? status)
        {
            return status == FieldStatus.Available || status == FieldStatus.Derived;
        }
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
