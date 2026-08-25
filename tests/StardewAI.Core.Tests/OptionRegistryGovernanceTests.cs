using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class OptionRegistryGovernanceTests
{
    [Fact]
    public void RegistryCountAndKindTests()
    {
        var options = new StardewAI.Core.OptionRegistry.OptionRegistry().All;

        Assert.Equal(136, options.Count);
        Assert.Equal(50, options.Count(row => !row.OptionId.StartsWith("executor.", StringComparison.Ordinal)));
        Assert.Equal(86, options.Count(row => row.OptionId.StartsWith("executor.", StringComparison.Ordinal)));
        Assert.Equal(2, options.Count(row => row.SemanticKind == OptionSemanticKind.GoalTemplate));
        Assert.Equal(48, options.Count(row => row.SemanticKind == OptionSemanticKind.CompositeOptionSpec));
        Assert.Equal(86, options.Count(row => row.SemanticKind == OptionSemanticKind.PrimitiveOptionSpec));
    }

    [Fact]
    public void DuplicateOptionIdFailsTests()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        var duplicate = registry.GetRequired("executor.wait_ticks");

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterForValidation(duplicate, "generated-options.cs", 42));

        Assert.Contains("executor.wait_ticks", error.Message, StringComparison.Ordinal);
        Assert.Contains("OptionRegistry.cs:", error.Message, StringComparison.Ordinal);
        Assert.Contains("generated-options.cs:42", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownOptionPolicyFailsClosed()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        var unknown = new OptionSpec
        {
            OptionId = "executor.unregistered_action",
            Domain = "test",
            Name = "Unregistered action"
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterForValidation(unknown, "generated-options.cs", 84));

        Assert.Contains("incomplete option_spec.v2 governance contract", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryOptionHasExplicitRiskPolicyTests()
    {
        var missing = Registry().Where(row =>
            row.SchemaVersion != "option_spec.v2" ||
            row.RiskClass == OptionRiskClass.Unknown ||
            row.Irreversibility == OptionIrreversibility.Unknown ||
            row.RiskLevel == "unknown" ||
            row.Recoverability == "unknown");

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryOptionHasExplicitConfirmationPolicyTests()
    {
        Assert.DoesNotContain(
            Registry(),
            row => row.ConfirmationPolicy == OptionConfirmationPolicy.Unknown);
    }

    [Fact]
    public void EveryOptionHasExplicitHostAndOwnershipPolicyTests()
    {
        Assert.DoesNotContain(
            Registry(),
            row => row.HostPolicy == OptionHostPolicy.Unknown ||
                row.OwnershipPolicy == OptionOwnershipPolicy.Unknown ||
                row.ModAdapterPolicy == OptionModAdapterPolicy.Unknown);
    }

    [Fact]
    public void IrreversibleOptionsRequireConfirmationTests()
    {
        Assert.DoesNotContain(
            Registry(),
            row => row.Irreversibility != OptionIrreversibility.None &&
                row.ConfirmationPolicy == OptionConfirmationPolicy.NotRequired);
    }

    [Fact]
    public void NoUnknownPolicyCanEnterAutonomousCandidateSetTests()
    {
        Assert.DoesNotContain(
            Registry(),
            row => row.AutonomousCandidatePolicy == AutonomousCandidatePolicy.Unknown ||
                row.RequiredFactPolicy.Mode == RequiredFactPolicyMode.Unknown ||
                row.ParameterSchema == ParameterSchemaPolicy.Unknown ||
                row.TrainingEligibility == OptionTrainingEligibility.Unknown ||
                row.RuntimeStatus == OptionRuntimeStatus.Unknown ||
                row.ProductStatus == OptionProductStatus.Unknown);
    }

    [Fact]
    public void SellShopItemIsNotRecoverableLowRiskTests()
    {
        var option = new StardewAI.Core.OptionRegistry.OptionRegistry()
            .GetRequired("executor.sell_shop_item");

        Assert.Equal(OptionRiskClass.R2Consumptive, option.RiskClass);
        Assert.Equal(OptionIrreversibility.Consumptive, option.Irreversibility);
        Assert.Equal(
            OptionConfirmationPolicy.PolicyAuthorizationRequired,
            option.ConfirmationPolicy);
        Assert.NotEqual("low", option.RiskLevel);
        Assert.NotEqual("recoverable", option.Recoverability);
    }

    [Fact]
    public void EveryOptionHasCompilerVerifierEvidenceAndTrainingBindings()
    {
        Assert.DoesNotContain(
            Registry(),
            row => string.IsNullOrWhiteSpace(row.CompilerBinding) ||
                string.IsNullOrWhiteSpace(row.BeforeVerifierBinding) ||
                string.IsNullOrWhiteSpace(row.AfterVerifierBinding) ||
                string.IsNullOrWhiteSpace(row.RuntimeEvidenceId));

        Assert.Equal(
            OptionCapabilityRegistrySource.TrainingAllowlist.OrderBy(value => value, StringComparer.Ordinal),
            Registry()
                .Where(row => row.TrainingEligibility == OptionTrainingEligibility.Eligible)
                .Select(row => row.OptionId)
                .OrderBy(value => value, StringComparer.Ordinal));
        Assert.All(
            Registry().Where(row => row.TrainingEligibility != OptionTrainingEligibility.Eligible),
            row => Assert.NotEmpty(row.TrainingExclusionReasons));
    }

    private static IReadOnlyCollection<OptionSpec> Registry()
    {
        return new StardewAI.Core.OptionRegistry.OptionRegistry().All;
    }
}
