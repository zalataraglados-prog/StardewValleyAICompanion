using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

/// <summary>Shared source replay for consumers; never grants native route admission.</summary>
public static class GoldenWalnutNativeEvidenceReader
{
    public const string BindingSchemaVersion = "golden_walnut_native_evidence_binding.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static GoldenWalnutNativeEvidenceReplay Verify(GoldenWalnutNativeEvidenceBinding binding)
    {
        if (binding is null) throw new ArgumentNullException(nameof(binding));
        Require(binding.SchemaVersion == BindingSchemaVersion, "Native evidence binding schema is unsupported.");
        Require(binding.GameVersion == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            "Native evidence binding game version is unsupported.");
        Require(!string.IsNullOrWhiteSpace(binding.SourceRunId) &&
                !string.IsNullOrWhiteSpace(binding.SourceSaveId) &&
                !string.IsNullOrWhiteSpace(binding.SourcePlayerId),
            "Native evidence binding requires explicit source run/save/player identities.");
        Require(string.IsNullOrEmpty(binding.GourmandContinuationReceiptPath) ==
                string.IsNullOrEmpty(binding.GourmandContinuationReceiptSha256),
            "Native continuation requires both its frozen path and hash.");
        VerifyHashes(binding);

        var schedule = ReadVerifiedSourceSchedule(
            Path.GetFullPath(binding.SourceSchedulePath), binding.SourceScheduleSha256.ToLowerInvariant(),
            binding.VerificationReceiptPath, binding.SourceRunId, binding.SourceSaveId, binding.SourcePlayerId);
        Require(schedule.GameVersion == binding.GameVersion, "Native evidence binding game version mismatch.");
        var continuation = string.IsNullOrEmpty(binding.GourmandContinuationReceiptPath) ? null :
            ReadVerifiedContinuation(binding.GourmandContinuationReceiptPath, binding.VerificationReceiptPath,
                schedule, binding.SourceRunId, binding.SourceSaveId, binding.SourcePlayerId, binding.GameVersion);

        VerifyHashes(binding);
        return new GoldenWalnutNativeEvidenceReplay(binding, schedule, continuation);
    }

    // These readers were the Master Angler audit's private source readers. Keep their
    // native replay and exact-content checks here so later consumers reuse one path.
    public static GoldenWalnutFreshSaveSourceSchedule ReadVerifiedSourceSchedule(
        string schedulePath, string scheduleSha256, string receiptPath,
        string? expectedRunId, string? expectedSaveId, string? expectedPlayerId)
    {
        Require(!string.IsNullOrWhiteSpace(expectedRunId) &&
                !string.IsNullOrWhiteSpace(expectedSaveId) && !string.IsNullOrWhiteSpace(expectedPlayerId),
            "Golden Walnut audit requires explicit expected run/save/player identities.");
        using var document = JsonDocument.Parse(File.ReadAllText(receiptPath));
        var receipt = document.RootElement;
        RequireUniqueProperties(receipt);
        string RequiredString(string name)
        {
            Require(receipt.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()),
                "Golden Walnut verification receipt is missing " + name + ".");
            return value.GetString()!;
        }
        Require(RequiredString("status") == "pass" &&
                receipt.TryGetProperty("training_label_eligible", out var eligible) &&
                eligible.ValueKind == JsonValueKind.False &&
                RequiredString("expected_player_id") == expectedPlayerId &&
                RequiredString("source_schedule_sha256") == scheduleSha256 &&
                Path.GetFullPath(RequiredString("source_schedule_path")) == schedulePath,
            "Golden Walnut verification receipt does not bind this schedule and player.");

