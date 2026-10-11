using System;
using System.Text.Json.Serialization;

namespace StardewAI.Core.Infrastructure;

public sealed class ValidationTraceDependencyInput
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("artifact_id")]
    public string ArtifactId { get; set; } = string.Empty;

    [JsonPropertyName("artifact_kind")]
    public string ArtifactKind { get; set; } = string.Empty;

    [JsonPropertyName("content_sha256")]
    public string ContentSha256 { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("reuse_scope")]
    public string ReuseScope { get; set; } = string.Empty;
}

public sealed class ValidationTraceDependencyNode
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("artifact_id")]
    public string ArtifactId { get; set; } = string.Empty;

    [JsonPropertyName("artifact_kind")]
    public string ArtifactKind { get; set; } = string.Empty;

    [JsonPropertyName("content_sha256")]
    public string ContentSha256 { get; set; } = string.Empty;

    [JsonPropertyName("producer_id")]
    public string ProducerId { get; set; } = string.Empty;

    [JsonPropertyName("producer_version")]
    public string ProducerVersion { get; set; } = string.Empty;

    [JsonPropertyName("input_artifact_ids")]
    public string[] InputArtifactIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("inputs")]
    public ValidationTraceDependencyInput[] Inputs { get; set; } =
        Array.Empty<ValidationTraceDependencyInput>();

    [JsonPropertyName("validation_level")]
    public string ValidationLevel { get; set; } = "observe_only_full_rebuild";

    [JsonPropertyName("reuse_scope")]
    public string ReuseScope { get; set; } = "invocation";

    [JsonPropertyName("invocation_count")]
    public long InvocationCount { get; set; } = 1;

    [JsonPropertyName("cache_hit")]
    public bool CacheHit { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("identity_conflict")]
    public bool IdentityConflict { get; set; }

    [JsonPropertyName("invalidation_reasons")]
    public string[] InvalidationReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("wall_ms")]
    public double WallMilliseconds { get; set; }

    [JsonPropertyName("cpu_ms")]
    public double CpuMilliseconds { get; set; }
}

public sealed class ValidationTraceInvalidationSet
{
    [JsonPropertyName("changed_input_artifact_id")]
    public string ChangedInputArtifactId { get; set; } = string.Empty;

    [JsonPropertyName("changed_input_names")]
    public string[] ChangedInputNames { get; set; } = Array.Empty<string>();

    [JsonPropertyName("changed_input_paths")]
    public string[] ChangedInputPaths { get; set; } = Array.Empty<string>();

    [JsonPropertyName("invalidated_node_ids")]
    public string[] InvalidatedNodeIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "input_identity_changed";

    [JsonPropertyName("propagation_rule")]
    public string PropagationRule { get; set; } = "all_descendants";
}

public sealed class ValidationTraceDependencyGraph
{
    public const string CurrentSchemaVersion =
        "stardewai.validation_dependency_graph.v1";

    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("observation_only")]
    public bool ObservationOnly { get; set; } = true;

    [JsonPropertyName("can_affect_admission")]
    public bool CanAffectAdmission { get; set; }

    [JsonPropertyName("incremental_reuse_authorized")]
    public bool IncrementalReuseAuthorized { get; set; }

    [JsonPropertyName("acyclic")]
    public bool Acyclic { get; set; } = true;

    [JsonPropertyName("complete")]
    public bool Complete { get; set; }

    [JsonPropertyName("full_rebuild_executed")]
    public bool FullRebuildExecuted { get; set; }

    [JsonPropertyName("top_level_node_ids")]
    public string[] TopLevelNodeIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("nodes")]
    public ValidationTraceDependencyNode[] Nodes { get; set; } =
        Array.Empty<ValidationTraceDependencyNode>();

    [JsonPropertyName("theoretical_invalidation_sets")]
    public ValidationTraceInvalidationSet[] TheoreticalInvalidationSets
        { get; set; } = Array.Empty<ValidationTraceInvalidationSet>();
}
