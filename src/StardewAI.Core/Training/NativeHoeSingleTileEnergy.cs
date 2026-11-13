using System;
using System.IO;

namespace StardewAI.Core.Training;

/// <summary>
/// Pure vanilla Hoe energy arithmetic for DoFunction argument power 1 / toolPower 0.
/// Callers must independently bind the native action and all supplied inputs.
/// </summary>
public static class NativeHoeSingleTileEnergy
{
    public static float ExpectedAfter(float before, int maxStamina, int effectiveFarmingLevel,
        bool isEfficient, bool hasStaminaDecreaseProtection)
    {
        RequireFinite(before, "before");
        if (maxStamina < 0 || effectiveFarmingLevel < 0)
            throw new InvalidDataException("Hoe energy requires nonnegative maximum stamina and effective farming level.");

        // Pinned Hoe.DoFunction 0x0600183a: efficiency skips the setter entirely.
        if (isEfficient) return before;
        var cost = 2f - (float)effectiveFarmingLevel * 0.1f;
        var requested = before - cost;

        // Pinned Farmer.set_Stamina 0x06000847: statue_of_blessings_2 guards
        // decreases only, before clamping. Equal values and increases still set.
        if (hasStaminaDecreaseProtection && requested < before) return before;
        return Math.Min((float)maxStamina, Math.Max(requested, -16f));
    }

    public static void Verify(float before, float after, int maxStamina, int effectiveFarmingLevel,
        bool isEfficient, bool hasStaminaDecreaseProtection)
    {
        RequireFinite(after, "after");
        if (after != ExpectedAfter(before, maxStamina, effectiveFarmingLevel, isEfficient, hasStaminaDecreaseProtection))
            throw new InvalidDataException("Hoe energy does not match the exact native single-tile float arithmetic.");
    }

    private static void RequireFinite(float value, string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new InvalidDataException("Hoe energy " + name + " must be finite.");
    }
}
