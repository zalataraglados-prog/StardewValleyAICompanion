using StardewAI.Contracts.Options;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static OptionAvailabilityEnvelope RevenueRebindAvailability(
        string stateHash) => new()
        {
            StateHash = stateHash,
            Options = new[]
            {
                ReadyRevenueOption(
                    "foraging.collect_spawned_objects",
                    eventCandidates: new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "forage:fixture",
                            Kind = "collect_spawned_object",
                            Available = true,
                            QualifiedItemId = "(O)16",
                            Quantity = 1,
                            UnitPrice = 80,
                            TotalValue = 80
                        }
                    }),
                ReadyRevenueOption(
                    "economy.sell_items",
                    economicCandidates: new[]
                    {
                        RevenueSaleCandidate("old", "SeedShop", 0, 80),
                        RevenueSaleCandidate("seed", "SeedShop", 2, 80),
                        RevenueSaleCandidate("fish", "FishShop", 2, 72)
                    }),
                ReadyRevenueOption(
                    "economy.ship_items",
                    eventCandidates: new[]
                    {
                        RevenueShippingCandidate("old", 0),
                        RevenueShippingCandidate("new", 2)
                    }),
                ReadyRevenueOption("recovery.stabilize_day")
            }
        };

    private static EconomicCandidate RevenueSaleCandidate(
        string id,
        string shopId,
        int slotIndex,
        int unitPrice) => new()
        {
            CandidateId = "sell:" + id,
            Kind = "sell_shop_item",
            Available = true,
            QualifiedItemId = "(O)16",
            ShopId = shopId,
            SlotIndex = slotIndex,
            Quantity = 1,
            UnitPrice = unitPrice,
            TotalValue = unitPrice
        };

    private static EventCandidate RevenueShippingCandidate(
        string id,
        int slotIndex) => new()
        {
            CandidateId = "ship:" + id,
            Kind = "ship_inventory_item_to_bin",
            Available = true,
            QualifiedItemId = "(O)16",
            ShopId = "ShippingBin",
            SlotIndex = slotIndex,
            Quantity = 1,
            UnitPrice = 80,
            TotalValue = 80
        };
}
