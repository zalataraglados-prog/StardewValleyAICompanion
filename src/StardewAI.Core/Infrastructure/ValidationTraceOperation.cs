using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace StardewAI.Core.Infrastructure;

public sealed class ValidationTraceOperation : IDisposable
{
    internal static readonly ValidationTraceOperation Disabled = new();

    private readonly ValidationTraceSession? session;
    private readonly string operationId = string.Empty;
    private readonly string operationKind = string.Empty;
    private readonly string producerId = string.Empty;
    private readonly string producerVersion = string.Empty;
    private readonly string[] inputPaths = Array.Empty<string>();
    private readonly long wallStarted;
    private readonly TimeSpan? cpuStarted;
    private List<string>? artifactIds;
    private long bytesRead;
    private long hashBytes;
    private double jsonParseMilliseconds;
    private double materializeMilliseconds;
    private bool cacheHit;
    private int disposed;

    private ValidationTraceOperation()
    {
    }

    internal ValidationTraceOperation(
        ValidationTraceSession session,
        string operationId,
        string operationKind,
        string producerId,
        string producerVersion,
        string[] inputPaths,
        bool sampleCpu)
    {
        this.session = session;
        this.operationId = operationId;
        this.operationKind = operationKind;
        this.producerId = producerId;
        this.producerVersion = producerVersion;
        this.inputPaths = inputPaths;
        wallStarted = Stopwatch.GetTimestamp();
        if (sampleCpu)
            cpuStarted = Process.GetCurrentProcess().TotalProcessorTime;
    }

    public bool IsEnabled => session is not null;

    public void AddBytesRead(long bytes)
    {
        if (session is not null)
            bytesRead += Math.Max(0, bytes);
    }

    public void AddHashBytes(long bytes)
    {
        if (session is not null)
            hashBytes += Math.Max(0, bytes);
    }

    public void AddJsonParseMilliseconds(double milliseconds)
    {
        if (session is not null)
            jsonParseMilliseconds += Math.Max(0, milliseconds);
    }

    public void AddMaterializeMilliseconds(double milliseconds)
    {
        if (session is not null)
            materializeMilliseconds += Math.Max(0, milliseconds);
    }

    public void AddArtifactId(string? artifactId)
    {
        if (session is null || string.IsNullOrWhiteSpace(artifactId))
            return;

        artifactIds ??= new List<string>();
        if (!artifactIds.Contains(artifactId, StringComparer.Ordinal))
            artifactIds.Add(artifactId);
    }

    public void MarkCacheHit()
    {
        if (session is not null)
            cacheHit = true;
    }

    public void Dispose()
    {
        if (session is null ||
            Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            var elapsedMilliseconds = Math.Max(
                0,
                (Stopwatch.GetTimestamp() - wallStarted) * 1000d /
                Stopwatch.Frequency);
            var cpuMilliseconds = cpuStarted.HasValue
                ? Math.Max(
                    0,
                    (Process.GetCurrentProcess().TotalProcessorTime -
                        cpuStarted.Value).TotalMilliseconds) *
                    ValidationTraceSession.OperationCpuSamplingRate
                : 0;
            session.Record(new ValidationTraceOperationRecord
            {
                InvocationCount = 1,
                OperationId = operationId,
                LastOperationId = operationId,
                OperationKind = operationKind,
                ProducerId = producerId,
                ProducerVersion = producerVersion,
                InputPaths = inputPaths,
                ArtifactIds = artifactIds is null
                    ? Array.Empty<string>()
                    : artifactIds.OrderBy(
                        value => value,
                        StringComparer.Ordinal).ToArray(),
                WallMilliseconds = ValidationTraceSession.Round(
                    elapsedMilliseconds),
                CpuMilliseconds = ValidationTraceSession.Round(cpuMilliseconds),
                CpuSampleCount = cpuStarted.HasValue ? 1 : 0,
                BytesRead = bytesRead,
                HashBytes = hashBytes,
                JsonParseMilliseconds = ValidationTraceSession.Round(
                    jsonParseMilliseconds),
                MaterializeMilliseconds = ValidationTraceSession.Round(
                    materializeMilliseconds),
                CacheHit = cacheHit
            });
        }
        catch
        {
            // Tracing must not change command semantics.
        }
    }
}
