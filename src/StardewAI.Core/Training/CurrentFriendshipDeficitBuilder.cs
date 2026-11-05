using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed class CurrentFriendshipDeficitBuilder
{
    // Live 1.6.15 Utility.getGrandpaScore/getNumberOfFriendsWithinThisRange.
    private const int MinimumPoints = 1975;
    private const int MaximumPoints = 999999;

    public CurrentFriendshipDeficit Build(SnapshotEnvelope snapshot)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.SchemaVersion != "snapshot.v1" ||
            snapshot.GameVersion != "1.6.15" || snapshot.GameTick < 0)
            return Blocked("friendship_deficit_snapshot_version_or_tick_unsupported");
        if (snapshot.State is null ||
            !SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash))
            return Blocked("friendship_deficit_state_hash_mismatch");
        if (!snapshot.State.TryGetValue("npcs", out var section) ||
            !Property(section, "grandpa_friendship_progress", out var envelope) ||
            !Property(envelope, "status", out var status) ||
            status.ValueKind != JsonValueKind.String || !ReadableStatus(status.GetString()) ||
            !Property(envelope, "value", out var progress))
            return Blocked("friendship_deficit_source_unreadable");
        if (!Integer(progress, "threshold_points", out var minimum) || minimum != MinimumPoints ||
            !Integer(progress, "maximum_points", out var maximum) || maximum != MaximumPoints ||
            !Boolean(progress, "romance_only", out var romanceOnly) || romanceOnly ||
            !Property(progress, "projection_status", out var projection) ||
            projection.ValueKind != JsonValueKind.String ||
            projection.GetString() != "complete_live_native_iteration" ||
            !Integer(progress, "eligible_villager_count", out var eligibleCount) || eligibleCount < 0 ||
            !Integer(progress, "qualifying_count", out var qualifyingCount) || qualifyingCount < 0 ||
            !Property(progress, "eligible_villager_rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != eligibleCount)
            return Blocked("friendship_deficit_source_shape_invalid");

        var villagers = new List<CurrentFriendshipDeficitRow>();
        var identities = new Dictionary<string, CurrentFriendshipDeficitRow>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            if (!TryReadRow(row, villagers.Count, out var projected))
                return Blocked("friendship_deficit_villager_row_invalid");
            if (identities.TryGetValue(projected.NpcName, out var earlier))
            {
                // Native scoring counts instances, but the farmer dictionary has one value per name.
                if (earlier.FriendshipRowExists != projected.FriendshipRowExists ||
                    earlier.FriendshipPoints != projected.FriendshipPoints)
                    return Blocked("friendship_deficit_duplicate_identity_conflict");
                earlier.UniqueNpcIdentity = false;
                projected.UniqueNpcIdentity = false;
                duplicates.Add(projected.NpcName);
            }
            else
                identities.Add(projected.NpcName, projected);
            villagers.Add(projected);
        }
        if (villagers.Count(row => row.Qualifies) != qualifyingCount)
            return Blocked("friendship_deficit_qualifying_count_inconsistent");

        return new CurrentFriendshipDeficit
        {
            Status = "ready_current_state_only",
            SourceStateHash = snapshot.StateHash,
            GameTick = snapshot.GameTick,
            EligibleVillagerCount = eligibleCount,
            QualifyingVillagerCount = qualifyingCount,
            VillagersMissingForFive = Math.Max(0, 5 - qualifyingCount),
            VillagersMissingForTen = Math.Max(0, 10 - qualifyingCount),
            Villagers = villagers.ToArray(),
            DuplicateNpcNames = duplicates.OrderBy(name => name, StringComparer.Ordinal).ToArray()
        };
    }

    private static bool TryReadRow(
        JsonElement row, int expectedIndex, out CurrentFriendshipDeficitRow projected)
    {
        projected = new CurrentFriendshipDeficitRow();
        if (!Integer(row, "native_iteration_index", out var index) || index != expectedIndex ||
            !Property(row, "npc_name", out var name) || name.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(name.GetString()) ||
            !Boolean(row, "is_villager", out var villager) || !villager ||
            !Boolean(row, "event_actor", out var eventActor) || eventActor ||
            !Boolean(row, "friendship_row_exists", out var exists) ||
            !Property(row, "friendship_points", out var points) ||
            !Boolean(row, "qualifies", out var qualifies))
            return false;

        int? friendshipPoints = null;
        if (exists)
        {
            if (points.ValueKind != JsonValueKind.Number || !points.TryGetInt32(out var value))
                return false;
            friendshipPoints = value;
        }
        else if (points.ValueKind != JsonValueKind.Null)
            return false;
        var nativeQualifies = friendshipPoints.HasValue &&
            friendshipPoints.Value >= MinimumPoints && friendshipPoints.Value <= MaximumPoints;
        if (qualifies != nativeQualifies)
            return false;

        projected = new CurrentFriendshipDeficitRow
        {
            NativeIterationIndex = index,
            NpcName = name.GetString()!,
            UniqueNpcIdentity = true,
            FriendshipRowExists = exists,
            FriendshipPoints = friendshipPoints,
            Qualifies = qualifies,
            // Absent records and points above the native upper bound have no additive deficit.
            PointsMissingToScoringRange = friendshipPoints.HasValue && friendshipPoints.Value <= MaximumPoints
                ? Math.Max(0L, (long)MinimumPoints - friendshipPoints.Value) : null
        };
        return true;
    }

    private static bool Property(JsonElement value, string name, out JsonElement property)
    {
        property = default;
        if (value.ValueKind != JsonValueKind.Object)
            return false;
        var found = false;
        foreach (var item in value.EnumerateObject())
        {
            if (!item.NameEquals(name))
                continue;
            if (found)
                return false;
            property = item.Value;
            found = true;
        }
        return found;
    }

    private static bool Integer(JsonElement value, string name, out int number)
    {
        number = 0;
        return Property(value, name, out var property) &&
            property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out number);
    }

    private static bool Boolean(JsonElement value, string name, out bool result)
    {
        result = false;
        if (!Property(value, name, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        result = property.GetBoolean();
        return true;
    }

    private static CurrentFriendshipDeficit Blocked(string reason) => new()
    {
        BlockingReasons = new[] { reason }
    };
}
