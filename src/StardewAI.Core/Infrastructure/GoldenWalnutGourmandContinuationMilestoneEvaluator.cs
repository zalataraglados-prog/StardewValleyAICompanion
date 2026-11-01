using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutGourmandContinuationMilestoneAssessment(
    bool Ready,
    int RequestsFulfilled,
    int CollectedGourmandWalnutQuantity,
    string[] BlockingReasons);

/// <summary>Transparent terminal state only; not episode continuity or training admission.</summary>
public static class GoldenWalnutGourmandContinuationMilestoneEvaluator
{
    private static readonly string[] SourceKeys =
        { "IslandGourmand1", "IslandGourmand2", "IslandGourmand3" };

    public static GoldenWalnutGourmandContinuationMilestoneAssessment Evaluate(SnapshotEnvelope snapshot)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var blockers = new List<string>();
        if (snapshot.GameVersion != GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion)
            blockers.Add("game_version_not_locked_1_6_15");
        if (snapshot.SaveId is null || snapshot.SaveId.Status != FieldStatus.Available ||
            string.IsNullOrWhiteSpace(snapshot.SaveId.Value))
            blockers.Add("save_identity_unavailable");
        if (snapshot.PlayerId is null || snapshot.PlayerId.Status != FieldStatus.Available ||
            string.IsNullOrWhiteSpace(snapshot.PlayerId.Value))
            blockers.Add("player_identity_unavailable");

        var requests = -1;
        var progress = ReadProjection<IslandGourmandProgressRef>(snapshot, "island_gourmand",
            "gourmand_progress", blockers, out var progressJson);
        if (progress is not null)
        {
            requests = progress.RequestsFulfilled;
            if (!HasProperties(progressJson, "schema_version", "projection_status") ||
                progress.SchemaVersion != "island_gourmand_progress.v1" ||
                progress.ProjectionStatus != "complete_native_three_requests")
                blockers.Add("gourmand_progress_identity_invalid");
            if (!HasProperties(progressJson, "required_mail_satisfied", "requests_fulfilled", "total_requests",
                    "complete", "active_request_index", "request_state", "required_crop_item_id",
                    "required_crop_qualified_item_id", "required_crop_display_name") ||
                !progress.RequiredMailSatisfied || progress.RequestsFulfilled != 3 || progress.TotalRequests != 3 ||
                !progress.Complete || progress.ActiveRequestIndex.HasValue || progress.RequestState != "complete" ||
                progress.RequiredCropItemId != string.Empty || progress.RequiredCropQualifiedItemId != string.Empty ||
                progress.RequiredCropDisplayName != string.Empty)
                blockers.Add("gourmand_requests_incomplete_or_contradictory");
        }

