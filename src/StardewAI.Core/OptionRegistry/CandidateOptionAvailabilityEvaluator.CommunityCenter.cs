using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] CommunityCenterDonationCandidates(SnapshotEnvelope snapshot)
    {
        var progress = ReadStateFieldValue(snapshot, "world_progress", "community_center");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
            !progress.Value.TryGetProperty("bundle_rows", out var bundles) || bundles.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var progressRow = progress.Value;
        var routeState = ReadString(progressRow, "route_state");
        var rowCountExact = ReadInt(progressRow, "bundle_data_row_count") == ReadInt(progressRow, "projected_bundle_row_count") &&
            ReadInt(progressRow, "unavailable_bundle_row_count") == 0;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var result = new List<EventCandidate>();

        foreach (var bundle in bundles.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object))
        {
            if (!bundle.TryGetProperty("donation_candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var noteX = NullableReadInt(bundle, "note_tile_x");
            var noteY = NullableReadInt(bundle, "note_tile_y");
            var stand = noteX.HasValue && noteY.HasValue ? FindBestStandTile(snapshot, noteX.Value, noteY.Value) : null;
            foreach (var candidate in candidates.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object))
            {
                var reasons = new List<string>();
                var actionStatus = ReadString(candidate, "action_status");
                if (actionStatus != "ready")
                {
                    reasons.Add(string.IsNullOrWhiteSpace(actionStatus) ? "community_center_donation_projection_unavailable" : actionStatus);
                }
                if (routeState != "undecided" && routeState != "community_center_locked")
                {
                    reasons.Add(routeState == "conflicting_irreversible_flags"
                        ? "community_center_route_state_conflict"
                        : "community_center_route_locked_out_by_joja");
                }
                if (!rowCountExact)
                {
                    reasons.Add("community_center_bundle_projection_incomplete");
                }
                if (ReadString(bundle, "projection_status") != "exact")
                {
                    reasons.Add("community_center_bundle_row_unavailable");
                }
                if (!noteX.HasValue || !noteY.HasValue)
                {
                    reasons.Add("community_center_note_tile_unavailable");
                }
                if (stand is null)
                {
                    reasons.Add("community_center_note_no_reachable_stand_tile");
                }

                var slot = ReadInt(candidate, "inventory_slot_index");
                var ingredientIndex = ReadInt(candidate, "ingredient_index");
                var before = ReadInt(candidate, "completed_ingredient_count_before");
                var after = ReadInt(candidate, "completed_ingredient_count_after");
                var requiredSlots = ReadInt(bundle, "required_slot_count");
                var ingredientCount = bundle.TryGetProperty("ingredients", out var ingredients) && ingredients.ValueKind == JsonValueKind.Array
                    ? ingredients.GetArrayLength()
                    : 0;
                var completesBundle = ReadBool(candidate, "completes_bundle") == true;
                var expectedAfter = completesBundle ? ingredientCount : before + 1;
                if (slot < 0 || ingredientIndex < 0 || requiredSlots < 1 || ingredientCount < requiredSlots || after != expectedAfter ||
                    ReadInt(candidate, "required_stack") < 1 || ReadInt(candidate, "stack_after") != ReadInt(candidate, "stack_before") - ReadInt(candidate, "required_stack") ||
                    ReadInt(candidate, "inventory_item_total_before") < ReadInt(candidate, "stack_before") ||
                    ReadInt(candidate, "inventory_item_total_after") != ReadInt(candidate, "inventory_item_total_before") - ReadInt(candidate, "required_stack"))
                {
                    reasons.Add("community_center_donation_candidate_typed_projection_invalid");
                }
                var parameters = stand is null || !noteX.HasValue || !noteY.HasValue
                    ? Array.Empty<SmallModelActionParameter>()
                    : CommunityCenterDonationParameters(progressRow, bundle, candidate, stand.X, stand.Y, noteX.Value, noteY.Value);
                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);

                result.Add(new EventCandidate
                {
                    CandidateId = "community-center-donate:" + ReadInt(bundle, "bundle_id") + ":" + ingredientIndex + ":" + slot,
                    Kind = "donate_community_center_item",
                    Available = reasons.Count == 0,
                    LocationId = "CommunityCenter",
                    TileX = noteX,
                    TileY = noteY,
                    ExpectedEffect = CommunityCenterDonationExpectedEffect(bundle, candidate),
                    ItemId = ReadString(candidate, "item_id"),
                    QualifiedItemId = ReadString(candidate, "qualified_item_id"),
                    SlotIndex = slot,
                    Quantity = ReadInt(candidate, "required_stack"),
                    EstimatedTicks = Math.Max(300, distance * 60 + 300),
                    AvailabilityClass = "transparent_native_community_center_donation",
                    AllowedNow = reasons.Count == 0,
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                });
            }
        }
        return result.ToArray();
    }

    private static SmallModelActionParameter[] CommunityCenterDonationParameters(
        JsonElement progress,
        JsonElement bundle,
        JsonElement candidate,
        int standX,
        int standY,
        int noteX,
        int noteY)
    {
        return new[]
        {
            Parameter("target_location", "CommunityCenter"),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("note_tile_x", noteX.ToString()),
            Parameter("note_tile_y", noteY.ToString()),
            Parameter("route_state", ReadString(progress, "route_state")),
            Parameter("bundle_data_key", ReadString(bundle, "bundle_data_key")),
            Parameter("bundle_id", ReadInt(bundle, "bundle_id").ToString()),
            Parameter("bundle_area_id", ReadInt(bundle, "area_id").ToString()),
            Parameter("bundle_area_name", ReadString(bundle, "area_name")),
            Parameter("bundle_ingredient_index", ReadInt(candidate, "ingredient_index").ToString()),
            Parameter("inventory_slot_index", ReadInt(candidate, "inventory_slot_index").ToString()),
            Parameter("item_id", ReadString(candidate, "item_id")),
            Parameter("qualified_item_id", ReadString(candidate, "qualified_item_id")),
            Parameter("target_runtime_type", ReadString(candidate, "runtime_type")),
            Parameter("expected_item_quality", ReadInt(candidate, "quality").ToString()),
            Parameter("required_stack", ReadInt(candidate, "required_stack").ToString()),
            Parameter("inventory_item_total_before", ReadInt(candidate, "inventory_item_total_before").ToString()),
            Parameter("inventory_item_total_after", ReadInt(candidate, "inventory_item_total_after").ToString()),
            Parameter("expected_stack_before", ReadInt(candidate, "stack_before").ToString()),
            Parameter("expected_stack_after", ReadInt(candidate, "stack_after").ToString()),
            Parameter("bundle_required_slot_count", ReadInt(bundle, "required_slot_count").ToString()),
            Parameter("expected_bundle_completed_count_before", ReadInt(candidate, "completed_ingredient_count_before").ToString()),
            Parameter("expected_bundle_completed_count_after", ReadInt(candidate, "completed_ingredient_count_after").ToString()),
            Parameter("expected_bundle_complete_after", ReadBool(candidate, "completes_bundle") == true ? "true" : "false"),
            Parameter("native_contract", "CommunityCenter.checkBundle_then_JunimoNoteMenu.receiveLeftClick_bundle_inventory_and_ingredient_slot_then_exitThisMenu")
        };
    }

    private static string CommunityCenterDonationExpectedEffect(JsonElement bundle, JsonElement candidate)
    {
        return "community_center.bundle=" + ReadInt(bundle, "bundle_id") +
            ":ingredient=" + ReadInt(candidate, "ingredient_index") + ":completed=true" +
            ";inventory_slot=" + ReadInt(candidate, "inventory_slot_index") + ":stack=" + ReadInt(candidate, "stack_after") +
            ";bundle_complete=" + (ReadBool(candidate, "completes_bundle") == true ? "true" : "false");
    }
}
