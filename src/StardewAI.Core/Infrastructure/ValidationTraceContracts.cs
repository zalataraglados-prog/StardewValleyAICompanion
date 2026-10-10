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
}
