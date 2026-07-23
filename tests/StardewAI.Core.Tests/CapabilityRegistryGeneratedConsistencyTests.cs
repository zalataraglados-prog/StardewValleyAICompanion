using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CapabilityRegistryGeneratedConsistencyTests
{
    [Fact]
    public void CapabilityCatalogGeneratedConsistencyTests()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        Assert.Equal(
            OptionCapabilityRegistrySource.All.Select(row => row.OptionId).OrderBy(id => id, StringComparer.Ordinal),
            registry.All.Select(row => row.OptionId).OrderBy(id => id, StringComparer.Ordinal));

        foreach (var option in registry.All)
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(option.OptionId);
            Assert.Equal(declaration.RegistrationStatus, option.RegistrationStatus);
            Assert.Equal(declaration.ReadStatus, option.ReadStatus);
            Assert.Equal(declaration.CandidateStatus, option.CandidateStatus);
            Assert.Equal(declaration.CompilerStatus, option.CompilerStatus);
            Assert.Equal(declaration.HarnessDispatchSupported, option.HarnessDispatchSupported);
            Assert.Equal(declaration.ProductExecutorSupported, option.ProductExecutorSupported);
            Assert.Equal(declaration.RuntimeEvidenceStatus, option.RuntimeStatus);
            Assert.Equal(declaration.TrainingEligibility, option.TrainingEligibility);
        }
    }

    [Fact]
    public void HarnessSupportDoesNotImplyRuntimeEvidenceTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("executor.interact");

        Assert.True(declaration.HarnessDispatchSupported);
        Assert.False(declaration.ProductExecutorSupported);
        Assert.Equal(OptionRuntimeStatus.RegisteredOnly, declaration.RuntimeEvidenceStatus);
        Assert.Equal(
            OptionTrainingEligibility.BlockedPendingRuntimeEvidence,
            declaration.TrainingEligibility);
        Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void ProductSupportDoesNotImplyTrainingEligibilityTests()
    {
        const bool productExecutorSupported = true;

        Assert.True(productExecutorSupported);
        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RegisteredOnly,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: true,
            playerConfirmationRequired: false));
    }

    [Fact]
    public void EveryFullActionHasStepCompilerTests()
    {
        var missing = new StardewAI.Core.OptionRegistry.OptionRegistry().All
            .Where(row => row.CompilerResponsibility == CompilerResponsibilities.FullActionExpansion)
            .Where(row => !ActionQueueCompiler.HasStepCompiler(row.OptionId))
            .Select(row => row.OptionId);

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryCompiledExecutorHasDeclaredDispatchStatusTests()
    {
        foreach (var optionId in ActionQueueCompiler.StepCompilerOptionIds
            .Where(id => id.StartsWith("executor.", StringComparison.Ordinal)))
        {
            Assert.True(OptionCapabilityRegistrySource.TryGet(optionId, out var declaration));
            Assert.Equal(
                RuntimeTestHarnessDispatchCatalog.IsSupported(optionId),
                declaration.HarnessDispatchSupported);
            Assert.Equal(
                ProductExecutorCapabilityCatalog.IsSupported(optionId),
                declaration.ProductExecutorSupported);
        }
    }

    [Fact]
    public void EveryLiteralCandidateKindIsClassifiedTests()
    {
        var optionRegistryRoot = Path.Combine(FindRepositoryRoot(), "src", "StardewAI.Core", "OptionRegistry");
        var generatedKinds = Directory
            .EnumerateFiles(optionRegistryRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    "Kind = \"(?<kind>[^\"]+)\"",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["kind"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var classifiedKinds = OptionCapabilityRegistrySource.DailyCandidates
            .Select(row => row.Kind)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(generatedKinds.Except(classifiedKinds, StringComparer.Ordinal));
        Assert.Equal(
            classifiedKinds.OrderBy(value => value, StringComparer.Ordinal),
            DailyPlanCandidateCapabilityCatalog.All
                .Select(row => row.Kind)
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void UnknownRuntimeOptionFailsClosedTests()
    {
        Assert.False(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.unknown"));
        Assert.False(ProductExecutorCapabilityCatalog.IsSupported("executor.unknown"));
        Assert.False(OptionCapabilityRegistrySource.TryGet("executor.unknown", out _));

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        Assert.Contains("runtime_executor_option_not_supported:", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "pending.Completion.SetResult(ExecuteMaintainCropsNoOp(pending.Request));",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingAllowlistRequiresRuntimeEvidenceTests()
    {
        Assert.All(OptionCapabilityRegistrySource.TrainingAllowlist, optionId =>
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
            Assert.True(declaration.RuntimeEvidenceStatus >= OptionRuntimeStatus.RuntimeVerified);
        });

        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RuntimeVerified,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: false,
            playerConfirmationRequired: false));
        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RuntimeVerified,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: true,
            playerConfirmationRequired: true));
    }

    [Fact]
    public void NoDuplicateCapabilityIdTests()
    {
        Assert.Equal(
            OptionCapabilityRegistrySource.All.Count,
            OptionCapabilityRegistrySource.All
                .Select(row => row.OptionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            OptionCapabilityRegistrySource.DailyCandidates.Count,
            OptionCapabilityRegistrySource.DailyCandidates
                .Select(row => row.Kind)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
