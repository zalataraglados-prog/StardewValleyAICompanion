using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

/// <summary>Offline verification of frozen artifacts, independent of loop status flags.</summary>
public static class GoldenWalnutFreshSaveSourceScheduleVerifier
{
    public static GoldenWalnutFreshSaveSourceSchedule Verify(
        string episodeDirectory,
        string manifestPath,
        string emittedSchedulePath,
        string scheduleId,
        string runId,
        string saveId,
        string playerId)
    {
        if (string.IsNullOrWhiteSpace(saveId) || string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Expected readable save and player identities are required.");

        // Re-enumerate durable evidence rather than trusting the frozen manifest's membership.
        // Compare before replay so an omitted, added or reordered episode cannot pass merely
        // because the loop's schedule and manifest agree with each other.
        var manifest = GoldenWalnutFreshSaveEpisodeManifestBuilder.BuildFromDirectory(
            episodeDirectory, scheduleId, runId);
        CompareArtifact(JsonSerializer.SerializeToElement(manifest), manifestPath, "manifest");

        var boundary = JsonSerializer.Deserialize<GoldenWalnutFreshSaveInitialBoundary>(
            File.ReadAllText(manifest.InitialBoundaryPath)) ??
            throw new InvalidDataException("Golden Walnut initial boundary is unreadable.");
        var initial = GoldenWalnutFreshSaveInitialBoundaryValidator.Validate(
            boundary, manifest.InitialBoundaryPath, runId);
        Require(initial.SaveId.Value == saveId, "identity.save_id");
        Require(initial.PlayerId.Value == playerId, "identity.player_id");

        // This is the sole schedule derivation implementation. It validates readable
        // identities at both ends of every episode and native false-to-true history.
        var rebuilt = GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build(manifestPath);
        Require(rebuilt.SourceCompletions.Length > 0, "source_completions (no source increment)");
        CompareArtifact(JsonSerializer.SerializeToElement(rebuilt), emittedSchedulePath, "schedule");
        return rebuilt;
    }

    private static void CompareArtifact(JsonElement expected, string path, string field)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Compare(expected, document.RootElement, field);
    }

    // Compare the serialized existing contract, including every required property. Do not
    // deserialize the claimed schedule: missing false/zero fields must not become defaults.
    // Object property order/whitespace are immaterial; array ordering remains evidence.
    private static void Compare(JsonElement expected, JsonElement actual, string field)
    {
        Require(expected.ValueKind == actual.ValueKind, field);
        if (expected.ValueKind == JsonValueKind.Object)
        {
            var properties = actual.EnumerateObject().ToArray();
            Require(properties.Length == expected.EnumerateObject().Count() &&
                properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length,
                field + " properties");
            foreach (var property in expected.EnumerateObject())
            {
                Require(actual.TryGetProperty(property.Name, out var value), field + "." + property.Name);
                Compare(property.Value, value, field + "." + property.Name);
            }
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            Require(expected.GetArrayLength() == actual.GetArrayLength(), field + " length");
            for (var index = 0; index < expected.GetArrayLength(); index++)
                Compare(expected[index], actual[index], field + "[" + index + "]");
        }
        else
        {
            Require(expected.ValueKind == JsonValueKind.String
                ? expected.GetString() == actual.GetString()
                : expected.GetRawText() == actual.GetRawText(), field);
        }
    }

    private static void Require(bool condition, string field)
    {
        if (!condition)
            throw new InvalidDataException("Golden Walnut independent verification failed at " + field + ".");
    }
}
