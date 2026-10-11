using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace StardewAI.Core.Infrastructure;

internal sealed class ValidationTraceDependencyGraphRecorder
{
    private readonly object sync = new();
    private readonly AsyncLocal<ValidationTraceDependencyNodeScope?> current =
        new();
    private readonly Dictionary<string, ValidationTraceDependencyNode> nodes =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> topLevelNodeIds =
        new(StringComparer.Ordinal);

    public ValidationTraceDependencyNodeScope Begin(
        string nodeId,
        string artifactKind,
        string producerId,
        string producerVersion,
        IEnumerable<ValidationTraceDependencyInput> inputs)
    {
        var parent = current.Value;
        var scope = new ValidationTraceDependencyNodeScope(
            this,
            parent,
            nodeId,
            artifactKind,
            producerId,
            producerVersion,
            inputs);
        current.Value = scope;
        return scope;
    }

    public void Record(
        ValidationTraceDependencyNodeScope scope,
        ValidationTraceDependencyNode node)
    {
        try
        {
            lock (sync)
            {
                if (nodes.TryGetValue(node.NodeId, out var existing))
                    Merge(existing, node);
                else
                    nodes.Add(node.NodeId, node);

                if (scope.Parent is null)
                    topLevelNodeIds.Add(node.NodeId);
            }

            scope.Parent?.AddNestedDependency(node);
        }
        finally
        {
            current.Value = scope.Parent;
        }
    }

    public ValidationTraceDependencyGraph Snapshot()
    {
        lock (sync)
        {
            var snapshot = nodes.Values
                .OrderBy(value => value.NodeId, StringComparer.Ordinal)
                .ToArray();
            return new ValidationTraceDependencyGraph
            {
                Acyclic = IsAcyclic(snapshot),
                Complete = snapshot.Length > 0 && snapshot.All(node =>
                    node.Completed && !node.IdentityConflict),
                TopLevelNodeIds = topLevelNodeIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                Nodes = snapshot,
                TheoreticalInvalidationSets = BuildInvalidationSets(snapshot)
            };
        }
    }

    private static void Merge(
        ValidationTraceDependencyNode target,
        ValidationTraceDependencyNode source)
    {
        target.InvocationCount += source.InvocationCount;
        target.CacheHit |= source.CacheHit;
        target.Completed &= source.Completed;
        var metadataConflict =
            !string.Equals(target.ArtifactId, source.ArtifactId,
                StringComparison.Ordinal) ||
            !string.Equals(target.ArtifactKind, source.ArtifactKind,
                StringComparison.Ordinal) ||
            !string.Equals(target.ProducerId, source.ProducerId,
                StringComparison.Ordinal) ||
            !string.Equals(target.ProducerVersion, source.ProducerVersion,
                StringComparison.Ordinal);
        var contentConflict =
            !string.IsNullOrWhiteSpace(target.ContentSha256) &&
            !string.IsNullOrWhiteSpace(source.ContentSha256) &&
            !string.Equals(target.ContentSha256, source.ContentSha256,
                StringComparison.OrdinalIgnoreCase);
        target.IdentityConflict |= source.IdentityConflict ||
            metadataConflict || contentConflict;
        if (target.IdentityConflict)
        {
            target.Completed = false;
            target.InvalidationReasons = target.InvalidationReasons
                .Concat(source.InvalidationReasons)
                .Concat(metadataConflict
                    ? new[] { "same_node_id_metadata_changed" }
                    : Array.Empty<string>())
                .Concat(contentConflict
                    ? new[]
                    {
                        "same_node_identity_output_content_changed"
                    }
                    : Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
        target.WallMilliseconds = ValidationTraceSession.Round(
            target.WallMilliseconds + source.WallMilliseconds);
        target.CpuMilliseconds = ValidationTraceSession.Round(
            target.CpuMilliseconds + source.CpuMilliseconds);
        target.Inputs = target.Inputs
            .Concat(source.Inputs)
            .GroupBy(InputKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.ArtifactId, StringComparer.Ordinal)
            .ThenBy(value => value.Name, StringComparer.Ordinal)
            .ToArray();
        target.InputArtifactIds = target.Inputs
            .Select(value => value.ArtifactId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (string.IsNullOrWhiteSpace(target.ContentSha256))
            target.ContentSha256 = source.ContentSha256;
    }

    private static string InputKey(ValidationTraceDependencyInput input) =>
        input.ArtifactId + "\n" + input.Name + "\n" + input.Path;

    private static bool IsAcyclic(
        IReadOnlyCollection<ValidationTraceDependencyNode> nodes)
    {
        var outputToNode = nodes
            .GroupBy(node => node.ArtifactId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().NodeId,
                StringComparer.Ordinal);
        var adjacency = nodes.ToDictionary(
            node => node.NodeId,
            _ => new List<string>(),
            StringComparer.Ordinal);
        var indegree = nodes.ToDictionary(
            node => node.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            foreach (var input in node.InputArtifactIds)
            {
                if (!outputToNode.TryGetValue(input, out var ancestor) ||
                    string.Equals(ancestor, node.NodeId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                adjacency[ancestor].Add(node.NodeId);
                indegree[node.NodeId]++;
            }
        }

        var pending = new Queue<string>(indegree
            .Where(pair => pair.Value == 0)
            .Select(pair => pair.Key));
        var visited = 0;
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            visited++;
            foreach (var descendant in adjacency[current])
            {
                indegree[descendant]--;
                if (indegree[descendant] == 0)
                    pending.Enqueue(descendant);
            }
        }
        return visited == nodes.Count;
    }

    private static ValidationTraceInvalidationSet[] BuildInvalidationSets(
        IReadOnlyCollection<ValidationTraceDependencyNode> nodes)
    {
        var outputIds = nodes
            .Select(node => node.ArtifactId)
            .ToHashSet(StringComparer.Ordinal);
        var externalInputs = nodes
            .SelectMany(node => node.Inputs)
            .Where(input => !string.IsNullOrWhiteSpace(input.ArtifactId) &&
                !outputIds.Contains(input.ArtifactId))
            .GroupBy(input => input.ArtifactId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        var consumers = nodes
            .SelectMany(node => node.InputArtifactIds.Select(input =>
                (Input: input, Node: node)))
            .GroupBy(pair => pair.Input, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.Node)
                    .GroupBy(node => node.NodeId, StringComparer.Ordinal)
                    .Select(nodeGroup => nodeGroup.First())
                    .ToArray(),
                StringComparer.Ordinal);

        return externalInputs.Select(group =>
        {
            var invalidated = Descendants(group.Key, consumers);
            return new ValidationTraceInvalidationSet
            {
                ChangedInputArtifactId = group.Key,
                ChangedInputNames = group
                    .Select(input => input.Name)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                ChangedInputPaths = group
                    .Select(input => input.Path)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                InvalidatedNodeIds = invalidated
            };
        }).ToArray();
    }

    private static string[] Descendants(
        string inputArtifactId,
        IReadOnlyDictionary<string, ValidationTraceDependencyNode[]> consumers)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(inputArtifactId);
        while (pending.Count > 0)
        {
            var input = pending.Dequeue();
            if (!consumers.TryGetValue(input, out var direct))
                continue;
            foreach (var node in direct)
            {
                if (!result.Add(node.NodeId))
                    continue;
                pending.Enqueue(node.ArtifactId);
            }
        }
        return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
