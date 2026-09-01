using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] BuildingConstructionCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var buildingType = IntentParameter(intent, "building_type");
        var placementLocation = IntentParameter(intent, "placement_location_id");
        var reason = IntentParameter(intent, "construction_reason");
        if (string.IsNullOrWhiteSpace(buildingType) ||
            string.IsNullOrWhiteSpace(placementLocation) ||
            string.IsNullOrWhiteSpace(reason))
        {
            return Array.Empty<EventCandidate>();
        }

        var catalog = ReadStateFieldValue(snapshot, "player", "building_construction_catalog");
        if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Object ||
            !catalog.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }
        var row = rows.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(value, "building_type"), buildingType, StringComparison.Ordinal) &&
            string.Equals(ReadString(value, "placement_location_id"), placementLocation, StringComparison.Ordinal));
        if (row.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var serviceLocation = ReadString(row, "service_location_id");
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, serviceLocation, StringComparison.OrdinalIgnoreCase))
        {
            if (ReadString(row, "action_status") != "route_to_builder_service_required")
            {
                return Array.Empty<EventCandidate>();
            }
            return BuildingConstructionRouteCandidate(snapshot, row, reason, currentLocation, serviceLocation);
        }

        var actionX = NullableReadInt(row, "service_action_tile_x");
        var actionY = NullableReadInt(row, "service_action_tile_y");
        var placementX = NullableReadInt(row, "placement_tile_x");
        var placementY = NullableReadInt(row, "placement_tile_y");
        var stand = actionX.HasValue && actionY.HasValue
            ? FindBestStandTile(snapshot, actionX.Value, actionY.Value)
            : null;
        var materials = row.TryGetProperty("build_materials", out var materialRows) && materialRows.ValueKind == JsonValueKind.Array
            ? materialRows.GetRawText()
            : "[]";
        var reasons = new List<string>();
        if (ReadString(row, "action_status") != "ready_for_native_construction")
        {
            reasons.Add("building_construction_not_ready:" + ReadString(row, "action_status"));
        }
        if (!actionX.HasValue || !actionY.HasValue || !placementX.HasValue || !placementY.HasValue || stand is null)
        {
            reasons.Add("building_construction_service_stand_or_placement_unavailable");
        }
        var reservation = new MachineCraftingMaterialReservationGuard().Evaluate(
            snapshot,
            materialRows,
            usesWorkbench: false,
            commitmentLedger);
        reasons.AddRange(reservation.BlockingReasons);

        var parameters = actionX.HasValue && actionY.HasValue && placementX.HasValue && placementY.HasValue && stand is not null
            ? BuildingConstructionParameters(row, reason, materials, reservation, actionX.Value, actionY.Value, stand, placementX.Value, placementY.Value)
            : Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "building-construct:" + buildingType + ":" + placementLocation,
                Kind = "construct_building",
                Available = reasons.Count == 0,
                LocationId = serviceLocation,
                TileX = actionX,
                TileY = actionY,
                DisplayName = ReadString(row, "display_name"),
                Quantity = 1,
                UnitPrice = ReadInt(row, "build_cost"),
                TotalValue = ReadInt(row, "build_cost"),
                EstimatedTicks = 600,
                EnergyCost = 0,
                AvailabilityClass = "transparent_purpose_bound_native_construction",
                ExpectedEffect = "building=" + buildingType + ";location=" + placementLocation +
                    ";construction_reason=" + reason + ";native_construction_started=true;fresh_snapshot_replan_required=true",
                BlockReasons = reasons.ToArray(),
                Parameters = parameters
            }
        };
    }

    private EventCandidate[] BuildingConstructionRouteCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        string reason,
        string currentLocation,
        string serviceLocation)
    {
        var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue)
            .Where(candidate => candidate.Kind == "route_connector_tile")
            .ToArray();
        var plan = FindResolvedRoutePlan(snapshot, currentLocation, serviceLocation, routeCandidates);
        if (plan?.FirstActionCandidate is null)
        {
            return Array.Empty<EventCandidate>();
        }
        var continuation = new[]
        {
            Parameter("continuation.building_type", ReadString(row, "building_type")),
            Parameter("continuation.placement_location_id", ReadString(row, "placement_location_id")),
            Parameter("continuation.construction_reason", reason)
        };
        return new[]
        {
            CloneCandidate(
                plan.FirstActionCandidate,
                candidateId: "building-route:" + ReadString(row, "building_type") + ":" + currentLocation + ":" + serviceLocation,
                expectedEffect: plan.FirstActionCandidate.ExpectedEffect + ";building_construction_service_location=" + serviceLocation,
                parameters: plan.FirstActionCandidate.Parameters.Concat(continuation).ToArray(),
                availabilityClass: "building_construction_rolling_route")
        };
    }

    private static SmallModelActionParameter[] BuildingConstructionParameters(
        JsonElement row,
        string reason,
        string materials,
        MachineCraftingMaterialReservationGuardResult reservation,
        int actionX,
        int actionY,
        CandidateTile stand,
        int placementX,
        int placementY) => new[]
    {
        Parameter("construction_purpose", "general_strategy"),
        Parameter("construction_reason", reason),
        Parameter("construction_building_type", ReadString(row, "building_type")),
        Parameter("project_id", ReadString(row, "building_type")),
        Parameter("construction_builder", ReadString(row, "builder")),
        Parameter("construction_build_days", ReadInt(row, "build_days").ToString(CultureInfo.InvariantCulture)),
        Parameter("construction_build_cost", ReadInt(row, "build_cost").ToString(CultureInfo.InvariantCulture)),
        Parameter("price", ReadInt(row, "build_cost").ToString(CultureInfo.InvariantCulture)),
        Parameter("construction_materials_json", materials),
        Parameter("commitment_ledger_id", reservation.LedgerId),
        Parameter("commitment_ledger_revision", reservation.LedgerRevision.ToString(CultureInfo.InvariantCulture)),
        Parameter("material_reservation_guard_status", reservation.Status),
        Parameter("material_reservation_ledger_id", reservation.LedgerId),
        Parameter("material_reservation_ledger_revision", reservation.LedgerRevision.ToString(CultureInfo.InvariantCulture)),
        Parameter("material_reservation_ids_json", JsonSerializer.Serialize(reservation.ReservationIds)),
        Parameter("expected_money_before", ReadInt(row, "expected_money_before").ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_money_after", ReadInt(row, "expected_money_after").ToString(CultureInfo.InvariantCulture)),
        Parameter("location_id", ReadString(row, "service_location_id")),
        Parameter("target_tile_x", actionX.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", actionY.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("placement_location_id", ReadString(row, "placement_location_id")),
        Parameter("building_tile_x", placementX.ToString(CultureInfo.InvariantCulture)),
        Parameter("building_tile_y", placementY.ToString(CultureInfo.InvariantCulture)),
        Parameter("placement_verification", ReadString(row, "placement_verification")),
        Parameter("builder_action_raw", ReadString(row, "service_action_raw")),
        Parameter("native_contract", ReadString(row, "native_contract"))
    };

    private static string IntentParameter(SmallModelActionParameter[] parameters, string name)
    {
        var value = ReadParameter(parameters, name);
        return string.IsNullOrWhiteSpace(value)
            ? ReadParameter(parameters, "continuation." + name)
            : value;
    }
}
