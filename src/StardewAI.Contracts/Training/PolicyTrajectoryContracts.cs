using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.Contracts.Training;

public sealed class PolicyDecisionTrajectoryEnvelope
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "policy_decision_trajectory.v1";

    [JsonPropertyName("trajectory_id")]
    public string TrajectoryId { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public PolicyTrajectoryContext Context { get; set; } = new();

    [JsonPropertyName("versions")]
    public PolicyTrajectoryVersions Versions { get; set; } = new();

    [JsonPropertyName("candidates")]
    public PolicyTrajectoryCandidate[] Candidates { get; set; } = Array.Empty<PolicyTrajectoryCandidate>();

    [JsonPropertyName("selection")]
    public PolicyTrajectorySelection Selection { get; set; } = new();

    [JsonPropertyName("outcome")]
    public PolicyTrajectoryOutcome Outcome { get; set; } = new();

    [JsonPropertyName("returns")]
    public PolicyTrajectoryReturns Returns { get; set; } = new();

    [JsonPropertyName("audit")]
    public PolicyTrajectoryAudit Audit { get; set; } = new();
}

public sealed class PolicyTrajectoryContext
{
    [JsonPropertyName("save_id")]
    public string SaveId { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("time")]
    public int Time { get; set; }

    [JsonPropertyName("split_key")]
    public string SplitKey { get; set; } = string.Empty;

    [JsonPropertyName("dataset_partition")]
    public string DatasetPartition { get; set; } = "unassigned";
}

public sealed class PolicyTrajectoryVersions
{
    [JsonPropertyName("feature_schema")]
    public string FeatureSchema { get; set; } = string.Empty;

    [JsonPropertyName("candidate_vocabulary")]
    public string CandidateVocabulary { get; set; } = string.Empty;

    [JsonPropertyName("capability_registry")]
    public string CapabilityRegistry { get; set; } = string.Empty;

    [JsonPropertyName("knowledge_dictionary")]
    public string KnowledgeDictionary { get; set; } = string.Empty;

    [JsonPropertyName("compiler")]
    public string Compiler { get; set; } = string.Empty;

    [JsonPropertyName("executor")]
    public string Executor { get; set; } = string.Empty;
}

public sealed class PolicyTrajectoryCandidate
{
    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("expected_reward")]
    public double ExpectedReward { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("admitted_for_policy")]
    public bool AdmittedForPolicy { get; set; }

    [JsonPropertyName("selected")]
    public bool Selected { get; set; }

    [JsonPropertyName("estimated_ticks")]
    public int EstimatedTicks { get; set; }

    [JsonPropertyName("energy_cost")]
    public int EnergyCost { get; set; }

    [JsonPropertyName("exclusion_reasons")]
    public string[] ExclusionReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("parameters")]
    public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();
}

public sealed class PolicyTrajectorySelection
{
    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();
}

public sealed class PolicyTrajectoryOutcome
{
    [JsonPropertyName("episode_id")]
    public string EpisodeId { get; set; } = string.Empty;

    [JsonPropertyName("queue_id")]
    public string QueueId { get; set; } = string.Empty;

    [JsonPropertyName("primitive_option_id")]
    public string PrimitiveOptionId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("actual_ticks")]
    public long ActualTicks { get; set; }

    [JsonPropertyName("state_hash_changed")]
    public bool StateHashChanged { get; set; }

    [JsonPropertyName("after_snapshot_fresh")]
    public bool AfterSnapshotFresh { get; set; }

    [JsonPropertyName("failure_attribution")]
    public string FailureAttribution { get; set; } = string.Empty;

    [JsonPropertyName("block_reasons")]
    public string[] BlockReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("changed_facts")]
    public JsonElement ChangedFacts { get; set; }
}

public sealed class PolicyTrajectoryReturns
{
    [JsonPropertyName("immediate")]
    public double Immediate { get; set; }

    [JsonPropertyName("day")]
    public double? Day { get; set; }

    [JsonPropertyName("season")]
    public double? Season { get; set; }

    [JsonPropertyName("year")]
    public double? Year { get; set; }

    [JsonPropertyName("grandpa_21")]
    public double? Grandpa21 { get; set; }

    [JsonPropertyName("long_horizon_status")]
    public string LongHorizonStatus { get; set; } = "pending";
}

public sealed class PolicyTrajectoryAudit
{
    [JsonPropertyName("writer")]
    public string Writer { get; set; } = "StardewAI.Core.Training.PolicyDecisionTrajectoryBuilder";

    [JsonPropertyName("policy")]
    public string Policy { get; set; } = "Every decision preserves the complete candidate set; only one evidence-admitted selected option may label a policy trajectory, while non-admitted candidates remain explicit negatives.";
}

public sealed class PolicyTrajectoryAppendResult
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "policy_trajectory_append.v1";

    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = string.Empty;

    [JsonPropertyName("trajectory_id")]
    public string TrajectoryId { get; set; } = string.Empty;

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }
}
