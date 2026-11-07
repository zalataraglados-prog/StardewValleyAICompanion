using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyShopReplayComparisonManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "money_shop_replay_comparison_manifest.v1";
    [JsonPropertyName("source_binding_receipt_paths")]
    public string[] SourceBindingReceiptPaths { get; set; } = Array.Empty<string>();
    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyShopReplayComparisonAlternative(
    [property: JsonPropertyName("reference_request_id")] string ReferenceRequestId,
    [property: JsonPropertyName("reference_concrete_alternative_id")] string ReferenceConcreteAlternativeId,
    [property: JsonPropertyName("actual_request_id")] string ActualRequestId,
    [property: JsonPropertyName("actual_request_sha256")] string ActualRequestSha256,
    [property: JsonPropertyName("actual_source_state_hash")] string ActualSourceStateHash,
    [property: JsonPropertyName("actual_run_id")] string ActualRunId,
    [property: JsonPropertyName("actual_request_nonce")] string ActualRequestNonce,
    [property: JsonPropertyName("source_kind")] string SourceKind,
    [property: JsonPropertyName("source_binding_receipt_path")] string SourceBindingReceiptPath,
    [property: JsonPropertyName("source_binding_receipt_sha256")] string SourceBindingReceiptSha256,
    [property: JsonPropertyName("actual_episode_path")] string ActualEpisodePath,
    [property: JsonPropertyName("actual_episode_sha256")] string ActualEpisodeSha256,
    [property: JsonPropertyName("actual_comparison_evidence")] MoneyRevenueComparisonAdmissionAlternative ActualComparisonEvidence);

public sealed class MoneyShopReplayComparisonReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "money_shop_replay_comparison_receipt.v1";
    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;
    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "criterion_local_declared_held_stock_direct_shop_sale";
    [JsonPropertyName("reference_request_manifest_path")]
    public string ReferenceRequestManifestPath { get; set; } = string.Empty;
    [JsonPropertyName("reference_request_catalog_sha256")]
    public string ReferenceRequestCatalogSha256 { get; set; } = string.Empty;
    [JsonPropertyName("reference_source_state_hash")]
    public string ReferenceSourceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("reference_snapshot_sha256")]
    public string ReferenceSnapshotSha256 { get; set; } = string.Empty;
    [JsonPropertyName("reference_strategy_ledger_sha256")]
    public string ReferenceStrategyLedgerSha256 { get; set; } = string.Empty;
    [JsonPropertyName("reference_ledger_revision")]
    public int ReferenceLedgerRevision { get; set; }
    [JsonPropertyName("request_ids")]
    public string[] RequestIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("concrete_alternative_ids")]
    public string[] ConcreteAlternativeIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("source_bindings")]
    public MoneyRevenueArtifactBinding[] SourceBindings { get; set; } = Array.Empty<MoneyRevenueArtifactBinding>();
    [JsonPropertyName("alternatives")]
    public MoneyShopReplayComparisonAlternative[] Alternatives { get; set; } = Array.Empty<MoneyShopReplayComparisonAlternative>();
    [JsonPropertyName("comparisons")]
    public MoneyRevenueAlternativeComparison[] Comparisons { get; set; } = Array.Empty<MoneyRevenueAlternativeComparison>();
    [JsonPropertyName("candidate_denominator_authoritative")]
    public bool CandidateDenominatorAuthoritative { get; set; }
    [JsonPropertyName("all_actual_native_outcomes_verified")]
    public bool AllActualNativeOutcomesVerified { get; set; }
    [JsonPropertyName("teacher_eligible_criterion_ids")]
    public string[] TeacherEligibleCriterionIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("original_exact_frame_index_completed")]
    public bool OriginalExactFrameIndexCompleted { get; set; }
    [JsonPropertyName("criterion_corpus_admission_verified")]
    public bool CriterionCorpusAdmissionVerified { get; set; }
    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = new[] { "criterion_partition_native_outcome_join_not_built" };
    [JsonPropertyName("receipt_sha256")]
    public string ReceiptSha256 { get; set; } = string.Empty;
}
