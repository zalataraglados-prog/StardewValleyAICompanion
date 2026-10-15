using System;
using System.Text.Json.Serialization;

namespace StardewAI.Core.Infrastructure;

public sealed class ValidationTraceInput
{
    [JsonPropertyName("argument_name")]
    public string ArgumentName { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("artifact_id")]
    public string? ArtifactId { get; set; }
}

public sealed class ValidationTraceOperationRecord
{
    [JsonPropertyName("invocation_count")]
    public long InvocationCount { get; set; } = 1;

    [JsonPropertyName("operation_id")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("last_operation_id")]
    public string LastOperationId { get; set; } = string.Empty;

    [JsonPropertyName("operation_kind")]
    public string OperationKind { get; set; } = string.Empty;

    [JsonPropertyName("producer_id")]
    public string ProducerId { get; set; } = string.Empty;

    [JsonPropertyName("producer_version")]
    public string ProducerVersion { get; set; } = string.Empty;

    [JsonPropertyName("input_paths")]
    public string[] InputPaths { get; set; } = Array.Empty<string>();

    [JsonPropertyName("artifact_ids")]
    public string[] ArtifactIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("artifact_ids_truncated")]
    public bool ArtifactIdsTruncated { get; set; }

    [JsonPropertyName("wall_ms")]
    public double WallMilliseconds { get; set; }

    [JsonPropertyName("cpu_ms")]
    public double CpuMilliseconds { get; set; }

    [JsonPropertyName("cpu_sample_count")]
    public long CpuSampleCount { get; set; }

    [JsonPropertyName("bytes_read")]
    public long BytesRead { get; set; }

    [JsonPropertyName("hash_bytes")]
    public long HashBytes { get; set; }

    [JsonPropertyName("allocated_bytes")]
    public long AllocatedBytes { get; set; }

    [JsonPropertyName("json_parse_ms")]
    public double JsonParseMilliseconds { get; set; }

    [JsonPropertyName("materialize_ms")]
    public double MaterializeMilliseconds { get; set; }

    [JsonPropertyName("process_start_ms")]
    public double ProcessStartMilliseconds { get; set; }

    [JsonPropertyName("build_ms")]
    public double BuildMilliseconds { get; set; }

    [JsonPropertyName("deterministic_rebuild_ms")]
    public double DeterministicRebuildMilliseconds { get; set; }

    [JsonPropertyName("evidence_index_ms")]
    public double EvidenceIndexMilliseconds { get; set; }

    [JsonPropertyName("native_runtime_ms")]
    public double NativeRuntimeMilliseconds { get; set; }

    [JsonPropertyName("cache_hit")]
    public bool CacheHit { get; set; }
}

public sealed class ValidationTraceReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = ValidationTraceSession.SchemaVersion;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("trace_id")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("scenario")]
    public string Scenario { get; set; } = string.Empty;

    [JsonPropertyName("producer_id")]
    public string ProducerId { get; set; } = string.Empty;

    [JsonPropertyName("producer_version")]
    public string ProducerVersion { get; set; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTimeOffset CompletedAt { get; set; }

    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set; }

    [JsonPropertyName("cache_hit")]
    public bool CacheHit { get; set; }

    [JsonPropertyName("inputs")]
    public ValidationTraceInput[] Inputs { get; set; } = Array.Empty<ValidationTraceInput>();

    [JsonPropertyName("wall_ms")]
    public double WallMilliseconds { get; set; }

    [JsonPropertyName("cpu_ms")]
    public double CpuMilliseconds { get; set; }

    [JsonPropertyName("peak_working_set_bytes")]
    public long PeakWorkingSetBytes { get; set; }

    [JsonPropertyName("private_memory_bytes")]
    public long PrivateMemoryBytes { get; set; }

    [JsonPropertyName("managed_heap_bytes")]
    public long ManagedHeapBytes { get; set; }

    [JsonPropertyName("managed_allocation_counter_available")]
    public bool ManagedAllocationCounterAvailable { get; set; }

    [JsonPropertyName("allocated_bytes")]
    public long AllocatedBytes { get; set; }

    [JsonPropertyName("gen0_collections")]
    public int Gen0Collections { get; set; }

    [JsonPropertyName("gen1_collections")]
    public int Gen1Collections { get; set; }

    [JsonPropertyName("gen2_collections")]
    public int Gen2Collections { get; set; }

    [JsonPropertyName("bytes_read")]
    public long BytesRead { get; set; }

    [JsonPropertyName("hash_bytes")]
    public long HashBytes { get; set; }

    [JsonPropertyName("json_parse_ms")]
    public double JsonParseMilliseconds { get; set; }

    [JsonPropertyName("materialize_ms")]
    public double MaterializeMilliseconds { get; set; }

    [JsonPropertyName("process_start_ms")]
    public double ProcessStartMilliseconds { get; set; }

    [JsonPropertyName("build_ms")]
    public double BuildMilliseconds { get; set; }

    [JsonPropertyName("deterministic_rebuild_ms")]
    public double DeterministicRebuildMilliseconds { get; set; }

    [JsonPropertyName("evidence_index_ms")]
    public double EvidenceIndexMilliseconds { get; set; }

    [JsonPropertyName("native_runtime_ms")]
    public double NativeRuntimeMilliseconds { get; set; }

    [JsonPropertyName("operations")]
    public ValidationTraceOperationRecord[] Operations { get; set; } =
        Array.Empty<ValidationTraceOperationRecord>();

    [JsonPropertyName("operation_invocation_count")]
    public long OperationInvocationCount { get; set; }

    [JsonPropertyName("operation_group_count")]
    public int OperationGroupCount { get; set; }

    [JsonPropertyName("operation_cpu_sampling_rate")]
    public int OperationCpuSamplingRate { get; set; }

    [JsonPropertyName("dependency_graph")]
    public ValidationTraceDependencyGraph DependencyGraph { get; set; } =
        new();

    [JsonPropertyName("invocation_read_session")]
    public ValidationTraceReadSessionMetrics InvocationReadSession { get; set; } =
        new();

    [JsonPropertyName("deterministic_build_session")]
    public ValidationTraceDeterministicBuildSessionMetrics
        DeterministicBuildSession { get; set; } = new();
}

