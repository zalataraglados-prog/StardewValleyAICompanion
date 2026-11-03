using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MasterAnglerSharedCapabilityCertificate(
    [property: JsonPropertyName("capability_id")] string CapabilityId,
    [property: JsonPropertyName("consumer_direction_id")] string ConsumerDirectionId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("source_snapshot_path")] string SourceSnapshotPath,
    [property: JsonPropertyName("source_snapshot_sha256")] string SourceSnapshotSha256,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("source_game_tick")] long SourceGameTick,
    [property: JsonPropertyName("selected_route_id")] string SelectedRouteId,
    [property: JsonPropertyName("verified_preconditions")] string[] VerifiedPreconditions,
    [property: JsonPropertyName("verified_results")] string[] VerifiedResults,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons)
{
    public const string Schema = "master_angler_shared_capability_certificate.v1";

    [JsonPropertyName("schema_version")] public string SchemaVersion => Schema;
    [JsonPropertyName("ready")] public bool Ready => BlockingReasons is { Length: 0 };
    [JsonPropertyName("training_label_eligible")] public bool TrainingLabelEligible => false;
}

public static class MasterAnglerSharedCapabilityCertificateBuilder
{
    public const string GingerIslandAccessCapabilityId = "ginger_island_access";
    public const string ConsumerDirectionId = "complete_master_angler";
    public const string CurrentStateRouteId = "current_native_access_state";

    private static readonly (string ProjectionField, string LocationId)[] RequiredTargets =
    {
        ("island_south_accessible", "IslandSouth"),
        ("island_north_accessible", "IslandNorth"),
        ("island_west_accessible", "IslandWest"),
        ("island_south_east_accessible", "IslandSouthEast"),
        ("island_south_east_cave_accessible", "IslandSouthEastCave")
    };

    public static MasterAnglerSharedCapabilityCertificate BuildGingerIslandAccess(
        string snapshotPath,
        string runId)
    {
        Require(!string.IsNullOrWhiteSpace(runId), "A run identity is required.");
        var fullPath = Path.GetFullPath(snapshotPath);
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            fullPath,
            "Master Angler shared-capability snapshot");
        Require(snapshot.SchemaVersion == "snapshot.v1",
            "Shared-capability snapshot schema mismatch.");
        Require(snapshot.GameVersion == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            "Shared-capability snapshot game version mismatch.");
        Require(FieldEnvelopeValidator.IsReadableStatus(snapshot.SaveId.Status) &&
                !string.IsNullOrWhiteSpace(snapshot.SaveId.Value) &&
                FieldEnvelopeValidator.IsReadableStatus(snapshot.PlayerId.Status) &&
                !string.IsNullOrWhiteSpace(snapshot.PlayerId.Value),
            "Shared-capability snapshot identity is unavailable.");
        Require(IsSha256(snapshot.StateHash) &&
                SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash),
            "Shared-capability snapshot state hash is invalid or does not match its state.");

        var blockers = new List<string>();
        var verifiedPreconditions = new List<string>
        {
            "snapshot_identity_readable",
            "snapshot_state_hash_verified"
        };
        var verifiedResults = new List<string>();
        if (!TryReadAvailableFieldValue(snapshot, "locations", GingerIslandAccessCapabilityId,
                out var access))
        {
            blockers.Add("ginger_island_access_projection_unavailable");
        }
        else if (access.ValueKind != JsonValueKind.Object ||
                 ReadString(access, "schema_version") != "ginger_island_access.v1" ||
                 ReadString(access, "projection_status") !=
                    "complete_locked_base_1.6.15_native_contract")
        {
            blockers.Add("ginger_island_access_projection_invalid");
        }
        else if (!access.TryGetProperty("target_access", out var targetAccess) ||
                 targetAccess.ValueKind != JsonValueKind.Object)
        {
            blockers.Add("ginger_island_access_targets_unavailable");
        }
        else
        {
            verifiedPreconditions.Add(
                "ginger_island_access_projection_locked_base_1.6.15");
            foreach (var (field, location) in RequiredTargets)
            {
                if (!targetAccess.TryGetProperty(field, out var value) ||
                    value.ValueKind != JsonValueKind.True)
                {
                    blockers.Add("ginger_island_target_unavailable:" + location);
                }
                else
                    verifiedResults.Add(location);
            }
        }

        return new MasterAnglerSharedCapabilityCertificate(
            GingerIslandAccessCapabilityId,
            ConsumerDirectionId,
            snapshot.GameVersion,
            runId,
            snapshot.SaveId.Value!,
            snapshot.PlayerId.Value!,
            fullPath,
            CurrentTeacherFrontierSupport.HashFile(fullPath),
            snapshot.StateHash,
            snapshot.GameTick,
            CurrentStateRouteId,
            verifiedPreconditions.ToArray(),
            verifiedResults.ToArray(),
            blockers.ToArray());
    }

    public static MasterAnglerSharedCapabilityCertificate ReadAndReplay(
        string certificatePath)
    {
        var fullPath = Path.GetFullPath(certificatePath);
        var bytes = File.ReadAllBytes(fullPath);
        using var document = JsonDocument.Parse(bytes);
        RequireUniqueProperties(document.RootElement);
        var claimed = document.RootElement.Deserialize<MasterAnglerSharedCapabilityCertificate>() ??
            throw new InvalidDataException("Shared-capability certificate is null.");
        Require(claimed.SchemaVersion == MasterAnglerSharedCapabilityCertificate.Schema &&
                claimed.CapabilityId == GingerIslandAccessCapabilityId &&
                claimed.ConsumerDirectionId == ConsumerDirectionId &&
                claimed.SelectedRouteId == CurrentStateRouteId &&
                !claimed.TrainingLabelEligible,
            "Shared-capability certificate scope or version mismatch.");
        var rebuilt = BuildGingerIslandAccess(claimed.SourceSnapshotPath, claimed.RunId);
        Require(string.Equals(
                    CurrentTeacherFrontierSupport.HashFile(claimed.SourceSnapshotPath),
                    claimed.SourceSnapshotSha256,
                    StringComparison.OrdinalIgnoreCase),
            "Shared-capability source snapshot SHA256 mismatch.");
        Require(System.Text.Json.Nodes.JsonNode.DeepEquals(
                JsonSerializer.SerializeToNode(rebuilt),
                System.Text.Json.Nodes.JsonNode.Parse(bytes)),
            "Shared-capability certificate does not match an independent replay.");
        return rebuilt;
    }

    private static bool TryReadAvailableFieldValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            FieldEnvelopeValidator.IsReadableStatus(status.GetString()) &&
            envelope.TryGetProperty("value", out value);
    }

    private static string ReadString(JsonElement source, string name) =>
        source.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void RequireUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Require(names.Add(property.Name),
                    "Evidence contains duplicate properties.");
                RequireUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                RequireUniqueProperties(item);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
