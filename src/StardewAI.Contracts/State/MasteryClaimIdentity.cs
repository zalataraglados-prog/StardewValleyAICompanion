using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Contracts.State;

public static class MasteryClaimIdentity
{
    public static string ComputeOptionFingerprint(MasteryClaimOptionRef option) => Hash(new
    {
        option.SkillId,
        option.SkillKey,
        option.SkillLevel,
        option.MasteryStatKey,
        option.MasteryStatValue,
        option.Claimed,
        option.ActionTile,
        option.DirectRewards,
        option.RecipeRewards,
        option.GrantsTrinketSlot
    });

    public static string ComputeProjectionFingerprint(MasteryClaimProjectionRef projection) => Hash(new
    {
        projection.TargetLocationId,
        projection.AllBaseSkillsLevelTen,
        projection.MasteryExperience,
        projection.CurrentMasteryLevel,
        projection.MasteryLevelsSpent,
        projection.UnspentMasteryLevels,
        projection.AllPlaquesCompleted,
        projection.TrinketSlots,
        Skills = Array.ConvertAll(projection.Skills, option => new
        {
            option.SkillId,
            option.SkillKey,
            option.SkillLevel,
            option.MasteryStatKey,
            option.MasteryStatValue,
            option.Claimed,
            option.Claimable,
            option.ActionTile,
            option.GrantsTrinketSlot
        }),
        projection.GameId,
        projection.PlayerId
    });

    private static string Hash<T>(T value)
    {
        var canonical = JsonSerializer.Serialize(value);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }
}
