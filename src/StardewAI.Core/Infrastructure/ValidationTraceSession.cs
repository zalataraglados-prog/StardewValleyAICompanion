using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace StardewAI.Core.Infrastructure;

public sealed class ValidationTraceSession : IDisposable
{
    public const string SchemaVersion = "stardewai.validation_trace.v2";
    public const int OperationCpuSamplingRate = 256;

    private static readonly AsyncLocal<ValidationTraceSession?> Ambient = new();
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly string outputPath;
    private readonly string command;
    private readonly string producerId;
    private readonly string producerVersion;
    private readonly string scenario;
    private readonly ValidationTraceInput[] inputs;
    private readonly ValidationTraceSession? previous;
    private readonly Stopwatch wallClock = Stopwatch.StartNew();
    private readonly TimeSpan cpuStarted;
    private readonly long allocatedBytesStarted;
    private readonly long privateMemoryStarted;
    private readonly int gen0CollectionsStarted;
    private readonly int gen1CollectionsStarted;
    private readonly int gen2CollectionsStarted;
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private readonly double processStartMilliseconds;
    private readonly ValidationTraceOperationAggregator operationAggregator =
        new();
    private readonly ValidationTraceDependencyGraphRecorder dependencyRecorder =
        new();
    private int nextOperationId;
    private int completed;
    private InvocationReadSessionSnapshot? readSessionSnapshot;
    private ValidationTraceDeterministicCacheMetrics?
        deterministicCacheMetrics;

    private ValidationTraceSession(
        string outputPath,
        string command,
        string producerId,
        string producerVersion,
        string scenario,
        IEnumerable<ValidationTraceInput> inputs,
        double processStartMilliseconds)
    {
        this.outputPath = Path.GetFullPath(outputPath);
        this.command = command;
        this.producerId = producerId;
        this.producerVersion = producerVersion;
        this.scenario = scenario;
        this.inputs = inputs.ToArray();
        this.processStartMilliseconds = processStartMilliseconds;
        var process = Process.GetCurrentProcess();
        cpuStarted = process.TotalProcessorTime;
        allocatedBytesStarted = GetTotalAllocatedBytes();
        privateMemoryStarted = SafePrivateMemorySize(process);
        gen0CollectionsStarted = GC.CollectionCount(0);
        gen1CollectionsStarted = GC.CollectionCount(1);
        gen2CollectionsStarted = GC.CollectionCount(2);
        previous = Ambient.Value;
        Ambient.Value = this;
    }

