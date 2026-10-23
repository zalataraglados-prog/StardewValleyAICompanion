using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string GourmandCompilerNativeContract =
        "IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward";

    private static CompiledActionStep[] CompileAdvanceGourmandRequestStep(SmallModelAction action)
    {
        var stage = ReadParameter(action, "gourmand_interaction_stage");
        var before = ReadIntParameter(action, "request_index_before");
        var after = ReadIntParameter(action, "request_index_after");
        if (string.IsNullOrWhiteSpace(stage) || !before.HasValue || !after.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "advance_gourmand_request",
                "island-gourmand:" + before.Value + ":" + stage,
                "world_progress.island_gourmand.requests_fulfilled=" + after.Value +
                ";talked_to_gourmand_received_or_pending=true" +
                ";collected_nut_key=" + ReadParameter(action, "expected_collected_nut_key") +
                ";walnut_debris_spawn_count=" + ReadParameter(action, "expected_walnut_debris_spawn_count"),
                stage == "complete_request" ? 1800 : 600)
        };
    }

    private static string[] ValidateGourmandRequestPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.advance_gourmand_request")
            return Array.Empty<string>();

        var actionX = ReadIntParameter(action, "target_tile_x");
        var actionY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var before = ReadIntParameter(action, "request_index_before");
        var after = ReadIntParameter(action, "request_index_after");
        var readyCount = ReadIntParameter(action, "ready_matching_crop_count");
        var walnutCount = ReadIntParameter(action, "expected_walnut_debris_spawn_count");
        var stage = ReadParameter(action, "gourmand_interaction_stage");
        var cropItemId = ReadParameter(action, "required_crop_item_id");
        var qualifiedCrop = ReadParameter(action, "required_crop_qualified_item_id");
        var completing = stage == "complete_request";
        if (!actionX.HasValue || !actionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1 ||
            !before.HasValue || before is < 0 or > 2 || !after.HasValue ||
            after.Value != (completing ? before.Value + 1 : before.Value) ||
            !readyCount.HasValue || readyCount < 1 || !walnutCount.HasValue ||
            walnutCount.Value != (completing ? 5 : 0) ||
            (stage != "introduce_request" && !completing) ||
            !GourmandCompilerCropMatchesRequest(before.Value, cropItemId) ||
            qualifiedCrop != "(O)" + cropItemId ||
            ReadParameter(action, "gourmand_action") != "Gourmand" ||
            ReadParameter(action, "gourmand_schema_version") != "island_gourmand_progress.v1" ||
            ReadParameter(action, "gourmand_projection_status") != "exact_native_active_request_projected" ||
            ReadParameter(action, "native_contract") != GourmandCompilerNativeContract ||
            !TryBoolParameter(action, "talked_to_gourmand_before", out var talkedBefore) ||
            !TryBoolParameter(action, "talked_to_gourmand_after", out var talkedAfter) || !talkedAfter ||
            talkedBefore != completing ||
            ReadParameter(action, "expected_collected_nut_key") !=
                (completing ? "IslandGourmand" + after.Value : string.Empty))
            return new[] { "gourmand_request_typed_projection_required" };

        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("gourmand_menu_must_be_clear");
        var location = ReadParameter(action, "target_location");
        if (!string.Equals(location, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("gourmand_target_location_mismatch");

        var progress = ReadStateFieldValue(snapshot, "world_progress", "island_gourmand");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
            ReadString(progress.Value, "schema_version") != "island_gourmand_progress.v1" ||
            ReadString(progress.Value, "projection_status") != "exact_native_active_request_projected" ||
            ReadString(progress.Value, "location_id") != location ||
            ReadBool(progress.Value, "is_current_location") != true ||
            ReadBool(progress.Value, "host_authority") != true ||
            ReadBool(progress.Value, "menu_clear") != true ||
            ReadBool(progress.Value, "mutex_projection_available") != true ||
            ReadBool(progress.Value, "mutex_locked") == true ||
            ReadBool(progress.Value, "triggered_gourmand") == true ||
            ReadBool(progress.Value, "complete") == true ||
            ReadNullableInt(progress.Value, "active_request_index") != before.Value ||
            ReadString(progress.Value, "required_crop_item_id") != cropItemId ||
            ReadString(progress.Value, "required_crop_qualified_item_id") != qualifiedCrop ||
            ReadInt(progress.Value, "ready_matching_crop_count") != readyCount.Value ||
            ReadBool(progress.Value, "talked_to_gourmand_received_or_pending") != talkedBefore ||
            !GourmandCompilerHasReadyCrop(progress.Value, cropItemId) ||
            !GourmandCompilerActionTileMatches(progress.Value, actionX.Value, actionY.Value))
            reasons.Add("gourmand_request_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool GourmandCompilerCropMatchesRequest(int requestIndex, string? itemId) =>
        (requestIndex == 0 && itemId == "254") ||
        (requestIndex == 1 && itemId == "262") ||
        (requestIndex == 2 && itemId == "248");

    private static bool GourmandCompilerHasReadyCrop(JsonElement progress, string? itemId) =>
        progress.TryGetProperty("matching_crops", out var crops) && crops.ValueKind == JsonValueKind.Array &&
        crops.EnumerateArray().Any(crop => crop.ValueKind == JsonValueKind.Object &&
            ReadString(crop, "harvest_item_id") == itemId && ReadBool(crop, "ready_for_request") == true);

    private static bool GourmandCompilerActionTileMatches(JsonElement progress, int x, int y) =>
        progress.TryGetProperty("action_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array &&
        tiles.EnumerateArray().Any(tile => tile.ValueKind == JsonValueKind.Object &&
            ReadInt(tile, "tile_x") == x && ReadInt(tile, "tile_y") == y &&
            ReadString(tile, "action") == "Gourmand");
}
