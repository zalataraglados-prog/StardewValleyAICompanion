using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class BootstrapSchemas
{
    public const string Demonstration = "goal_conditioned_demonstration.v1";
    public const string TeacherPlan = "goal_conditioned_teacher_plan.v1";
    public const string KnowledgeAudit = "goal_method_knowledge_audit.v1";
    public const string Retrieval = "goal_conditioned_demonstration_retrieval.v1";
}

public static class DemonstrationSourceKinds
{
    public const string HumanPlayer = "human_player";
    public const string DeterministicTeacher = "deterministic_teacher";
    public const string LegacyAiRollout = "legacy_ai_rollout";

    public static bool CanTeach(string value) =>
        value is HumanPlayer or DeterministicTeacher;
}

public sealed class GoalConditionedDemonstration
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = BootstrapSchemas.Demonstration;

    [JsonPropertyName("demonstration_id")]
    public string DemonstrationId { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public DemonstrationSource Source { get; set; } = new();

    [JsonPropertyName("goal")]
    public DemonstrationGoal Goal { get; set; } = new();

    [JsonPropertyName("context")]
    public DemonstrationContext Context { get; set; } = new();

    [JsonPropertyName("state_features")]
    public FeatureVector StateFeatures { get; set; } = new();

    [JsonPropertyName("segments")]
    public DemonstrationSegment[] Segments { get; set; } = Array.Empty<DemonstrationSegment>();

    [JsonPropertyName("outcome")]
    public DemonstrationOutcome Outcome { get; set; } = new();

    [JsonPropertyName("audit")]
    public DemonstrationAudit Audit { get; set; } = new();
}

public sealed class DemonstrationSource
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("source_paths")]
    public string[] SourcePaths { get; set; } = Array.Empty<string>();

    [JsonPropertyName("source_sha256")]
    public string[] SourceSha256 { get; set; } = Array.Empty<string>();
}

public sealed class DemonstrationGoal
{
    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("target_score")]
    public int? TargetScore { get; set; }

    [JsonPropertyName("active_criterion_ids")]
    public string[] ActiveCriterionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("method_ids")]
    public string[] MethodIds { get; set; } = Array.Empty<string>();
}

public sealed class DemonstrationContext
{
    [JsonPropertyName("save_id")]
    public string SaveId { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("start_time")]
    public int StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public int EndTime { get; set; }

    [JsonPropertyName("start_state_hash")]
    public string StartStateHash { get; set; } = string.Empty;

    [JsonPropertyName("end_state_hash")]
    public string EndStateHash { get; set; } = string.Empty;
}

public sealed class DemonstrationSegment
{
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("bundle_id")]
    public string BundleId { get; set; } = string.Empty;

    [JsonPropertyName("method_id")]
    public string MethodId { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("candidate_kind")]
    public string CandidateKind { get; set; } = string.Empty;

    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("start_state_hash")]
    public string StartStateHash { get; set; } = string.Empty;

    [JsonPropertyName("end_state_hash")]
    public string EndStateHash { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("semantic_confidence")]
    public double SemanticConfidence { get; set; }

    [JsonPropertyName("observed_effects")]
    public JsonElement ObservedEffects { get; set; }
}

public sealed class DemonstrationOutcome
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("day_boundary_observed")]
    public bool DayBoundaryObserved { get; set; }

    [JsonPropertyName("all_segments_verified")]
    public bool AllSegmentsVerified { get; set; }

    [JsonPropertyName("goal_progress_before")]
    public double? GoalProgressBefore { get; set; }

    [JsonPropertyName("goal_progress_after")]
    public double? GoalProgressAfter { get; set; }

    [JsonPropertyName("terminal_goal_complete")]
    public bool TerminalGoalComplete { get; set; }
}

public sealed class DemonstrationAudit
{
    [JsonPropertyName("expert_admitted")]
    public bool ExpertAdmitted { get; set; }

    [JsonPropertyName("admission_reasons")]
    public string[] AdmissionReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("policy")]
    public string Policy { get; set; } =
        "Only verified human-player or deterministic-teacher demonstrations may supervise the goal-to-method policy. Legacy AI rollouts remain evaluation evidence only.";
}

