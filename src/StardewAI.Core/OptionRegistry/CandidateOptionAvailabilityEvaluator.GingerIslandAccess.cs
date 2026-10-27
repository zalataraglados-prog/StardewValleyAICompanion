using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static readonly IslandGourmandSupplyScheduleProof GourmandSupplyDeadline =
        IslandGourmandSupplyScheduleProofBuilder.Build();

    private bool TryBuildGingerIslandPrerequisiteCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation,
        out EventCandidate[] candidates)
    {
        candidates = Array.Empty<EventCandidate>();
        var targetLocation = intentParameters
            .Single(parameter => parameter.Name == "master_angler_target_location")
            .Value;
        var gingerIslandTarget =
            string.Equals(targetLocation, "IslandSouth", StringComparison.Ordinal) ||
            string.Equals(targetLocation, "IslandNorth", StringComparison.Ordinal) ||
            string.Equals(targetLocation, "IslandWest", StringComparison.Ordinal) ||
            string.Equals(targetLocation, "IslandSouthEast", StringComparison.Ordinal) ||
            string.Equals(targetLocation, "IslandSouthEastCave", StringComparison.Ordinal);
        if (!gingerIslandTarget)
            return false;

        var access = ReadStateFieldValue(snapshot, "locations", "ginger_island_access");
        if (!access.HasValue || access.Value.ValueKind != JsonValueKind.Object ||
            ReadString(access.Value, "schema_version") != "ginger_island_access.v1" ||
            ReadString(access.Value, "projection_status") != "complete_locked_base_1.6.15_native_contract")
        {
            candidates = new[] { BlockedGingerIslandCandidate("master_angler_ginger_island_access_projection_missing", intentParameters) };
            return true;
        }

        var row = access.Value;
        var targetAccess = row.GetProperty("target_access");
        if (targetLocation == "IslandSouth" && ReadBool(targetAccess, "island_south_accessible") == true ||
            targetLocation == "IslandNorth" && ReadBool(targetAccess, "island_north_accessible") == true ||
            targetLocation == "IslandWest" && ReadBool(targetAccess, "island_west_accessible") == true ||
            targetLocation == "IslandSouthEast" && ReadBool(targetAccess, "island_south_east_accessible") == true ||
            targetLocation == "IslandSouthEastCave" && ReadBool(targetAccess, "island_south_east_cave_accessible") == true)
        {
            return false;
        }

        var status = ReadString(row, "status");
        var resortAccessRequired =
            targetLocation is "IslandSouthEast" or "IslandSouthEastCave";
        var deadlineBlockReason = resortAccessRequired
            ? GingerIslandResortDeadlineBlockReason(
                snapshot,
                ReadBool(targetAccess, "island_west_accessible") == true)
            : null;
        if (deadlineBlockReason is not null)
        {
            candidates = new[]
            {
                BlockedGingerIslandCandidate(
                    deadlineBlockReason,
                    intentParameters)
            };
            return true;
        }
        if (ActiveMenuOpenForCandidate(snapshot) &&
            status is not ("process_invitation_mail" or "advance_boat_departure_event" or "advance_boat_journey"))
        {
            var close = RecoveryCandidates(snapshot, Array.Empty<SmallModelActionParameter>())
                .FirstOrDefault(candidate => candidate.Kind == "recovery_close_menu");
            candidates = close is null
                ? new[] { BlockedGingerIslandCandidate("master_angler_ginger_island_active_menu_not_safely_closeable", intentParameters) }
                : GingerIslandClone(new[] { close }, "close-menu", intentParameters, continuation);
            return true;
        }

        candidates = status switch
        {
            "wait_for_invitation_mail" or "wait_for_boat_repair_mail" or "wait_for_boat_repair_night" =>
                GingerIslandWaitCandidates(snapshot, status, intentParameters, continuation),
            "process_invitation_mail" => GingerIslandClone(
                MailProcessingCandidates(snapshot), "process-invitation", intentParameters, continuation),
            "ready_to_donate_batteries" => GingerIslandRepairCandidates(
                snapshot, row, "battery", intentParameters, continuation),
            "ready_to_donate_hardwood" => GingerIslandRepairCandidates(
                snapshot, row, "hull", intentParameters, continuation),
            "ready_to_donate_iridium" => GingerIslandRepairCandidates(
                snapshot, row, "anchor", intentParameters, continuation),
            "requires_battery_packs" => GingerIslandMaterialSupplyCandidates(
                snapshot, row, "battery", intentParameters, continuation),
            "requires_hardwood" => GingerIslandMaterialSupplyCandidates(
                snapshot, row, "hull", intentParameters, continuation),
            "requires_iridium_bars" => GingerIslandMaterialSupplyCandidates(
                snapshot, row, "anchor", intentParameters, continuation),
            "requires_ticket_money" => GingerIslandTicketMoneyCandidates(
                snapshot, row, intentParameters, continuation),
            "requires_first_parrot_walnut" => GingerIslandWalnutSupplyCandidates(
                snapshot, row, "first_parrot", intentParameters, continuation),
            "requires_western_turtle_walnuts" => GingerIslandWalnutSupplyCandidates(
                snapshot, row, "western_turtle_parrot", intentParameters, continuation),
            "requires_island_house_walnuts" => GingerIslandWalnutSupplyCandidates(
                snapshot, row, "island_house_parrot", intentParameters, continuation),
            "requires_resort_walnuts" => GingerIslandWalnutSupplyCandidates(
                snapshot, row, "resort_parrot", intentParameters, continuation),
            "ready_to_buy_ticket" => GingerIslandTicketCandidates(
                snapshot, row, intentParameters, continuation),
            "advance_boat_departure_event" => GingerIslandClone(
                StoryEventCandidates(snapshot, Array.Empty<SmallModelActionParameter>())
                    .Where(candidate => ReadParameter(candidate.Parameters, "story_event_id") == "-78765")
                    .ToArray(),
                "boat-departure", intentParameters, continuation),
            "advance_boat_journey" => GingerIslandClone(
                StoryEventMinigameCandidates(snapshot, Array.Empty<SmallModelActionParameter>())
                    .Where(candidate => ReadParameter(candidate.Parameters, "story_event_minigame_type") ==
                        "StardewValley.Minigames.BoatJourney")
                    .ToArray(),
                "boat-journey", intentParameters, continuation),
            "route_to_island_hut" => GingerIslandRouteCandidates(
                snapshot, "IslandHut", "route-to-first-parrot", intentParameters, continuation),
            "ready_to_unlock_first_parrot" => GingerIslandPerchCandidates(
                snapshot, row, "first_parrot", "unlock-first-parrot", "IslandFirstParrot", intentParameters, continuation),
            "route_to_western_turtle" => GingerIslandRouteCandidates(
                snapshot, "IslandSouth", "route-to-western-turtle", intentParameters, continuation),
            "ready_to_move_western_turtle" => GingerIslandPerchCandidates(
                snapshot, row, "western_turtle_parrot", "move-western-turtle", "IslandWesternTurtle", intentParameters, continuation),
            "route_to_island_house" => GingerIslandRouteCandidates(
                snapshot, "IslandWest", "route-to-island-house", intentParameters, continuation),
            "ready_to_restore_island_house" => GingerIslandPerchCandidates(
                snapshot, row, "island_house_parrot", "restore-island-house", "IslandHouse", intentParameters, continuation),
            "route_to_resort_parrot" => GingerIslandRouteCandidates(
                snapshot, "IslandSouth", "route-to-resort-parrot", intentParameters, continuation),
            "ready_to_restore_resort" => GingerIslandPerchCandidates(
                snapshot, row, "resort_parrot", "restore-resort", "IslandResort", intentParameters, continuation),
            _ => new[] { BlockedGingerIslandCandidate(
                "master_angler_ginger_island_access_" +
                (string.IsNullOrWhiteSpace(status) ? "status_missing" : status),
                intentParameters) }
        };
        if (candidates.Length == 0)
            candidates = new[] { BlockedGingerIslandCandidate("master_angler_ginger_island_native_subflow_unavailable", intentParameters) };
        if (resortAccessRequired)
        {
            candidates = candidates.Select(candidate => CloneCandidate(
                candidate,
                expectedEffect: candidate.ExpectedEffect +
                    ";gourmand_latest_western_turtle_unlock_total_day=" +
                    GourmandSupplyDeadline.LatestWesternTurtleUnlockTotalDayInclusive +
                    ";gourmand_latest_island_house_unlock_total_day=" +
                    GourmandSupplyDeadline.LatestIslandHouseUnlockTotalDayInclusive,
                parameters: candidate.Parameters.Concat(new[]
                {
                    Parameter(
                        "ginger_island_latest_western_turtle_unlock_total_day_inclusive",
                        GourmandSupplyDeadline.LatestWesternTurtleUnlockTotalDayInclusive
                            .ToString(CultureInfo.InvariantCulture)),
                    Parameter(
                        "ginger_island_latest_island_house_unlock_total_day_inclusive",
                        GourmandSupplyDeadline.LatestIslandHouseUnlockTotalDayInclusive
                            .ToString(CultureInfo.InvariantCulture))
                }).ToArray())).ToArray();
        }
        return true;
    }

    private static string? GingerIslandResortDeadlineBlockReason(
        SnapshotEnvelope snapshot,
        bool westernTurtleComplete)
    {
        var currentTotalDay = ReadStateFieldInt(
            snapshot,
            "time",
            "total_days");
        return !westernTurtleComplete &&
               currentTotalDay >
               GourmandSupplyDeadline.LatestWesternTurtleUnlockTotalDayInclusive
            ? "master_angler_ginger_island_western_turtle_deadline_exceeded"
            : null;
    }

    private EventCandidate[] GingerIslandRepairCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        string repairKey,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        if (ReadStateFieldString(snapshot, "player", "location_id") != "BoatTunnel")
            return GingerIslandRouteCandidates(snapshot, "BoatTunnel", "route-to-repair-" + repairKey, intentParameters, continuation);
        var repair = access.GetProperty("repairs").GetProperty(repairKey);
        return GingerIslandInteractionCandidates(
            snapshot,
            "repair-" + repairKey,
            "BoatTunnel",
            ReadInt(repair, "tile_x"),
            ReadInt(repair, "tile_y"),
            "boat_tunnel_check_action",
            ReadString(repair, "expected_action_type"),
            "repairs." + repairKey + ".complete_or_pending=true",
            intentParameters,
            continuation);
    }

    private EventCandidate[] GingerIslandTicketCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        if (ReadStateFieldString(snapshot, "player", "location_id") != "BoatTunnel")
            return GingerIslandRouteCandidates(snapshot, "BoatTunnel", "route-to-ticket", intentParameters, continuation);
        var ticket = access.GetProperty("ticket");
        return GingerIslandInteractionCandidates(
            snapshot,
            "buy-ticket",
            "BoatTunnel",
            ReadInt(ticket, "tile_x"),
            ReadInt(ticket, "tile_y"),
            "boat_tunnel_check_action",
            "WillyBoatTicket",
            "journey.departure_event_active=true",
            intentParameters,
            continuation);
    }

    private EventCandidate[] GingerIslandPerchCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        string field,
        string stage,
        string expectedActionType,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var perch = access.GetProperty(field);
        return GingerIslandInteractionCandidates(
            snapshot,
            stage,
            ReadString(perch, "source_location"),
            ReadInt(perch, "tile_x"),
            ReadInt(perch, "tile_y"),
            "parrot_upgrade_perch",
            expectedActionType,
            field + ".complete_or_pending=true",
            intentParameters,
            continuation);
    }

    private EventCandidate[] GingerIslandInteractionCandidates(
        SnapshotEnvelope snapshot,
        string stage,
        string location,
        int targetX,
        int targetY,
        string interactionKind,
        string expectedActionType,
        string postcondition,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var stand = FindBestStandTile(snapshot, targetX, targetY);
        if (stand is null)
            return new[] { BlockedGingerIslandCandidate("master_angler_ginger_island_" + stage + "_no_reachable_adjacent_stand", intentParameters) };
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "master-angler-ginger-island:" + stage + ":" + location + ":" + targetX + "," + targetY,
                Kind = "interact_endpoint",
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                LocationId = location,
                TileX = targetX,
                TileY = targetY,
                EstimatedTicks = Math.Max(60, (stand.Distance + 1) * 60),
                EnergyCost = 0,
                AvailabilityClass = "master_angler_ginger_island_exact_native_" + stage,
                ExpectedEffect = "move_to_adjacent=" + stand.X + "," + stand.Y +
                    ";preview_interact=" + expectedActionType + ";" + postcondition +
                    ";fresh_snapshot_and_ginger_island_replan_required=true",
                Parameters = new[]
                {
                    Parameter("target_location", location),
                    Parameter("target_tile_x", targetX.ToString(CultureInfo.InvariantCulture)),
                    Parameter("target_tile_y", targetY.ToString(CultureInfo.InvariantCulture)),
                    Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
                    Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
                    Parameter("route_distance_tiles", stand.Distance.ToString(CultureInfo.InvariantCulture)),
                    Parameter("interaction_kind", interactionKind),
                    Parameter("expected_action_type", expectedActionType),
                    Parameter("required_executor_profile", "ginger_island_access_executor"),
                    Parameter("ginger_island_stage", stage),
                    Parameter("ginger_island_required_postcondition", postcondition)
                }.Concat(intentParameters).Concat(continuation).ToArray()
            }
        };
    }

    private EventCandidate[] GingerIslandRouteCandidates(
        SnapshotEnvelope snapshot,
        string targetLocation,
        string stage,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var current = ReadStateFieldString(snapshot, "player", "location_id");
        var route = FindResolvedRoutePlan(
            snapshot, current, targetLocation, RouteConnectorCandidates(snapshot, int.MaxValue))?.FirstActionCandidate;
        return route is null
            ? new[] { BlockedGingerIslandCandidate("master_angler_ginger_island_" + stage + "_route_unavailable", intentParameters) }
            : GingerIslandClone(new[] { route }, stage, intentParameters, continuation);
    }

    private EventCandidate[] GingerIslandWaitCandidates(
        SnapshotEnvelope snapshot,
        string stage,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var waits = RecoveryCandidates(snapshot, new[] { Parameter("control_plane.native_save_boundary", "true") });
        return GingerIslandClone(waits, stage, intentParameters, continuation);
    }

    private static EventCandidate[] GingerIslandClone(
        EventCandidate[] source,
        string stage,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation) => source.Select(candidate => CloneCandidate(
            candidate,
            candidateId: "master-angler-ginger-island:" + stage + ":" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect + ";ginger_island_stage=" + stage +
                ";fresh_snapshot_and_ginger_island_replan_required=true",
            parameters: candidate.Parameters
                .Where(parameter => !parameter.Name.StartsWith("continuation.", StringComparison.Ordinal))
                .Concat(intentParameters)
                .Concat(continuation)
                .ToArray(),
            availabilityClass: "master_angler_ginger_island_" + stage)).ToArray();

    private static EventCandidate BlockedGingerIslandCandidate(
        string reason,
        SmallModelActionParameter[] parameters) => new()
        {
            CandidateId = "master-angler:ginger-island-access:blocked",
            Kind = "ginger_island_access_wait",
            Available = false,
            AllowedNow = false,
            AllowedToday = false,
            EstimatedTicks = -1,
            AvailabilityClass = "master_angler_ginger_island_access_fail_closed",
            ExpectedEffect = "ginger_island_access_not_compiled;fresh_snapshot_required=true",
            BlockReasons = new[] { reason },
            Parameters = parameters
        };
}
