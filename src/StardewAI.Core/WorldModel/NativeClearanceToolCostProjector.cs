using System;

namespace StardewAI.Core.WorldModel
{
    public enum NativeClearanceToolKind
    {
        Axe = 1,
        Pickaxe = 2
    }

    /// <summary>
    /// Actual vanilla 1.6.15 inputs at the explicit debit boundary, after base
    /// DoFunction and Pickaxe's power copy, not parent-entry values or route estimates.
    /// EffectiveSkillLevel is
    /// Farmer.ForagingLevel for Axe or Farmer.MiningLevel for Pickaxe (including buffs).
    /// MaxStamina is the effective getter value; HasStaminaDecreaseProtection means
    /// Farmer.hasBuff("statue_of_blessings_2"). ArgumentPower retains the original
    /// raw argument. FarmerToolPower is only consumed by Pickaxe; its value is not
    /// an additional applicable/measured Axe input.
    /// </summary>
    public sealed class NativeClearanceToolCostInput
    {
        public NativeClearanceToolCostInput(
            NativeClearanceToolKind tool, float staminaBefore, int maxStamina,
            bool isEfficient, bool hasStaminaDecreaseProtection,
            int argumentPower, int farmerToolPower, int effectiveSkillLevel)
        {
            Tool = tool;
            StaminaBefore = staminaBefore;
            MaxStamina = maxStamina;
            IsEfficient = isEfficient;
            HasStaminaDecreaseProtection = hasStaminaDecreaseProtection;
            ArgumentPower = argumentPower;
            FarmerToolPower = farmerToolPower;
            EffectiveSkillLevel = effectiveSkillLevel;
        }

        public NativeClearanceToolKind Tool { get; }
        public float StaminaBefore { get; }
        public int MaxStamina { get; }
        public bool IsEfficient { get; }
        public bool HasStaminaDecreaseProtection { get; }
        public int ArgumentPower { get; }
        public int FarmerToolPower { get; }
        public int EffectiveSkillLevel { get; }
    }

    public sealed class NativeClearanceToolCostProjection
    {
        internal NativeClearanceToolCostProjection(
            NativeClearanceToolCostInput input, float? nominalStaminaDebit,
            float? requestedStamina, float staminaAfter)
        {
            Input = input;
            NominalStaminaDebit = nominalStaminaDebit;
            RequestedStamina = requestedStamina;
            StaminaAfter = staminaAfter;
        }

        public NativeClearanceToolCostInput Input { get; }
        // Null means Efficient skipped the expression and setter, not a zero-cost assignment.
        public float? NominalStaminaDebit { get; }
        public float? RequestedStamina { get; }
        public bool StaminaSetterInvoked => RequestedStamina.HasValue;
        public float StaminaAfter { get; }
        public bool NativeOutcomeVerified => false;
        public bool ResourceCreditAuthorized => false;
        public bool FormalProductTrainingAuthorized => false;
    }

    /// <summary>
    /// Projects only the explicit stamina assignment in one Axe/Pickaxe.DoFunction.
    /// Does not prove invocation origin, successful clearance, swing count, callback
    /// side effects, animation phases, or the resource delta of an entire tool action.
    /// </summary>
    public static class NativeClearanceToolCostProjector
    {
        public static NativeClearanceToolCostProjection Project(NativeClearanceToolCostInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Tool != NativeClearanceToolKind.Axe && input.Tool != NativeClearanceToolKind.Pickaxe)
                throw new ArgumentOutOfRangeException(nameof(input), "Only vanilla Axe and Pickaxe are supported.");
            if (float.IsNaN(input.StaminaBefore) || float.IsInfinity(input.StaminaBefore))
                throw new ArgumentOutOfRangeException(nameof(input), "Stamina must be finite.");
            if (input.MaxStamina < 0 || input.EffectiveSkillLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(input), "Effective native getter values must be nonnegative.");

            if (input.IsEfficient)
                return new NativeClearanceToolCostProjection(input, null, null, input.StaminaBefore);

            // Native int arithmetic wraps before conversion to float. Do not use double,
            // clamp skills/costs to planner ranges, or combine multiple calls into one debit.
            int baseDebit = input.Tool == NativeClearanceToolKind.Axe
                ? unchecked(2 * input.ArgumentPower)
                : unchecked(2 * (input.FarmerToolPower + 1));
            float skillDiscount = (float)input.EffectiveSkillLevel * 0.1f;
            float debit = (float)baseDebit - skillDiscount;
            float requested = input.StaminaBefore - debit;

            // Farmer.Stamina setter: protection rejects decreases BEFORE clamping.
            float after = input.HasStaminaDecreaseProtection && requested < input.StaminaBefore
                ? input.StaminaBefore
                : Math.Min((float)input.MaxStamina, Math.Max(requested, -16f));
            return new NativeClearanceToolCostProjection(input, debit, requested, after);
        }
    }
}
