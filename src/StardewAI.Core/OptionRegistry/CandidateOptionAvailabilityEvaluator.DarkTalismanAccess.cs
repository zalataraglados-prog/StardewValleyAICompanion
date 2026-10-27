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
    private bool TryBuildDarkTalismanPrerequisiteCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerWindowIntentValidation intent,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation,
        out EventCandidate[] candidates)
    {
        candidates = [];
        var targetsBugLand = string.Equals(intent.TargetLocation, "BugLand", StringComparison.Ordinal);
        var targetsWitchSwamp = string.Equals(intent.TargetLocation, "WitchSwamp", StringComparison.Ordinal);
        if (!targetsBugLand && !targetsWitchSwamp)
            return false;

        var access = ReadStateFieldValue(snapshot, "locations", "dark_talisman_access");
        if (!access.HasValue || access.Value.ValueKind != JsonValueKind.Object ||
            ReadString(access.Value, "schema_version") != "dark_talisman_access.v1" ||
            ReadString(access.Value, "projection_status") != "complete_locked_base_1.6.15_native_contract")
        {
            candidates = [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_access_projection_missing",
                intentParameters)];
            return true;
        }

        var row = access.Value;
        var status = ReadString(row, "status");
        if (targetsBugLand && ReadBool(row, "krobus_unseal_mail_received") == true)
            return false;
        if (targetsWitchSwamp && status == "witch_swamp_accessible")
            return false;

        if (status != "advance_railroad_event" && ActiveMenuOpenForCandidate(snapshot))
        {
            var close = RecoveryCandidates(snapshot, [])
                .FirstOrDefault(candidate => candidate.Kind == "recovery_close_menu");
            candidates = close is null
                ? [BlockedDarkTalismanCandidate(
                    "master_angler_dark_talisman_active_menu_not_safely_closeable",
                    intentParameters)]
                : [CloneCandidate(
                    close,
                    candidateId: "master-angler-dark-talisman:close-menu:" + close.CandidateId,
                    expectedEffect: close.ExpectedEffect + ";fresh_snapshot_and_dark_talisman_replan_required=true",
                    parameters: close.Parameters.Concat(intentParameters).Concat(continuation).ToArray(),
                    availabilityClass: "master_angler_dark_talisman_menu_recovery")];
            return true;
        }

        candidates = status switch
        {
            "route_to_railroad_event" => DarkTalismanRailroadEventRouteCandidates(
                snapshot,
                intentParameters,
                continuation),
            "advance_railroad_event" => DarkTalismanStoryEventCandidates(
                snapshot,
                intentParameters,
                continuation),
            "ready_to_talk_to_krobus" => DarkTalismanKrobusCandidates(
                snapshot,
                row,
                intentParameters,
                continuation),
            "route_to_bug_lair_chest" => DarkTalismanRouteCandidates(
                snapshot,
                "BugLand",
                "route-to-bug-land",
                intentParameters,
                continuation),
            "ready_to_claim_dark_talisman" => DarkTalismanChestCandidates(
                snapshot,
                row,
                intentParameters,
                continuation),
            "ready_to_remove_railroad_statue" => DarkTalismanStatueCandidates(
                snapshot,
                row,
                intentParameters,
                continuation),
            _ => [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_access_" +
                    (string.IsNullOrWhiteSpace(status) ? "status_missing" : status),
                intentParameters)]
        };
        return true;
    }

    private EventCandidate[] DarkTalismanRailroadEventRouteCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var current = ReadStateFieldString(snapshot, "player", "location_id");
        return string.Equals(current, "Railroad", StringComparison.Ordinal)
            ? DarkTalismanRouteCandidates(
                snapshot,
                "Mountain",
                "leave-railroad-for-event-reentry",
                intentParameters,
                continuation)
            : DarkTalismanRouteCandidates(
                snapshot,
                "Railroad",
                "route-to-railroad-event",
                intentParameters,
                continuation);
    }

    private EventCandidate[] DarkTalismanStoryEventCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var events = StoryEventCandidates(snapshot, []);
        if (events.Length == 0 || events.Any(candidate =>
                !string.Equals(
                    ReadParameter(candidate.Parameters, "story_event_id"),
                    "529952",
                    StringComparison.Ordinal)))
        {
            return [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_railroad_event_boundary_unavailable",
                intentParameters)];
        }

        return events.Select(candidate => CloneCandidate(
                candidate,
                candidateId: "master-angler-dark-talisman:" + candidate.CandidateId,
                expectedEffect: candidate.ExpectedEffect +
                    ";dark_talisman_quest_28_expected_after_event=true" +
                    ";fresh_snapshot_and_dark_talisman_replan_required=true",
                parameters: candidate.Parameters
                    .Concat(intentParameters)
                    .Concat(continuation)
                    .ToArray(),
                availabilityClass: "master_angler_dark_talisman_railroad_event"))
            .ToArray();
    }

    private EventCandidate[] DarkTalismanKrobusCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var current = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(current, "Sewer", StringComparison.Ordinal))
            return DarkTalismanRouteCandidates(
                snapshot,
                "Sewer",
                "route-to-krobus",
                intentParameters,
                continuation);

        if (!access.TryGetProperty("krobus", out var krobus) ||
            krobus.ValueKind != JsonValueKind.Object ||
            ReadBool(krobus, "in_sewer") != true)
        {
            return [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_krobus_projection_drifted",
                intentParameters)];
        }
        return DarkTalismanInteractionCandidate(
            snapshot,
            "talk-krobus",
            "Sewer",
            ReadInt(krobus, "tile_x"),
            ReadInt(krobus, "tile_y"),
            "npc_check_action",
            "DarkTalismanKrobus",
            "quest.28.present=false;quests.mail_received.krobusUnseal=true",
            intentParameters,
            continuation);
    }

    private EventCandidate[] DarkTalismanChestCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var current = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(current, "BugLand", StringComparison.Ordinal))
            return DarkTalismanRouteCandidates(
                snapshot,
                "BugLand",
                "route-to-dark-talisman-chest",
                intentParameters,
                continuation);
        if (!access.TryGetProperty("bug_lair", out var bugLair) ||
            bugLair.ValueKind != JsonValueKind.Object ||
            ReadBool(bugLair, "contains_exact_dark_talisman") != true ||
            ReadInt(bugLair, "expected_special_item_which") != 6)
        {
            return [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_chest_projection_drifted",
                intentParameters)];
        }
        return DarkTalismanInteractionCandidate(
            snapshot,
            "claim-dark-talisman",
            "BugLand",
            ReadInt(bugLair, "chest_tile_x"),
            ReadInt(bugLair, "chest_tile_y"),
            "overlay_object",
            "DarkTalismanChest",
            "player.has_dark_talisman=true",
            intentParameters,
            continuation);
    }

    private EventCandidate[] DarkTalismanStatueCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var current = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(current, "Railroad", StringComparison.Ordinal))
            return DarkTalismanRouteCandidates(
                snapshot,
                "Railroad",
                "route-to-witch-statue",
                intentParameters,
                continuation);
        if (!access.TryGetProperty("railroad_statue", out var statue) ||
            statue.ValueKind != JsonValueKind.Object ||
            ReadBool(statue, "identity_exact") != true ||
            ReadInt(statue, "buildings_tile_index") != 287)
        {
            return [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_statue_projection_drifted",
                intentParameters)];
        }
        return DarkTalismanInteractionCandidate(
            snapshot,
            "remove-witch-statue",
            "Railroad",
            ReadInt(statue, "tile_x"),
            ReadInt(statue, "tile_y"),
            "location_check_action",
            "DarkTalismanStatue",
            "quests.mail_received.witchStatueGone=true",
            intentParameters,
            continuation);
    }

    private EventCandidate[] DarkTalismanInteractionCandidate(
        SnapshotEnvelope snapshot,
        string stage,
        string locationId,
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
            return [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_" + stage + "_no_reachable_adjacent_stand",
                intentParameters)];

        var parameters = new[]
            {
                Parameter("target_location", locationId),
                Parameter("target_tile_x", targetX.ToString(CultureInfo.InvariantCulture)),
                Parameter("target_tile_y", targetY.ToString(CultureInfo.InvariantCulture)),
                Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
                Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
                Parameter("route_distance_tiles", stand.Distance.ToString(CultureInfo.InvariantCulture)),
                Parameter("interaction_kind", interactionKind),
                Parameter("expected_action_type", expectedActionType),
                Parameter("required_executor_profile", "dark_talisman_story_executor"),
                Parameter("dark_talisman_stage", stage),
                Parameter("dark_talisman_required_postcondition", postcondition)
            }
            .Concat(intentParameters)
            .Concat(continuation)
            .ToArray();
        return
        [
            new EventCandidate
            {
                CandidateId = "master-angler-dark-talisman:" + stage + ":" + locationId + ":" + targetX + "," + targetY,
                Kind = "interact_endpoint",
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                LocationId = locationId,
                TileX = targetX,
                TileY = targetY,
                EstimatedTicks = Math.Max(60, (stand.Distance + 1) * 60),
                EnergyCost = 0,
                AvailabilityClass = "master_angler_dark_talisman_exact_native_" + stage,
                ExpectedEffect = "move_to_adjacent=" + stand.X + "," + stand.Y +
                    ";preview_interact=" + expectedActionType +
                    ";" + postcondition +
                    ";fresh_snapshot_and_dark_talisman_replan_required=true",
                Parameters = parameters
            }
        ];
    }

    private EventCandidate[] DarkTalismanRouteCandidates(
        SnapshotEnvelope snapshot,
        string targetLocation,
        string stage,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var current = ReadStateFieldString(snapshot, "player", "location_id");
        var route = FindResolvedRoutePlan(
                snapshot,
                current,
                targetLocation,
                RouteConnectorCandidates(snapshot, int.MaxValue))
            ?.FirstActionCandidate;
        if (route is null)
            return [BlockedDarkTalismanCandidate(
                "master_angler_dark_talisman_" + stage + "_route_unavailable",
                intentParameters)];

        return
        [
            CloneCandidate(
                route,
                candidateId: "master-angler-dark-talisman:" + stage + ":" + route.CandidateId,
                expectedEffect: route.ExpectedEffect +
                    ";dark_talisman_stage=" + stage +
                    ";fresh_snapshot_and_dark_talisman_replan_required=true",
                parameters: route.Parameters
                    .Concat(intentParameters)
                    .Concat(continuation)
                    .ToArray(),
                availabilityClass: "master_angler_dark_talisman_" + stage)
        ];
    }

    private static EventCandidate BlockedDarkTalismanCandidate(
        string reason,
        SmallModelActionParameter[] parameters) => new()
        {
            CandidateId = "master-angler:dark-talisman-access:blocked",
            Kind = "dark_talisman_access_wait",
            Available = false,
            AllowedNow = false,
            AllowedToday = false,
            EstimatedTicks = -1,
            AvailabilityClass = "master_angler_dark_talisman_access_fail_closed",
            ExpectedEffect = "dark_talisman_access_not_compiled;fresh_snapshot_required=true",
            BlockReasons = [reason],
            Parameters = parameters
        };
}
