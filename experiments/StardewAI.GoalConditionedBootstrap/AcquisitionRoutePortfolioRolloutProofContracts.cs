using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioRolloutProofManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_rollout_proof_manifest.v1";

    [JsonPropertyName("initial_checkpoint_proof")]
    public AcquisitionRoutePortfolioInitialCheckpointProof
        InitialCheckpointProof { get; set; } = new();

    [JsonPropertyName("initial_checkpoint_path")]
    public string InitialCheckpointPath { get; set; } = string.Empty;

    [JsonPropertyName("continuation_transitions")]
    public AcquisitionRoutePortfolioContinuationTransitionProof[]
        ContinuationTransitions { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioContinuationTransitionProof>();

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }
}

public sealed class AcquisitionRoutePortfolioContinuationTransitionProof
{
    [JsonPropertyName("continuation_request_path")]
    public string ContinuationRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("execution_inputs")]
    public AcquisitionRouteExecutionBindingInputs ExecutionInputs
    { get; set; } = new();

    [JsonPropertyName("execution_binding_path")]
    public string ExecutionBindingPath { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_path")]
    public string FreshTerminalReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("executor_version")]
    public string ExecutorVersion { get; set; } = string.Empty;

    [JsonPropertyName("settlement_request_path")]
    public string SettlementRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("settlement_result_path")]
    public string SettlementResultPath { get; set; } = string.Empty;

    [JsonPropertyName("settled_ledger_path")]
    public string SettledLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("settlement_receipt_path")]
    public string SettlementReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_path")]
    public string CheckpointPath { get; set; } = string.Empty;
}

public sealed class AcquisitionRoutePortfolioRolloutProofReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_rollout_proof_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("community_center_provenance")]
    public AcquisitionRouteCommunityCenterProvenance
        CommunityCenterProvenance { get; set; } = new();

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("latest_checkpoint_sha256")]
    public string LatestCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("continuation_transition_count")]
    public int ContinuationTransitionCount { get; set; }

    [JsonPropertyName("latest_state_hash")]
    public string LatestStateHash { get; set; } = string.Empty;

    [JsonPropertyName("latest_ledger_revision")]
    public int LatestLedgerRevision { get; set; }

    [JsonPropertyName("latest_ledger_sha256")]
    public string LatestLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("portfolio_completion_verified")]
    public bool PortfolioCompletionVerified { get; set; }

    [JsonPropertyName("proof_chain_verified")]
    public bool ProofChainVerified { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "The manifest is an ordered proof chain, not a trusted summary. Verification exactly rebuilds the initial checkpoint and every continuation transition from its authoritative planning, execution, fresh-terminal and settlement artifacts. Each stored checkpoint must equal the rebuilt value, bind the immediately prior checkpoint hash and advance transition_count exactly once. The receipt exposes the latest verified checkpoint but never authorizes formal training.";
}

internal sealed record AcquisitionRoutePortfolioVerifiedCheckpoint(
    string CheckpointPath,
    AcquisitionRoutePortfolioRolloutCheckpoint Checkpoint);
