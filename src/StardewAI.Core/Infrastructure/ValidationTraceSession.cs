using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private static readonly Func<bool, long>? TotalAllocatedBytesCounter =
        CreateTotalAllocatedBytesCounter();
    private static readonly Func<long>? CurrentThreadAllocatedBytesCounter =
        CreateCurrentThreadAllocatedBytesCounter();

    private readonly string outputPath;
    private readonly string command;
    private readonly string producerId;
    private readonly string producerVersion;
    private readonly string scenario;
    private readonly ValidationTraceInput[] inputs;
    private readonly ValidationTraceSession? previous;
    private readonly Stopwatch wallClock = Stopwatch.StartNew();
    private readonly TimeSpan cpuStarted;
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private readonly double processStartMilliseconds;
    private readonly long? allocatedBytesStarted;
    private readonly int gen0CollectionsStarted;
    private readonly int gen1CollectionsStarted;
    private readonly int gen2CollectionsStarted;
    private readonly ValidationTraceOperationAggregator operationAggregator =
        new();
    private readonly ValidationTraceDependencyGraphRecorder dependencyRecorder =
        new();
    private int nextOperationId;
    private int completed;
    private ValidationTraceReadSessionMetrics readSessionMetrics = new();
    private ValidationTraceDeterministicBuildSessionMetrics
        deterministicBuildSessionMetrics = new();

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
        cpuStarted = Process.GetCurrentProcess().TotalProcessorTime;
        allocatedBytesStarted = TryGetTotalAllocatedBytes();
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

    public void SetInvocationReadSessionMetrics(
        InvocationReadSessionSnapshot snapshot)
    {
        if (Volatile.Read(ref completed) != 0)
            return;
        readSessionMetrics = new ValidationTraceReadSessionMetrics
        {
            ReaderVersion = snapshot.ReaderVersion,
            Enabled = snapshot.Enabled,
            RegisteredPathCount = snapshot.RegisteredPathCount,
            EntryCount = snapshot.EntryCount,
            PhysicalReadCount = snapshot.PhysicalReadCount,
            PhysicalBytesRead = snapshot.PhysicalBytesRead,
            FullHashCount = snapshot.FullHashCount,
            HashBytes = snapshot.HashBytes,
            MaterializationCount = snapshot.MaterializationCount,
            CacheHitCount = snapshot.CacheHitCount,
            RetainedRawBytes = snapshot.RetainedRawBytes,
            RetainedStoredTextBytes = snapshot.RetainedStoredTextBytes,
            RetainedMaterializedTextBytes =
                snapshot.RetainedMaterializedTextBytes,
            RetainedHashTextBytes = snapshot.RetainedHashTextBytes,
            RetainedBytes = snapshot.RetainedBytes,
            MaxEntryRetainedBytes = snapshot.MaxEntryRetainedBytes,
            RetentionBudgetBytes = snapshot.RetentionBudgetBytes,
            EntryRetentionLimitBytes = snapshot.EntryRetentionLimitBytes,
            DynamicRegistrationCount = snapshot.DynamicRegistrationCount,
            EvictionCount = snapshot.EvictionCount,
            LargeEntryBypassCount = snapshot.LargeEntryBypassCount,
            ProbationBypassCount = snapshot.ProbationBypassCount,
            ReleasedRawBytes = snapshot.ReleasedRawBytes,
            ReleasedStoredTextBytes = snapshot.ReleasedStoredTextBytes,
            LargestEntries = snapshot.LargestEntries.Select(entry =>
                new ValidationTraceReadSessionEntryMetrics
                {
                    Path = entry.Path,
                    RetainedBytes = entry.RetainedBytes,
                    AccessCount = entry.AccessCount
                }).ToArray()
        };
    }

    public void SetDeterministicBuildSessionMetrics(
        ValidationTraceDeterministicBuildSessionMetrics metrics)
    {
        if (Volatile.Read(ref completed) == 0)
            deterministicBuildSessionMetrics = metrics;
    }

    public static ValidationTraceOperation BeginOperation(
        string operationKind,
        string producerId,
        string producerVersion,
        params string[] inputPaths)
    {
        var session = Ambient.Value;
        if (session is null)
            return ValidationTraceOperation.Disabled;

        try
        {
            var sequence = Interlocked.Increment(ref session.nextOperationId);
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
                sequence % OperationCpuSamplingRate == 1);
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
        var allocatedBytesCompleted = TryGetTotalAllocatedBytes();
        var allocationCounterAvailable = allocatedBytesStarted.HasValue &&
            allocatedBytesCompleted.HasValue;
        var cpuMilliseconds = Math.Max(
            0,
            (process.TotalProcessorTime - cpuStarted).TotalMilliseconds);
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
            PrivateMemoryBytes = process.PrivateMemorySize64,
            ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            ManagedAllocationCounterAvailable = allocationCounterAvailable,
            AllocatedBytes = allocationCounterAvailable
                ? Math.Max(
                    0,
                    allocatedBytesCompleted!.Value -
                    allocatedBytesStarted!.Value)
                : 0,
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
            DependencyGraph = dependencyGraph,
            InvocationReadSession = readSessionMetrics,
            DeterministicBuildSession = deterministicBuildSessionMetrics
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

    internal static long? TryGetCurrentThreadAllocatedBytes() =>
        TryInvokeAllocationCounter(CurrentThreadAllocatedBytesCounter);

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

    private static long? TryGetTotalAllocatedBytes() =>
        TryInvokeAllocationCounter(TotalAllocatedBytesCounter, false);

    private static Func<bool, long>? CreateTotalAllocatedBytesCounter()
    {
        try
        {
            var method = typeof(GC).GetMethod(
                "GetTotalAllocatedBytes",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);
            return method is null
                ? null
                : (Func<bool, long>)method.CreateDelegate(
                    typeof(Func<bool, long>));
        }
        catch
        {
            return null;
        }
    }

    private static Func<long>? CreateCurrentThreadAllocatedBytesCounter()
    {
        try
        {
            var method = typeof(GC).GetMethod(
                "GetAllocatedBytesForCurrentThread",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            return method is null
                ? null
                : (Func<long>)method.CreateDelegate(typeof(Func<long>));
        }
        catch
        {
            return null;
        }
    }

    private static long? TryInvokeAllocationCounter(Func<long>? counter)
    {
        try
        {
            return counter?.Invoke();
        }
        catch
        {
            return null;
        }
    }

    private static long? TryInvokeAllocationCounter(
        Func<bool, long>? counter,
        bool precise)
    {
        try
        {
            return counter?.Invoke(precise);
        }
        catch
        {
            return null;
        }
    }

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

    internal static double Round(double value) => Math.Round(value, 3);
}
