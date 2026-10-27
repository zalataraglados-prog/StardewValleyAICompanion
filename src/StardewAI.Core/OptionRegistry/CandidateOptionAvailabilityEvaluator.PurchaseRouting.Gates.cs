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
        private PurchaseServiceGateResult PurchaseServiceGate(
            SnapshotEnvelope snapshot,
            JsonElement endpoint,
            JsonElement[] graphEdges,
            string targetLocation)
        {
            var blockReasons = new List<string>();
            var gateReasons = new List<string>();
            var openTime = ReadNullableInt(endpoint, "open_time");
            var closeTime = ReadNullableInt(endpoint, "close_time");
            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            var nextOpenTime = (int?)null;
            var allowedNow = true;
            var allowedToday = true;
            var ownerRequired = endpoint.TryGetProperty(
                    "owner_service_status",
                    out var endpointOwnerStatus) &&
                endpointOwnerStatus.ValueKind == JsonValueKind.Object &&
                ReadBool(endpointOwnerStatus, "owner_required") == true;
            var festivalClosed = ReadBool(endpoint, "festival_closed") == true;
            if (festivalClosed)
            {
                blockReasons.Add("stores_closed_for_festival");
            }

            var endpointCanOpenLaterToday = !festivalClosed &&
                openTime.HasValue &&
                closeTime.HasValue &&
                currentTime < openTime.Value &&
                currentTime < closeTime.Value;
            if (ReadBool(endpoint, "direct_time_allowed") == false)
            {
                allowedNow = false;
                if (endpointCanOpenLaterToday)
                {
                    nextOpenTime = LaterTime(nextOpenTime, openTime);
                    gateReasons.Add("shop_endpoint_not_open_yet");
                }
                else
                {
                    blockReasons.Add("shop_endpoint_direct_time_gate_blocked");
                }
            }

            if (!ownerRequired &&
                ReadBool(endpoint, "allowed_now") == false &&
                blockReasons.Count == 0)
            {
                allowedNow = false;
                if (endpointCanOpenLaterToday)
                {
                    nextOpenTime = LaterTime(nextOpenTime, openTime);
                    gateReasons.Add("shop_endpoint_not_open_yet");
                }
                else
                {
                    blockReasons.Add("shop_endpoint_direct_time_gate_blocked");
                }
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
                var entranceOpenTime =
                    ReadNullableInt(selected, "effective_open_time") ??
                    ReadNullableInt(selected, "open_time");
                var entranceCloseTime = ReadNullableInt(selected, "close_time");
                openTime = LaterTime(openTime, entranceOpenTime);
                closeTime = EarlierTime(closeTime, entranceCloseTime);
                var entranceAllowedNow =
                    allowedEntrance.ValueKind == JsonValueKind.Object;
                var dateAllowed = ReadBool(selected, "festival_closed") != true &&
                    ReadBool(selected, "seed_shop_wednesday_closed") != true &&
                    ReadBool(selected, "fish_shop_gone_fishing_closed") != true &&
                    ReadBool(selected, "friendship_allowed") != false &&
                    ReadBool(selected, "allowed_on_capture_date") != false &&
                    string.IsNullOrWhiteSpace(
                        ReadString(selected, "unresolved_reason"));
                var canOpenLaterToday = !entranceAllowedNow &&
                    dateAllowed &&
                    openTime.HasValue &&
                    closeTime.HasValue &&
                    currentTime < openTime.Value &&
                    currentTime < closeTime.Value;

                allowedNow &= entranceAllowedNow;
                allowedToday &= entranceAllowedNow || canOpenLaterToday;
                if (canOpenLaterToday)
                {
                    nextOpenTime = LaterTime(nextOpenTime, openTime);
                    gateReasons.Add("shop_entrance_not_open_yet");
                }
                else if (!entranceAllowedNow)
                {
                    blockReasons.AddRange(
                        LockedDoorPurchaseGateReasons(selected));
                }
            }

            if (allowedToday)
            {
                var ownerGate = PurchaseOwnerServiceGate(
                    snapshot,
                    endpoint,
                    targetLocation,
                    currentTime,
                    openTime,
                    closeTime,
                    allowedNow);
                allowedNow &= ownerGate.AllowedNow;
                allowedToday &= ownerGate.AllowedToday;
                nextOpenTime = LaterTime(
                    nextOpenTime,
                    ownerGate.NextOpenTime);
                openTime = LaterTime(openTime, ownerGate.OpenTime);
                closeTime = EarlierTime(closeTime, ownerGate.CloseTime);
                gateReasons.AddRange(ownerGate.GateReasons);
                blockReasons.AddRange(ownerGate.BlockReasons);
            }

            var shops = ReadStateFieldValue(snapshot, "locations", "shops");
            if (shops.HasValue &&
                shops.Value.ValueKind == JsonValueKind.Object &&
                ReadBool(shops.Value, "stores_closed_for_festival") == true)
            {
                blockReasons.Add("stores_closed_for_festival");
            }

            if (blockReasons.Count > 0)
            {
                allowedNow = false;
                allowedToday = false;
            }

            return new PurchaseServiceGateResult(
                allowedNow,
                allowedToday,
                nextOpenTime,
                openTime,
                closeTime,
                gateReasons.Distinct(StringComparer.Ordinal).ToArray(),
                blockReasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        private static int? LaterTime(int? left, int? right) =>
            left.HasValue && right.HasValue
                ? Math.Max(left.Value, right.Value)
                : left ?? right;

        private static int? EarlierTime(int? left, int? right) =>
            left.HasValue && right.HasValue
                ? Math.Min(left.Value, right.Value)
                : left ?? right;

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
            if (ReadBool(gate, "fish_shop_gone_fishing_closed") == true)
            {
                yield return "fish_shop_gone_fishing_closed";
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
            bool AllowedToday,
            int? NextOpenTime,
            int? OpenTime,
            int? CloseTime,
            string[] GateReasons,
            string[] BlockReasons);

    }
}
