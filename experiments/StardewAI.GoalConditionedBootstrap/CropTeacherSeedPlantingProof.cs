using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSeedPlantingProof(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("portfolio_id")] string PortfolioId,
    [property: JsonPropertyName("reservation_id")] string ReservationId,
    [property: JsonPropertyName("consumed_quantity")] int ConsumedQuantity,
    [property: JsonPropertyName("planned_quantity")] int PlannedQuantity,
    [property: JsonPropertyName("batch_completed")] bool BatchCompleted,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("executor_version")] string ExecutorVersion,
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId,
    [property: JsonPropertyName("queue_sha256")] string QueueSha256,
    [property: JsonPropertyName("marker_ledger_sha256")] string MarkerLedgerSha256,
    [property: JsonPropertyName("artifact_hashes")] IReadOnlyDictionary<string, string> ArtifactHashes,
    [property: JsonPropertyName("native_outcome_verified")] bool NativeOutcomeVerified,
    [property: JsonPropertyName("formal_product_training_authorized")] bool FormalProductTrainingAuthorized);
