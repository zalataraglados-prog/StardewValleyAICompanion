using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.Contracts.State;

public static class QuestCancellationIdentity
{
    public static string Compute(QuestProgressRef quest) =>
        Compute(
            quest.Id,
            quest.RuntimeType,
            quest.Title,
            quest.CurrentObjective,
            quest.QuestType,
            quest.Accepted,
            quest.Completed,
            quest.Hidden,
            quest.DailyQuest,
            quest.CanBeCancelled,
            quest.DayQuestAccepted,
            quest.DaysLeft,
            quest.MoneyReward,
            quest.Destroy);

    public static string Compute(
        string? questId,
        string? runtimeType,
        string? title,
        string? currentObjective,
        int questType,
        bool accepted,
        bool completed,
        bool hidden,
        bool dailyQuest,
        bool canBeCancelled,
        int dayQuestAccepted,
        int daysLeft,
        int moneyReward,
        bool destroy)
    {
        var canonical = string.Join("\n", new[]
        {
            questId ?? string.Empty,
            runtimeType ?? string.Empty,
            title ?? string.Empty,
            currentObjective ?? string.Empty,
            questType.ToString(CultureInfo.InvariantCulture),
            accepted ? "true" : "false",
            completed ? "true" : "false",
            hidden ? "true" : "false",
            dailyQuest ? "true" : "false",
            canBeCancelled ? "true" : "false",
            dayQuestAccepted.ToString(CultureInfo.InvariantCulture),
            daysLeft.ToString(CultureInfo.InvariantCulture),
            moneyReward.ToString(CultureInfo.InvariantCulture),
            destroy ? "true" : "false"
        });
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
