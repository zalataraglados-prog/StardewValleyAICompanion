using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep>
            PlaceStorageItemSteps(
                PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue ||
                !candidate.TileY.HasValue ||
                candidate.SlotIndex is null ||
                string.IsNullOrWhiteSpace(
                    candidate.QualifiedItemId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var stand = ParseCoordinate(
                candidate.ExpectedEffect,
                "move_to_adjacent=");
            if (!stand.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var locationId = candidate.LocationId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter(
                    "inventory_slot_index",
                    candidate.SlotIndex.Value.ToString()),
                Parameter(
                    "qualified_item_id",
                    candidate.QualifiedItemId),
                Parameter("item_id", candidate.ItemId),
                Parameter("location_id", locationId),
                Parameter(
                    "stand_tile_x",
                    stand.Value.X.ToString()),
                Parameter(
                    "stand_tile_y",
                    stand.Value.Y.ToString())
            };
            foreach (var name in new[]
            {
                "inventory_stack_before",
                "storage_placement_projection_fingerprint",
                "native_storage_branch",
                "placed_runtime_type",
                "special_chest_type",
                "actual_capacity",
                "storage_role",
                "layout_projection_basis",
                "route_distance_tiles",
                "placement_probe_status",
                "native_contract",
                "commitment_ledger_id",
                "commitment_ledger_revision",
                "material_reservation_guard_status",
                "material_reservation_ledger_id",
                "material_reservation_ledger_revision",
                "material_reservation_ids_json"
            })
            {
                var value = CandidateParameter(candidate, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters.Add(Parameter(name, value));
                }
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(
                        candidate,
                        "move_to_storage_placement_adjacent",
                        0),
                    Kind = "move_to_tile",
                    TargetLocation = locationId,
                    TargetTileX = stand.Value.X,
                    TargetTileY = stand.Value.Y,
                    EstimatedMinutes = TicksToMinutes(
                        candidate.EstimatedTicks),
                    Preconditions = new[]
                    {
                        "candidate_id:" +
                        candidate.CandidateId
                    },
                    ExpectedEffects = new[]
                    {
                        "player.tile=" + stand.Value.X +
                        "," + stand.Value.Y
                    },
                    SafetyConstraints = new[]
                    {
                        "collision_checked_by_action_queue_compiler"
                    },
                    FailurePolicy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(
                        candidate,
                        "place_storage_item",
                        1),
                    Kind = "place_storage_item",
                    TargetLocation = locationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "candidate_id:" +
                        candidate.CandidateId,
                        "player_inventory_slot_contains_storage=true",
                        "storage_layout_exact_tile_rebound=true"
                    },
                    ExpectedEffects = new[]
                    {
                        candidate.ExpectedEffect
                    },
                    SafetyConstraints = new[]
                    {
                        "runtime_rechecks_Utility.playerCanPlaceItemHere",
                        "native_placement_callbacks_only",
                        "route_and_existing_access_remain_connected"
                    },
                    FailurePolicy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    },
                    Parameters = parameters.ToArray()
                }
            };
        }
    }
}
