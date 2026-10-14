using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class ValidationTraceSessionTests
{
    [Fact]
    public void ExplicitTraceCapturesReadsWithoutChangingArtifactResult()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var artifactPath = Path.Combine(root, "artifact.json");
        var tracePath = Path.Combine(root, "trace.json");
        const string artifact = "{\"schema_version\":\"sample.v1\",\"value\":7}";
        File.WriteAllText(artifactPath, artifact);

        using (var session = ValidationTraceSession.TryStart(
            tracePath,
            "sample-command",
            "sample-producer",
            "v1",
            "single_low_risk_decision",
            new[]
            {
                new ValidationTraceInput
                {
                    ArgumentName = "artifact",
                    Path = artifactPath
                }
            },
            3.5))
        {
            Assert.NotNull(session);
            Assert.Equal(
                artifact,
                ContentAddressedJsonArtifactReader.ReadAllText(artifactPath));
            Assert.Equal(
                artifact,
                ContentAddressedJsonArtifactReader.ReadAllText(artifactPath));
            session!.Complete(0);
        }

        using var trace = JsonDocument.Parse(File.ReadAllText(tracePath));
        var rootElement = trace.RootElement;
        Assert.Equal(
            ValidationTraceSession.SchemaVersion,
            rootElement.GetProperty("schema_version").GetString());
        Assert.Equal("sample-command", rootElement.GetProperty("command").GetString());
        Assert.False(rootElement.GetProperty("cache_hit").GetBoolean());
        Assert.True(rootElement.GetProperty("bytes_read").GetInt64() > 0);
        Assert.True(rootElement.GetProperty("process_start_ms").GetDouble() >= 3.5);
        Assert.Equal(
            2,
            rootElement.GetProperty("operation_invocation_count").GetInt64());
        Assert.Equal(
            1,
            rootElement.GetProperty("operation_group_count").GetInt32());
        Assert.Equal(
            ValidationTraceSession.OperationCpuSamplingRate,
            rootElement.GetProperty("operation_cpu_sampling_rate").GetInt32());
        var operation = Assert.Single(
            rootElement.GetProperty("operations").EnumerateArray());
        Assert.Equal(2, operation.GetProperty("invocation_count").GetInt64());
        Assert.Equal(
            "content_addressed_json_read",
            operation.GetProperty("operation_kind").GetString());
        Assert.False(operation.GetProperty("cache_hit").GetBoolean());
    }

    [Fact]
    public void TraceWriteFailureDoesNotChangeMeasuredOperation()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var result = 0;

        using (var session = ValidationTraceSession.TryStart(
            root,
            "sample-command",
            "sample-producer",
            "v1",
            null,
            Array.Empty<ValidationTraceInput>(),
            0))
        {
            Assert.NotNull(session);
            using (ValidationTraceSession.BeginOperation(
                "sample-operation",
                "sample-producer",
                "v1"))
            {
                result = 7;
            }
            session!.Complete(0);
        }

        Assert.Equal(7, result);
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void ExplicitMeasuredOperationUsesExactCpuSampleWeight()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var tracePath = Path.Combine(root, "trace.json");

        using (var session = ValidationTraceSession.TryStart(
            tracePath,
            "measured-command",
            "sample-producer",
            "v1",
            null,
            Array.Empty<ValidationTraceInput>(),
            0))
        {
            using (ValidationTraceSession.BeginMeasuredOperation(
                "proof-verification",
                "sample-producer",
                "v1"))
            {
                Thread.SpinWait(10_000);
            }
            session!.Complete(0);
        }

        using var trace = JsonDocument.Parse(File.ReadAllText(tracePath));
        var operation = Assert.Single(
            trace.RootElement.GetProperty("operations").EnumerateArray());
        Assert.Equal(1, operation.GetProperty("cpu_sample_count").GetInt64());
        Assert.True(operation.GetProperty("cpu_ms").GetDouble() >= 0);
    }

    [Fact]
    public void InvocationReadReuseIsVisibleInTrace()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var artifactPath = Path.Combine(root, "artifact.json");
        var tracePath = Path.Combine(root, "trace.json");
        const string artifact = "{\"schema_version\":\"sample.v1\",\"value\":8}";
        File.WriteAllText(artifactPath, artifact);

        using (InvocationReadSession.Start(new[] { artifactPath }))
        using (var traceSession = ValidationTraceSession.TryStart(
            tracePath,
            "sample-command",
            "sample-producer",
            "v1",
            "single_low_risk_decision",
            Array.Empty<ValidationTraceInput>(),
            0))
        {
            Assert.Equal(
                artifact,
                ContentAddressedJsonArtifactReader.ReadAllText(artifactPath));
            Assert.Equal(
                artifact,
                ContentAddressedJsonArtifactReader.ReadAllText(artifactPath));
            traceSession!.Complete(0);
        }

        using var trace = JsonDocument.Parse(File.ReadAllText(tracePath));
        var rootElement = trace.RootElement;
        Assert.True(rootElement.GetProperty("cache_hit").GetBoolean());
        Assert.Equal(
            new FileInfo(artifactPath).Length,
            rootElement.GetProperty("bytes_read").GetInt64());
        var operation = Assert.Single(
            rootElement.GetProperty("operations").EnumerateArray());
        Assert.True(operation.GetProperty("cache_hit").GetBoolean());
    }

    [Fact]
    public void DependencyGraphExplainsNestedInputInvalidationWithoutReuse()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var tracePath = Path.Combine(root, "trace.json");
        var external = new ValidationTraceDependencyInput
        {
            Name = "BeforeSnapshotPath",
            ArtifactId = "file:snapshot-sha",
            ArtifactKind = "snapshot_fact",
            ContentSha256 = "snapshot-sha",
            Path = Path.Combine(root, "snapshot.json"),
            ReuseScope = "snapshot"
        };

        using (var session = ValidationTraceSession.TryStart(
            tracePath,
            "sample-command",
            "sample-producer",
            "v1",
            "single_low_risk_decision",
            Array.Empty<ValidationTraceInput>(),
            0))
        {
            using (var outer = ValidationTraceSession.BeginDependencyNode(
                "outer-node",
                "compile_binding_certificate",
                "outer-producer",
                "v1",
                Array.Empty<ValidationTraceDependencyInput>()))
            {
                using (var inner = ValidationTraceSession.BeginDependencyNode(
                    "inner-node",
                    "candidate_denominator_certificate",
                    "inner-producer",
                    "v1",
                    new[] { external }))
                {
                    inner.SetOutputArtifact("inner-artifact", "inner-sha");
                }
                outer.SetOutputArtifact("outer-artifact", "outer-sha");
            }
            session!.Complete(0);
        }

        using var trace = JsonDocument.Parse(File.ReadAllText(tracePath));
        var graph = trace.RootElement.GetProperty("dependency_graph");
        Assert.Equal(
            ValidationTraceDependencyGraph.CurrentSchemaVersion,
            graph.GetProperty("schema_version").GetString());
        Assert.True(graph.GetProperty("observation_only").GetBoolean());
        Assert.False(graph.GetProperty("can_affect_admission").GetBoolean());
        Assert.False(graph.GetProperty("incremental_reuse_authorized")
            .GetBoolean());
        Assert.True(graph.GetProperty("acyclic").GetBoolean());
        Assert.True(graph.GetProperty("complete").GetBoolean());
        Assert.True(graph.GetProperty("full_rebuild_executed").GetBoolean());
        Assert.Equal(2, graph.GetProperty("nodes").GetArrayLength());
        Assert.Equal(
            "outer-node",
            Assert.Single(graph.GetProperty("top_level_node_ids")
                .EnumerateArray()).GetString());

        var invalidation = Assert.Single(
            graph.GetProperty("theoretical_invalidation_sets")
                .EnumerateArray());
        Assert.Equal(
            "file:snapshot-sha",
            invalidation.GetProperty("changed_input_artifact_id").GetString());
        Assert.Equal(
            new[] { "inner-node", "outer-node" },
            invalidation.GetProperty("invalidated_node_ids")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal(
            "input_identity_changed",
            invalidation.GetProperty("reason").GetString());
        Assert.Equal(
            "all_descendants",
            invalidation.GetProperty("propagation_rule").GetString());
    }

    [Fact]
    public void DependencyGraphFailsClosedOnSameIdentityOutputDrift()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var tracePath = Path.Combine(root, "trace.json");

        using (var session = ValidationTraceSession.TryStart(
            tracePath,
            "sample-command",
            "sample-producer",
            "v1",
            null,
            Array.Empty<ValidationTraceInput>(),
            0))
        {
            using (var first = ValidationTraceSession.BeginDependencyNode(
                "same-node",
                "sample-certificate",
                "sample-producer",
                "v1",
                Array.Empty<ValidationTraceDependencyInput>()))
            {
                first.SetOutputArtifact("same-artifact", "first-sha");
            }
            using (var second = ValidationTraceSession.BeginDependencyNode(
                "same-node",
                "sample-certificate",
                "sample-producer",
                "v1",
                Array.Empty<ValidationTraceDependencyInput>()))
            {
                second.SetOutputArtifact("same-artifact", "second-sha");
            }
            session!.Complete(0);
        }

        using var trace = JsonDocument.Parse(File.ReadAllText(tracePath));
        var graph = trace.RootElement.GetProperty("dependency_graph");
        Assert.False(graph.GetProperty("complete").GetBoolean());
        Assert.False(graph.GetProperty("full_rebuild_executed").GetBoolean());
        var node = Assert.Single(graph.GetProperty("nodes").EnumerateArray());
        Assert.True(node.GetProperty("identity_conflict").GetBoolean());
        Assert.Contains(
            "same_node_identity_output_content_changed",
            node.GetProperty("invalidation_reasons")
                .EnumerateArray()
                .Select(value => value.GetString()));
    }

    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(),
        "stardewai-validation-trace-tests",
        Guid.NewGuid().ToString("N"));
}