        var schedule = GoldenWalnutFreshSaveSourceScheduleVerifier.Verify(
            RequiredString("episode_directory"), RequiredString("episode_manifest_path"), schedulePath,
            "golden-walnut-fresh-save:" + expectedRunId, expectedRunId!, expectedSaveId!, expectedPlayerId!);
        Require(receipt.TryGetProperty("schedule", out var claimed),
            "Golden Walnut verification receipt schedule missing.");
        RequireExactEvidence(JsonSerializer.SerializeToElement(schedule, JsonOptions), claimed);
        return schedule;
    }

    public static GoldenWalnutGourmandContinuationReceipt ReadVerifiedContinuation(
        string path, string sourceReceiptPath, GoldenWalnutFreshSaveSourceSchedule schedule,
        string runId, string saveId, string playerId, string gameVersion)
    {
        using var sourceDocument = JsonDocument.Parse(File.ReadAllText(sourceReceiptPath));
        var source = sourceDocument.RootElement;
        RequireUniqueProperties(source);
        using var claimedDocument = JsonDocument.Parse(File.ReadAllText(path));
        var claimed = claimedDocument.RootElement;
        RequireUniqueProperties(claimed);
        // The independent receipt selects the frozen artifacts, never the claimed continuation.
        var manifestPath = source.GetProperty("episode_manifest_path").GetString()!;
        var rebuilt = GoldenWalnutGourmandContinuationReceiptVerifier.Verify(
            source.GetProperty("episode_directory").GetString()!, manifestPath,
            schedule.SourceEpisodeManifestSha256, schedule.SourceEpisodeEvidenceRootSha256);
        RequireExactEvidence(JsonSerializer.SerializeToElement(rebuilt, JsonOptions), claimed);
        Require(rebuilt.SchemaVersion == GoldenWalnutGourmandContinuationReceiptVerifier.SchemaVersion &&
                rebuilt.SourceRunId == runId && rebuilt.SourceRunId == schedule.SourceRunId &&
                rebuilt.SaveId == saveId && rebuilt.SaveId == schedule.SourceSaveId &&
                rebuilt.PlayerId == playerId && rebuilt.GameVersion == gameVersion &&
                rebuilt.GameVersion == schedule.GameVersion &&
                rebuilt.SourceEpisodeManifestPath == Path.GetFullPath(manifestPath) &&
                rebuilt.SourceEpisodeManifestSha256 == schedule.SourceEpisodeManifestSha256 &&
                rebuilt.SourceEpisodeEvidenceRootSha256 == schedule.SourceEpisodeEvidenceRootSha256,
            "Gourmand continuation identity or frozen evidence disagrees with the verified source schedule.");
        return rebuilt;
    }

    private static void VerifyHashes(GoldenWalnutNativeEvidenceBinding binding)
    {
        VerifyFile(binding.SourceSchedulePath, binding.SourceScheduleSha256);
        VerifyFile(binding.VerificationReceiptPath, binding.VerificationReceiptSha256);
        if (!string.IsNullOrEmpty(binding.GourmandContinuationReceiptPath))
            VerifyFile(binding.GourmandContinuationReceiptPath, binding.GourmandContinuationReceiptSha256);
    }

    private static void VerifyFile(string path, string expectedHash)
    {
        Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path),
            "Native evidence binding requires an absolute frozen artifact path.");
        Require(expectedHash is { Length: 64 } && expectedHash.All(Uri.IsHexDigit),
            "Native evidence binding requires a SHA-256 for each declared artifact.");
        Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0,
            "Native evidence binding cannot reference a reparse artifact.");
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        Require(string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase),
            "Native evidence binding artifact hash mismatch: " + path);
    }

    private static void RequireUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = value.EnumerateObject().ToArray();
            Require(properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length,
                "Evidence contains duplicate properties.");
            foreach (var property in properties) RequireUniqueProperties(property.Value);
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RequireUniqueProperties(item);
    }

    private static void RequireExactEvidence(JsonElement expected, JsonElement claimed)
    {
        Require(expected.ValueKind == claimed.ValueKind, "Evidence content changed.");
        if (expected.ValueKind == JsonValueKind.Object)
        {
            Require(expected.EnumerateObject().Count() == claimed.EnumerateObject().Count(),
                "Evidence properties changed.");
            foreach (var property in expected.EnumerateObject())
            {
                Require(claimed.TryGetProperty(property.Name, out var value), "Evidence missing " + property.Name + ".");
                RequireExactEvidence(property.Value, value);
            }
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            Require(expected.GetArrayLength() == claimed.GetArrayLength(), "Evidence array changed.");
            for (var index = 0; index < expected.GetArrayLength(); index++)
                RequireExactEvidence(expected[index], claimed[index]);
        }
        else
            Require(expected.ValueKind == JsonValueKind.String ? expected.GetString() == claimed.GetString() :
                expected.GetRawText() == claimed.GetRawText(), "Evidence value changed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
