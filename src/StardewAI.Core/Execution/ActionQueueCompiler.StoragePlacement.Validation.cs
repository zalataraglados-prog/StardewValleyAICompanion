using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[]
            ValidatePlaceStoragePlan(
                SmallModelAction action,
                SnapshotEnvelope snapshot,
                StrategyCommitmentLedger? commitmentLedger)
        {
            if (action.OptionId != "executor.place_storage")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var slotIndex = ReadIntParameter(
                action,
                "inventory_slot_index");
            var targetX = ReadIntParameter(
                action,
                "target_tile_x");
            var targetY = ReadIntParameter(
                action,
                "target_tile_y");
            var standX = ReadIntParameter(
                action,
                "stand_tile_x");
            var standY = ReadIntParameter(
                action,
                "stand_tile_y");
            var locationId = ReadParameter(
                action,
                "location_id");
            var qualifiedItemId = ReadParameter(
                action,
                "qualified_item_id");
            var itemId = ReadParameter(action, "item_id");
            var expectedStack = ReadIntParameter(
                action,
                "inventory_stack_before");
            if (!slotIndex.HasValue ||
                !targetX.HasValue ||
                !targetY.HasValue ||
                !standX.HasValue ||
                !standY.HasValue ||
                string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !expectedStack.HasValue)
            {
                reasons.Add(
                    "place_storage_typed_target_fields_required");
                return reasons.ToArray();
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add(
                    "place_storage_menu_must_be_clear");
            }
            if (!string.Equals(
                    locationId,
                    ReadStateFieldString(
                        snapshot,
                        "player",
                        "location_id"),
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    ReadParameter(action, "target_location"),
                    locationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(
                    "place_storage_requires_loaded_target_location");
            }
            if (Math.Abs(standX.Value - targetX.Value) +
                    Math.Abs(standY.Value - targetY.Value) != 1 ||
                PlacementCollisionGridBlocks(
                    snapshot,
                    standX.Value,
                    standY.Value))
            {
                reasons.Add(
                    "place_storage_adjacent_stand_geometry_invalid");
            }

            var context = ReadStateFieldValue(
                snapshot,
                "player",
                "storage_placement");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object)
            {
                reasons.Add(
                    "place_storage_projection_unavailable");
                return reasons
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
            var liveFingerprint = ReadString(
                context.Value,
                "static_projection_fingerprint");
            if (string.IsNullOrWhiteSpace(liveFingerprint) ||
                !string.Equals(
                    ReadParameter(
                        action,
                        "storage_placement_projection_fingerprint"),
                    liveFingerprint,
                    StringComparison.Ordinal))
            {
                reasons.Add(
                    "place_storage_projection_fingerprint_drifted");
            }

            var row = StoragePlacementRow(
                context.Value,
                slotIndex.Value,
                qualifiedItemId);
            if (!row.HasValue ||
                !StoragePlacementIdentityMatches(
                    action,
                    row.Value,
                    itemId,
                    expectedStack.Value))
            {
                reasons.Add(
                    "place_storage_inventory_identity_drifted");
            }
            else
            {
                var location = StoragePlacementLocation(
                    row.Value,
                    locationId);
                if (!location.HasValue)
                {
                    reasons.Add(
                        "place_storage_location_projection_unavailable");
                }
                else
                {
                    var layout =
                        new StoragePlacementLayoutProjection()
                            .SelectCurrentMapTile(
                                snapshot,
                                location.Value);
                    if (!StoragePlacementLayoutMatches(
                            action,
                            layout,
                            targetX.Value,
                            targetY.Value,
                            standX.Value,
                            standY.Value))
                    {
                        reasons.Add(
                            "place_storage_route_safe_layout_drifted");
                    }
                }
            }

            var reservationGuard =
                new InventoryPlacementMaterialReservationGuard()
                    .Evaluate(
                        commitmentLedger,
                        slotIndex.Value,
                        qualifiedItemId);
            if (!reservationGuard.Ready)
            {
                reasons.Add(
                    "place_storage_material_reservation_guard_blocked");
            }
            if (!StoragePlacementReservationMatches(
                    action,
                    reservationGuard))
            {
                reasons.Add(
                    "place_storage_material_reservation_projection_drifted");
            }

            return reasons
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