        var catalog = ReadProjection<GoldenWalnutSourceCatalogRef>(snapshot, "golden_walnut_sources",
            "golden_walnut_source_catalog", blockers, out var catalogJson);
        var collected = catalog is null ? 0 : ValidateSources(catalog, catalogJson, blockers);
        if (collected != 15)
            blockers.Add("gourmand_walnut_quantity_incomplete");
        return new GoldenWalnutGourmandContinuationMilestoneAssessment(
            blockers.Count == 0 && collected == 15, requests, collected, blockers.ToArray());
    }

    private static int ValidateSources(GoldenWalnutSourceCatalogRef catalog, JsonElement catalogJson,
        ICollection<string> blockers)
    {
        if (catalog.SchemaVersion != GoldenWalnutNativeSourceCatalog.SchemaVersion ||
            catalog.ProjectionStatus != GoldenWalnutNativeSourceCatalog.ProjectionStatus ||
            catalog.NativeSourceKeyCount != GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount ||
            catalog.NativeWalnutTotal != GoldenWalnutNativeSourceCatalog.NativeWalnutTotal)
        {
            blockers.Add("golden_walnut_source_catalog_identity_invalid");
            return 0;
        }

        var rows = catalog.Sources ?? Array.Empty<GoldenWalnutSourceProgressRef>();
        ValidateCatalogRowsAndTotals(catalog, catalogJson, rows, blockers);
        var collected = 0;
        foreach (var key in SourceKeys)
        {
            var matches = rows.Where(row => row is not null && row.SourceKey == key).ToArray();
            if (matches.Length != 1)
            {
                blockers.Add("gourmand_source_missing_or_duplicate:" + key);
                continue;
            }
            var row = matches[0];
            var rawRow = catalogJson.GetProperty("sources").EnumerateArray().Single(value =>
                value.ValueKind == JsonValueKind.Object && value.TryGetProperty("source_key", out var sourceKey) &&
                sourceKey.ValueKind == JsonValueKind.String && sourceKey.GetString() == key);
            var definition = GoldenWalnutNativeSourceCatalog.Sources.Single(source => source.SourceKey == key);
            if (!HasProperties(rawRow, "tracking_kind", "location_id", "tile_x", "tile_y", "native_quantity",
                    "acquisition_option_id", "probability_status", "access_phase", "required_mail_key",
                    "required_mail_satisfied", "resort_only", "optional_unlock_walnut_cost", "required_secret_note_id") ||
                row.TrackingKind != definition.TrackingKind || row.LocationId != definition.LocationId ||
                row.TileX != definition.TileX || row.TileY != definition.TileY ||
                row.NativeQuantity != definition.NativeQuantity || row.NativeQuantity != 5 ||
                row.AcquisitionOptionId != definition.AcquisitionOptionId ||
                row.ProbabilityStatus != definition.ProbabilityStatus ||
                row.AccessPhase != definition.AccessPhase || row.RequiredMailKey != definition.RequiredMailKey ||
                !row.RequiredMailSatisfied || row.ResortOnly != definition.ResortOnly ||
                row.OptionalUnlockWalnutCost != definition.OptionalUnlockWalnutCost ||
                row.RequiredSecretNoteId != definition.RequiredSecretNoteId)
            {
                blockers.Add("gourmand_source_identity_invalid:" + key);
                continue;
            }
            // A missing zero-valued remainder must not deserialize into completion.
            if (!HasProperties(rawRow, "remaining_quantity") || row.CollectedQuantity != 5 || row.RemainingQuantity != 0)
            {
                blockers.Add("gourmand_source_quantity_incomplete:" + key);
                continue;
            }
            collected += row.CollectedQuantity;
        }
        return collected;
    }

    private static void ValidateCatalogRowsAndTotals(GoldenWalnutSourceCatalogRef catalog, JsonElement catalogJson,
        GoldenWalnutSourceProgressRef[] rows, ICollection<string> blockers)
    {
        var expected = GoldenWalnutNativeSourceCatalog.Sources.ToDictionary(source => source.SourceKey, StringComparer.Ordinal);
        var rowsExact = catalogJson.TryGetProperty("sources", out var rawRows) &&
            rawRows.ValueKind == JsonValueKind.Array &&
            rawRows.GetArrayLength() == expected.Count && rows.Length == expected.Count &&
            rawRows.EnumerateArray().All(row => row.ValueKind == JsonValueKind.Object &&
                HasProperties(row, "native_quantity", "collected_quantity", "remaining_quantity")) &&
            rows.All(row => row is not null && !string.IsNullOrWhiteSpace(row.SourceKey) &&
                expected.TryGetValue(row.SourceKey, out var definition) &&
                row.NativeQuantity == definition.NativeQuantity &&
                row.CollectedQuantity >= 0 && row.CollectedQuantity <= row.NativeQuantity &&
                row.RemainingQuantity == row.NativeQuantity - row.CollectedQuantity) &&
            rows.Select(row => row.SourceKey).Distinct(StringComparer.Ordinal).Count() == expected.Count;
        if (!rowsExact)
        {
            blockers.Add("golden_walnut_source_catalog_rows_invalid");
            return;
        }
        if (!HasProperties(catalogJson, "tracked_collected_quantity", "tracked_remaining_quantity") ||
            catalog.TrackedCollectedQuantity != rows.Sum(row => row.CollectedQuantity) ||
            catalog.TrackedRemainingQuantity != rows.Sum(row => row.RemainingQuantity))
            blockers.Add("golden_walnut_source_catalog_totals_invalid");
    }

    private static T? ReadProjection<T>(SnapshotEnvelope snapshot, string key, string blockerPrefix,
        ICollection<string> blockers, out JsonElement value) where T : class
    {
        value = default;
        if (snapshot.State is null || !snapshot.State.TryGetValue("world_progress", out var section) ||
            section.ValueKind != JsonValueKind.Object || !section.TryGetProperty(key, out var field) ||
            field.ValueKind != JsonValueKind.Object || !field.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String || status.GetString() != "available" ||
            !field.TryGetProperty("value", out value))
        {
            blockers.Add(blockerPrefix + "_unavailable");
            return null;
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            try
            {
                var projection = JsonSerializer.Deserialize<T>(value.GetRawText());
                if (projection is not null)
                    return projection;
            }
            catch (JsonException)
            {
                // An incomplete/malformed projection is an ordinary blocked state.
            }
        }
        blockers.Add(blockerPrefix + "_unreadable");
        return null;
    }

    private static bool HasProperties(JsonElement value, params string[] names) =>
        names.All(name => value.TryGetProperty(name, out _));
}
