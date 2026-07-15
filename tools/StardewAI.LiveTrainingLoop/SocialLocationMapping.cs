using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static class SocialLocationMapping
{
    public static string ResolveLocationId(JsonObject? item, string optionId)
    {
        if (!IsSocialExecutor(optionId))
            return string.Empty;

        return ReadQueueParameterString(item, "target_location");
    }

    public static bool IsSocialExecutor(string optionId)
    {
        return string.Equals(optionId, "executor.social_interact", StringComparison.Ordinal);
    }

    private static string ReadQueueParameterString(JsonObject? item, string name)
    {
        var parameters = item?["normalized_command"]?["parameters"]?.AsArray();
        if (parameters is null)
            return string.Empty;

        foreach (var parameter in parameters)
        {
            var p = parameter?.AsObject();
            if (p is not null &&
                string.Equals(p["name"]?.GetValue<string>(), name, StringComparison.Ordinal))
            {
                return p["value"]?.GetValue<string>() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
