using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> RecoveryRefreshSteps(PolicyEventCandidatePrediction candidate)
        {
            var waitTicks = CandidateInt(candidate, "wait_ticks") ?? candidate.EstimatedTicks;
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "refresh_wait", 0),
                    Kind = "wait_ticks",
                    WaitTicks = Math.Min(MaxWaitTicksPerStep, Math.Max(1, waitTicks)),
                    EstimatedMinutes = TicksToMinutes(waitTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "wait_only_recovery_candidate" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoveryExecutionSteps(PolicyEventCandidatePrediction candidate)
        {
            return CandidateParameter(candidate, "execution_option_id") switch
            {
                "executor.sleep" => RecoverySleepSteps(candidate),
                "executor.traverse_connector" => RecoveryRouteSteps(candidate),
                _ => Array.Empty<SmallModelPlanStep>()
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoveryCloseMenuSteps(PolicyEventCandidatePrediction candidate)
        {
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "close_blocking_menu", 0),
                    Kind = "close_menu",
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "menus.active_menu.is_open=true" },
                    ExpectedEffects = new[] { "menus.active_menu.is_open=false" },
                    SafetyConstraints = new[] { "close_only_safe_whitelisted_menu", "recovery_menu_close" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = ContinuationParameters(candidate)
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoveryRouteSteps(PolicyEventCandidatePrediction candidate)
        {
            var connectorKind = CandidateParameter(candidate, "connector_kind");
            var expectedTargetLocation = CandidateParameter(candidate, "expected_target_location");
            var targetTileX = candidate.TileX ?? CandidateInt(candidate, "target_tile_x");
            var targetTileY = candidate.TileY ?? CandidateInt(candidate, "target_tile_y");
            if (!targetTileX.HasValue ||
                !targetTileY.HasValue ||
                string.IsNullOrWhiteSpace(candidate.LocationId) ||
                string.IsNullOrWhiteSpace(connectorKind) ||
                string.IsNullOrWhiteSpace(expectedTargetLocation))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("connector_kind", connectorKind),
                Parameter("expected_target_location", expectedTargetLocation)
            };
            foreach (var name in new[]
            {
                "expected_arrival_tile_x",
                "expected_arrival_tile_y",
                "max_movement_tiles",
                "estimated_ticks",
                "estimated_minutes",
                "compiler_context.remaining_connector_count"
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
                    StepId = StepId(candidate, "return_home_connector", 0),
                    Kind = "traverse_connector",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = targetTileX,
                    TargetTileY = targetTileY,
                    EstimatedMinutes = CandidateInt(candidate, "estimated_minutes") ?? TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "current_location=" + candidate.LocationId,
                        "transparent_connector_still_matches_snapshot=true"
                    },
                    ExpectedEffects = new[]
                    {
                        "player.location_id=" + expectedTargetLocation,
                        "fresh_snapshot_replan_required=true"
                    },
                    SafetyConstraints = new[]
                    {
                        "connector_target_from_transparent_route_graph",
                        "connector_gate_checked_upstream",
                        "no_direct_coordinate_teleport",
                        "one_connector_per_recovery_replan"
                    },
                    FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoverySleepSteps(PolicyEventCandidatePrediction candidate)
        {
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "terminal_sleep", 0),
                    Kind = "sleep",
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "player.at_home=true", "bed_reachable=true" },
                    ExpectedEffects = new[] { "day_safely_ended" },
                    SafetyConstraints = new[] { "terminal_sleep_only_via_recovery_candidate" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }

    }
}
