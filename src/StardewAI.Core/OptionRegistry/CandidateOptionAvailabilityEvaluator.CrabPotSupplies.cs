using System;
using System.Collections.Generic;
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
    private const string FishShopId = "FishShop";
    private const string CrabPotQualifiedItemId = "(O)710";
    private const string BaitQualifiedItemId = "(O)685";

    private EventCandidate[] CrabPotBaitSupplyCandidates(
        SnapshotEnvelope snapshot)
    {
        if (HasCrabPotBaitInInventory(snapshot) ||
            !MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var missing) ||
            missing.Count == 0 ||
            !TryReadCompleteCrabPotNetwork(snapshot, out var networkRows))
        {
            return Array.Empty<EventCandidate>();
        }

        var targets = networkRows
            .Where(row =>
                ReadBool(row, "exact_base_crab_pot") == true &&
                ReadBool(row, "production_domain_complete") == true &&
                string.Equals(
                    ReadString(row, "service_status"),
                    "bait_required",
                    StringComparison.Ordinal))
            .SelectMany(row => ReadCrabPotStringArray(
                row,
                "possible_qualified_item_ids"))
            .Where(missing.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return targets.Length == 0
            ? Array.Empty<EventCandidate>()
            : CrabPotSupplyPurchaseCandidates(
                snapshot,
                BaitQualifiedItemId,
                maximumUnitPrice: 5,
                supplyKind: "bait",
                targetSpecies: targets);
    }

    private EventCandidate[] CrabPotInfrastructureSupplyCandidates(
        SnapshotEnvelope snapshot)
    {
        if (!TrySelectCrabPotInfrastructureNeed(snapshot, out var need))
            return Array.Empty<EventCandidate>();

        return CrabPotSupplyPurchaseCandidates(
            snapshot,
            CrabPotQualifiedItemId,
            maximumUnitPrice: 1500,
            supplyKind: "crab_pot",
            targetSpecies: need.TargetSpecies);
    }

    private EventCandidate[] CrabPotSupplyPurchaseCandidates(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        int maximumUnitPrice,
        string supplyKind,
        string[] targetSpecies)
    {
        var seed = new[]
        {
            Parameter("continuation.option_id", "fishing.collect_crab_pots"),
            Parameter("continuation.shop_id", FishShopId),
            Parameter("continuation.qualified_item_id", qualifiedItemId),
            Parameter(
                "continuation.max_unit_price",
                maximumUnitPrice.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.quantity", "1"),
            Parameter("continuation.crab_pot_supply_kind", supplyKind),
            Parameter(
                "master_angler_target_qualified_item_ids_json",
                JsonSerializer.Serialize(targetSpecies)),
            Parameter(
                "possible_qualified_item_ids_json",
                JsonSerializer.Serialize(targetSpecies)),
            Parameter("outcome_distribution_complete", "true")
        };

        if (ActiveShopMenuOpen(snapshot))
        {
            return BuyCandidates(snapshot, seed)
                .Where(candidate =>
                    string.Equals(
                        candidate.ShopId,
                        FishShopId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidate.QualifiedItemId,
                        qualifiedItemId,
                        StringComparison.Ordinal))
                .Select(candidate => CrabPotSupplyBuyCandidate(
                    snapshot,
                    candidate,
                    supplyKind,
                    targetSpecies))
                .ToArray();
        }

        var previews = BuyCandidatesFromShopPreview(snapshot)
            .Where(candidate =>
                string.Equals(
                    candidate.ShopId,
                    FishShopId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    candidate.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.Ordinal) &&
                candidate.UnitPrice <= maximumUnitPrice)
            .ToArray();
        return ShopObjectiveStageCandidates(
            snapshot,
            previews,
            "crab_pot_supply",
            (candidate, targetLocation) =>
                PurchaseContinuationParameters(
                        candidate,
                        targetLocation,
                        seed)
                    .Concat(seed.Where(parameter =>
                        !parameter.Name.StartsWith(
                            "continuation.",
                            StringComparison.Ordinal)))
                    .ToArray());
    }

    private static EventCandidate CrabPotSupplyBuyCandidate(
        SnapshotEnvelope snapshot,
        EconomicCandidate candidate,
        string supplyKind,
        string[] targetSpecies)
    {
        return new EventCandidate
        {
            CandidateId = "crab-pot-supply-buy:" + supplyKind + ":" +
                candidate.CandidateId,
            Kind = "buy_shop_item",
            Available = candidate.Available,
            LocationId = ReadStateFieldString(
                snapshot,
                "player",
                "location_id"),
            ItemId = candidate.ItemId,
            QualifiedItemId = candidate.QualifiedItemId,
            DisplayName = candidate.DisplayName,
            SlotIndex = candidate.SlotIndex,
            Quantity = 1,
            ShopId = candidate.ShopId,
            UnitPrice = candidate.UnitPrice,
            TotalValue = candidate.UnitPrice,
            EstimatedTicks = 1,
            EnergyCost = 0,
            ExpectedEffect =
                "crab_pot_supply_purchased=true;" +
                "crab_pot_supply_kind=" + supplyKind + ";" +
                "crab_pot_lifecycle_replan_required=true",
            AvailabilityClass =
                "transparent_crab_pot_fish_shop_supply_purchase",
            BlockReasons = candidate.BlockReasons,
            Parameters = candidate.Parameters
                .Concat(new[]
                {
                    Parameter("continuation.crab_pot_supply_kind", supplyKind),
                    Parameter(
                        "master_angler_target_qualified_item_ids_json",
                        JsonSerializer.Serialize(targetSpecies)),
                    Parameter(
                        "possible_qualified_item_ids_json",
                        JsonSerializer.Serialize(targetSpecies)),
                    Parameter("outcome_distribution_complete", "true")
                })
                .GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray()
        };
    }

    private static bool HasCrabPotBaitInInventory(
        SnapshotEnvelope snapshot)
    {
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        return inventory.HasValue &&
            inventory.Value.ValueKind == JsonValueKind.Array &&
            inventory.Value.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.Object &&
                ReadInt(item, "stack") > 0 &&
                (ReadInt(item, "category") == -21 ||
                 string.Equals(
                     ReadString(item, "qualified_item_id"),
                     BaitQualifiedItemId,
                     StringComparison.Ordinal)));
    }
}
