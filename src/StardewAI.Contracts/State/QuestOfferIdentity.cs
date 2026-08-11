using System;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.Contracts.State;

public static class QuestOfferIdentity
{
    public static string Compute(
        string? id,
        string? runtimeType,
        string? title,
        string? description,
        string? currentObjective)
    {
        var canonical = string.Join(
            "\n",
            id ?? string.Empty,
            runtimeType ?? string.Empty,
            title ?? string.Empty,
            description ?? string.Empty,
            currentObjective ?? string.Empty);
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(
                sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    public static string Compute(QuestProgressRef quest) =>
        Compute(
            quest.Id,
            quest.RuntimeType,
            quest.Title,
            quest.Description,
            quest.CurrentObjective);
}
