using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyCounterfactualSourceAudit
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "money_counterfactual_source_audit.v1";
    [JsonPropertyName("reference_manifest_path")]
    public string ReferenceManifestPath { get; set; } = string.Empty;
    [JsonPropertyName("reference_manifest_sha256")]
    public string ReferenceManifestSha256 { get; set; } = string.Empty;
    [JsonPropertyName("reference_request_id")]
    public string ReferenceRequestId { get; set; } = string.Empty;
    [JsonPropertyName("reference_request_sha256")]
    public string ReferenceRequestSha256 { get; set; } = string.Empty;
    [JsonPropertyName("reference_snapshot_path")]
    public string ReferenceSnapshotPath { get; set; } = string.Empty;
    [JsonPropertyName("reference_snapshot_sha256")]
    public string ReferenceSnapshotSha256 { get; set; } = string.Empty;
    [JsonPropertyName("reference_state_hash")]
    public string ReferenceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("replay_catalog_path")]
    public string ReplayCatalogPath { get; set; } = string.Empty;
    [JsonPropertyName("replay_catalog_sha256")]
    public string ReplayCatalogSha256 { get; set; } = string.Empty;
    [JsonPropertyName("replay_snapshot_path")]
    public string ReplaySnapshotPath { get; set; } = string.Empty;
    [JsonPropertyName("replay_snapshot_sha256")]
    public string ReplaySnapshotSha256 { get; set; } = string.Empty;
    [JsonPropertyName("replay_state_hash")]
    public string ReplayStateHash { get; set; } = string.Empty;
    [JsonPropertyName("reference_candidate")]
    public MoneyRevenueCandidateCatalogEntry ReferenceCandidate { get; set; } = new();
    [JsonPropertyName("replay_candidate")]
    public MoneyRevenueCandidateCatalogEntry? ReplayCandidate { get; set; }
    [JsonPropertyName("replay_objective_parameters")]
    public SmallModelActionParameter[] ReplayObjectiveParameters { get; set; } = Array.Empty<SmallModelActionParameter>();
    [JsonPropertyName("same_raw_state_hash")]
    public bool SameRawStateHash { get; set; }
    [JsonPropertyName("same_snapshot_bytes")]
    public bool SameSnapshotBytes { get; set; }
    [JsonPropertyName("same_actor_and_version")]
    public bool SameActorAndVersion { get; set; }
    [JsonPropertyName("same_money_checkpoint_facts")]
    public bool SameMoneyCheckpointFacts { get; set; }
    [JsonPropertyName("reference_persistent_boundary_available")]
    public bool ReferencePersistentBoundaryAvailable { get; set; }
    [JsonPropertyName("replay_persistent_boundary_available")]
    public bool ReplayPersistentBoundaryAvailable { get; set; }
    [JsonPropertyName("same_persistent_boundary")]
    public bool SamePersistentBoundary { get; set; }
    [JsonPropertyName("changed_persistent_fields")]
    public string[] ChangedPersistentFields { get; set; } = Array.Empty<string>();
    [JsonPropertyName("same_native_shop_context")]
    public bool SameNativeShopContext { get; set; }
    [JsonPropertyName("matching_replay_candidate_count")]
    public int MatchingReplayCandidateCount { get; set; }
    [JsonPropertyName("replay_binding_authorized")]
    public bool ReplayBindingAuthorized { get; set; }
    [JsonPropertyName("native_outcome_verified")]
    public bool NativeOutcomeVerified { get; set; }
    [JsonPropertyName("teacher_preference_label_eligible")]
    public bool TeacherPreferenceLabelEligible { get; set; }
    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    [JsonPropertyName("audit_sha256")]
    public string AuditSha256 { get; set; } = string.Empty;
}
