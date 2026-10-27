using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutFreshSaveMilestoneAssessment(
    bool Ready,
    int CurrentTotalDay,
    int TrackedCollectedQuantity,
    bool IslandReached,
    bool FirstParrotComplete,
    bool WesternTurtleComplete,
    bool IslandHouseComplete,
    string[] BlockingReasons);

public static class GoldenWalnutFreshSaveMilestoneEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static GoldenWalnutFreshSaveMilestoneAssessment Evaluate(
        SnapshotEnvelope snapshot)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var blockers = new List<string>();
        if (snapshot.GameVersion !=
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion)
        {
            blockers.Add("game_version_not_locked_1_6_15");
        }
        if (snapshot.SaveId.Status != FieldStatus.Available ||
            string.IsNullOrWhiteSpace(snapshot.SaveId.Value))
        {
            blockers.Add("save_identity_unavailable");
        }

        var currentTotalDay = -1;
        if (!TryReadAvailableIntField(
                snapshot,
                "time",
                "total_days",
                out currentTotalDay))
        {
            blockers.Add("total_day_unavailable");
        }
        else if (currentTotalDay >=
            GoldenWalnutUnlockScheduleProofBuilder.DeadlineTotalDayExclusive)
        {
            blockers.Add("golden_walnut_deadline_total_day_reached");
        }

        var islandReached = false;
        var firstParrot = false;
        var westernTurtle = false;
        var islandHouse = false;
        if (TryReadAvailableFieldValue(
                snapshot,
                "locations",
                "ginger_island_access",
                out var access))
        {
            if (!TryReadString(access, "schema_version", out var accessSchema) ||
                accessSchema != "ginger_island_access.v1")
            {
                blockers.Add("ginger_island_access_schema_invalid");
            }
            if (!TryReadString(access, "projection_status", out var projection) ||
                projection != "complete_locked_base_1.6.15_native_contract")
            {
                blockers.Add("ginger_island_access_projection_unlocked");
            }
            islandReached = ReadMilestoneBool(
                access,
                new[] { "journey", "island_reached" },
                "first_island_arrival_unobserved",
                blockers);
            firstParrot = ReadMilestoneBool(
                access,
                new[] { "first_parrot", "complete_or_pending" },
                "first_parrot_incomplete",
                blockers);
            westernTurtle = ReadMilestoneBool(
                access,
                new[] { "western_turtle_parrot", "complete_or_pending" },
                "western_turtle_incomplete",
                blockers);
            islandHouse = ReadMilestoneBool(
                access,
                new[] { "island_house_parrot", "complete_or_pending" },
                "island_house_incomplete",
                blockers);
        }
        else
        {
            blockers.Add("ginger_island_access_unavailable");
        }

        var trackedCollectedQuantity = 0;
        if (TryReadAvailableFieldValue(
                snapshot,
                "world_progress",
                "golden_walnut_sources",
                out var sourcesValue))
        {
            GoldenWalnutSourceCatalogRef? catalog = null;
            try
            {
                catalog = JsonSerializer.Deserialize<GoldenWalnutSourceCatalogRef>(
                    sourcesValue.GetRawText(),
                    JsonOptions);
            }
            catch (JsonException)
            {
                // The blocker below is the stable external failure contract.
            }

            if (catalog is null)
            {
                blockers.Add("golden_walnut_source_catalog_unreadable");
            }
            else
            {
                trackedCollectedQuantity = ValidateSourceCatalog(catalog, blockers);
            }
        }
        else
        {
            blockers.Add("golden_walnut_source_catalog_unavailable");
        }

        return new GoldenWalnutFreshSaveMilestoneAssessment(
            blockers.Count == 0,
            currentTotalDay,
            trackedCollectedQuantity,
            islandReached,
            firstParrot,
            westernTurtle,
            islandHouse,
            blockers.ToArray());
    }

    private static int ValidateSourceCatalog(
        GoldenWalnutSourceCatalogRef catalog,
        ICollection<string> blockers)
    {
        if (catalog.SchemaVersion != GoldenWalnutNativeSourceCatalog.SchemaVersion ||
            catalog.ProjectionStatus != GoldenWalnutNativeSourceCatalog.ProjectionStatus ||
            catalog.NativeSourceKeyCount != GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount ||
            catalog.NativeWalnutTotal != GoldenWalnutNativeSourceCatalog.NativeWalnutTotal)
        {
            blockers.Add("golden_walnut_source_catalog_identity_invalid");
        }

        var rows = catalog.Sources ?? Array.Empty<GoldenWalnutSourceProgressRef>();
        var duplicateOrEmptyKey = rows
            .GroupBy(row => row.SourceKey, StringComparer.Ordinal)
            .Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() != 1);
        var expected = GoldenWalnutNativeSourceCatalog.Sources
            .ToDictionary(row => row.SourceKey, StringComparer.Ordinal);
        var actual = duplicateOrEmptyKey
            ? new Dictionary<string, GoldenWalnutSourceProgressRef>(StringComparer.Ordinal)
            : rows.ToDictionary(row => row.SourceKey, StringComparer.Ordinal);
        var rowsExact = !duplicateOrEmptyKey &&
            actual.Count == GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount &&
            expected.Count == actual.Count &&
            expected.All(pair =>
                actual.TryGetValue(pair.Key, out var row) &&
                row.NativeQuantity == pair.Value.NativeQuantity &&
                row.CollectedQuantity >= 0 &&
                row.CollectedQuantity <= pair.Value.NativeQuantity);
        if (!rowsExact)
            blockers.Add("golden_walnut_source_catalog_rows_invalid");

        var collected = rowsExact
            ? actual.Values.Sum(row => row.CollectedQuantity)
            : 0;
        var remaining = rowsExact
            ? actual.Values.Sum(row => row.NativeQuantity - row.CollectedQuantity)
            : GoldenWalnutNativeSourceCatalog.NativeWalnutTotal;
        if (rowsExact &&
            (catalog.TrackedCollectedQuantity != collected ||
             catalog.TrackedRemainingQuantity != remaining))
        {
            blockers.Add("golden_walnut_source_catalog_totals_invalid");
        }
        if (collected <= 0)
            blockers.Add("golden_walnut_source_increment_unobserved");
        return collected;
    }

    private static bool TryReadAvailableFieldValue(
        SnapshotEnvelope snapshot,
        string domain,
        string key,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(domain, out var section) &&
            section.ValueKind == JsonValueKind.Object &&
            section.TryGetProperty(key, out var field) &&
            field.ValueKind == JsonValueKind.Object &&
            field.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() == "available" &&
            field.TryGetProperty("value", out value) &&
            value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryReadAvailableIntField(
        SnapshotEnvelope snapshot,
        string domain,
        string key,
        out int value)
    {
        value = -1;
        return snapshot.State.TryGetValue(domain, out var section) &&
            section.ValueKind == JsonValueKind.Object &&
            section.TryGetProperty(key, out var field) &&
            field.ValueKind == JsonValueKind.Object &&
            field.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() == "available" &&
            field.TryGetProperty("value", out var rawValue) &&
            rawValue.ValueKind == JsonValueKind.Number &&
            rawValue.TryGetInt32(out value);
    }

    private static bool ReadMilestoneBool(
        JsonElement root,
        IReadOnlyList<string> path,
        string incompleteReason,
        ICollection<string> blockers)
    {
        var cursor = root;
        foreach (var segment in path)
        {
            if (cursor.ValueKind != JsonValueKind.Object ||
                !cursor.TryGetProperty(segment, out cursor))
            {
                blockers.Add(incompleteReason + "_field_missing");
                return false;
            }
        }
        if (cursor.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            blockers.Add(incompleteReason + "_field_invalid");
            return false;
        }
        if (!cursor.GetBoolean())
        {
            blockers.Add(incompleteReason);
            return false;
        }
        return true;
    }

    private static bool TryReadString(
        JsonElement root,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = element.GetString() ?? string.Empty;
        return true;
    }
}
