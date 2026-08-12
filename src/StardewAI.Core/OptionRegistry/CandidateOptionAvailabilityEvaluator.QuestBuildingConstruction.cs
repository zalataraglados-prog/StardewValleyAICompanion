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
    private IEnumerable<EventCandidate> BindBuildingQuestCandidates(
        SnapshotEnvelope snapshot,
        QuestCandidateRef quest,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var context = ReadStateFieldValue(snapshot, "player", "quest_building_construction");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return new[] { BlockedQuestCandidate(snapshot, quest, "quest_building_projection_unavailable") };
        }
        var row = rows.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(value, "quest_id"), quest.QuestId, StringComparison.Ordinal));
        if (row.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(row, "target_building_type"), quest.RequiredBuildingType, StringComparison.Ordinal))
        {
            return new[] { BlockedQuestCandidate(snapshot, quest, "quest_building_target_row_not_found") };
        }

        var status = ReadString(row, "action_status");
        if (status == "construction_in_progress")
        {
            var recovery = RecoveryCandidates(snapshot).FirstOrDefault(candidate => candidate.Available);
            if (recovery is null)
            {
                return new[] { BlockedQuestCandidate(snapshot, quest, "quest_building_day_settlement_unavailable") };
            }
            return new[]
            {
                AttachQuest(recovery, quest, new[]
                {
                    Parameter("construction_building_type", ReadString(row, "target_building_type")),
                    Parameter("construction_days_left", ReadInt(row, "construction_days_left").ToString(CultureInfo.InvariantCulture)),
                    Parameter("construction_settlement", "native_day_update_then_fresh_snapshot")
                })
            };
        }
        if (status == "route_to_science_house_required")
        {
            return new[] { BindQuestLocationRoute(snapshot, quest, "ScienceHouse") };
        }
        if (status != "ready_for_native_carpenter_menu")
        {
            return new[] { BlockedQuestCandidate(snapshot, quest, "quest_building_not_ready:" + status) };
        }

        var actionX = NullableReadInt(row, "carpenter_action_tile_x");
        var actionY = NullableReadInt(row, "carpenter_action_tile_y");
        var placementX = NullableReadInt(row, "placement_tile_x");
        var placementY = NullableReadInt(row, "placement_tile_y");
        var stand = actionX.HasValue && actionY.HasValue
            ? FindBestStandTile(snapshot, actionX.Value, actionY.Value)
            : null;
        if (!actionX.HasValue || !actionY.HasValue || !placementX.HasValue || !placementY.HasValue || stand is null)
        {
            return new[] { BlockedQuestCandidate(snapshot, quest, "quest_building_action_stand_or_placement_unavailable") };
        }
        var materials = row.TryGetProperty("build_materials", out var materialRows) && materialRows.ValueKind == JsonValueKind.Array
            ? materialRows.GetRawText()
            : "[]";
        var reservation = new MachineCraftingMaterialReservationGuard().Evaluate(
            snapshot,
            materialRows,
            usesWorkbench: false,
            commitmentLedger);
        var source = new EventCandidate
        {
            CandidateId = "quest-construct:" + quest.QuestId + ":" + ReadString(row, "target_building_type"),
            Kind = "construct_quest_building",
            Available = reservation.Ready,
            LocationId = "ScienceHouse",
            TileX = actionX,
            TileY = actionY,
            EstimatedTicks = 600,
            EnergyCost = 0,
            AvailabilityClass = "transparent_quest_native_carpenter_construction",
            ExpectedEffect = "native_building_placed_under_construction=true;fresh_snapshot_replan_required=true",
            BlockReasons = reservation.BlockingReasons,
            Parameters = new[]
            {
                Parameter("construction_purpose", "ordinary_building_quest"),
                Parameter("construction_reason", "active_quest_requirement"),
                Parameter("construction_building_type", ReadString(row, "target_building_type")),
                Parameter("project_id", ReadString(row, "target_building_type")),
                Parameter("construction_builder", "Robin"),
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
                Parameter("location_id", "ScienceHouse"),
                Parameter("target_tile_x", actionX.Value.ToString(CultureInfo.InvariantCulture)),
                Parameter("target_tile_y", actionY.Value.ToString(CultureInfo.InvariantCulture)),
                Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
                Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
                Parameter("placement_location_id", ReadString(row, "placement_location_id")),
                Parameter("building_tile_x", placementX.Value.ToString(CultureInfo.InvariantCulture)),
                Parameter("building_tile_y", placementY.Value.ToString(CultureInfo.InvariantCulture)),
                Parameter("placement_verification", ReadString(row, "placement_verification")),
                Parameter("carpenter_action_raw", ReadString(row, "carpenter_action_raw")),
                Parameter("builder_action_raw", ReadString(row, "carpenter_action_raw")),
                Parameter("native_contract", "GameLocation.checkAction_Carpenter->answerDialogue_carpenter_Construct->CarpenterMenu.receiveLeftClick->tryToBuild->Building.FinishConstruction->HaveBuildingQuest.OnBuildingExists")
            }
        };
        return new[] { AttachQuest(source, quest) };
    }
}
