using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Contracts.State;

public static class AdventureGuildRewardIdentity
{
    public static string Compute(AdventureGuildRewardGoalRef[] goals)
    {
        var canonical = JsonSerializer.Serialize(goals);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
