global using static StardewAI.GoalConditionedBootstrap.BootstrapArtifactGuard;

using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static class BootstrapArtifactGuard
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    public static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    public static bool IsLowerSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsSha256(string value) => IsLowerSha256(value);
}
