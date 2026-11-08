using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

// Synthetic per-call fixtures from vanilla 1.6.15 Axe.DoFunction (63-69),
// Pickaxe.DoFunction (82-89), and Farmer.Stamina (1826-1837); not native outcome evidence.
public sealed class NativeClearanceToolCostProjectorTests
{
    [Theory]
    [InlineData(NativeClearanceToolKind.Axe, 3, 0, 6f, 94f)]
    [InlineData(NativeClearanceToolKind.Axe, 0, 7, 0f, 100f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, 3, 0, 2f, 98f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, 0, 7, 16f, 84f)]
    public void UsesDistinctNativePowerSources(
        NativeClearanceToolKind tool, int argumentPower, int farmerToolPower, float debit, float after)
    {
        var result = NativeClearanceToolCostProjector.Project(Input(
            tool, argumentPower: argumentPower, farmerToolPower: farmerToolPower));

        Assert.Equal(debit, result.NominalStaminaDebit);
        Assert.Equal(after, result.RequestedStamina);
        Assert.Equal(after, result.StaminaAfter);
        Assert.True(result.StaminaSetterInvoked);
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe)]
    [InlineData(NativeClearanceToolKind.Pickaxe)]
    public void PreservesNativeSinglePrecisionInsteadOfDoubleEstimates(NativeClearanceToolKind tool)
    {
        var result = NativeClearanceToolCostProjector.Project(Input(tool, skill: 3));

        AssertBits(0x3fd9999a, result.NominalStaminaDebit!.Value); // float 2 - (float 3 * 0.1f)
        AssertBits(0x42c4999a, result.StaminaAfter); // float 100 - float debit
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe)]
    [InlineData(NativeClearanceToolKind.Pickaxe)]
    public void RepeatedIndividualCallsRoundAtEveryAssignment(NativeClearanceToolKind tool)
    {
        uint[] expected = { 0x4104cccd, 0x40d33334, 0x409cccce, 0x404ccccf, 0x3fc00004, 0xbe4cccb0 };
        float stamina = 10f;
        foreach (uint bits in expected)
        {
            stamina = NativeClearanceToolCostProjector.Project(Input(tool, stamina: stamina, skill: 3)).StaminaAfter;
            AssertBits(bits, stamina);
        }

        Assert.NotEqual(10f - 6f * 1.7f, stamina);
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe)]
    [InlineData(NativeClearanceToolKind.Pickaxe)]
    public void EfficientSkipsSetterEvenForOutOfRangeFiniteStamina(NativeClearanceToolKind tool)
    {
        foreach (float before in new[] { -20f, 300f, float.MaxValue, -float.MaxValue })
        {
            var result = NativeClearanceToolCostProjector.Project(Input(tool, stamina: before, efficient: true));
            Assert.Equal(before, result.StaminaAfter);
            Assert.Null(result.NominalStaminaDebit);
            Assert.Null(result.RequestedStamina);
            Assert.False(result.StaminaSetterInvoked);
        }
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe, -14.25f, 5, -15.75f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, -14.25f, 5, -15.75f)]
    [InlineData(NativeClearanceToolKind.Axe, -14.75f, 5, -16f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, -14.75f, 5, -16f)]
    [InlineData(NativeClearanceToolKind.Axe, 269.5f, 30, 270f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, 269.5f, 30, 270f)]
    public void AppliesFractionalLowerAndUpperSetterBoundaries(
        NativeClearanceToolKind tool, float before, int skill, float after)
    {
        Assert.Equal(after, NativeClearanceToolCostProjector.Project(Input(tool, stamina: before, skill: skill)).StaminaAfter);
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe)]
    [InlineData(NativeClearanceToolKind.Pickaxe)]
    public void ProtectionRejectsDecreaseBeforeClampButAllowsGain(NativeClearanceToolKind tool)
    {
        var rejected = NativeClearanceToolCostProjector.Project(Input(tool, stamina: 300f, protection: true));
        Assert.Equal(298f, rejected.RequestedStamina);
        Assert.Equal(300f, rejected.StaminaAfter);
        Assert.True(rejected.StaminaSetterInvoked);
        Assert.Equal(101f, NativeClearanceToolCostProjector.Project(Input(tool, skill: 30, protection: true)).StaminaAfter);
        Assert.Equal(-20f, NativeClearanceToolCostProjector.Project(Input(tool, stamina: -20f, protection: true)).StaminaAfter);
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe)]
    [InlineData(NativeClearanceToolKind.Pickaxe)]
    public void ZeroDebitStillRunsSetterAndProtectionDoesNotRejectEquality(NativeClearanceToolKind tool)
    {
        var result = NativeClearanceToolCostProjector.Project(Input(tool, stamina: 300f, skill: 20, protection: true));
        Assert.Equal(0f, result.NominalStaminaDebit);
        Assert.Equal(300f, result.RequestedStamina);
        Assert.Equal(270f, result.StaminaAfter);
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe, -1, 0, -2f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, 1, -2, -2f)]
    [InlineData(NativeClearanceToolKind.Axe, int.MaxValue, 0, -2f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, 1, int.MaxValue, 0f)]
    [InlineData(NativeClearanceToolKind.Axe, int.MinValue, 0, 0f)]
    [InlineData(NativeClearanceToolKind.Pickaxe, 1, int.MinValue, 2f)]
    public void PreservesRawIntPowersIncludingNativeUncheckedOverflow(
        NativeClearanceToolKind tool, int argumentPower, int farmerToolPower, float debit)
    {
        var result = NativeClearanceToolCostProjector.Project(Input(tool, argumentPower: argumentPower, farmerToolPower: farmerToolPower));
        Assert.Equal(debit, result.NominalStaminaDebit);
        Assert.Equal(100f - debit, result.StaminaAfter);
    }

    [Theory]
    [InlineData(NativeClearanceToolKind.Axe)]
    [InlineData(NativeClearanceToolKind.Pickaxe)]
    public void DoesNotClampUnusualEffectiveSkillsToPlannerRanges(NativeClearanceToolKind tool)
    {
        var result = NativeClearanceToolCostProjector.Project(Input(tool, skill: 1000));
        Assert.Equal(-98f, result.NominalStaminaDebit);
        Assert.Equal(198f, result.StaminaAfter);
        var extreme = NativeClearanceToolCostProjector.Project(Input(tool, skill: int.MaxValue));
        Assert.Equal(-214748368f, extreme.NominalStaminaDebit);
        Assert.Equal(270f, extreme.StaminaAfter);
    }

    [Fact]
    public void ConvertsEffectiveMaxStaminaToFloatLikeNativeSetter()
    {
        var input = Input(NativeClearanceToolKind.Axe, stamina: 16777220f, maxStamina: 16777217, skill: 20);
        Assert.Equal(16777216f, NativeClearanceToolCostProjector.Project(input).StaminaAfter);
        Assert.Equal(0f, NativeClearanceToolCostProjector.Project(Input(NativeClearanceToolKind.Axe, maxStamina: 0)).StaminaAfter);
    }

    [Fact]
    public void RejectsUnsupportedNonfiniteOrImpossibleGetterInputsEvenWhenEfficient()
    {
        Assert.Throws<ArgumentNullException>(() => NativeClearanceToolCostProjector.Project(null!));
        foreach (bool efficient in new[] { false, true })
        {
            foreach (int tool in new[] { 0, 3, -1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => NativeClearanceToolCostProjector.Project(Input((NativeClearanceToolKind)tool, efficient: efficient)));
            foreach (float stamina in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => NativeClearanceToolCostProjector.Project(Input(NativeClearanceToolKind.Axe, stamina: stamina, efficient: efficient)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NativeClearanceToolCostProjector.Project(Input(NativeClearanceToolKind.Axe, maxStamina: -1, efficient: efficient)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NativeClearanceToolCostProjector.Project(Input(NativeClearanceToolKind.Pickaxe, skill: -1, efficient: efficient)));
        }
    }

    [Fact]
    public void RetainsImmutableInputsAndNeverAuthorizesOutcomeOrTraining()
    {
        var input = Input(NativeClearanceToolKind.Pickaxe, stamina: 123.5f, argumentPower: 9, farmerToolPower: 3, skill: 15, protection: true);
        var first = NativeClearanceToolCostProjector.Project(input);
        var second = NativeClearanceToolCostProjector.Project(input);
        Assert.Same(input, first.Input);
        Assert.Equal(123.5f, input.StaminaBefore);
        Assert.Equal(9, input.ArgumentPower);
        Assert.Equal(3, input.FarmerToolPower);
        Assert.Equal(15, input.EffectiveSkillLevel);
        Assert.Equal(first.StaminaAfter, second.StaminaAfter);
        Assert.All(typeof(NativeClearanceToolCostInput).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(NativeClearanceToolCostProjection).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.False(first.NativeOutcomeVerified);
        Assert.False(first.ResourceCreditAuthorized);
        Assert.False(first.FormalProductTrainingAuthorized);
    }

    private static NativeClearanceToolCostInput Input(
        NativeClearanceToolKind tool, float stamina = 100f, int maxStamina = 270,
        bool efficient = false, bool protection = false, int argumentPower = 1,
        int farmerToolPower = 0, int skill = 0) =>
        new(tool, stamina, maxStamina, efficient, protection, argumentPower, farmerToolPower, skill);

    private static void AssertBits(uint expected, float actual) =>
        Assert.Equal(expected, unchecked((uint)BitConverter.SingleToInt32Bits(actual)));
}
