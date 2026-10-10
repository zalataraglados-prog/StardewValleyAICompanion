using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static class ValidationTraceBaselineBuilder
{
    private static readonly string[] RepresentativeScenarios =
    {
        "single_low_risk_decision",
        "machine_supporting_transition_portfolio",
        "full_shipment_single_layer_proof",
        "anchor_high_risk_evidence_preflight",
        "teacher_dataset_generation_relabel"
    };

    public static ValidationTraceBaselineReport Build(string traceRoot)
    {
        var root = Path.GetFullPath(traceRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(
                "Validation trace root is missing: " + root);

        var files = Directory.EnumerateFiles(
                root,
                "*.validation-trace.json",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
            throw new InvalidDataException(
                "Validation trace root contains no *.validation-trace.json files.");

        var traces = files.Select(ReadTrace).ToArray();
        var scenarios = traces
            .Select(trace => trace.Scenario)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var missing = RepresentativeScenarios
            .Except(scenarios, StringComparer.Ordinal)
            .ToArray();

        return new ValidationTraceBaselineReport
        {
            Status = missing.Length == 0 ? "complete" : "partial",
            TraceRoot = PortableTraceRoot(root),
            TraceCount = traces.Length,
            CacheHitCount = traces.Count(trace => trace.CacheHit),
            OperationCpuSamplingRates = traces
                .Select(trace => trace.OperationCpuSamplingRate)
                .Distinct()
                .OrderBy(value => value)
                .ToArray(),
            CoveredScenarios = scenarios,
            MissingRepresentativeScenarios = missing,
            RepresentativeCoverageComplete = missing.Length == 0,
            TotalWallMilliseconds = Round(traces.Sum(trace => trace.WallMilliseconds)),
            TotalCpuMilliseconds = Round(traces.Sum(trace => trace.CpuMilliseconds)),
            TotalBytesRead = traces.Sum(trace => trace.BytesRead),
            TotalHashBytes = traces.Sum(trace => trace.HashBytes),
            TopCommands = traces
                .GroupBy(
                    trace => new { trace.Command, trace.ProducerVersion })
                .Select(group => new ValidationTraceCostSummary
                {
                    OperationKind = "command",
                    ProducerId = "StardewAI.GoalConditionedBootstrap",
                    ProducerVersion = group.Key.ProducerVersion,
                    Command = group.Key.Command,
                    InvocationCount = group.LongCount(),
                    CumulativeWallMilliseconds = Round(group.Sum(
                        value => value.WallMilliseconds)),
                    CumulativeCpuMilliseconds = Round(group.Sum(
                        value => value.CpuMilliseconds)),
                    BytesRead = group.Sum(value => value.BytesRead),
                    HashBytes = group.Sum(value => value.HashBytes),
                    JsonParseMilliseconds = Round(group.Sum(
                        value => value.JsonParseMilliseconds)),
                    MaterializeMilliseconds = Round(group.Sum(
                        value => value.MaterializeMilliseconds))
                })
                .OrderByDescending(value => value.CumulativeWallMilliseconds)
                .ThenBy(value => value.Command, StringComparer.Ordinal)
                .Take(10)
                .ToArray(),
            TopOperations = traces
                .SelectMany(trace => trace.Operations)
                .GroupBy(
                    operation => new
                    {
                        operation.OperationKind,
                        operation.ProducerId,
                        operation.ProducerVersion
                    })
                .Select(group => new ValidationTraceCostSummary
                {
                    OperationKind = group.Key.OperationKind,
                    ProducerId = group.Key.ProducerId,
                    ProducerVersion = group.Key.ProducerVersion,
                    InvocationCount = group.Sum(value =>
                        value.InvocationCount),
                    CumulativeWallMilliseconds = Round(group.Sum(
                        value => value.WallMilliseconds)),
                    CumulativeCpuMilliseconds = Round(group.Sum(
                        value => value.CpuMilliseconds)),
                    BytesRead = group.Sum(value => value.BytesRead),
                    HashBytes = group.Sum(value => value.HashBytes),
                    JsonParseMilliseconds = Round(group.Sum(
                        value => value.JsonParseMilliseconds)),
                    MaterializeMilliseconds = Round(group.Sum(
                        value => value.MaterializeMilliseconds))
                })
                .OrderByDescending(value => value.CumulativeWallMilliseconds)
                .ThenBy(value => value.OperationKind, StringComparer.Ordinal)
                .ThenBy(value => value.ProducerId, StringComparer.Ordinal)
                .Take(10)
                .ToArray()
        };
    }

    private static ValidationTraceReport ReadTrace(string path)
    {
        var trace = JsonSerializer.Deserialize<ValidationTraceReport>(
            File.ReadAllText(path)) ?? throw new InvalidDataException(
                "Validation trace is null: " + path);
        if (!string.Equals(
                trace.SchemaVersion,
                ValidationTraceSession.SchemaVersion,
                StringComparison.Ordinal) ||
            !string.Equals(trace.Status, "complete", StringComparison.Ordinal) ||
            trace.ExitCode != 0 ||
            string.IsNullOrWhiteSpace(trace.Command) ||
            trace.CacheHit ||
            trace.Operations.Any(operation => operation.CacheHit))
        {
            throw new InvalidDataException(
                "Validation trace is incomplete or enables a Phase 0 cache: " + path);
        }
        return trace;
    }

    private static double Round(double value) => Math.Round(value, 3);

    private static string PortableTraceRoot(string root)
    {
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, root);
        return relative.StartsWith("..", StringComparison.Ordinal)
            ? root
            : relative.Replace('\\', '/');
    }

}

internal sealed class ValidationTraceBaselineReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "stardewai.validation_trace_baseline.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("trace_root")]
    public string TraceRoot { get; set; } = string.Empty;

    [JsonPropertyName("trace_count")]
    public int TraceCount { get; set; }

    [JsonPropertyName("cache_hit_count")]
    public int CacheHitCount { get; set; }

    [JsonPropertyName("operation_cpu_sampling_rates")]
    public int[] OperationCpuSamplingRates { get; set; } = Array.Empty<int>();

    [JsonPropertyName("covered_scenarios")]
    public string[] CoveredScenarios { get; set; } = Array.Empty<string>();

    [JsonPropertyName("missing_representative_scenarios")]
    public string[] MissingRepresentativeScenarios { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("representative_coverage_complete")]
    public bool RepresentativeCoverageComplete { get; set; }

    [JsonPropertyName("total_wall_ms")]
    public double TotalWallMilliseconds { get; set; }

    [JsonPropertyName("total_cpu_ms")]
    public double TotalCpuMilliseconds { get; set; }

    [JsonPropertyName("total_bytes_read")]
    public long TotalBytesRead { get; set; }

    [JsonPropertyName("total_hash_bytes")]
    public long TotalHashBytes { get; set; }

    [JsonPropertyName("top_commands")]
    public ValidationTraceCostSummary[] TopCommands { get; set; } =
        Array.Empty<ValidationTraceCostSummary>();

    [JsonPropertyName("top_operations")]
    public ValidationTraceCostSummary[] TopOperations { get; set; } =
        Array.Empty<ValidationTraceCostSummary>();
}

internal sealed class ValidationTraceCostSummary
{
    [JsonPropertyName("operation_kind")]
    public string OperationKind { get; set; } = string.Empty;

    [JsonPropertyName("producer_id")]
    public string ProducerId { get; set; } = string.Empty;

    [JsonPropertyName("producer_version")]
    public string ProducerVersion { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("invocation_count")]
    public long InvocationCount { get; set; }

    [JsonPropertyName("cumulative_wall_ms")]
    public double CumulativeWallMilliseconds { get; set; }

    [JsonPropertyName("cumulative_cpu_ms")]
    public double CumulativeCpuMilliseconds { get; set; }

    [JsonPropertyName("bytes_read")]
    public long BytesRead { get; set; }

    [JsonPropertyName("hash_bytes")]
    public long HashBytes { get; set; }

    [JsonPropertyName("json_parse_ms")]
    public double JsonParseMilliseconds { get; set; }

    [JsonPropertyName("materialize_ms")]
    public double MaterializeMilliseconds { get; set; }
}
