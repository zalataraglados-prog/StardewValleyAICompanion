using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class JsonlPolicyHorizonObservationWriter
{
    public bool AppendIfNew(string path, PolicyHorizonObservationEnvelope observation)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        if (File.Exists(fullPath) && File.ReadLines(fullPath).Any(line => HasObservationId(line, observation.ObservationId)))
            return false;
        File.AppendAllText(fullPath, JsonSerializer.Serialize(observation, JsonOptions) + Environment.NewLine);
        return true;
    }

    private static bool HasObservationId(string line, string observationId)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty("observation_id", out var value) &&
                string.Equals(value.GetString(), observationId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
