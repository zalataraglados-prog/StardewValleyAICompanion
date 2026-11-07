using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSeedTransferProof(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("portfolio_id")] string PortfolioId,
    [property: JsonPropertyName("prepared_material_transfer_sha256")] string PreparedMaterialTransferSha256,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("executor_version")] string ExecutorVersion,
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId,
    [property: JsonPropertyName("queue_path")] string QueuePath,
    [property: JsonPropertyName("queue_sha256")] string QueueSha256,
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("before_snapshot_sha256")] string BeforeSnapshotSha256,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("execution_receipt_sha256")] string ExecutionReceiptSha256,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("after_snapshot_sha256")] string AfterSnapshotSha256,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick,
    [property: JsonPropertyName("material_relocations")] ReservationPortfolioMaterialRelocation[] MaterialRelocations,
    [property: JsonPropertyName("artifact_hashes")] IReadOnlyDictionary<string, string> ArtifactHashes,
    [property: JsonPropertyName("native_outcome_verified")] bool NativeOutcomeVerified,
    [property: JsonPropertyName("formal_product_training_authorized")] bool FormalProductTrainingAuthorized);
