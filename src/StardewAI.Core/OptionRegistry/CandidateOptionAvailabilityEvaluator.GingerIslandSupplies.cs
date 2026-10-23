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
    private EventCandidate[] GingerIslandMaterialSupplyCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        string repairKey,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var repair = access.GetProperty("repairs").GetProperty(repairKey);
        var qualifiedItemId = ReadString(repair, "qualified_item_id");
        var requiredCount = ReadInt(repair, "required_count");
        var availableCount = ReadInt(repair, "available_count");
        var deficitCount = Math.Max(0, requiredCount - availableCount);
        if (deficitCount == 0)
        {
            return new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_material_projection_requires_refresh:" + repairKey,
                    intentParameters)
            };
        }

        var producers = new List<EventCandidate>();
        producers.AddRange(MachineServiceCandidates(snapshot, null)
            .Where(candidate => candidate.Available &&
                candidate.Kind is ("collect_machine_output_tile" or "route_connector_tile") &&
                string.Equals(candidate.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal)));

        if (string.Equals(qualifiedItemId, "(O)709", StringComparison.Ordinal))
        {
            producers.AddRange(WildTreeChopAcquisitionCandidates(snapshot)
                .Where(candidate => candidate.Available && CandidateGuaranteesItem(candidate, qualifiedItemId)));
            producers.AddRange(ClearFarmResourceClumpCandidates(snapshot)
                .Where(candidate => candidate.Available && CandidateGuaranteesItem(candidate, qualifiedItemId)));
        }

        var candidates = producers
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => BindGingerIslandSupplyCandidate(
                group.First(),
                "material-" + repairKey,
                qualifiedItemId,
                requiredCount,
                availableCount,
                deficitCount,
                intentParameters,
                continuation))
            .OrderBy(candidate => candidate.EstimatedTicks <= 0 ? int.MaxValue : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length > 0
            ? candidates
            : new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_no_exact_material_producer:" + qualifiedItemId,
                    intentParameters)
            };
    }

    private EventCandidate[] GingerIslandWalnutSupplyCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        string perchKey,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var perch = access.GetProperty(perchKey);
        var requiredCount = ReadInt(perch, "required_nuts");
        var availableCount = ReadInt(access.GetProperty("walnuts"), "current");
        var deficitCount = Math.Max(0, requiredCount - availableCount);
        if (deficitCount == 0)
        {
            return new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_walnut_projection_requires_refresh:" + perchKey,
                    intentParameters)
            };
        }

        var producers = BushHarvestCandidates(snapshot)
            .Concat(FishingEventCandidateBuilder.Build(snapshot))
            .Concat(SpawnedObjectForagingCandidates(snapshot))
            .Concat(ArtifactSpotExcavationCandidates(snapshot))
            .Concat(GeodeProcessingCandidates(snapshot, Array.Empty<SmallModelActionParameter>()))
            .Concat(FieldOfficeSurveyCandidates(snapshot))
            .Where(candidate => candidate.Available)
            .Select(candidate => new
            {
                Candidate = candidate,
                Quantity = GingerIslandWalnutQuantity(candidate)
            })
            .Where(source => source.Quantity > 0)
            .GroupBy(source => source.Candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(source => source.Candidate.EstimatedTicks <= 0
                ? int.MaxValue
                : source.Candidate.EstimatedTicks)
            .ThenBy(source => source.Candidate.CandidateId, StringComparer.Ordinal)
            .Select(source => BindGingerIslandSupplyCandidate(
                source.Candidate,
                "walnuts-" + perchKey,
                "golden-walnut",
                requiredCount,
                availableCount,
                deficitCount,
                intentParameters,
                continuation,
                projectedQuantity: source.Quantity,
                producerProjection: GingerIslandWalnutProducerProjection(source.Candidate)))
            .ToArray();

        return producers.Length > 0
            ? producers
            : new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_no_exact_current_walnut_producer:" + perchKey,
                    intentParameters)
            };
    }

    private static int GingerIslandWalnutQuantity(EventCandidate candidate)
    {
        const string walnutQualifiedItemId = "(O)73";
        if (candidate.Kind is ("harvest_bush" or "catch_fish" or "collect_spawned_object") &&
            string.Equals(candidate.QualifiedItemId, walnutQualifiedItemId, StringComparison.Ordinal))
        {
            return Math.Max(1, candidate.Quantity);
        }

        if (candidate.Kind == "clear_obstacle_tile")
        {
            var jsonQuantity = CandidateJsonItemQuantity(
                ReadParameter(candidate.Parameters, "clear_output_items_json"),
                walnutQualifiedItemId);
            if (jsonQuantity > 0)
                return jsonQuantity;
            if (ReadParameter(candidate.Parameters, "clear_output_qualified_item_id") == walnutQualifiedItemId)
            {
                var minimum = ReadIntParameter(candidate.Parameters, "clear_output_quantity_min");
                var maximum = ReadIntParameter(candidate.Parameters, "clear_output_quantity_max");
                return minimum > 0 && minimum == maximum ? minimum : 0;
            }
        }

        if (candidate.Kind == "crack_geode" &&
            ReadParameter(candidate.Parameters, "geode_expected_output_qid") == walnutQualifiedItemId)
        {
            return Math.Max(0, ReadIntParameter(candidate.Parameters, "geode_expected_output_stack"));
        }

        return candidate.Kind == "answer_field_office_survey"
            ? Math.Max(0, ReadIntParameter(candidate.Parameters, "golden_walnuts_found_delta"))
            : 0;
    }

    private static string GingerIslandWalnutProducerProjection(EventCandidate candidate) =>
        candidate.Kind == "catch_fish" &&
        string.Equals(
            ReadParameter(candidate.Parameters, "result_is_stochastic"),
            "true",
            StringComparison.OrdinalIgnoreCase)
            ? "exact_native_stochastic_retry_source"
            : "exact_native_current_snapshot_receipt";

    private EventCandidate[] GingerIslandTicketMoneyCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var money = ReadInt(access.GetProperty("ticket"), "money_available");
        var deficit = Math.Max(0, 1000 - money);
        if (deficit == 0)
        {
            return new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_ticket_money_projection_requires_refresh",
                    intentParameters)
            };
        }

        var pendingSettlement = GingerIslandPendingShippingSettlement(snapshot);
        EventCandidate[] sources;
        string stage;
        if (pendingSettlement >= deficit)
        {
            sources = RecoveryCandidates(
                snapshot,
                new[] { Parameter("control_plane.native_save_boundary", "true") });
            stage = "ticket-money-settlement";
        }
        else
        {
            sources = ShipItemStageCandidates(snapshot, Array.Empty<SmallModelActionParameter>())
                .Where(candidate => candidate.Available && candidate.TotalValue > 0)
                .ToArray();
            stage = "ticket-money-shipping";
        }

        var candidates = sources
            .Select(candidate => BindGingerIslandSupplyCandidate(
                candidate,
                stage,
                "money",
                1000,
                money,
                deficit,
                intentParameters,
                continuation,
                pendingSettlement))
            .ToArray();
        return candidates.Length > 0
            ? candidates
            : new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_no_safe_ticket_money_producer",
                    intentParameters)
            };
    }

    private static EventCandidate BindGingerIslandSupplyCandidate(
        EventCandidate candidate,
        string stage,
        string resourceId,
        int requiredCount,
        int availableCount,
        int deficitCount,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation,
        int pendingSettlement = 0,
        int projectedQuantity = 0,
        string producerProjection = "exact_native_current_snapshot_receipt") => CloneCandidate(
            candidate,
            candidateId: "master-angler-ginger-island:" + stage + ":" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect +
                ";ginger_island_supply_stage=" + stage +
                ";ginger_island_supply_resource=" + resourceId +
                ";ginger_island_supply_deficit=" + deficitCount +
                ";ginger_island_supply_projected_quantity=" + projectedQuantity +
                ";ginger_island_supply_producer_projection=" + producerProjection +
                ";fresh_snapshot_and_ginger_island_replan_required=true",
            parameters: candidate.Parameters
                .Where(parameter => !parameter.Name.StartsWith("continuation.", StringComparison.Ordinal))
                .Concat(intentParameters)
                .Concat(continuation)
                .Concat(new[]
                {
                    Parameter("ginger_island_supply_stage", stage),
                    Parameter("ginger_island_supply_resource", resourceId),
                    Parameter("ginger_island_supply_required_count", requiredCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_available_count", availableCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_deficit_count", deficitCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_pending_shipping_settlement", pendingSettlement.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_projected_quantity", projectedQuantity.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_producer_projection", producerProjection)
                })
                .ToArray(),
            availabilityClass: "master_angler_ginger_island_" + stage);

    private static int GingerIslandPendingShippingSettlement(SnapshotEnvelope snapshot)
    {
        var bins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
        if (!bins.HasValue || bins.Value.ValueKind != JsonValueKind.Array)
            return 0;
        // Every completed bin projects the same native shipping inventory aggregate.
        return bins.Value.EnumerateArray()
            .Where(bin => bin.ValueKind == JsonValueKind.Object &&
                ReadBool(bin, "pending_settlement_value_complete") == true)
            .Select(bin => Math.Max(0, ReadInt(bin, "pending_settlement_value")))
            .DefaultIfEmpty(0)
            .Max();
    }
}
