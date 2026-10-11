using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace StardewAI.Core.Infrastructure;

public sealed class ValidationTraceDependencyNodeScope : IDisposable
{
    internal static readonly ValidationTraceDependencyNodeScope Disabled =
        new();

    private readonly ValidationTraceDependencyGraphRecorder? recorder;
    private readonly string nodeId = string.Empty;
    private readonly string artifactKind = string.Empty;
    private readonly string producerId = string.Empty;
    private readonly string producerVersion = string.Empty;
    private readonly List<ValidationTraceDependencyInput> inputs = new();
    private readonly long wallStarted;
    private readonly TimeSpan cpuStarted;
    private string artifactId = string.Empty;
    private string contentSha256 = string.Empty;
    private bool cacheHit;
    private bool completedSuccessfully;
    private int disposed;

    private ValidationTraceDependencyNodeScope()
    {
    }

    internal ValidationTraceDependencyNodeScope(
        ValidationTraceDependencyGraphRecorder recorder,
        ValidationTraceDependencyNodeScope? parent,
        string nodeId,
        string artifactKind,
        string producerId,
        string producerVersion,
        IEnumerable<ValidationTraceDependencyInput> inputs)
    {
        this.recorder = recorder;
        Parent = parent;
        this.nodeId = nodeId;
        this.artifactKind = artifactKind;
        this.producerId = producerId;
        this.producerVersion = producerVersion;
        this.inputs.AddRange(inputs);
        artifactId = nodeId;
        wallStarted = Stopwatch.GetTimestamp();
        cpuStarted = Process.GetCurrentProcess().TotalProcessorTime;
    }

    internal ValidationTraceDependencyNodeScope? Parent { get; }

    public bool IsEnabled => recorder is not null;

    public void SetOutputArtifact(
        string outputArtifactId,
        string? outputContentSha256)
    {
        if (recorder is null)
            return;
        if (!string.IsNullOrWhiteSpace(outputArtifactId))
            artifactId = outputArtifactId;
        contentSha256 = outputContentSha256 ?? string.Empty;
        completedSuccessfully = !string.IsNullOrWhiteSpace(contentSha256);
    }

    public void MarkCacheHit()
    {
        if (recorder is not null)
            cacheHit = true;
    }

    internal void AddNestedDependency(ValidationTraceDependencyNode node)
    {
        if (recorder is null)
            return;
        inputs.Add(new ValidationTraceDependencyInput
        {
            Name = "nested_dependency",
            ArtifactId = node.ArtifactId,
            ArtifactKind = node.ArtifactKind,
            ContentSha256 = node.ContentSha256,
            ReuseScope = node.ReuseScope
        });
    }

    public void Dispose()
    {
        if (recorder is null ||
            Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            var process = Process.GetCurrentProcess();
            var node = new ValidationTraceDependencyNode
            {
                NodeId = nodeId,
                ArtifactId = artifactId,
                ArtifactKind = artifactKind,
                ContentSha256 = contentSha256,
                ProducerId = producerId,
                ProducerVersion = producerVersion,
                Inputs = inputs
                    .GroupBy(
                        input => input.ArtifactId + "\n" + input.Name +
                            "\n" + input.Path,
                        StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(input => input.ArtifactId, StringComparer.Ordinal)
                    .ThenBy(input => input.Name, StringComparer.Ordinal)
                    .ToArray(),
                CacheHit = cacheHit,
                Completed = completedSuccessfully,
                WallMilliseconds = ValidationTraceSession.Round(Math.Max(
                    0,
                    (Stopwatch.GetTimestamp() - wallStarted) * 1000d /
                    Stopwatch.Frequency)),
                CpuMilliseconds = ValidationTraceSession.Round(Math.Max(
                    0,
                    (process.TotalProcessorTime - cpuStarted)
                        .TotalMilliseconds))
            };
            node.InputArtifactIds = node.Inputs
                .Select(input => input.ArtifactId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            recorder.Record(this, node);
        }
        catch
        {
            // Dependency tracing is diagnostic-only.
        }
    }
}
