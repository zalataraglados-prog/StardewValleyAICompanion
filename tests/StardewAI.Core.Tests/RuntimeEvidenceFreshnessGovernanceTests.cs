using System.Text.Json;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Core.Tests;

public sealed class RuntimeEvidenceFreshnessGovernanceTests
{
    private static readonly IReadOnlyDictionary<string, string> NativeObjectArtifacts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["world.rotate_house_plant"] = "runtime-house-plant-20260829-001431",
            ["world.play_singing_stone"] = "runtime-singing-stone-20260829-001624",
            ["farming.collect_slime_ball"] = "runtime-slime-ball-20260829-001854",
            ["animals.withdraw_feed_hopper_hay"] = "runtime-feed-hopper-20260829-001943",
            ["animals.collect_auto_grabber_contents"] = "runtime-auto-grabber-20260829-002033",
            ["movement.use_mini_obelisk"] = "runtime-mini-obelisk-20260829-011139"
        };

    [Fact]
    public void RefactoredNativeObjectCapabilitiesRequireCurrentRuntimeEvidenceBindings()
    {
        foreach (var pair in NativeObjectArtifacts)
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(pair.Key);
            var binding = Assert.Single(capability.RuntimeEvidenceBindings);

            Assert.Equal(RuntimeEvidenceFreshnessStatus.Current, capability.RuntimeEvidenceFreshness);
            Assert.Equal(
                RuntimeEvidenceCatalogSource.NativeObjectRuntimePathRevision,
                capability.ExpectedRuntimePathRevision);
            Assert.Equal(
                RuntimeEvidenceCatalogSource.NativeObjectRuntimeSourceSha256,
                capability.RuntimePathSourceSha256);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.RuntimeTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.OutputTrainingGate);
            Assert.Equal(pair.Value, binding.ArtifactId);
            Assert.Equal(capability.RuntimeEvidenceIds.Single(), binding.EvidenceId);
            Assert.Equal(capability.ExpectedRuntimePathRevision, binding.RuntimePathRevision);
            Assert.Equal(capability.RuntimePathSourceSha256, binding.RuntimeSourceSha256);
            Assert.Equal(40, binding.SourceCommit.Length);
            Assert.False(string.IsNullOrWhiteSpace(binding.BuildIdentity));
            Assert.Equal(64, binding.RuntimeHarnessDllSha256.Length);
            Assert.Equal(64, binding.ContractsDllSha256.Length);
            Assert.Equal(64, binding.TransparentBridgeDllSha256.Length);
            Assert.EndsWith(binding.RuntimeHarnessDllSha256, binding.BuildIdentity, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RuntimeEvidenceFreshnessFailsClosedForUnknownEvidenceOrChangedRevision()
    {
        var changedRevision = RuntimeEvidenceCatalogSource.EvaluateFreshness(
            new[] { "EVD-271" },
            "native_object_execution.v3",
            out var changedRevisionBindings,
            out var changedRevisionPath);
        var unknownEvidence = RuntimeEvidenceCatalogSource.EvaluateFreshness(
            new[] { "EVD-unknown" },
            RuntimeEvidenceCatalogSource.NativeObjectRuntimePathRevision,
            out var unknownBindings,
            out var knownPath);

        Assert.Equal(RuntimeEvidenceFreshnessStatus.StaleOrMissing, changedRevision);
        Assert.Single(changedRevisionBindings);
        Assert.Null(changedRevisionPath);
        Assert.Equal(RuntimeEvidenceFreshnessStatus.StaleOrMissing, unknownEvidence);
        Assert.Empty(unknownBindings);
        Assert.NotNull(knownPath);
    }

    [Fact]
    public void NativeObjectRuntimePathFingerprintMatchesCurrentSources()
    {
        var actual = RuntimeEvidenceCatalogSource.ComputeRuntimeSourceSha256(
            FindRepositoryRoot(),
            RuntimeEvidenceCatalogSource.NativeObjectRuntimePathRevision);

        Assert.Equal(RuntimeEvidenceCatalogSource.NativeObjectRuntimeSourceSha256, actual);
    }

    [Fact]
    public void CapabilityOutputIncludesTraceableRuntimeEvidenceIdentity()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("farming.collect_slime_ball");
        var json = JsonSerializer.Serialize(capability);

        Assert.Contains("\"runtime_evidence_freshness\":\"Current\"", json, StringComparison.Ordinal);
        Assert.Contains("\"artifact_id\":\"runtime-slime-ball-20260829-001854\"", json, StringComparison.Ordinal);
        Assert.Contains("\"source_commit\":", json, StringComparison.Ordinal);
        Assert.Contains("\"build_identity\":", json, StringComparison.Ordinal);
        Assert.Contains("\"runtime_harness_dll_sha256\":", json, StringComparison.Ordinal);
        Assert.Contains("\"runtime_path_source_sha256\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicQualityWorkflowExecutesCoreGovernanceTests()
    {
        var workflow = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "quality.yml"));

        Assert.Contains(
            "dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("GameFreeGovernance=true", workflow, StringComparison.Ordinal);
        Assert.Contains("--no-restore", workflow, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate StardewAI repository root.");
    }
}
