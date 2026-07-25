using System.Text.RegularExpressions;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DownstreamCapabilityCatalogTests
{
    [Fact]
    public void EveryFullActionOptionHasARegisteredStepCompiler()
    {
        var missing = new StardewAI.Core.OptionRegistry.OptionRegistry().All
            .Where(option => option.CompilerResponsibility == CompilerResponsibilities.FullActionExpansion)
            .Where(option => !ActionQueueCompiler.HasStepCompiler(option.OptionId))
            .Select(option => option.OptionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryCompiledExecutorPrimitiveHasRuntimeDispatchSupport()
    {
        var missing = ActionQueueCompiler.StepCompilerOptionIds
            .Where(optionId => optionId.StartsWith("executor.", StringComparison.Ordinal))
            .Where(optionId => !RuntimeExecutorCapabilityCatalog.IsSupported(optionId))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void RuntimeCatalogMatchesProductionDispatchBranches()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        var dispatched = Regex.Matches(
                source,
                "pending\\.Request\\.OptionId == \"(?<id>[^\"]+)\"",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["id"].Value)
            .Where(optionId => !optionId.StartsWith("debug.", StringComparison.Ordinal))
            .Where(optionId => optionId != "debug.visible_walk")
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            RuntimeExecutorCapabilityCatalog.OptionIds.OrderBy(value => value, StringComparer.Ordinal),
            dispatched.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void UnknownRuntimeOptionCannotFallBackToCropMaintenance()
    {
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
    public void ShopSaleRuntimeUsesNativeMenuInputAndExactPostStateChecks()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Shop.cs"));

        Assert.Contains("ExecuteSellShopItem", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y", source, StringComparison.Ordinal);
        Assert.Contains("afterMoney == expectedMoney && afterCount == expectedCount", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money += unitPrice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Items[slotIndex] = null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DailyPlanCatalogMatchesDispatchBranches()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "StardewAI.Core",
            "Training",
            "DailyPlanCompiler.Dispatch.cs"));
        var dispatched = Regex.Matches(
                source,
                "candidate\\.Kind == \"(?<kind>[^\"]+)\"",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["kind"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            DailyPlanCandidateCapabilityCatalog.CompilableKinds.OrderBy(value => value, StringComparer.Ordinal),
            dispatched.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryLiteralCandidateKindIsClassified()
    {
        var optionRegistryRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "StardewAI.Core",
            "OptionRegistry");
        var generatedKinds = Directory
            .EnumerateFiles(optionRegistryRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    "Kind = \"(?<kind>[^\"]+)\"",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["kind"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var classifiedKinds = DailyPlanCandidateCapabilityCatalog.All
            .Select(row => row.Kind)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(generatedKinds.Except(classifiedKinds, StringComparer.Ordinal));
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
