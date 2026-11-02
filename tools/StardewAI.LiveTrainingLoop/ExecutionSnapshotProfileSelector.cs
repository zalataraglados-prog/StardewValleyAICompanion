using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static class ExecutionSnapshotProfileSelector
{
    private const string FullProfile = "full";
    private const string FishingProfile = "fishing";

    private static readonly HashSet<string> FishingExecutionOptionIds =
        new(StringComparer.Ordinal)
        {
            "executor.traverse_connector",
            "executor.move_to_tile",
            "executor.catch_fish",
            "executor.collect_crab_pot"
        };

    public static string ResolveForQueue(
        JsonObject? queue,
        string configuredProfile)
    {
        if (!string.Equals(
                configuredProfile,
                FullProfile,
                StringComparison.OrdinalIgnoreCase) ||
            queue?["items"] is not JsonArray items ||
            items.Count == 0)
        {
            return configuredProfile;
        }

        var executableItems = items
            .OfType<JsonObject>()
            .Where(IsExecutable)
            .ToArray();
        if (executableItems.Length == 0 ||
            executableItems.Any(item =>
                !FishingExecutionOptionIds.Contains(ReadString(item, "option_id"))) ||
            !executableItems.Any(IsMasterAnglerItem))
        {
            return configuredProfile;
        }

        return FishingProfile;
    }

    private static bool IsExecutable(JsonObject item)
    {
        var status = ReadString(item, "status");
        return string.IsNullOrWhiteSpace(status) ||
            string.Equals(status, "pending", StringComparison.Ordinal);
    }

    private static bool IsMasterAnglerItem(JsonObject item)
    {
        if (ReadString(item, "source_action_id").Contains(
                "master_angler",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return item["normalized_command"]?["parameters"] is JsonArray parameters &&
            parameters.OfType<JsonObject>().Any(parameter =>
                ReadString(parameter, "name").Contains(
                    "master_angler",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadString(JsonObject? value, string name) =>
        value?[name] is JsonValue jsonValue &&
        jsonValue.TryGetValue<string>(out var result)
            ? result
            : string.Empty;
}
