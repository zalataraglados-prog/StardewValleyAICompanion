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
    private EventCandidate[] RainTotemHardwoodReserveCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters,
        MasterAnglerRainTotemReserveIntentValidation intent)
    {
        EventCandidate[] candidates = intent.ReserveStage switch
        {
            "clear_loaded_farm_resource_clump" =>
                ClearFarmResourceClumpCandidates(snapshot)
                    .Where(candidate => HardwoodSourceMatches(candidate, intent))
                    .ToArray(),
            "chop_loaded_wild_tree" =>
                WildTreeChopAcquisitionCandidates(snapshot)
                    .Where(candidate => HardwoodSourceMatches(candidate, intent))
                    .ToArray(),
            "route_to_farm_resource_clump" =>
                RouteToHardwoodFarmSourceCandidates(snapshot, intent),
            "route_to_blacksmith_tool_upgrade" or
                "route_to_blacksmith_tool_claim" =>
                RouteToHardwoodToolUpgradeCandidates(snapshot, intent),
            "submit_next_axe_upgrade" =>
                HardwoodToolUpgradeActionCandidates(snapshot, intent, claim: false),
            "claim_ready_axe_upgrade" =>
                HardwoodToolUpgradeActionCandidates(snapshot, intent, claim: true),
            "wait_for_axe_upgrade" or
                "wait_for_other_tool_upgrade" or
                "blocked_axe_claim_inventory_full" or
                "blocked_tool_upgrade_requirements_missing" or
                "blocked_no_next_axe_upgrade" or
                "blocked_blacksmith_service_unavailable" or
                "blocked_tool_upgrade_projection_missing" or
                "blocked_no_exact_hardwood_source" =>
            [
                BlockedHardwoodReserveCandidate(
                    "master_angler_hardwood_reserve_" + intent.ReserveStage,
                    boundParameters,
                    intent.ReserveStage)
            ],
            _ => Array.Empty<EventCandidate>()
        };

        var bound = candidates
            .Select(candidate => BindHardwoodReserveCandidate(
                candidate,
                boundParameters,
                intent))
            .ToArray();
        return bound.Length > 0
            ? bound
            :
            [
                BlockedHardwoodReserveCandidate(
                    "master_angler_hardwood_reserve_no_exact_stage_candidate:" +
                        intent.ReserveStage,
                    boundParameters,
                    intent.ReserveStage)
            ];
    }

    private EventCandidate[] RouteToHardwoodToolUpgradeCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerRainTotemReserveIntentValidation intent)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "tool_upgrade");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_live_native_contract" ||
            ReadString(projection.Value, "lifecycle_stage") != intent.ReserveStage)
        {
            return Array.Empty<EventCandidate>();
        }
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(projection.Value, "location_id");
        var route = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                targetLocation,
                RouteConnectorCandidates(snapshot, int.MaxValue)
                    .Where(candidate => candidate.Kind == "route_connector_tile")
                    .ToArray())
            ?.FirstActionCandidate;
        return route is null
            ? Array.Empty<EventCandidate>()
            :
            [
                CloneCandidate(
                    route,
                    candidateId: "rain-totem-hardwood-tool-route:" +
                        intent.ReserveStage + ":" + currentLocation,
                    expectedEffect: route.ExpectedEffect +
                        ";tool_upgrade_continuation=" + intent.ReserveStage +
                        ";fresh_snapshot_replan_required=true",
                    availabilityClass: "master_angler_hardwood_tool_upgrade_route")
            ];
    }

    private EventCandidate[] HardwoodToolUpgradeActionCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerRainTotemReserveIntentValidation intent,
        bool claim)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "tool_upgrade");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_live_native_contract" ||
            ReadString(projection.Value, "lifecycle_stage") != intent.ReserveStage ||
            ReadString(projection.Value, "service_status") != "ready" ||
            !TryResolveHardwoodToolUpgradeTarget(snapshot, projection.Value, out var target))
        {
            return Array.Empty<EventCandidate>();
        }

        var detailName = claim ? "pending_tool" : "next_axe_upgrade";
        if (!projection.Value.TryGetProperty(detailName, out var detail) ||
            detail.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }
        var qualifiedItemId = ReadString(
            detail,
            claim ? "qualified_item_id" : "target_qualified_item_id");
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            return Array.Empty<EventCandidate>();

        var parameters = new System.Collections.Generic.List<SmallModelActionParameter>
        {
            Parameter("tool_upgrade_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("tool_upgrade_lifecycle_stage", intent.ReserveStage),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("tool_upgrade_action_raw", target.ActionRaw),
            Parameter("tool_upgrade_action_token", "Blacksmith"),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
        if (claim)
        {
            parameters.AddRange(
            [
                Parameter("tool_upgrade_pending_qualified_item_id", ReadString(detail, "qualified_item_id")),
                Parameter("tool_upgrade_pending_item_id", ReadString(detail, "item_id")),
                Parameter("tool_upgrade_pending_level", ReadInt(detail, "upgrade_level").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_days_left_before", ReadInt(detail, "days_left").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_free_slots_before", ReadInt(projection.Value, "free_inventory_slots").ToString(CultureInfo.InvariantCulture))
            ]);
        }
        else
        {
            parameters.AddRange(
            [
                Parameter("tool_upgrade_source_slot_index", ReadInt(detail, "source_slot_index").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_source_qualified_item_id", ReadString(detail, "source_qualified_item_id")),
                Parameter("tool_upgrade_source_item_id", ReadString(detail, "source_item_id")),
                Parameter("tool_upgrade_source_level", ReadInt(detail, "source_upgrade_level").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_target_qualified_item_id", ReadString(detail, "target_qualified_item_id")),
                Parameter("tool_upgrade_target_item_id", ReadString(detail, "target_item_id")),
                Parameter("tool_upgrade_target_level", ReadInt(detail, "target_upgrade_level").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_money_before", ReadInt(projection.Value, "money_before").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_price_gold", ReadInt(detail, "price_gold").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_trade_qualified_item_id", ReadString(detail, "trade_item_qualified_item_id")),
                Parameter("tool_upgrade_trade_item_id", ReadString(detail, "trade_item_id")),
                Parameter("tool_upgrade_trade_amount", ReadInt(detail, "trade_item_amount").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_trade_available_before", ReadInt(detail, "trade_item_available").ToString(CultureInfo.InvariantCulture)),
                Parameter("tool_upgrade_expected_days_left_after", "2")
            ]);
        }

        return
        [
            new EventCandidate
            {
                CandidateId = "rain-totem-hardwood-" +
                    (claim ? "claim-tool:" : "submit-tool:") + qualifiedItemId,
                Kind = claim ? "claim_tool_upgrade" : "submit_tool_upgrade",
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                LocationId = "Blacksmith",
                TileX = target.TargetX,
                TileY = target.TargetY,
                ItemId = ReadString(detail, claim ? "item_id" : "target_item_id"),
                QualifiedItemId = qualifiedItemId,
                Quantity = 1,
                EstimatedTicks = claim ? 180 : 300,
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_blacksmith_tool_upgrade",
                ExpectedEffect = claim
                    ? "completed_axe_upgrade_claimed_through_native_blacksmith_counter"
                    : "source_axe_money_and_bars_consumed_by_native_ClintUpgrade_purchase",
                Parameters = parameters.ToArray()
            }
        ];
    }

    private static bool TryResolveHardwoodToolUpgradeTarget(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        out HardwoodToolUpgradeTarget target)
    {
        target = default!;
        if (!projection.TryGetProperty("counter_action_tiles", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var candidates = rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                ReadString(row, "action_token") == "Blacksmith")
            .Select(row => new
            {
                Row = row,
                Stand = FindBestStandTile(
                    snapshot,
                    ReadInt(row, "tile_x"),
                    ReadInt(row, "tile_y"))
            })
            .Where(row => row.Stand is not null)
            .OrderBy(row => row.Stand!.Distance)
            .ThenBy(row => ReadInt(row.Row, "tile_y"))
            .ThenBy(row => ReadInt(row.Row, "tile_x"))
            .FirstOrDefault();
        if (candidates is null)
            return false;
        target = new HardwoodToolUpgradeTarget(
            ReadInt(candidates.Row, "tile_x"),
            ReadInt(candidates.Row, "tile_y"),
            candidates.Stand!.X,
            candidates.Stand.Y,
            ReadString(candidates.Row, "action_raw"));
        return true;
    }

    private sealed record HardwoodToolUpgradeTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        string ActionRaw);

    private EventCandidate[] RouteToHardwoodFarmSourceCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerRainTotemReserveIntentValidation intent)
    {
        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var route = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                intent.HardwoodSourceLocationId,
                RouteConnectorCandidates(snapshot, int.MaxValue))
            ?.FirstActionCandidate;
        return route is null
            ? Array.Empty<EventCandidate>()
            :
            [
                CloneCandidate(
                    route,
                    candidateId: "rain-totem-hardwood-route:" +
                        currentLocation + ":" +
                        intent.HardwoodSourceLocationId + ":" +
                        route.CandidateId,
                    expectedEffect: route.ExpectedEffect +
                        ";hardwood_source_location=" +
                        intent.HardwoodSourceLocationId +
                        ";hardwood_source_tile=" +
                        intent.HardwoodSourceTileX.ToString(CultureInfo.InvariantCulture) +
                        "," +
                        intent.HardwoodSourceTileY.ToString(CultureInfo.InvariantCulture) +
                        ";fresh_snapshot_replan_required=true",
                    availabilityClass:
                        "master_angler_hardwood_source_route")
            ];
    }

    private static bool HardwoodSourceMatches(
        EventCandidate candidate,
        MasterAnglerRainTotemReserveIntentValidation intent) =>
        string.Equals(
            candidate.LocationId,
            intent.HardwoodSourceLocationId,
            StringComparison.OrdinalIgnoreCase) &&
        candidate.TileX == intent.HardwoodSourceTileX &&
        candidate.TileY == intent.HardwoodSourceTileY &&
        CandidateGuaranteesItem(candidate, "(O)709") &&
        CandidateGuaranteedItemQuantity(candidate, "(O)709") ==
            intent.HardwoodSourceGuaranteedQuantity;

    private static EventCandidate BindHardwoodReserveCandidate(
        EventCandidate candidate,
        SmallModelActionParameter[] boundParameters,
        MasterAnglerRainTotemReserveIntentValidation intent) =>
        CloneCandidate(
            candidate,
            candidateId: "rain-totem-hardwood-reserve:" +
                intent.ReserveStage + ":" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect +
                ";master_angler_weather_reserve_material=(O)709" +
                ";master_angler_hardwood_reserve_stage=" +
                intent.ReserveStage +
                ";master_angler_hardwood_guaranteed_quantity=" +
                intent.HardwoodSourceGuaranteedQuantity.ToString(
                    CultureInfo.InvariantCulture) +
                ";fresh_snapshot_and_weather_reserve_replan_required=true",
            parameters: candidate.Parameters
                .Concat(boundParameters)
                .ToArray(),
            availabilityClass:
                "master_angler_future_rain_window_hardwood_" +
                intent.ReserveStage);

    private static EventCandidate BlockedHardwoodReserveCandidate(
        string reason,
        SmallModelActionParameter[] parameters,
        string stage) => new()
        {
            CandidateId = "master-angler:rain-totem-hardwood-reserve:" +
                stage + ":blocked",
            Kind = "rain_totem_hardwood_reserve_wait",
            Available = false,
            QualifiedItemId = "(O)709",
            Quantity = 1,
            EstimatedTicks = -1,
            AvailabilityClass =
                "master_angler_hardwood_reserve_fail_closed",
            ExpectedEffect =
                "master_angler_hardwood_reserve_not_compiled;fresh_snapshot_required=true",
            BlockReasons = [reason],
            Parameters = parameters
        };
}
