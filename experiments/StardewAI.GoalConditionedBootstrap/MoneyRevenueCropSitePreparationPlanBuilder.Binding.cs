using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSitePreparationPlanBuilder
{
    private static MoneyRevenueCropSitePreparationStep BuildStep(
        MoneyRevenueCropPlantingTile tile,
        JsonElement contextTiles,
        EventCandidate[] clearCandidates,
        EventCandidate[] clumpCandidates,
        EventCandidate[] tillCandidates)
    {
        var rows = contextTiles.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == tile.TileX &&
                ReadInt(row, "tile_y") == tile.TileY)
            .ToArray();
        if (rows.Length != 1)
        {
            return Blocked(
                tile,
                "missing",
                "missing_or_duplicated_site_preparation_tile");
        }

        var row = rows[0];
        var observedStatus = ReadString(row, "status");
        if (observedStatus == "already_prepared")
        {
            return new MoneyRevenueCropSitePreparationStep(
                tile.TileX,
                tile.TileY,
                observedStatus,
                "already_prepared",
                string.Empty,
                string.Empty,
                Array.Empty<SmallModelActionParameter>(),
                "planting_layout_proof",
                Array.Empty<string>());
        }

        if (observedStatus == "ready_to_till")
        {
            return BoundStep(
                tile,
                observedStatus,
                SingleTileCandidate(tillCandidates, tile.TileX, tile.TileY),
                "executor.till_soil",
                "direct_till_ready",
                "fresh_snapshot_then_planting_layout_proof");
        }

        if (observedStatus != "requires_single_clearance_then_till")
            return Blocked(tile, observedStatus, observedStatus);
        if (!row.TryGetProperty("clearable_blocker_kinds", out var blockerKinds) ||
            blockerKinds.ValueKind != JsonValueKind.Array)
        {
            return Blocked(
                tile,
                observedStatus,
                "clearable_blocker_identity_missing");
        }

        var kinds = blockerKinds.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (kinds.Length != 1)
        {
            return Blocked(
                tile,
                observedStatus,
                "clearable_blocker_identity_not_unique");
        }

        if (kinds[0] is "object" or "terrain_feature")
        {
            return BoundStep(
                tile,
                observedStatus,
                SingleTileCandidate(
                    clearCandidates,
                    tile.TileX,
                    tile.TileY),
                "executor.clear_obstacle",
                "clear_then_fresh_snapshot_rebind",
                "fresh_snapshot_rebind_executor.till_soil");
        }
        if (kinds[0] == "resource_clump")
        {
            var candidates = clumpCandidates.Where(candidate =>
                    CandidateClumpContains(candidate, tile.TileX, tile.TileY))
                .ToArray();
            return BoundStep(
                tile,
                observedStatus,
                candidates.Length == 1 ? candidates[0] : null,
                "executor.break_farm_resource_clump",
                "clear_then_fresh_snapshot_rebind",
                "fresh_snapshot_rebind_executor.till_soil");
        }
        return Blocked(
            tile,
            observedStatus,
            "unsupported_site_preparation_blocker_kind");
    }

    private static MoneyRevenueCropSitePreparationStep BoundStep(
        MoneyRevenueCropPlantingTile tile,
        string observedStatus,
        EventCandidate? candidate,
        string optionId,
        string readyStatus,
        string continuation)
    {
        if (candidate is null)
        {
            return Blocked(
                tile,
                observedStatus,
                "required_site_preparation_candidate_missing");
        }
        if (!candidate.Available)
        {
            return new MoneyRevenueCropSitePreparationStep(
                tile.TileX,
                tile.TileY,
                observedStatus,
                "blocked",
                optionId,
                candidate.CandidateId,
                candidate.Parameters,
                continuation,
                candidate.BlockReasons.Length > 0
                    ? candidate.BlockReasons
                    : new[] { "required_site_preparation_candidate_unavailable" });
        }
        return new MoneyRevenueCropSitePreparationStep(
            tile.TileX,
            tile.TileY,
            observedStatus,
            readyStatus,
            optionId,
            candidate.CandidateId,
            candidate.Parameters,
            continuation,
            Array.Empty<string>());
    }

    private static MoneyRevenueCropSitePreparationStep Blocked(
        MoneyRevenueCropPlantingTile tile,
        string observedStatus,
        string reason) => new(
            tile.TileX,
            tile.TileY,
            observedStatus,
            "blocked",
            string.Empty,
            string.Empty,
            Array.Empty<SmallModelActionParameter>(),
            "manual_layout_reselection_or_fresh_snapshot_required",
            new[] { reason });

    private static EventCandidate? SingleTileCandidate(
        EventCandidate[] candidates,
        int x,
        int y)
    {
        var matches = candidates.Where(candidate =>
                candidate.TileX == x && candidate.TileY == y)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool CandidateClumpContains(
        EventCandidate candidate,
        int x,
        int y)
    {
        var anchorX = ReadParameterInt(
            candidate.Parameters,
            "resource_clump_tile_x");
        var anchorY = ReadParameterInt(
            candidate.Parameters,
            "resource_clump_tile_y");
        var width = ReadParameterInt(
            candidate.Parameters,
            "resource_clump_width");
        var height = ReadParameterInt(
            candidate.Parameters,
            "resource_clump_height");
        return anchorX.HasValue && anchorY.HasValue && width > 0 && height > 0 &&
            x >= anchorX && x < anchorX + width &&
            y >= anchorY && y < anchorY + height;
    }

    private static int? ReadParameterInt(
        SmallModelActionParameter[] parameters,
        string name)
    {
        var value = parameters.FirstOrDefault(parameter =>
            parameter.Name == name)?.Value;
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static EventCandidate[] Candidates(
        OptionAvailability[] options,
        string optionId) => options.Single(option =>
            option.OptionId == optionId).EventCandidates;
}
