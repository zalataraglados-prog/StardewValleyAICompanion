using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] CommunityCenterMoneyPaymentCandidates(
        SnapshotEnvelope snapshot,
        JsonElement progress,
        JsonElement bundles,
        string currentLocation,
        string routeState,
        bool canReadJunimoText,
        bool rowCountExact)
    {
        var pending = bundles.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                          ReadBool(row, "complete") != true &&
                          row.TryGetProperty("money_payment", out var payment) &&
                          payment.ValueKind == JsonValueKind.Object &&
                          ReadString(payment, "projection_status") == "exact")
            .OrderBy(row => ReadInt(row, "bundle_id"))
            .ToArray();
        if (pending.Length == 0)
            return Array.Empty<EventCandidate>();

        if (ReadBool(progress, "community_center_is_current_location") != true)
        {
            return CommunityCenterMoneyPaymentRouteCandidates(
                snapshot,
                pending,
                currentLocation,
                routeState,
                canReadJunimoText,
                rowCountExact);
        }

        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return pending.Select(bundle =>
        {
            var payment = bundle.GetProperty("money_payment");
            var interactionX = NullableReadInt(bundle, "interaction_tile_x");
            var interactionY = NullableReadInt(bundle, "interaction_tile_y");
            var stand = interactionX.HasValue && interactionY.HasValue
                ? FindBestStandTile(snapshot, interactionX.Value, interactionY.Value)
                : null;
            var reasons = CommunityCenterMoneyPaymentBlockReasons(
                progress,
                bundle,
                payment,
                routeState,
                canReadJunimoText,
                rowCountExact,
                requireStand: true,
                standAvailable: stand is not null);
            var blocks = reasons.Distinct(StringComparer.Ordinal).ToArray();
            var distance = stand is null
                ? 0
                : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
            return new EventCandidate
            {
                CandidateId = "community-center-pay-vault:" + ReadInt(bundle, "bundle_id"),
                Kind = "pay_community_center_vault_bundle",
                Available = blocks.Length == 0,
                LocationId = "CommunityCenter",
                TileX = interactionX,
                TileY = interactionY,
                ItemId = "-1",
                QualifiedItemId = string.Empty,
                Quantity = ReadInt(payment, "required_money"),
                EstimatedTicks = Math.Max(240, distance * 60 + 240),
                EnergyCost = 0,
                ExpectedEffect = CommunityCenterMoneyPaymentExpectedEffect(bundle, payment),
                AvailabilityClass = "transparent_native_community_center_vault_payment",
                AllowedNow = blocks.Length == 0,
                BlockReasons = blocks,
                Parameters = stand is null || !interactionX.HasValue || !interactionY.HasValue
                    ? Array.Empty<SmallModelActionParameter>()
                    : CommunityCenterMoneyPaymentParameters(
                        progress,
                        bundle,
                        payment,
                        stand.X,
                        stand.Y,
                        interactionX.Value,
                        interactionY.Value)
            };
        }).ToArray();
    }

    private EventCandidate[] CommunityCenterMoneyPaymentRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement[] pending,
        string currentLocation,
        string routeState,
        bool canReadJunimoText,
        bool rowCountExact)
    {
        var route = FindResolvedRoutePlan(
            snapshot,
            currentLocation,
            "CommunityCenter",
            RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile")
                .ToArray());
        var connector = route?.FirstActionCandidate;
        return pending.Select(bundle =>
        {
            var payment = bundle.GetProperty("money_payment");
            var reasons = CommunityCenterMoneyPaymentBlockReasons(
                default,
                bundle,
                payment,
                routeState,
                canReadJunimoText,
                rowCountExact,
                requireStand: false,
                standAvailable: true);
            if (ReadString(payment, "action_status") != "community_center_not_current_location")
                reasons.Add("community_center_remote_vault_payment_projection_unavailable");
            reasons.AddRange(connector?.BlockReasons ??
                new[] { "community_center_vault_payment_cross_map_route_unavailable" });
            var blocks = reasons.Distinct(StringComparer.Ordinal).ToArray();
            var bundleId = ReadInt(bundle, "bundle_id");
            var amount = ReadInt(payment, "required_money");
            return new EventCandidate
            {
                CandidateId = "community-center-pay-vault-route:" + bundleId + ":" + currentLocation,
                Kind = "route_connector_tile",
                Available = connector is not null && connector.Available && blocks.Length == 0,
                LocationId = currentLocation,
                TileX = connector?.TileX,
                TileY = connector?.TileY,
                ItemId = "-1",
                QualifiedItemId = string.Empty,
                Quantity = amount,
                EstimatedTicks = connector?.EstimatedTicks ?? -1,
                EnergyCost = connector?.EnergyCost ?? 0,
                ExpectedEffect = (connector?.ExpectedEffect ?? string.Empty) +
                    ";community_center_vault_bundle=" + bundleId +
                    ";required_money=" + amount +
                    ";one_connector_then_fresh_snapshot=true",
                AvailabilityClass = connector is null
                    ? "community_center_vault_payment_route_blocked"
                    : "community_center_vault_payment_rolling_route",
                AllowedNow = connector?.AllowedNow,
                AllowedToday = connector?.AllowedToday,
                NextOpenTime = connector?.NextOpenTime,
                EffectiveOpenTime = connector?.EffectiveOpenTime,
                ClosesAt = connector?.ClosesAt,
                WaitCost = connector?.WaitCost,
                GateReasons = connector?.GateReasons ?? Array.Empty<string>(),
                BlockReasons = blocks,
                Parameters = (connector?.Parameters ?? Array.Empty<SmallModelActionParameter>())
                    .Concat(new[]
                    {
                        Parameter("continuation.option_id", "community_center.donate_bundle_items"),
                        Parameter("continuation.target_location", "CommunityCenter"),
                        Parameter("continuation.bundle_data_key", ReadString(bundle, "bundle_data_key")),
                        Parameter("continuation.bundle_id", bundleId.ToString(CultureInfo.InvariantCulture)),
                        Parameter("continuation.required_money", amount.ToString(CultureInfo.InvariantCulture)),
                        Parameter("continuation.route_kind", "native_money_payment"),
                        Parameter("continuation.source_id", "money")
                    })
                    .ToArray()
            };
        }).ToArray();
    }

    private static List<string> CommunityCenterMoneyPaymentBlockReasons(
        JsonElement progress,
        JsonElement bundle,
        JsonElement payment,
        string routeState,
        bool canReadJunimoText,
        bool rowCountExact,
        bool requireStand,
        bool standAvailable)
    {
        var reasons = new List<string>();
        if (routeState is not ("undecided" or "community_center_locked"))
            reasons.Add(routeState == "conflicting_irreversible_flags"
                ? "community_center_route_state_conflict"
                : "community_center_route_locked_out_by_joja");
        if (!rowCountExact)
            reasons.Add("community_center_bundle_projection_incomplete");
        if (!canReadJunimoText)
            reasons.Add("community_center_junimo_text_not_readable");
        if (ReadString(bundle, "projection_status") != "exact" ||
            ReadInt(bundle, "area_id") != 4 ||
            ReadBool(bundle, "complete") == true ||
            ReadString(payment, "projection_status") != "exact" ||
            ReadInt(payment, "ingredient_index") != 0 ||
            ReadInt(payment, "required_money") < 1 ||
            ReadInt(payment, "money_before") < ReadInt(payment, "required_money") ||
            ReadInt(payment, "money_after") !=
                ReadInt(payment, "money_before") - ReadInt(payment, "required_money") ||
            ReadBool(payment, "affordable") != true ||
            ReadBool(payment, "completes_bundle") != true ||
            ReadBool(payment, "expected_bundle_reward_available_after") != true ||
            !NullableReadInt(bundle, "note_tile_x").HasValue ||
            !NullableReadInt(bundle, "note_tile_y").HasValue ||
            !NullableReadInt(bundle, "interaction_tile_x").HasValue ||
            !NullableReadInt(bundle, "interaction_tile_y").HasValue ||
            RawJson(payment, "authoritative_route_sources") is "" or "[]")
        {
            reasons.Add("community_center_vault_payment_typed_projection_invalid");
        }
        if (progress.ValueKind == JsonValueKind.Object &&
            ReadInt(payment, "expected_complete_bundle_count_after") <
                ReadInt(progress, "complete_bundle_count"))
        {
            reasons.Add("community_center_vault_payment_completion_projection_invalid");
        }
        if (ReadString(payment, "action_status") is not ("ready" or "community_center_not_current_location"))
            reasons.Add(ReadString(payment, "action_status"));
        if (requireStand && !standAvailable)
            reasons.Add("community_center_vault_payment_no_reachable_stand_tile");
        return reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).ToList();
    }

    private static SmallModelActionParameter[] CommunityCenterMoneyPaymentParameters(
        JsonElement progress,
        JsonElement bundle,
        JsonElement payment,
        int standX,
        int standY,
        int interactionX,
        int interactionY) =>
        new[]
        {
            Parameter("target_location", "CommunityCenter"),
            Parameter("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_x", interactionX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", interactionY.ToString(CultureInfo.InvariantCulture)),
            Parameter("community_center_note_tile_x", NullableReadInt(bundle, "note_tile_x")?.ToString(CultureInfo.InvariantCulture) ?? "-1"),
            Parameter("community_center_note_tile_y", NullableReadInt(bundle, "note_tile_y")?.ToString(CultureInfo.InvariantCulture) ?? "-1"),
            Parameter("route_state", ReadString(progress, "route_state")),
            Parameter("bundle_data_key", ReadString(bundle, "bundle_data_key")),
            Parameter("bundle_id", ReadInt(bundle, "bundle_id").ToString(CultureInfo.InvariantCulture)),
            Parameter("bundle_area_id", ReadInt(bundle, "area_id").ToString(CultureInfo.InvariantCulture)),
            Parameter("bundle_area_name", ReadString(bundle, "area_name")),
            Parameter("bundle_ingredient_index", ReadInt(payment, "ingredient_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("bundle_required_slot_count", ReadInt(bundle, "required_slot_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("price", ReadInt(payment, "required_money").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_money_before", ReadInt(payment, "money_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_money_after", ReadInt(payment, "money_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_bundle_completed_count_before", ReadInt(payment, "completed_ingredient_count_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_bundle_completed_count_after", ReadInt(payment, "completed_ingredient_count_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_bundle_complete_after", ReadBool(payment, "completes_bundle") == true ? "true" : "false"),
            Parameter("expected_bundle_reward_available_after", ReadBool(payment, "expected_bundle_reward_available_after") == true ? "true" : "false"),
            Parameter("expected_complete_bundle_count_after", ReadInt(payment, "expected_complete_bundle_count_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("completes_area", ReadBool(payment, "completes_area") == true ? "true" : "false"),
            Parameter("expected_area_complete_after", ReadBool(payment, "expected_area_complete_after") == true ? "true" : "false"),
            Parameter("area_completion_mail_id", ReadString(bundle, "area_completion_mail_id")),
            Parameter("expected_area_completion_mail_pending_after", ReadBool(payment, "expected_area_completion_mail_pending_after") == true ? "true" : "false"),
            Parameter("expected_bulletin_thank_you_pending_after", ReadBool(payment, "expected_bulletin_thank_you_pending_after") == true ? "true" : "false"),
            Parameter("expected_all_areas_complete_after", ReadBool(payment, "expected_all_areas_complete_after") == true ? "true" : "false"),
            Parameter("newly_appearing_note_area_ids_json", RawJson(payment, "newly_appearing_note_area_ids")),
            Parameter("authoritative_route_sources_json", RawJson(payment, "authoritative_route_sources")),
            Parameter("native_contract", "CommunityCenter.checkBundle_then_JunimoNoteMenu.receiveLeftClick_bundle_then_purchaseButton"),
            Parameter("max_movement_tiles", "512")
        };

    private static string CommunityCenterMoneyPaymentExpectedEffect(
        JsonElement bundle,
        JsonElement payment) =>
        "community_center.bundle=" + ReadInt(bundle, "bundle_id") +
        ":money_payment=" + ReadInt(payment, "required_money") +
        ";player.money=" + ReadInt(payment, "money_after") +
        ";bundle_complete=true" +
        ";bundle_reward_available=true" +
        ";area_complete=" + (ReadBool(payment, "expected_area_complete_after") == true ? "true" : "false") +
        ";new_note_areas=" + RawJson(payment, "newly_appearing_note_area_ids");
}
