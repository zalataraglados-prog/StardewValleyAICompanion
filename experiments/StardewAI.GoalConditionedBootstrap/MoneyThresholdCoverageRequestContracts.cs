using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdCoverageRequestVersions
{
    public const string Manifest =
        "money_threshold_coverage_request_manifest.v1";
}

public sealed class MoneyThresholdCoverageRequestManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdCoverageRequestVersions.Manifest;

    [JsonPropertyName("base_coverage_request_path")]
    public string BaseCoverageRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("money_threshold_corpus_path")]
    public string MoneyThresholdCorpusPath { get; set; } = string.Empty;

    [JsonPropertyName("source_id")]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("attachment_policy")]
    public string AttachmentPolicy { get; set; } =
        "Rebuild the referenced Money corpus, preserve every non-Money coverage source, replace all prior Money sources with exactly one source ID, and keep formal training unauthorized. The resulting request remains subject to the independent coverage and reconciliation builders.";
}