public sealed class TeacherPlan
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = BootstrapSchemas.TeacherPlan;

    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("knowledge_sha256")]
    public string KnowledgeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("candidate_count")]
    public int CandidateCount { get; set; }

    [JsonPropertyName("bundles")]
    public TeacherBundle[] Bundles { get; set; } = Array.Empty<TeacherBundle>();

    [JsonPropertyName("deferred_candidates")]
    public TeacherDeferredCandidate[] DeferredCandidates { get; set; } = Array.Empty<TeacherDeferredCandidate>();

    [JsonPropertyName("audit")]
    public TeacherPlanAudit Audit { get; set; } = new();
}

public sealed class TeacherBundle
{
    [JsonPropertyName("bundle_id")]
    public string BundleId { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("method_ids")]
    public string[] MethodIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("priority_class")]
    public string PriorityClass { get; set; } = string.Empty;

    [JsonPropertyName("estimated_ticks")]
    public int EstimatedTicks { get; set; }

    [JsonPropertyName("items")]
    public TeacherPlanItem[] Items { get; set; } = Array.Empty<TeacherPlanItem>();
}

public sealed class TeacherPlanItem
{
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("method_id")]
    public string MethodId { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int? TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int? TileY { get; set; }

    [JsonPropertyName("teacher_score")]
    public double TeacherScore { get; set; }

    [JsonPropertyName("marginal_route_ticks")]
    public int MarginalRouteTicks { get; set; }

    [JsonPropertyName("reasons")]
    public string[] Reasons { get; set; } = Array.Empty<string>();
}

public sealed class TeacherDeferredCandidate
{
    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class TeacherPlanAudit
{
    [JsonPropertyName("uses_model_score_as_label")]
    public bool UsesModelScoreAsLabel { get; set; }

    [JsonPropertyName("uses_expert_demonstration_guidance")]
    public bool UsesExpertDemonstrationGuidance { get; set; }

    [JsonPropertyName("expert_demonstration_id")]
    public string ExpertDemonstrationId { get; set; } = string.Empty;

    [JsonPropertyName("expert_demonstration_similarity")]
    public double? ExpertDemonstrationSimilarity { get; set; }

    [JsonPropertyName("route_cost_policy")]
    public string RouteCostPolicy { get; set; } = "first item pays destination entry; same-location items pay Manhattan marginal distance";

    [JsonPropertyName("training_policy")]
    public string TrainingPolicy { get; set; } = "Teacher plans require runtime verification before becoming expert demonstrations.";

    [JsonPropertyName("guidance_policy")]
    public string GuidancePolicy { get; set; } =
        "Expert demonstrations order matching methods inside the same hard priority class; availability, timeline, safety, and deterministic completion remain authoritative.";
}

public sealed class DemonstrationRetrieval
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = BootstrapSchemas.Retrieval;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("query_state_hash")]
    public string QueryStateHash { get; set; } = string.Empty;

    [JsonPropertyName("library_rows")]
    public int LibraryRows { get; set; }

    [JsonPropertyName("expert_rows")]
    public int ExpertRows { get; set; }

    [JsonPropertyName("matches")]
    public DemonstrationMatch[] Matches { get; set; } = Array.Empty<DemonstrationMatch>();

    [JsonPropertyName("audit")]
    public string Audit { get; set; } =
        "Only validator-admitted human or deterministic-teacher demonstrations are retrievable; legacy AI rollouts cannot bootstrap the policy.";
}

public sealed class DemonstrationMatch
{
    [JsonPropertyName("demonstration_id")]
    public string DemonstrationId { get; set; } = string.Empty;

    [JsonPropertyName("source_kind")]
    public string SourceKind { get; set; } = string.Empty;

    [JsonPropertyName("similarity")]
    public double Similarity { get; set; }

    [JsonPropertyName("shared_feature_count")]
    public int SharedFeatureCount { get; set; }

    [JsonPropertyName("segments")]
    public DemonstrationSegment[] Segments { get; set; } = Array.Empty<DemonstrationSegment>();
}
