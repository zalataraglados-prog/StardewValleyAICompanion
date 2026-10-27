using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private OwnerServiceWindowGateResult PurchaseOwnerServiceGate(
            SnapshotEnvelope snapshot,
            JsonElement endpoint,
            string targetLocation,
            int currentTime,
            int? openTime,
            int? closeTime,
            bool baseAllowedNow)
        {
            if (!endpoint.TryGetProperty(
                    "owner_service_status",
                    out var ownerStatus) ||
                ownerStatus.ValueKind != JsonValueKind.Object ||
                ReadBool(ownerStatus, "owner_required") != true)
            {
                return OwnerServiceWindowGateResult.NotRequired;
            }

            var ownerNpc = ReadString(ownerStatus, "owner_npc");
            var ownerCurrentlyPresent =
                ReadBool(ownerStatus, "in_service_area") == true;
            var ownerBlockReason = ReadString(ownerStatus, "block_reason");
            if (baseAllowedNow && ownerCurrentlyPresent)
            {
                return OwnerServiceWindowGateResult.AvailableNow;
            }

            if (ownerNpc.Length == 0 ||
                !endpoint.TryGetProperty("parsed", out var parsed) ||
                parsed.ValueKind != JsonValueKind.Object ||
                !parsed.TryGetProperty(
                    "owner_service_area",
                    out var serviceArea) ||
                serviceArea.ValueKind != JsonValueKind.Object ||
                !TryReadServiceArea(serviceArea, out var area))
            {
                return OwnerServiceWindowGateResult.Blocked(
                    string.IsNullOrWhiteSpace(ownerBlockReason)
                        ? "shop_owner_not_at_service_counter"
                        : ownerBlockReason,
                    "shop_owner_service_area_unavailable");
            }

            var presence = ResolveOwnerPresence(
                snapshot,
                ownerNpc,
                currentTime);
            if (presence.Status !=
                    NpcFuturePresenceWindowResolutionStatus.Exact)
            {
                return OwnerServiceWindowGateResult.Blocked(
                    presence.BlockingReasons
                        .Prepend(string.IsNullOrWhiteSpace(ownerBlockReason)
                            ? "shop_owner_not_at_service_counter"
                            : ownerBlockReason)
                        .ToArray());
            }

            var serviceOpen = openTime ?? 600;
            var serviceClose = closeTime ?? 2600;
            var windows = presence.Windows
                .Where(window => window.HasStableInterval &&
                    window.EndpointBehaviorComplete &&
                    string.Equals(
                        window.LocationName,
                        targetLocation,
                        StringComparison.OrdinalIgnoreCase) &&
                    area.Contains(window.TileX, window.TileY))
                .Select(window => new OwnerServiceWindow(
                    LaterGameTime(
                        window.WindowStartTime,
                        serviceOpen),
                    EarlierGameTime(
                        window.WindowEndTimeExclusive,
                        serviceClose)))
                .Where(window => IsBeforeGameTime(
                    window.StartTime,
                    window.EndTimeExclusive))
                .OrderBy(window => GameMinutes(window.StartTime))
                .ToArray();

            if (ownerCurrentlyPresent)
            {
                var currentWindow = windows.FirstOrDefault(window =>
                    !IsBeforeGameTime(currentTime, window.StartTime) &&
                    IsBeforeGameTime(currentTime, window.EndTimeExclusive));
                if (currentWindow is not null)
                {
                    return OwnerServiceWindowGateResult.Deferred(
                        currentWindow.StartTime,
                        currentWindow.EndTimeExclusive);
                }
            }

            var futureWindow = windows.FirstOrDefault(window =>
                IsBeforeGameTime(currentTime, window.StartTime));
            if (futureWindow is not null)
            {
                return OwnerServiceWindowGateResult.Deferred(
                    futureWindow.StartTime,
                    futureWindow.EndTimeExclusive);
            }

            return OwnerServiceWindowGateResult.Blocked(
                "shop_owner_has_no_service_window_today");
        }

        private NpcFuturePresenceWindowResolution ResolveOwnerPresence(
            SnapshotEnvelope snapshot,
            string ownerNpc,
            int currentTime)
        {
            var cache = ownerServicePresenceBySnapshot.GetValue(
                snapshot,
                static _ => new Dictionary<
                    string,
                    NpcFuturePresenceWindowResolution>(
                        StringComparer.Ordinal));
            lock (cache)
            {
                if (cache.TryGetValue(ownerNpc, out var cached))
                    return cached;
            }

            NpcFuturePresenceWindowResolution resolved;
            if (currentTime == 600)
            {
                var projected =
                    new NpcCurrentScheduleSnapshotProjectionResolver()
                        .Resolve(snapshot, ownerNpc);
                if (projected.Status == "pass" &&
                    projected.Projection is not null)
                {
                    resolved = new NpcFuturePresenceWindowResolver().Resolve(
                        projected.Projection);
                }
                else
                {
                    resolved = BlockedOwnerPresence(projected.Issues);
                }
            }
            else
            {
                var loaded = new NpcCurrentLoadedSchedulePresenceResolver()
                    .Resolve(snapshot, ownerNpc);
                resolved = loaded.Status == "pass" &&
                    loaded.Presence is not null
                    ? loaded.Presence
                    : BlockedOwnerPresence(loaded.BlockingReasons);
            }

            lock (cache)
            {
                if (!cache.TryGetValue(ownerNpc, out var cached))
                {
                    cache.Add(ownerNpc, resolved);
                    return resolved;
                }
                return cached;
            }
        }

        private static NpcFuturePresenceWindowResolution BlockedOwnerPresence(
            params string[] reasons) => new()
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Blocked,
            BlockingReasons = reasons.Length > 0
                ? reasons
                : new[] { "shop_owner_schedule_not_exact" }
        };

        private static bool TryReadServiceArea(
            JsonElement serviceArea,
            out OwnerServiceArea area)
        {
            var x = ReadNullableInt(serviceArea, "x");
            var y = ReadNullableInt(serviceArea, "y");
            var width = ReadNullableInt(serviceArea, "width");
            var height = ReadNullableInt(serviceArea, "height");
            if (!x.HasValue || !y.HasValue ||
                !width.HasValue || width.Value <= 0 ||
                !height.HasValue || height.Value <= 0)
            {
                area = default;
                return false;
            }

            area = new OwnerServiceArea(
                x.Value,
                y.Value,
                width.Value,
                height.Value);
            return true;
        }

        private static int GameMinutes(int time) =>
            time / 100 * 60 + time % 100;

        private static bool IsBeforeGameTime(int left, int right) =>
            GameMinutes(left) < GameMinutes(right);

        private static int LaterGameTime(int left, int right) =>
            GameMinutes(left) >= GameMinutes(right) ? left : right;

        private static int EarlierGameTime(int left, int right) =>
            GameMinutes(left) <= GameMinutes(right) ? left : right;

        private sealed record OwnerServiceWindowGateResult(
            bool AllowedNow,
            bool AllowedToday,
            int? NextOpenTime,
            int? OpenTime,
            int? CloseTime,
            string[] GateReasons,
            string[] BlockReasons)
        {
            public static OwnerServiceWindowGateResult NotRequired { get; } =
                new(true, true, null, null, null,
                    Array.Empty<string>(), Array.Empty<string>());

            public static OwnerServiceWindowGateResult AvailableNow { get; } =
                NotRequired;

            public static OwnerServiceWindowGateResult Deferred(
                int openTime,
                int closeTime) =>
                new(false, true, openTime, openTime, closeTime,
                    new[] { "shop_owner_service_window_not_open_yet" },
                    Array.Empty<string>());

            public static OwnerServiceWindowGateResult Blocked(
                params string[] reasons) =>
                new(false, false, null, null, null,
                    Array.Empty<string>(),
                    reasons.Where(value =>
                            !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray());
        }

        private sealed record OwnerServiceWindow(
            int StartTime,
            int EndTimeExclusive);

        private readonly record struct OwnerServiceArea(
            int X,
            int Y,
            int Width,
            int Height)
        {
            public bool Contains(int x, int y) =>
                x >= X && x < X + Width &&
                y >= Y && y < Y + Height;
        }
    }
}