public sealed class ValidationTraceReadSessionMetrics
{
    [JsonPropertyName("reader_version")]
    public string ReaderVersion { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("registered_path_count")]
    public int RegisteredPathCount { get; set; }

    [JsonPropertyName("entry_count")]
    public int EntryCount { get; set; }

    [JsonPropertyName("physical_read_count")]
    public long PhysicalReadCount { get; set; }

    [JsonPropertyName("physical_bytes_read")]
    public long PhysicalBytesRead { get; set; }

    [JsonPropertyName("full_hash_count")]
    public long FullHashCount { get; set; }

    [JsonPropertyName("hash_bytes")]
    public long HashBytes { get; set; }

    [JsonPropertyName("materialization_count")]
    public long MaterializationCount { get; set; }

    [JsonPropertyName("cache_hit_count")]
    public long CacheHitCount { get; set; }

    [JsonPropertyName("retained_raw_bytes")]
    public long RetainedRawBytes { get; set; }

    [JsonPropertyName("retained_stored_text_bytes")]
    public long RetainedStoredTextBytes { get; set; }

    [JsonPropertyName("retained_materialized_text_bytes")]
    public long RetainedMaterializedTextBytes { get; set; }

    [JsonPropertyName("retained_hash_text_bytes")]
    public long RetainedHashTextBytes { get; set; }

    [JsonPropertyName("retained_bytes")]
    public long RetainedBytes { get; set; }

    [JsonPropertyName("max_entry_retained_bytes")]
    public long MaxEntryRetainedBytes { get; set; }

    [JsonPropertyName("retention_budget_bytes")]
    public long RetentionBudgetBytes { get; set; }

    [JsonPropertyName("entry_retention_limit_bytes")]
    public long EntryRetentionLimitBytes { get; set; }

    [JsonPropertyName("dynamic_registration_count")]
    public long DynamicRegistrationCount { get; set; }

    [JsonPropertyName("eviction_count")]
    public long EvictionCount { get; set; }

    [JsonPropertyName("large_entry_bypass_count")]
    public long LargeEntryBypassCount { get; set; }

    [JsonPropertyName("probation_bypass_count")]
    public long ProbationBypassCount { get; set; }

    [JsonPropertyName("released_raw_bytes")]
    public long ReleasedRawBytes { get; set; }

    [JsonPropertyName("released_stored_text_bytes")]
    public long ReleasedStoredTextBytes { get; set; }

    [JsonPropertyName("largest_entries")]
    public ValidationTraceReadSessionEntryMetrics[] LargestEntries { get; set; } =
        Array.Empty<ValidationTraceReadSessionEntryMetrics>();
}

public sealed class ValidationTraceReadSessionEntryMetrics
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("retained_bytes")]
    public long RetainedBytes { get; set; }

    [JsonPropertyName("access_count")]
    public long AccessCount { get; set; }
}

public sealed class ValidationTraceDeterministicBuildSessionMetrics
{
    [JsonPropertyName("session_version")]
    public string SessionVersion { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("build_count")]
    public int BuildCount { get; set; }

    [JsonPropertyName("cache_hit_count")]
    public int CacheHitCount { get; set; }

    [JsonPropertyName("entry_count")]
    public int EntryCount { get; set; }

    [JsonPropertyName("reference_entry_count")]
    public int ReferenceEntryCount { get; set; }

    [JsonPropertyName("cached_bytes")]
    public long CachedBytes { get; set; }

    [JsonPropertyName("max_entry_bytes")]
    public long MaxEntryBytes { get; set; }

    [JsonPropertyName("cache_budget_bytes")]
    public long CacheBudgetBytes { get; set; }

    [JsonPropertyName("entry_limit_bytes")]
    public long EntryLimitBytes { get; set; }

    [JsonPropertyName("eviction_count")]
    public long EvictionCount { get; set; }

    [JsonPropertyName("rejected_entry_count")]
    public long RejectedEntryCount { get; set; }

    [JsonPropertyName("producers")]
    public ValidationTraceDeterministicProducerMetrics[] Producers { get; set; } =
        Array.Empty<ValidationTraceDeterministicProducerMetrics>();
}

public sealed class ValidationTraceDeterministicProducerMetrics
{
    [JsonPropertyName("producer_id")]
    public string ProducerId { get; set; } = string.Empty;

    [JsonPropertyName("build_count")]
    public long BuildCount { get; set; }

    [JsonPropertyName("cache_hit_count")]
    public long CacheHitCount { get; set; }

    [JsonPropertyName("cached_bytes")]
    public long CachedBytes { get; set; }

    [JsonPropertyName("max_entry_bytes")]
    public long MaxEntryBytes { get; set; }

    [JsonPropertyName("reference_build_count")]
    public long ReferenceBuildCount { get; set; }

    [JsonPropertyName("reference_hit_count")]
    public long ReferenceHitCount { get; set; }

    [JsonPropertyName("eviction_count")]
    public long EvictionCount { get; set; }

    [JsonPropertyName("rejected_entry_count")]
    public long RejectedEntryCount { get; set; }

    [JsonPropertyName("wall_saved_estimate_ms")]
    public double WallSavedEstimateMilliseconds { get; set; }
}
