using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Infrastructure;

internal sealed class ValidationTraceOperationAggregator
{
    private const int MaximumArtifactIdsPerOperationGroup = 32;

    private readonly object sync = new();
    private readonly Dictionary<OperationAggregationKey,
        ValidationTraceOperationRecord> operations = new();

    public void Record(ValidationTraceOperationRecord operation)
    {
        try
        {
            lock (sync)
            {
                var key = new OperationAggregationKey(operation);
                if (!operations.TryGetValue(key, out var existing))
                {
                    BoundArtifactIds(operation);
                    operations.Add(key, operation);
                    return;
                }

                Merge(existing, operation);
            }
        }
        catch
        {
            // Tracing must not change command semantics.
        }
    }

    public ValidationTraceOperationRecord[] Snapshot()
    {
        try
        {
            lock (sync)
            {
                return operations.Values
                    .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                    .ToArray();
            }
        }
        catch
        {
            return Array.Empty<ValidationTraceOperationRecord>();
        }
    }

    private static void BoundArtifactIds(
        ValidationTraceOperationRecord operation)
    {
        if (operation.ArtifactIds.Length <= MaximumArtifactIdsPerOperationGroup)
            return;

        operation.ArtifactIds = operation.ArtifactIds
            .Take(MaximumArtifactIdsPerOperationGroup)
            .ToArray();
        operation.ArtifactIdsTruncated = true;
    }

    private static void Merge(
        ValidationTraceOperationRecord target,
        ValidationTraceOperationRecord source)
    {
        target.InvocationCount += source.InvocationCount;
        target.LastOperationId = source.LastOperationId;
        target.WallMilliseconds = ValidationTraceSession.Round(
            target.WallMilliseconds + source.WallMilliseconds);
        target.CpuMilliseconds = ValidationTraceSession.Round(
            target.CpuMilliseconds + source.CpuMilliseconds);
        target.CpuSampleCount += source.CpuSampleCount;
        target.BytesRead += source.BytesRead;
        target.HashBytes += source.HashBytes;
        target.AllocatedBytes += source.AllocatedBytes;
        target.JsonParseMilliseconds = ValidationTraceSession.Round(
            target.JsonParseMilliseconds + source.JsonParseMilliseconds);
        target.MaterializeMilliseconds = ValidationTraceSession.Round(
            target.MaterializeMilliseconds + source.MaterializeMilliseconds);
        target.ProcessStartMilliseconds = ValidationTraceSession.Round(
            target.ProcessStartMilliseconds + source.ProcessStartMilliseconds);
        target.BuildMilliseconds = ValidationTraceSession.Round(
            target.BuildMilliseconds + source.BuildMilliseconds);
        target.DeterministicRebuildMilliseconds = ValidationTraceSession.Round(
            target.DeterministicRebuildMilliseconds +
            source.DeterministicRebuildMilliseconds);
        target.EvidenceIndexMilliseconds = ValidationTraceSession.Round(
            target.EvidenceIndexMilliseconds + source.EvidenceIndexMilliseconds);
        target.NativeRuntimeMilliseconds = ValidationTraceSession.Round(
            target.NativeRuntimeMilliseconds + source.NativeRuntimeMilliseconds);
        target.CacheHit |= source.CacheHit;
        MergeArtifactIds(target, source);
    }

    private static void MergeArtifactIds(
        ValidationTraceOperationRecord target,
        ValidationTraceOperationRecord source)
    {
        if (target.ArtifactIdsTruncated)
            return;

        var artifactIds = target.ArtifactIds.ToList();
        foreach (var artifactId in source.ArtifactIds)
        {
            if (artifactIds.Contains(artifactId, StringComparer.Ordinal))
                continue;
            if (artifactIds.Count == MaximumArtifactIdsPerOperationGroup)
            {
                target.ArtifactIdsTruncated = true;
                break;
            }
            artifactIds.Add(artifactId);
        }
        target.ArtifactIdsTruncated |= source.ArtifactIdsTruncated;
        if (artifactIds.Count == target.ArtifactIds.Length)
            return;

        artifactIds.Sort(StringComparer.Ordinal);
        target.ArtifactIds = artifactIds.ToArray();
    }

    private readonly struct OperationAggregationKey :
        IEquatable<OperationAggregationKey>
    {
        private readonly string operationKind;
        private readonly string producerId;
        private readonly string producerVersion;
        private readonly string[] inputPaths;

        public OperationAggregationKey(ValidationTraceOperationRecord operation)
        {
            operationKind = operation.OperationKind;
            producerId = operation.ProducerId;
            producerVersion = operation.ProducerVersion;
            inputPaths = operation.InputPaths;
        }

        public bool Equals(OperationAggregationKey other) =>
            string.Equals(operationKind, other.operationKind,
                StringComparison.Ordinal) &&
            string.Equals(producerId, other.producerId,
                StringComparison.Ordinal) &&
            string.Equals(producerVersion, other.producerVersion,
                StringComparison.Ordinal) &&
            inputPaths.SequenceEqual(
                other.inputPaths,
                StringComparer.OrdinalIgnoreCase);

        public override bool Equals(object? value) =>
            value is OperationAggregationKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(operationKind);
                hash = (hash * 397) ^
                    StringComparer.Ordinal.GetHashCode(producerId);
                hash = (hash * 397) ^
                    StringComparer.Ordinal.GetHashCode(producerVersion);
                foreach (var path in inputPaths)
                {
                    hash = (hash * 397) ^
                        StringComparer.OrdinalIgnoreCase.GetHashCode(path);
                }
                return hash;
            }
        }
    }
}
