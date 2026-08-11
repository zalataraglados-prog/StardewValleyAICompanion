using System;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.Contracts.State;

public static class SpecialOrderOfferIdentity
{
    public static string Compute(SpecialOrderProgressRef order) =>
        Compute(
            order.OrderType,
            order.QuestKey,
            order.GenerationSeed,
            order.DueDate,
            order.Duration);

    public static string Compute(
        string? orderType,
        string? questKey,
        int generationSeed,
        int dueDate,
        string? duration)
    {
        var canonical = string.Join(
            "\n",
            orderType ?? string.Empty,
            questKey ?? string.Empty,
            generationSeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            dueDate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            duration ?? string.Empty);
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
