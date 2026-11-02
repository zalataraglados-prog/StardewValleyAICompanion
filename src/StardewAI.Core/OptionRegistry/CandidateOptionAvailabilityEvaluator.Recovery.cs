using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
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
            candidates.AddRange(FishingItemGrabRewardRecoveryCandidates(snapshot));
            var nativeControlSettle =
                NativeControlSettleRecovery.TryCreate(snapshot);
            if (nativeControlSettle is not null)
            {
                candidates.Add(nativeControlSettle);
                return candidates.ToArray();
            }

            var time = ReadStateFieldInt(snapshot, "time", "time");
            var nativeSaveBoundaryRequired = string.Equals(
                ReadParameter(
                    requestParameters,
                    "control_plane.native_save_boundary"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                var professionInputSettle =
                    LevelUpProfessionInputSettleCandidate(snapshot);
                if (professionInputSettle is not null)
                {
                    candidates.Add(professionInputSettle);
                }

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
                var bedStandTile = currentLocationIsHome
                    ? Infrastructure.NativeSleepTargetProjection
                        .SelectApproachTile(snapshot, bedX, bedY)
                    : null;
                var sleepImmediatelyBlocks = new List<string>();
                EventCandidate? recoveryRouteAction = null;
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
                        sleepImmediatelyBlocks.AddRange(
                            SleepApproachCompilerBlockingReasons(
                                snapshot,
                                bedStandTile));
                    }
                }
                else
                {
                    recoveryRouteAction = RecoveryRouteActionCandidate(
                        snapshot,
                        homeLocation);
                    sleepImmediatelyBlocks.AddRange(
                        recoveryRouteAction?.BlockReasons ??
                        RecoveryRouteBlockReasons(
                            snapshot,
                            nativeSaveBoundaryRequired));
                }

                var recoveryParameters = currentLocationIsHome
                    ? new[] { Parameter("execution_option_id", "executor.sleep") }
                    : recoveryRouteAction?.Parameters ?? Array.Empty<SmallModelActionParameter>();
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
                if (!currentLocationIsHome && recoveryRouteAction is not null &&
                    string.Equals(
                        recoveryRouteAction.Kind,
                        "clear_obstacle_tile",
                        StringComparison.Ordinal))
                {
                    candidates.Add(RecoveryRouteRepairCandidate(
                        recoveryRouteAction,
                        nativeSaveBoundaryRequired
                            ? "recovery:native_save_boundary"
                            : "recovery:sleep_immediately",
                        homeLocation,
                        nativeSaveBoundaryRequired));
                    return candidates.ToArray();
                }
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
                var bedStandTile = currentLocationIsHome
                    ? Infrastructure.NativeSleepTargetProjection
                        .SelectApproachTile(snapshot, bedX, bedY)
                    : null;
                var returnHomeBlocks = new List<string>();
                EventCandidate? recoveryRouteAction = null;
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
                        returnHomeBlocks.AddRange(
                            SleepApproachCompilerBlockingReasons(
                                snapshot,
                                bedStandTile));
                    }
                }
                else
                {
                    recoveryRouteAction = RecoveryRouteActionCandidate(
                        snapshot,
                        homeLocation);
                    returnHomeBlocks.AddRange(
                        recoveryRouteAction?.BlockReasons ??
                        RecoveryRouteBlockReasons(
                            snapshot,
                            nativeSaveBoundaryRequired: false));
                }

                var recoveryParameters = currentLocationIsHome
                    ? new[] { Parameter("execution_option_id", "executor.sleep") }
                    : recoveryRouteAction?.Parameters ?? Array.Empty<SmallModelActionParameter>();
                var routeTileX = ReadParameterInt(recoveryParameters, "target_tile_x");
                var routeTileY = ReadParameterInt(recoveryParameters, "target_tile_y");
                var routeTargetLocation = ReadParameter(recoveryParameters, "expected_target_location");
                var routeKind = ReadParameter(recoveryParameters, "connector_kind");
                var routeEstimatedTicks = ReadParameterInt(recoveryParameters, "estimated_ticks") ?? 0;
                candidates.Add(!currentLocationIsHome && recoveryRouteAction is not null &&
                    string.Equals(
                        recoveryRouteAction.Kind,
                        "clear_obstacle_tile",
                        StringComparison.Ordinal)
                    ? RecoveryRouteRepairCandidate(
                        recoveryRouteAction,
                        "recovery:return_home",
                        homeLocation,
                        nativeSaveBoundaryRequired: false)
                    : new EventCandidate
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

        private string[] SleepApproachCompilerBlockingReasons(
            SnapshotEnvelope snapshot,
            Infrastructure.NativeSleepApproachTile approach)
        {
            if (approach.IsNativeSleepTouchTile)
            {
                return Array.Empty<string>();
            }

            return CompilerProbeBlockingReasons(
                snapshot,
                new OptionAvailabilityCandidate
                {
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", approach.X.ToString()),
                        Parameter("target_tile_y", approach.Y.ToString())
                    }
                });
        }

        private static EventCandidate? LevelUpProfessionInputSettleCandidate(
            SnapshotEnvelope snapshot)
        {
            if (!string.Equals(
                    ActiveMenuTypeForCandidate(snapshot),
                    "LevelUpMenu",
                    StringComparison.Ordinal))
            {
                return null;
            }

            var state = ReadStateFieldValue(
                snapshot,
                "menus",
                "menu_specific_state");
            if (!state.HasValue ||
                state.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(state.Value, "kind"),
                    "level_up",
                    StringComparison.Ordinal) ||
                ReadBool(state.Value, "reflection_fields_complete") != true ||
                ReadBool(state.Value, "is_active") != true ||
                ReadBool(state.Value, "is_profession_chooser") != true ||
                ReadBool(state.Value, "information_up") != true ||
                ReadBool(state.Value, "can_receive_input") != false ||
                !HasNumber(state.Value, "timer_before_start") ||
                !HasNumber(state.Value, "current_skill") ||
                !HasNumber(state.Value, "current_level") ||
                !state.Value.TryGetProperty(
                    "profession_choices",
                    out var choices) ||
                choices.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var timerBeforeStart = ReadInt(state.Value, "timer_before_start");
            var choiceIds = choices.EnumerateArray()
                .Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    row.TryGetProperty("profession_id", out var id) &&
                    id.TryGetInt32(out _))
                .Select(row => row.GetProperty("profession_id").GetInt32())
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            if (timerBeforeStart is < 1 or > 250 || choiceIds.Length != 2)
            {
                return null;
            }

            var skill = ReadInt(state.Value, "current_skill");
            var level = ReadInt(state.Value, "current_level");
            var waitTicks = Math.Clamp(
                (timerBeforeStart + 15) / 16 + 1,
                1,
                30);
            var choiceIdentity = string.Join(",", choiceIds);
            return new EventCandidate
            {
                CandidateId =
                    $"recovery:wait_for_level_up_profession_input:{skill}:{level}:{choiceIdentity}",
                Kind =
                    StageOneMandatoryMenuRecovery.ProfessionInputSettleKind,
                Available = true,
                LocationId = ReadStateFieldString(
                    snapshot,
                    "player",
                    "location_id"),
                ExpectedEffect =
                    "level_up_menu.can_receive_input=true;fresh_snapshot_replan_required=true",
                EstimatedTicks = waitTicks,
                EnergyCost = 0,
                AvailabilityClass =
                    "native_level_up_profession_input_settle_wait",
                Parameters = new[]
                {
                    Parameter("execution_option_id", "executor.wait_ticks"),
                    Parameter("wait_ticks", waitTicks.ToString()),
                    Parameter("profession_input_settle_skill", skill.ToString()),
                    Parameter("profession_input_settle_level", level.ToString()),
                    Parameter(
                        "profession_input_settle_choice_ids",
                        choiceIdentity),
                    Parameter(
                        "profession_input_settle_timer_before_start_ms",
                        timerBeforeStart.ToString())
                }
            };
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

        private EventCandidate? RecoveryRouteActionCandidate(
            SnapshotEnvelope snapshot,
            string homeLocation)
        {
            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            return FindResolvedRoutePlan(
                    snapshot,
                    currentLocation,
                    homeLocation,
                    RouteConnectorCandidates(snapshot, int.MaxValue))
                ?.FirstActionCandidate;
        }

        private string[] RecoveryRouteBlockReasons(
            SnapshotEnvelope snapshot,
            bool nativeSaveBoundaryRequired)
        {
            var reasons = CompilerProbeBlockingReasons(
                CompilerProbeItem(
                    snapshot,
                    RecoveryRouteProbeCandidate(
                        nativeSaveBoundaryRequired)));
            return reasons.Length > 0
                ? reasons
                : new[] { "recovery_route_graph_no_path_home" };
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

        private static EventCandidate RecoveryRouteRepairCandidate(
            EventCandidate repair,
            string candidateId,
            string homeLocation,
            bool nativeSaveBoundaryRequired)
        {
            var parameters = repair.Parameters
                .Where(parameter =>
                    !string.Equals(
                        parameter.Name,
                        "control_plane.native_save_boundary",
                        StringComparison.Ordinal))
                .Concat(new[]
                {
                    Parameter("recovery.target_location_id", homeLocation),
                    Parameter(
                        "recovery.snapshot_policy",
                        "clear_one_obstacle_then_fresh_snapshot")
                })
                .ToList();
            if (nativeSaveBoundaryRequired)
            {
                parameters.Add(Parameter(
                    "control_plane.native_save_boundary",
                    "true"));
            }

            return new EventCandidate
            {
                CandidateId = candidateId,
                Kind = repair.Kind,
                Available = repair.Available,
                LocationId = repair.LocationId,
                TileX = repair.TileX,
                TileY = repair.TileY,
                ExpectedEffect = repair.ExpectedEffect +
                    ";rolling_horizon_route_to_home=" + homeLocation +
                    ";terminal_sleep_pending=true",
                EstimatedTicks = repair.EstimatedTicks,
                EnergyCost = repair.EnergyCost,
                AvailabilityClass =
                    "recovery_route_repair_clearable_obstacle",
                AllowedNow = repair.AllowedNow,
                AllowedToday = repair.AllowedToday,
                NextOpenTime = repair.NextOpenTime,
                EffectiveOpenTime = repair.EffectiveOpenTime,
                ClosesAt = repair.ClosesAt,
                WaitCost = repair.WaitCost,
                GateReasons = repair.GateReasons,
                BlockReasons = repair.BlockReasons,
                Parameters = parameters.ToArray()
            };
        }

    }
}
