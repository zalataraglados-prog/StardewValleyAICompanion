using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private const string GarbageCanNativeContract =
        "GameLocation.checkAction -> performAction Garbage -> CheckGarbage -> TryGetGarbageItem -> CheckedGarbage/stat/output/native NPC reaction; no direct checked-set, stat, friendship, inventory, debris, or RNG mutation";

    private static IEnumerable<SmallModelPlanStep> RummageGarbageSteps(PolicyEventCandidatePrediction candidate)
    {
        var stand = ParseCoordinate(candidate.ExpectedEffect, "garbage_can_stand_tile=");
        var interaction = ParseCoordinate(candidate.ExpectedEffect, "garbage_can_interaction_tile=");
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue || !interaction.HasValue)
            return Array.Empty<SmallModelPlanStep>();

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "rummage_garbage", 0),
                Kind = "rummage_garbage",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_garbage_can_projection_still_ready=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_checkAction_CheckGarbage_only",
                    "deterministic_prediction_without_rng_consumption",
                    "no_negative_friendship_witness",
                    "empty_toolbar_slot_then_restore",
                    "no_direct_checked_stat_friendship_inventory_debris_or_rng_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                {
                    Parameter("interaction_tile_x", interaction.Value.X.ToString()), Parameter("interaction_tile_y", interaction.Value.Y.ToString()),
                    Parameter("stand_tile_x", stand.Value.X.ToString()), Parameter("stand_tile_y", stand.Value.Y.ToString()),
                    Parameter("garbage_can_action", CandidateParameter(candidate, "garbage_can_action")),
                    Parameter("garbage_can_id", CandidateParameter(candidate, "garbage_can_id")),
                    Parameter("expected_checked_today_before", CandidateParameter(candidate, "expected_checked_today_before")),
                    Parameter("expected_checked_today_after", CandidateParameter(candidate, "expected_checked_today_after")),
                    Parameter("expected_trash_cans_checked_before", CandidateParameter(candidate, "expected_trash_cans_checked_before")),
                    Parameter("expected_trash_cans_checked_delta", CandidateParameter(candidate, "expected_trash_cans_checked_delta")),
                    Parameter("expected_daily_luck", CandidateParameter(candidate, "expected_daily_luck")),
                    Parameter("expected_alleyway_buffet_read", CandidateParameter(candidate, "expected_alleyway_buffet_read")),
                    Parameter("predicted_item_produced", CandidateParameter(candidate, "predicted_item_produced")),
                    Parameter("selected_entry_id", CandidateParameter(candidate, "selected_entry_id")),
                    Parameter("selected_ignore_base_chance", CandidateParameter(candidate, "selected_ignore_base_chance")),
                    Parameter("selected_mega_success", CandidateParameter(candidate, "selected_mega_success")),
                    Parameter("selected_double_mega_success", CandidateParameter(candidate, "selected_double_mega_success")),
                    Parameter("qualified_item_id", candidate.QualifiedItemId), Parameter("quantity", candidate.Quantity.ToString()),
                    Parameter("output_delivery", CandidateParameter(candidate, "output_delivery")),
                    Parameter("expected_output_json", CandidateParameter(candidate, "expected_output_json")),
                    Parameter("reacting_npc_json", CandidateParameter(candidate, "reacting_npc_json")),
                    Parameter("safe_slot_index", CandidateParameter(candidate, "safe_slot_index")), Parameter("safe_slot_kind", "empty"),
                    Parameter("restore_slot_index", CandidateParameter(candidate, "restore_slot_index")),
                    Parameter("garbage_can_data_payload_sha256", CandidateParameter(candidate, "garbage_can_data_payload_sha256")),
                    Parameter("garbage_can_data_contract_status", CandidateParameter(candidate, "garbage_can_data_contract_status")),
                    Parameter("garbage_can_prediction_status", CandidateParameter(candidate, "garbage_can_prediction_status")),
                    Parameter("garbage_can_projection_fingerprint", CandidateParameter(candidate, "garbage_can_projection_fingerprint")),
                    Parameter("garbage_can_native_contract", GarbageCanNativeContract),
                    Parameter("max_movement_tiles", CandidateParameter(candidate, "max_movement_tiles"))
                }.Concat(candidate.Parameters.Where(parameter => parameter.Name.StartsWith("quest_", StringComparison.Ordinal))).ToArray()
            }
        };
    }
}
