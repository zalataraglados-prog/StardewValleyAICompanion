using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static PurchaseServiceGateResult PurchaseServiceGate(
            SnapshotEnvelope snapshot,
            JsonElement endpoint,
            JsonElement[] graphEdges,
            string targetLocation)
        {
            var reasons = new List<string>();
            var openTime = ReadNullableInt(endpoint, "open_time");
            var closeTime = ReadNullableInt(endpoint, "close_time");
            if (ReadBool(endpoint, "festival_closed") == true)
            {
                reasons.Add("stores_closed_for_festival");
            }

            if (ReadBool(endpoint, "direct_time_allowed") == false)
            {
                reasons.Add("shop_endpoint_direct_time_gate_blocked");
            }

            if (endpoint.TryGetProperty("owner_service_status", out var ownerStatus) &&
                ownerStatus.ValueKind == JsonValueKind.Object &&
                ReadBool(ownerStatus, "owner_required") == true &&
                ReadBool(ownerStatus, "in_service_area") != true)
            {
                var ownerBlockReason = ReadString(ownerStatus, "block_reason");
                reasons.Add(string.IsNullOrWhiteSpace(ownerBlockReason)
                    ? "shop_owner_not_at_service_counter"
                    : ownerBlockReason);
            }

            if (ReadBool(endpoint, "allowed_now") == false && reasons.Count == 0)
            {
                reasons.Add("shop_endpoint_direct_time_gate_blocked");
            }

            var entranceGates = graphEdges
                .Where(edge => string.Equals(
                    ReadString(edge, "kind"),
                    "locked_door_warp",
                    StringComparison.OrdinalIgnoreCase))
                .Where(edge => string.Equals(
                    ReadString(edge, "target_location"),
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
                .Where(edge => edge.TryGetProperty("gate", out var gate) &&
                    gate.ValueKind == JsonValueKind.Object)
                .Select(edge => edge.GetProperty("gate"))
                .ToArray();
            if (entranceGates.Length > 0)
            {
                var allowedEntrance = entranceGates.FirstOrDefault(gate =>
                    ReadBool(gate, "allowed_now") == true);
                var selected = allowedEntrance.ValueKind == JsonValueKind.Object
                    ? allowedEntrance
                    : entranceGates[0];
                openTime ??= ReadNullableInt(selected, "effective_open_time") ??
                    ReadNullableInt(selected, "open_time");
                closeTime ??= ReadNullableInt(selected, "close_time");
                if (allowedEntrance.ValueKind != JsonValueKind.Object)
                {
                    reasons.AddRange(LockedDoorPurchaseGateReasons(selected));
                }
            }

            var shops = ReadStateFieldValue(snapshot, "locations", "shops");
            if (shops.HasValue &&
                shops.Value.ValueKind == JsonValueKind.Object &&
                ReadBool(shops.Value, "stores_closed_for_festival") == true)
            {
                reasons.Add("stores_closed_for_festival");
            }

            return new PurchaseServiceGateResult(
                reasons.Count == 0,
                openTime,
                closeTime,
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        private static IEnumerable<string> LockedDoorPurchaseGateReasons(
            JsonElement gate)
        {
            if (ReadBool(gate, "festival_closed") == true)
            {
                yield return "stores_closed_for_festival";
            }
            if (ReadBool(gate, "seed_shop_wednesday_closed") == true)
            {
                yield return "seed_shop_wednesday_closed_before_community_center_event";
            }
            if (ReadBool(gate, "time_allowed") == false)
            {
                yield return "shop_entrance_time_gate_blocked";
            }
            if (ReadBool(gate, "friendship_allowed") == false)
            {
                yield return "shop_entrance_friendship_gate_blocked";
            }
            if (ReadBool(gate, "allowed_now") != true)
            {
                yield return "shop_entrance_gate_blocked";
            }
        }

        private static bool ActiveShopMenuOpen(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            return activeMenu.HasValue &&
                activeMenu.Value.ValueKind == JsonValueKind.Object &&
                ReadBool(activeMenu.Value, "is_open") == true &&
                string.Equals(
                    ReadString(activeMenu.Value, "type"),
                    "ShopMenu",
                    StringComparison.Ordinal);
        }

        private sealed record PurchaseServiceGateResult(
            bool AllowedNow,
            int? OpenTime,
            int? CloseTime,
            string[] BlockReasons);
    }
}
