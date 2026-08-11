using System;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.Contracts.State;

public static class QuestRewardClaimIdentity
{
    public static string Compute(QuestProgressRef quest) =>
        Compute(
            quest.Id,
            quest.RuntimeType,
            quest.Title,
            quest.MoneyReward,
            quest.DayQuestAccepted,
            quest.DailyQuest);

    public static string Compute(
        string? questId,
        string? runtimeType,
        string? title,
        int moneyReward,
        int dayQuestAccepted,
        bool dailyQuest)
    {
        var canonical = string.Join("\n", new[]
        {
            questId ?? string.Empty,
            runtimeType ?? string.Empty,
            title ?? string.Empty,
            moneyReward.ToString(System.Globalization.CultureInfo.InvariantCulture),
            dayQuestAccepted.ToString(System.Globalization.CultureInfo.InvariantCulture),
            dailyQuest ? "true" : "false"
        });
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
