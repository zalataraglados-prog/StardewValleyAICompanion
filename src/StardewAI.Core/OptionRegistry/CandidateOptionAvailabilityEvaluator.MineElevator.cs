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
    private EventCandidate[] MiningReachDepthCandidates(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters)
    {
        var currentFloorCandidates = MiningReachDepthCandidateBuilder.Build(snapshot, parameters);
        var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
        var resources = ReadStateFieldValue(snapshot, "mining", "player_resources");
        if (!currentMine.HasValue || currentMine.Value.ValueKind != JsonValueKind.Object ||
            !resources.HasValue || resources.Value.ValueKind != JsonValueKind.Object)
            return currentFloorCandidates;

        var currentDepth = ReadInt(currentMine.Value, "mine_level");
        var currentFamily = ReadString(currentMine.Value, "mine_kind");
        var targetDepth = ReadParameterInt(parameters, "target_depth");
        var requestedFamily = ReadParameter(parameters, "target_location_family");
        if (MiningReachDepthCandidateBuilder.ValidateTarget(
                currentDepth,
                currentFamily,
                targetDepth,
                requestedFamily).Length > 0)
            return currentFloorCandidates;

        var deepest = ReadIntOptional(resources.Value, "deepest_mine_level");
        var elevatorTarget = MiningReachDepthCandidateBuilder.ElevatorStartFor(
            currentDepth,
            targetDepth,
            currentFamily,
            deepest);
        if (!targetDepth.HasValue || !elevatorTarget.HasValue || elevatorTarget.Value <= currentDepth ||
            !MineElevatorEndpointOrMenuPresent(snapshot))
            return currentFloorCandidates;

        var elevatorMenuOpen = string.Equals(
            ActiveMenuTypeForCandidate(snapshot),
            "MineElevatorMenu",
            StringComparison.Ordinal);
        if (!elevatorMenuOpen && !CurrentFloorStepMayYieldToElevator(currentFloorCandidates))
            return currentFloorCandidates;

        var elevatorParameters = parameters
            .Where(parameter =>
                !string.Equals(parameter.Name, "target_depth", StringComparison.Ordinal) &&
                !string.Equals(parameter.Name, "target_location_family", StringComparison.Ordinal))
            .Concat(new[]
            {
                Parameter("target_depth", elevatorTarget.Value.ToString()),
                Parameter("target_location_family", "ordinary_mines")
            })
            .ToArray();
        var candidates = MiningElevatorCandidates(snapshot, elevatorParameters);
        foreach (var candidate in candidates)
        {
            candidate.CandidateId = candidate.CandidateId.Replace(
                "mining.use_elevator:",
                "mining.reach_depth:elevator:",
                StringComparison.Ordinal);
            candidate.ExpectedEffect +=
                ";reach_depth_objective_target=" + targetDepth.Value +
                ";elevator_checkpoint=" + elevatorTarget.Value;
            candidate.Parameters = UpsertMineReachDepthContinuation(
                candidate.Parameters,
                targetDepth.Value,
                elevatorTarget.Value);
        }
        return candidates;
    }

    private static bool CurrentFloorStepMayYieldToElevator(EventCandidate[] candidates)
    {
        var candidate = candidates.SingleOrDefault();
        if (candidate is null || !candidate.Available)
            return false;

        var executionOptionId = ReadParameter(candidate.Parameters, "execution_option_id");
        return executionOptionId == "executor.mine_stone" ||
            executionOptionId == "executor.descend_ladder" ||
            executionOptionId == "executor.descend_shaft" ||
            executionOptionId == "executor.place_staircase";
    }

    private EventCandidate[] MiningElevatorCandidates(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters)
    {
        var target = ReadParameterInt(parameters, "target_depth");
        var deepest = ReadStateFieldIntOptional(snapshot, "player", "deepest_mine_level");
        var currentLevel = ReadStateFieldIntOptional(snapshot, "player", "current_mine_level");
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetFamily = ReadParameter(parameters, "target_location_family");
        var reasons = MineElevatorTargetReasons(target, deepest, currentLevel, targetFamily).ToList();
        var activeMenuType = ActiveMenuTypeForCandidate(snapshot);

        if (string.Equals(activeMenuType, "MineElevatorMenu", StringComparison.Ordinal))
            return MineElevatorMenuCandidates(snapshot, target, reasons);
        if (ActiveMenuOpenForCandidate(snapshot))
            return new[] { BlockedMineElevatorCandidate(target, reasons.Append("mine_elevator_other_menu_open").ToArray()) };

        var inMineShaft = IsOrdinaryMineShaft(snapshot);
        if (target == 0 && !inMineShaft)
            reasons.Add("mine_elevator_floor_zero_requires_loaded_mineshaft");

        var endpointField = ReadStateFieldValue(snapshot, "current_location", "mine_elevator_action_tiles");
        var endpoint = endpointField.HasValue && endpointField.Value.ValueKind == JsonValueKind.Array
            ? endpointField.Value.EnumerateArray().FirstOrDefault(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "action_type"), "MineElevator", StringComparison.Ordinal) &&
                ReadBool(row, "menu_available") == true)
            : default;
        if (endpoint.ValueKind != JsonValueKind.Object)
        {
            if (!inMineShaft && !string.Equals(currentLocation, "Mine", StringComparison.OrdinalIgnoreCase) && target > 0)
            {
                var route = FindResolvedRoutePlan(snapshot, currentLocation, "Mine", RouteConnectorCandidates(snapshot))?.FirstActionCandidate;
                if (route is null)
                    return new[] { BlockedMineElevatorCandidate(target, reasons.Append("mine_elevator_route_to_entrance_unavailable").ToArray()) };

                var routeReasons = reasons.Concat(route.BlockReasons).Distinct(StringComparer.Ordinal).ToArray();
                return new[]
                {
                    new EventCandidate
                    {
                        CandidateId = $"mining.use_elevator:route:{target}:{currentLocation}:{route.TileX},{route.TileY}",
                        Kind = "route_connector_tile",
                        Available = route.Available && routeReasons.Length == 0,
                        LocationId = currentLocation,
                        TileX = route.TileX,
                        TileY = route.TileY,
                        ExpectedEffect = "mine_entrance_route_progress=true;fresh_snapshot_replan_required=true",
                        EstimatedTicks = route.EstimatedTicks,
                        EnergyCost = 0,
                        AvailabilityClass = "mine_elevator_cross_map_route_step",
                        AllowedNow = route.AllowedNow,
                        AllowedToday = route.AllowedToday,
                        NextOpenTime = route.NextOpenTime,
                        EffectiveOpenTime = route.EffectiveOpenTime,
                        ClosesAt = route.ClosesAt,
                        WaitCost = route.WaitCost,
                        GateReasons = route.GateReasons,
                        BlockReasons = routeReasons,
                        Parameters = route.Parameters.Concat(MineElevatorIdentityParameters(target)).Concat(new[]
                        {
                            Parameter("continuation.option_id", "mining.use_elevator"),
                            Parameter("continuation.target_location", "Mine")
                        }).ToArray()
                    }
                };
            }

            reasons.Add(inMineShaft
                ? "mine_elevator_endpoint_not_present_on_current_mine_floor"
                : "mine_elevator_endpoint_missing_at_mine_entrance");
            return new[] { BlockedMineElevatorCandidate(target, reasons.ToArray()) };
        }

        var actionX = ReadInt(endpoint, "tile_x");
        var actionY = ReadInt(endpoint, "tile_y");
        var stand = FindBestStandTile(snapshot, actionX, actionY);
        if (stand is null)
            reasons.Add("mine_elevator_adjacent_stand_tile_unavailable");
        var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
        var identity = MineElevatorIdentityParameters(target);

        if (stand is not null && (playerX != stand.X || playerY != stand.Y))
        {
            var approachReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = $"mining.use_elevator:approach:{target}:{actionX},{actionY}",
                    Kind = "mine_elevator_approach",
                    Available = approachReasons.Length == 0,
                    LocationId = currentLocation,
                    TileX = stand.X,
                    TileY = stand.Y,
                    ExpectedEffect = "player_adjacent_to_exact_mine_elevator_action=true;fresh_snapshot_replan_required=true",
                    EstimatedTicks = 180,
                    EnergyCost = 0,
                    AvailabilityClass = "mine_elevator_approach",
                    BlockReasons = approachReasons,
                    Parameters = identity.Concat(new[]
                    {
                        Parameter("target_tile_x", stand.X.ToString()),
                        Parameter("target_tile_y", stand.Y.ToString()),
                        Parameter("elevator_action_tile_x", actionX.ToString()),
                        Parameter("elevator_action_tile_y", actionY.ToString()),
                        Parameter("max_movement_tiles", "128")
                    }).ToArray()
                }
            };
        }

        var openReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = $"mining.use_elevator:open:{target}:{actionX},{actionY}",
                Kind = "open_mine_elevator",
                Available = openReasons.Length == 0,
                LocationId = currentLocation,
                TileX = actionX,
                TileY = actionY,
                ExpectedEffect = "menus.active_menu.type=MineElevatorMenu;fresh_snapshot_replan_required=true",
                EstimatedTicks = 30,
                EnergyCost = 0,
                AvailabilityClass = "native_mine_elevator_action_ready",
                BlockReasons = openReasons,
                Parameters = identity.Concat(new[]
                {
                    Parameter("target_tile_x", actionX.ToString()),
                    Parameter("target_tile_y", actionY.ToString()),
                    Parameter("stand_tile_x", stand?.X.ToString() ?? string.Empty),
                    Parameter("stand_tile_y", stand?.Y.ToString() ?? string.Empty),
                    Parameter("interaction_kind", "map_action"),
                    Parameter("expected_action_type", "MineElevator")
                }).ToArray()
            }
        };
    }

    private static EventCandidate[] MineElevatorMenuCandidates(SnapshotEnvelope snapshot, int? target, List<string> reasons)
    {
        var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(state.Value, "kind"), "mine_elevator", StringComparison.Ordinal))
            return new[] { BlockedMineElevatorCandidate(target, reasons.Append("mine_elevator_transparent_menu_state_missing").ToArray()) };

        var menu = state.Value;
        var entries = menu.TryGetProperty("entries", out var entriesValue) ? entriesValue : default;
        if (!target.HasValue || entries.ValueKind != JsonValueKind.Array)
            reasons.Add("mine_elevator_destination_entries_missing");
        var offered = target.HasValue && entries.ValueKind == JsonValueKind.Array && entries.EnumerateArray().Any(entry =>
            ReadInt(entry, "floor", -1) == target.Value && ReadBool(entry, "selectable") == true);
        if (!offered)
            reasons.Add("mine_elevator_target_not_selectable_in_live_menu");
        var identity = ReadString(menu, "menu_identity_sha256");
        if (string.IsNullOrWhiteSpace(identity)) reasons.Add("mine_elevator_menu_identity_missing");
        var blocks = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = $"mining.use_elevator:select:{target}:{identity}",
                Kind = "select_mine_elevator_floor",
                Available = blocks.Length == 0,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                DisplayName = target?.ToString() ?? string.Empty,
                ExpectedEffect = target == 0 ? "player.location_id=Mine;player.tile=17,4" : $"mining.current_mine.mine_level={target}",
                EstimatedTicks = 30,
                EnergyCost = 0,
                AvailabilityClass = blocks.Length == 0 ? "native_mine_elevator_destination_ready" : "mine_elevator_destination_blocked",
                BlockReasons = blocks,
                Parameters = MineElevatorIdentityParameters(target).Concat(new[]
                {
                    Parameter("target_runtime_type", "MineElevatorMenu"),
                    Parameter("target_runtime_identity", identity),
                    Parameter("mine_elevator_menu_identity_sha256", identity)
                }).ToArray()
            }
        };
    }

    private static string[] MineElevatorTargetReasons(int? target, int? deepest, int? currentLevel, string targetFamily)
    {
        var reasons = new List<string>();
        if (!target.HasValue) reasons.Add("mine_elevator_target_depth_required");
        else if (target.Value != 0 && (target.Value < 5 || target.Value > 120 || target.Value % 5 != 0))
            reasons.Add("mine_elevator_target_must_be_zero_or_multiple_of_five_through_120");
        if (!deepest.HasValue) reasons.Add("player_deepest_mine_level_missing");
        else if (target > Math.Min(deepest.Value, 120)) reasons.Add("mine_elevator_target_beyond_unlocked_checkpoint");
        if (target.HasValue && currentLevel == target) reasons.Add("mine_elevator_target_is_current_level");
        if (!string.IsNullOrWhiteSpace(targetFamily) && !string.Equals(targetFamily, "ordinary_mines", StringComparison.Ordinal))
            reasons.Add("mine_elevator_requires_ordinary_mines_family");
        return reasons.ToArray();
    }

    private static bool IsOrdinaryMineShaft(SnapshotEnvelope snapshot)
    {
        var mine = ReadStateFieldValue(snapshot, "mining", "current_mine");
        return mine.HasValue && mine.Value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(mine.Value, "mine_kind"), "ordinary_mines", StringComparison.Ordinal);
    }

    private static SmallModelActionParameter[] MineElevatorIdentityParameters(int? target) => new[]
    {
        Parameter("target_depth", target?.ToString() ?? string.Empty),
        Parameter("target_location_family", "ordinary_mines"),
        Parameter("expected_mine_level_after", target?.ToString() ?? string.Empty)
    };

    private static bool MineElevatorEndpointOrMenuPresent(SnapshotEnvelope snapshot)
    {
        if (string.Equals(ActiveMenuTypeForCandidate(snapshot), "MineElevatorMenu", StringComparison.Ordinal))
            return true;

        var endpoints = ReadStateFieldValue(snapshot, "current_location", "mine_elevator_action_tiles");
        return endpoints.HasValue && endpoints.Value.ValueKind == JsonValueKind.Array &&
            endpoints.Value.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "action_type"), "MineElevator", StringComparison.Ordinal) &&
                ReadBool(row, "menu_available") == true);
    }

    private static SmallModelActionParameter[] UpsertMineReachDepthContinuation(
        SmallModelActionParameter[] parameters,
        int objectiveTarget,
        int elevatorTarget)
    {
        var replacedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "continuation.option_id",
            "continuation.target_depth",
            "reach_depth_objective_target",
            "elevator_target_depth"
        };
        return parameters
            .Where(parameter => !replacedNames.Contains(parameter.Name))
            .Concat(new[]
            {
                Parameter("continuation.option_id", "mining.reach_depth"),
                Parameter("continuation.target_depth", objectiveTarget.ToString()),
                Parameter("reach_depth_objective_target", objectiveTarget.ToString()),
                Parameter("elevator_target_depth", elevatorTarget.ToString())
            })
            .ToArray();
    }

    private static EventCandidate BlockedMineElevatorCandidate(int? target, params string[] reasons) => new()
    {
        CandidateId = $"mining.use_elevator:blocked:{target}",
        Kind = "select_mine_elevator_floor",
        Available = false,
        ExpectedEffect = "mine_elevator_not_used",
        AvailabilityClass = "mine_elevator_blocked",
        BlockReasons = reasons.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray(),
        Parameters = MineElevatorIdentityParameters(target)
    };
}