    public static ValidationTraceSession? TryStart(
        string? outputPath,
        string command,
        string producerId,
        string producerVersion,
        string? scenario,
        IEnumerable<ValidationTraceInput>? inputs,
        double processStartMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return null;

        try
        {
            return new ValidationTraceSession(
                outputPath,
                command,
                producerId,
                producerVersion,
                scenario?.Trim() ?? string.Empty,
                inputs ?? Array.Empty<ValidationTraceInput>(),
                processStartMilliseconds);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsActive => Ambient.Value is not null;

    public void CaptureReadSession(InvocationReadSessionSnapshot snapshot)
    {
        if (Volatile.Read(ref completed) == 0)
            readSessionSnapshot = snapshot;
    }

    public void CaptureDeterministicCache(
        ValidationTraceDeterministicCacheMetrics metrics)
    {
        if (Volatile.Read(ref completed) == 0)
            deterministicCacheMetrics = metrics;
    }

    public static ValidationTraceOperation BeginOperation(
        string operationKind,
        string producerId,
        string producerVersion,
        params string[] inputPaths)
    {
        var sequence = Ambient.Value is null
            ? 0
            : Interlocked.Increment(ref Ambient.Value.nextOperationId);
        var cpuSampleWeight = sequence > 0 &&
            sequence % OperationCpuSamplingRate == 1
                ? OperationCpuSamplingRate
                : 0;
        return BeginOperation(
            operationKind,
            producerId,
            producerVersion,
            cpuSampleWeight,
            sequence,
            inputPaths);
    }

    public static ValidationTraceOperation BeginMeasuredOperation(
        string operationKind,
        string producerId,
        string producerVersion,
        params string[] inputPaths)
    {
        var sequence = Ambient.Value is null
            ? 0
            : Interlocked.Increment(ref Ambient.Value.nextOperationId);
        return BeginOperation(
            operationKind,
            producerId,
            producerVersion,
            cpuSampleWeight: 1,
            sequence,
            inputPaths);
    }

    private static ValidationTraceOperation BeginOperation(
        string operationKind,
        string producerId,
        string producerVersion,
        int cpuSampleWeight,
        int sequence,
        params string[] inputPaths)
    {
        var session = Ambient.Value;
        if (session is null)
            return ValidationTraceOperation.Disabled;

        try
        {
            var operationId = string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:D6}",
                session.command,
                sequence);
            return new ValidationTraceOperation(
                session,
                operationId,
                operationKind,
                producerId,
                producerVersion,
                inputPaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(SafeFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                cpuSampleWeight);
        }
        catch
        {
            return ValidationTraceOperation.Disabled;
        }
    }

    public static ValidationTraceDependencyNodeScope BeginDependencyNode(
        string nodeId,
        string artifactKind,
        string producerId,
        string producerVersion,
        IEnumerable<ValidationTraceDependencyInput> inputs)
    {
        var session = Ambient.Value;
        if (session is null)
            return ValidationTraceDependencyNodeScope.Disabled;

        try
        {
            return session.dependencyRecorder.Begin(
                nodeId,
                artifactKind,
                producerId,
                producerVersion,
                inputs ?? Array.Empty<ValidationTraceDependencyInput>());
        }
        catch
        {
            return ValidationTraceDependencyNodeScope.Disabled;
        }
    }

    public void Complete(int exitCode)
    {
        if (Interlocked.Exchange(ref completed, 1) != 0)
            return;

        wallClock.Stop();
        var completedAt = DateTimeOffset.UtcNow;
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var cpuMilliseconds = Math.Max(
            0,
            (process.TotalProcessorTime - cpuStarted).TotalMilliseconds);
        var privateMemoryBytes = SafePrivateMemorySize(process);
        var totalAllocatedBytes = GetTotalAllocatedBytes();
        var operationSnapshot = operationAggregator.Snapshot();
        var dependencyGraph = SafeDependencySnapshot();
        dependencyGraph.FullRebuildExecuted =
            exitCode == 0 && dependencyGraph.Complete;

        var deterministicMilliseconds = IsDeterministicCommand(command)
            ? wallClock.Elapsed.TotalMilliseconds
            : 0;
        var evidenceIndexMilliseconds = string.Equals(
            command,
            "build-full-shipment-runtime-sample-evidence-index",
            StringComparison.Ordinal)
                ? wallClock.Elapsed.TotalMilliseconds
                : 0;
        var report = new ValidationTraceReport
        {
            SchemaVersion = SchemaVersion,
            Status = "complete",
            TraceId = Guid.NewGuid().ToString("N"),
            Command = command,
            Scenario = scenario,
            ProducerId = producerId,
            ProducerVersion = producerVersion,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ExitCode = exitCode,
            CacheHit = operationSnapshot.Any(value => value.CacheHit),
            Inputs = inputs,
            WallMilliseconds = Round(wallClock.Elapsed.TotalMilliseconds),
            CpuMilliseconds = Round(cpuMilliseconds),
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            WorkingSetBytes = SafeWorkingSetSize(process),
            PrivateMemoryBytes = privateMemoryBytes,
            PrivateMemoryDeltaBytes = Math.Max(
                0,
                privateMemoryBytes - privateMemoryStarted),
            AllocatedBytes = totalAllocatedBytes <= 0 ||
                allocatedBytesStarted <= 0
                    ? 0
                    : Math.Max(0, totalAllocatedBytes - allocatedBytesStarted),
            Gen0Collections = Math.Max(
                0,
                GC.CollectionCount(0) - gen0CollectionsStarted),
            Gen1Collections = Math.Max(
                0,
                GC.CollectionCount(1) - gen1CollectionsStarted),
            Gen2Collections = Math.Max(
                0,
                GC.CollectionCount(2) - gen2CollectionsStarted),
            BytesRead = operationSnapshot.Sum(value => value.BytesRead),
            HashBytes = operationSnapshot.Sum(value => value.HashBytes),
            JsonParseMilliseconds = Round(operationSnapshot.Sum(
                value => value.JsonParseMilliseconds)),
            MaterializeMilliseconds = Round(operationSnapshot.Sum(
                value => value.MaterializeMilliseconds)),
            ProcessStartMilliseconds = Round(processStartMilliseconds),
            BuildMilliseconds = 0,
            DeterministicRebuildMilliseconds = Round(
                deterministicMilliseconds),
            EvidenceIndexMilliseconds = Round(evidenceIndexMilliseconds),
            NativeRuntimeMilliseconds = 0,
            Operations = operationSnapshot,
            OperationInvocationCount = operationSnapshot.Sum(
                value => value.InvocationCount),
            OperationGroupCount = operationSnapshot.Length,
            OperationCpuSamplingRate = OperationCpuSamplingRate,
            ReadSession = readSessionSnapshot,
            DeterministicCache = deterministicCacheMetrics,
            DependencyGraph = dependencyGraph
        };

        TryWrite(report);
    }

    public void Dispose()
    {
        Complete(Environment.ExitCode);
        if (ReferenceEquals(Ambient.Value, this))
            Ambient.Value = previous;
    }

    internal void Record(ValidationTraceOperationRecord operation)
        => operationAggregator.Record(operation);

    private ValidationTraceDependencyGraph SafeDependencySnapshot()
    {
        try
        {
            return dependencyRecorder.Snapshot();
        }
        catch
        {
            return new ValidationTraceDependencyGraph
            {
                Complete = false
            };
        }
    }

    private void TryWrite(ValidationTraceReport report)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
                return;
            Directory.CreateDirectory(directory);
            var temporary = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(
                    temporary,
                    JsonSerializer.Serialize(report) + Environment.NewLine,
                    Utf8WithoutBom);
                if (File.Exists(outputPath))
                    File.Replace(temporary, outputPath, null);
                else
                    File.Move(temporary, outputPath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        catch
        {
            // Tracing is diagnostic-only and must never fail the command.
        }
    }

    private static bool IsDeterministicCommand(string command) =>
        command.StartsWith("build-", StringComparison.Ordinal) ||
        command.StartsWith("compile-", StringComparison.Ordinal) ||
        command.StartsWith("audit-", StringComparison.Ordinal) ||
        command.StartsWith("validate-", StringComparison.Ordinal) ||
        command.StartsWith("score-", StringComparison.Ordinal) ||
        command.StartsWith("select-", StringComparison.Ordinal) ||
        command.StartsWith("self-test", StringComparison.Ordinal);

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static long GetTotalAllocatedBytes()
    {
        try
        {
            var method = typeof(GC).GetMethod(
                "GetTotalAllocatedBytes",
                new[] { typeof(bool) });
            return method?.Invoke(null, new object[] { false }) is long value
                ? value
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static long SafePrivateMemorySize(Process process)
    {
        try
        {
            return Math.Max(0, process.PrivateMemorySize64);
        }
        catch
        {
            return 0;
        }
    }

    private static long SafeWorkingSetSize(Process process)
    {
        try
        {
            return Math.Max(0, process.WorkingSet64);
        }
        catch
        {
            return 0;
        }
    }

    internal static double Round(double value) => Math.Round(value, 3);
}
