using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] RecoveryCandidates(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] requestParameters)
        {
            var candidates = new List<EventCandidate>();
            var time = ReadStateFieldInt(snapshot, "time", "time");
            var nativeSaveBoundaryRequired = string.Equals(
                ReadParameter(
                    requestParameters,
                    "control_plane.native_save_boundary"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                var closeMenuReasons = CloseMenuCandidateBlockReasons(snapshot);
                var closeMenuParameters = new List<SmallModelActionParameter>
                {
                    Parameter("execution_option_id", "executor.close_menu")
                };
                if (Infrastructure.IncubatorSnapshotProjection
                    .IsBirthMessage(snapshot))
                {
                    closeMenuParameters.Add(Parameter(
                        "interaction_kind",
                        "incubator_birth_message"));
                }
                closeMenuParameters.AddRange(LevelUpMenuRecoveryParameters(snapshot));
                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:close_blocking_menu",
                    Kind = "recovery_close_menu",
                    Available = closeMenuReasons.Length == 0,
                    LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                    ExpectedEffect = "menu_not_blocking_execution",
                    EstimatedTicks = 10,
                    BlockReasons = closeMenuReasons,
                    Parameters = closeMenuParameters.ToArray()
                });
            }

            if (SleepPromptOpenForCandidate(snapshot))
            {
                var resumeBlocks =
                    Infrastructure.SleepPromptResumeProjection.BlockReasons(
                        snapshot);
                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:resume_sleep_prompt",
                    Kind = "recovery_resume_sleep_prompt",
                    Available = resumeBlocks.Length == 0,
                    LocationId = ReadStateFieldString(
                        snapshot,
                        "player",
                        "location_id"),
                    TileX = ReadStateFieldIntOptional(
                        snapshot,
                        "player",
                        "tile_x"),
                    TileY = ReadStateFieldIntOptional(
                        snapshot,
                        "player",
                        "tile_y"),
                    ExpectedEffect =
                        "existing_exact_sleep_prompt_confirmed;day_safely_ended",
                    EstimatedTicks = 120,
                    BlockReasons = resumeBlocks,
                    Parameters = new[]
                    {
                        Parameter(
                            "execution_option_id",
                            "executor.sleep"),
                        Parameter(
                            "sleep_resume_mode",
                            Infrastructure.SleepPromptResumeProjection.ResumeMode)
                    }
                });
            }

            if (time >= 2400 || nativeSaveBoundaryRequired)
            {
                var homeContext = ReadStateFieldValue(snapshot, "current_location", "home_context");
                var homeLocation = homeContext.HasValue ? ReadString(homeContext.Value, "home_location_id") : string.Empty;
                var currentLocationIsHome = homeContext.HasValue && ReadBool(homeContext.Value, "current_location_is_home") == true;
                var bedX = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_x") ? ReadInt(homeContext.Value, "bed_tile_x") : 0;
                var bedY = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_y") ? ReadInt(homeContext.Value, "bed_tile_y") : 0;
                var bedTileHasBed = homeContext.HasValue && ReadBool(homeContext.Value, "bed_tile_has_bed") == true;
                var bedStandTile = currentLocationIsHome ? FindBestStandTile(snapshot, bedX, bedY) : null;
                var sleepImmediatelyBlocks = new List<string>();
                ActionQueueItem? recoveryProbe = null;
                if (!homeContext.HasValue || string.IsNullOrWhiteSpace(homeLocation))
                {
                    sleepImmediatelyBlocks.Add("recovery_home_route_target_unavailable");
                }
                else if (currentLocationIsHome)
                {
                    if (ActiveMenuOpenForCandidate(snapshot))
                    {
                        sleepImmediatelyBlocks.Add("sleep_prompt_menu_must_be_clear");
                    }

                    if (SleepPromptOpenForCandidate(snapshot))
                    {
                        sleepImmediatelyBlocks.Add("recovery_sleep_prompt_already_open");
                    }

                    if (!bedTileHasBed)
                    {
                        sleepImmediatelyBlocks.Add("recovery_bed_tile_not_confirmed");
                    }
                    else if (bedStandTile is null)
                    {
                        sleepImmediatelyBlocks.Add("recovery_bed_adjacent_stand_tile_unavailable");
                    }
                    else
                    {
                        sleepImmediatelyBlocks.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "exploration.visit_location",
                            Parameters = new[]
                            {
                                Parameter("target_tile_x", bedStandTile.X.ToString()),
                                Parameter("target_tile_y", bedStandTile.Y.ToString())
                            }
                        }));
                    }
                }
                else
                {
                    recoveryProbe = CompilerProbeItem(
                        snapshot,
                        RecoveryRouteProbeCandidate(nativeSaveBoundaryRequired));
                    sleepImmediatelyBlocks.AddRange(CompilerProbeBlockingReasons(recoveryProbe));
                }

                var recoveryParameters = currentLocationIsHome
                    ? new[] { Parameter("execution_option_id", "executor.sleep") }
                    : recoveryProbe?.NormalizedCommand.Parameters ?? Array.Empty<SmallModelActionParameter>();
                if (nativeSaveBoundaryRequired &&
                    !string.Equals(
                        ReadParameter(
                            recoveryParameters,
                            "control_plane.native_save_boundary"),
                        "true",
                        StringComparison.OrdinalIgnoreCase))
                {
                    recoveryParameters = recoveryParameters
                        .Append(Parameter(
                            "control_plane.native_save_boundary",
                            "true"))
                        .ToArray();
                }
                var routeTileX = ReadParameterInt(recoveryParameters, "target_tile_x");
                var routeTileY = ReadParameterInt(recoveryParameters, "target_tile_y");
                var routeTargetLocation = ReadParameter(recoveryParameters, "expected_target_location");
                var routeKind = ReadParameter(recoveryParameters, "connector_kind");
                var routeEstimatedTicks = ReadParameterInt(recoveryParameters, "estimated_ticks") ?? 0;
                candidates.Add(new EventCandidate
                {
                    CandidateId = nativeSaveBoundaryRequired
                        ? "recovery:native_save_boundary"
                        : "recovery:sleep_immediately",
                    Kind = "recovery_sleep_immediately",
                    Available = sleepImmediatelyBlocks.Count == 0,
                    LocationId = currentLocationIsHome
                        ? homeLocation
                        : ReadStateFieldString(snapshot, "player", "location_id"),
                    TileX = currentLocationIsHome ? bedStandTile?.X : routeTileX,
                    TileY = currentLocationIsHome ? bedStandTile?.Y : routeTileY,
                    ExpectedEffect = currentLocationIsHome
                        ? bedStandTile is null
                            ? "bed_tile=" + bedX + "," + bedY + ";sleep_not_executed"
                            : "move_to_bed_adjacent=" + bedStandTile.X + "," + bedStandTile.Y + ";step_onto_sleep_touch_tile=" + bedX + "," + bedY + ";touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed"
                        : "rolling_horizon_route_to_home=" + homeLocation +
                            ";connector_kind=" + routeKind +
                            ";expected_target_location=" + routeTargetLocation +
                            ";one_connector_then_fresh_snapshot;terminal_sleep_pending",
                    EstimatedTicks = currentLocationIsHome ? 240 : routeEstimatedTicks,
                    BlockReasons = sleepImmediatelyBlocks.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = recoveryParameters
                });
                return candidates.ToArray();
            }

            if (GameClockBudgetPolicy.RecoveryWindowStarted(time))
            {
                var homeContext = ReadStateFieldValue(snapshot, "current_location", "home_context");
                var homeLocation = homeContext.HasValue ? ReadString(homeContext.Value, "home_location_id") : string.Empty;
                var currentLocationIsHome = homeContext.HasValue && ReadBool(homeContext.Value, "current_location_is_home") == true;
                var bedX = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_x") ? ReadInt(homeContext.Value, "bed_tile_x") : 0;
                var bedY = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_y") ? ReadInt(homeContext.Value, "bed_tile_y") : 0;
                var bedTileHasBed = homeContext.HasValue && ReadBool(homeContext.Value, "bed_tile_has_bed") == true;
                var bedStandTile = currentLocationIsHome ? FindBestStandTile(snapshot, bedX, bedY) : null;
                var returnHomeBlocks = new List<string>();
                ActionQueueItem? recoveryProbe = null;
                if (!homeContext.HasValue || string.IsNullOrWhiteSpace(homeLocation))
                {
                    returnHomeBlocks.Add("recovery_home_route_target_unavailable");
                }
                else if (currentLocationIsHome)
                {
                    if (ActiveMenuOpenForCandidate(snapshot))
                    {
                        returnHomeBlocks.Add("sleep_prompt_menu_must_be_clear");
                    }

                    if (SleepPromptOpenForCandidate(snapshot))
                    {
                        returnHomeBlocks.Add("recovery_sleep_prompt_already_open");
                    }

                    if (!bedTileHasBed)
                    {
                        returnHomeBlocks.Add("recovery_bed_tile_not_confirmed");
                    }
                    else if (bedStandTile is null)
                    {
                        returnHomeBlocks.Add("recovery_bed_adjacent_stand_tile_unavailable");
                    }
                    else
                    {
                        returnHomeBlocks.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "exploration.visit_location",
                            Parameters = new[]
                            {
                                Parameter("target_tile_x", bedStandTile.X.ToString()),
                                Parameter("target_tile_y", bedStandTile.Y.ToString())
                            }
                        }));
                    }
                }
                else
                {
                    recoveryProbe = CompilerProbeItem(
                        snapshot,
                        RecoveryRouteProbeCandidate(nativeSaveBoundaryRequired: false));
                    returnHomeBlocks.AddRange(CompilerProbeBlockingReasons(recoveryProbe));
                }

                var recoveryParameters = currentLocationIsHome
                    ? new[] { Parameter("execution_option_id", "executor.sleep") }
                    : recoveryProbe?.NormalizedCommand.Parameters ?? Array.Empty<SmallModelActionParameter>();
                var routeTileX = ReadParameterInt(recoveryParameters, "target_tile_x");
                var routeTileY = ReadParameterInt(recoveryParameters, "target_tile_y");
                var routeTargetLocation = ReadParameter(recoveryParameters, "expected_target_location");
                var routeKind = ReadParameter(recoveryParameters, "connector_kind");
                var routeEstimatedTicks = ReadParameterInt(recoveryParameters, "estimated_ticks") ?? 0;
                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:return_home",
                    Kind = "recovery_return_home",
                    Available = returnHomeBlocks.Count == 0,
                    LocationId = currentLocationIsHome
                        ? homeLocation
                        : ReadStateFieldString(snapshot, "player", "location_id"),
                    TileX = currentLocationIsHome ? bedStandTile?.X : routeTileX,
                    TileY = currentLocationIsHome ? bedStandTile?.Y : routeTileY,
                    ExpectedEffect = currentLocationIsHome
                        ? bedStandTile is null
                            ? "bed_tile=" + bedX + "," + bedY + ";sleep_not_executed"
                            : "move_to_bed_adjacent=" + bedStandTile.X + "," + bedStandTile.Y + ";step_onto_sleep_touch_tile=" + bedX + "," + bedY + ";touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed"
                        : "rolling_horizon_route_to_home=" + homeLocation +
                            ";connector_kind=" + routeKind +
                            ";expected_target_location=" + routeTargetLocation +
                            ";one_connector_then_fresh_snapshot;terminal_sleep_pending",
                    EstimatedTicks = currentLocationIsHome ? 240 : routeEstimatedTicks,
                    BlockReasons = returnHomeBlocks.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = recoveryParameters
                });
                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:sleep_before_collapse",
                    Kind = "recovery_sleep_before_collapse",
                    Available = false,
                    LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                    ExpectedEffect = "day_safely_ended",
                    EstimatedTicks = 120,
                    BlockReasons = new[] { "recovery_terminal_sleep_already_covered_by_return_home" }
                });
                return candidates.ToArray();
            }

            return candidates.ToArray();
        }

        private static EventCandidate RecoveryRefreshCandidate(SnapshotEnvelope snapshot)
        {
            var menuOpen = ActiveMenuOpenForCandidate(snapshot);
            return new EventCandidate
            {
                CandidateId = "recovery:refresh_plan_after_stabilization",
                Kind = "recovery_refresh_plan",
                Available = !menuOpen,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                ExpectedEffect = "executor.wait_ticks=30;urgent_risks_rechecked",
                EstimatedTicks = 30,
                EnergyCost = 0,
                BlockReasons = menuOpen
                    ? new[] { "intervening_menu_must_be_cleared_first" }
                    : Array.Empty<string>(),
                Parameters = new[]
                {
                    Parameter("execution_option_id", "executor.wait_ticks"),
                    Parameter("wait_ticks", "30")
                }
            };
        }

        private static OptionAvailabilityCandidate RecoveryRouteProbeCandidate(
            bool nativeSaveBoundaryRequired)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("compiler_context.recovery_route_probe", "true")
            };
            if (nativeSaveBoundaryRequired)
            {
                parameters.Add(Parameter(
                    "control_plane.native_save_boundary",
                    "true"));
            }

            return new OptionAvailabilityCandidate
            {
                OptionId = "recovery.stabilize_day",
                Parameters = parameters.ToArray()
            };
        }

        private static SmallModelActionParameter[] LevelUpMenuRecoveryParameters(SnapshotEnvelope snapshot)
        {
            if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "LevelUpMenu", StringComparison.Ordinal))
            {
                return Array.Empty<SmallModelActionParameter>();
            }

            var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
            if (!state.HasValue ||
                state.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(state.Value, "kind"), "level_up", StringComparison.Ordinal) ||
                ReadBool(state.Value, "is_profession_chooser") != true ||
                !state.Value.TryGetProperty("profession_choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<SmallModelActionParameter>();
            }

            var choiceIds = choices.EnumerateArray()
                .Where(row => row.TryGetProperty("profession_id", out var id) && id.TryGetInt32(out _))
                .Select(row => row.GetProperty("profession_id").GetInt32())
                .Distinct()
                .ToArray();
            var preferred = PreferredGrandpaPerfectionProfession(choiceIds);
            return preferred.HasValue
                ? new[]
                {
                    Parameter("profession_choice_id", preferred.Value.ToString()),
                    Parameter("profession_choice_source", "baseline_grandpa_perfection_policy_v1")
                }
                : Array.Empty<SmallModelActionParameter>();
        }

        private static int? PreferredGrandpaPerfectionProfession(IReadOnlyCollection<int> choices)
        {
            var preferredOrder = new[] { 1, 4, 6, 8, 13, 16, 18, 21, 24, 26 };
            return preferredOrder.Cast<int?>()
                .FirstOrDefault(choice => choice.HasValue && choices.Contains(choice.Value));
        }

    }
}
