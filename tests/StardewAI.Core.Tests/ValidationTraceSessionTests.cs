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

    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(),
        "stardewai-validation-trace-tests",
        Guid.NewGuid().ToString("N"));
}
