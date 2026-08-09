using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
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
            .Where(option =>
                !ActionQueueCompiler.HasStepCompiler(option.OptionId) &&
                !DailyPlanCompiler.HasOptionCompiler(option.OptionId))
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
            .Where(optionId => !RuntimeTestHarnessDispatchCatalog.IsSupported(optionId))
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
        var dispatchMatches = Regex.Matches(
                source,
                "pending\\.Request\\.OptionId == \"(?<id>[^\"]+)\"",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["id"].Value)
            .Where(optionId => !optionId.StartsWith("debug.", StringComparison.Ordinal))
            .Where(optionId => optionId != "debug.visible_walk")
            .ToArray();
        Assert.Equal(
            dispatchMatches.Length,
            dispatchMatches.Distinct(StringComparer.Ordinal).Count());
        var dispatched = dispatchMatches
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            RuntimeTestHarnessDispatchCatalog.OptionIds.OrderBy(value => value, StringComparer.Ordinal),
            dispatched.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void RuntimeDebugAllowlistMatchesDebugDispatchBranches()
    {
        var root = FindRepositoryRoot();
        var dispatchSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        var allowlistSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.SupportedOptions.cs"));
        var dispatched = Regex.Matches(
                dispatchSource,
                "\"(?<id>debug\\.[^\"]+)\"",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["id"].Value)
            .ToArray();
        var allowed = Regex.Matches(
                allowlistSource,
                "\"(?<id>debug\\.[^\"]+)\"",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["id"].Value)
            .ToArray();

        Assert.Equal(
            dispatched.Length,
            dispatched.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            allowed.Length,
            allowed.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            dispatched.OrderBy(value => value, StringComparer.Ordinal),
            allowed.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void RuntimeValidationUsesCapabilityCatalogInsteadOfExecutorIdList()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Shipping.Utilities.cs"));

        Assert.Contains(
            "RuntimeTestHarnessDispatchCatalog.IsSupported",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "request.OptionId != \"executor.",
            source,
            StringComparison.Ordinal);
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
        var dispatchMatches = Regex.Matches(
                source,
                "candidate\\.Kind == \"(?<kind>[^\"]+)\"",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["kind"].Value)
            .ToArray();
        Assert.Equal(
            dispatchMatches.Length,
            dispatchMatches.Distinct(StringComparer.Ordinal).Count());
        var dispatched = dispatchMatches
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
