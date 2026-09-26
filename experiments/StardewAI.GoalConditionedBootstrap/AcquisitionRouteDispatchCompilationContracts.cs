using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteDispatchCompilation
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_dispatch_compilation.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("route_kind")]
    public string RouteKind { get; set; } = string.Empty;

    [JsonPropertyName("source_id")]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("ranking_sha256")]
    public string RankingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_id")]
    public string SourceCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("endpoint_option_id")]
    public string EndpointOptionId { get; set; } = string.Empty;

    [JsonPropertyName("selected_route_option_role")]
    public string SelectedRouteOptionRole { get; set; } = string.Empty;

    [JsonPropertyName("terminal_receipt_eligible")]
    public bool TerminalReceiptEligible { get; set; }

    [JsonPropertyName("fresh_replan_required_after_success")]
    public bool FreshReplanRequiredAfterSuccess { get; set; }

    [JsonPropertyName("source_binding_evidence")]
    public string SourceBindingEvidence { get; set; } = string.Empty;

    [JsonPropertyName("support_request_sha256")]
    public string SupportRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("support_commit_receipt_sha256")]
    public string SupportCommitReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("support_deadline_total_day")]
    public int? SupportDeadlineTotalDay { get; set; }

    [JsonPropertyName("support_expected_ready_total_day")]
    public int? SupportExpectedReadyTotalDay { get; set; }

    [JsonPropertyName("support_reservation_commit_verified")]
    public bool SupportReservationCommitVerified { get; set; }

    [JsonPropertyName("prior_supporting_transition_replan_sha256")]
    public string PriorSupportingTransitionReplanSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("uses_learner_rank_or_score")]
    public bool UsesLearnerRankOrScore { get; set; }

    [JsonPropertyName("compiled_plan")]
    public SmallModelPlanEnvelope? CompiledPlan { get; set; }

    [JsonPropertyName("action_queue")]
    public ActionQueueEnvelope? ActionQueue { get; set; }

    [JsonPropertyName("dispatch_ready")]
    public bool DispatchReady { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The dispatcher selects only a current, available terminal or source-bound supporting candidate proven from the live ranking and same-state transparent snapshot. Learner rank, score and reward are erased before the existing daily-plan and action-queue compilers run. Every expanded primitive repeats the selected route, reservation ledger, source candidate, route-option role and ranking hash. A supporting transition requires a fresh snapshot and is never terminal-receipt eligible. Missing or ambiguous source evidence, blocked compilation or lost lineage fails closed; this artifact cannot authorize formal training.";
}

internal sealed record AcquisitionRouteDispatchCandidateMatch(
    StardewAI.Contracts.Training.PolicyEventCandidatePrediction Candidate,
    string IdentityEvidence,
    string RouteOptionRole = "terminal_transition");
