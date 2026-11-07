using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyShopReplaySourceBindingManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "money_shop_replay_source_binding_manifest.v1";
    [JsonPropertyName("reference_request_id")]
    public string ReferenceRequestId { get; set; } = string.Empty;
    [JsonPropertyName("replay_request_id")]
    public string ReplayRequestId { get; set; } = string.Empty;
    [JsonPropertyName("reference")]
    public MoneyShopReplaySource Reference { get; set; } = new();
    [JsonPropertyName("replay")]
    public MoneyShopReplaySource Replay { get; set; } = new();
    [JsonPropertyName("executables")]
    public MoneyShopReplayExecutable[] Executables { get; set; } = Array.Empty<MoneyShopReplayExecutable>();
    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyShopReplaySource
{
    [JsonPropertyName("request_manifest_path")]
    public string RequestManifestPath { get; set; } = string.Empty;
    [JsonPropertyName("candidate_catalog_path")]
    public string CandidateCatalogPath { get; set; } = string.Empty;
    [JsonPropertyName("native_witness_proof_path")]
    public string NativeWitnessProofPath { get; set; } = string.Empty;
    [JsonPropertyName("run_identity_path")]
    public string RunIdentityPath { get; set; } = string.Empty;
    [JsonPropertyName("measurement_identity_path")]
    public string MeasurementIdentityPath { get; set; } = string.Empty;
    [JsonPropertyName("source_save_slot_path")]
    public string SourceSaveSlotPath { get; set; } = string.Empty;
    [JsonPropertyName("isolated_save_slot_path")]
    public string IsolatedSaveSlotPath { get; set; } = string.Empty;
    [JsonPropertyName("mods_root_path")]
    public string ModsRootPath { get; set; } = string.Empty;
    [JsonPropertyName("setup_steps")]
    public MoneyShopReplaySetupStep[] SetupSteps { get; set; } = Array.Empty<MoneyShopReplaySetupStep>();
}

public sealed record MoneyShopReplayExecutable(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("path")] string Path);
public sealed record MoneyShopReplaySetupStep(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("result_path")] string ResultPath);
public sealed record MoneyShopReplayRequestMapping(
    [property: JsonPropertyName("reference_request_id")] string ReferenceRequestId,
    [property: JsonPropertyName("reference_request_sha256")] string ReferenceRequestSha256,
    [property: JsonPropertyName("replay_request_id")] string ReplayRequestId,
    [property: JsonPropertyName("replay_request_sha256")] string ReplayRequestSha256,
    [property: JsonPropertyName("catalog_candidate_id")] string CatalogCandidateId);

public sealed class MoneyShopReplaySourceBindingReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "money_shop_replay_source_binding_receipt.v1";
    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;
    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "criterion_local_declared_held_stock_direct_shop_sale";
    [JsonPropertyName("reference_state_hash")]
    public string ReferenceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("replay_state_hash")]
    public string ReplayStateHash { get; set; } = string.Empty;
    [JsonPropertyName("reference_request_id")]
    public string ReferenceRequestId { get; set; } = string.Empty;
    [JsonPropertyName("replay_request_id")]
    public string ReplayRequestId { get; set; } = string.Empty;
    [JsonPropertyName("source_save_sha256")]
    public string SourceSaveSha256 { get; set; } = string.Empty;
    [JsonPropertyName("declared_setup_sha256")]
    public string DeclaredSetupSha256 { get; set; } = string.Empty;
    [JsonPropertyName("persistent_boundary_contract")]
    public string PersistentBoundaryContract { get; set; } = string.Empty;
    [JsonPropertyName("bound_execution_metadata_differences")]
    public string[] BoundExecutionMetadataDifferences { get; set; } = Array.Empty<string>();
    [JsonPropertyName("full_candidate_count")]
    public int FullCandidateCount { get; set; }
    [JsonPropertyName("request_mapping")]
    public MoneyShopReplayRequestMapping[] RequestMapping { get; set; } = Array.Empty<MoneyShopReplayRequestMapping>();
    [JsonPropertyName("reference_native_witness")]
    public MoneyRevenueTerminalExecutionEvidence ReferenceNativeWitness { get; set; } = null!;
    [JsonPropertyName("replay_native_evidence")]
    public MoneyRevenueTerminalExecutionEvidence ReplayNativeEvidence { get; set; } = null!;
    [JsonPropertyName("source_artifacts")]
    public MoneyRevenueArtifactBinding[] SourceArtifacts { get; set; } = Array.Empty<MoneyRevenueArtifactBinding>();
    [JsonPropertyName("source_replay_binding_verified")]
    public bool SourceReplayBindingVerified { get; set; }
    [JsonPropertyName("original_reference_fragment_completed")]
    public bool OriginalReferenceFragmentCompleted { get; set; }
    [JsonPropertyName("teacher_preference_label_eligible")]
    public bool TeacherPreferenceLabelEligible { get; set; }
    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = new[] { "complete_replay_aware_comparison_admission_not_built" };
    [JsonPropertyName("receipt_sha256")]
    public string ReceiptSha256 { get; set; } = string.Empty;
}
