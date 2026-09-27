using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineInputPurchasePrerequisite()
    {
        var route = MachineFacilityStaticRoute() with
        {
            RequiredAmount = 3
        };
        var facility = MachineResourceFacilityRoute(route);

        VerifyPurchasePass(
            route,
            facility,
            availableInputQuantity: 1,
            money: 1_000,
            expectedPurchases: 2,
            expectedMaterialClaim: 1,
            expectedCurrencyClaim: 160);
        VerifyPurchasePass(
            route,
            facility,
            availableInputQuantity: 2,
            money: 1_000,
            expectedPurchases: 1,
            expectedMaterialClaim: 2,
            expectedCurrencyClaim: 80);

        var insufficient = AcquisitionRouteTargetDateCurrencyBuilder.Evaluate(
            AcquisitionRouteTargetDateResourceBuilder.Evaluate(
                facility,
                route,
                MachineResourceState(
                    MachineResourceSlot(0, "(O)262", 1))),
            route,
            MachineInputPurchaseCurrencyState(100));
        Require(insufficient.CurrencyBudgetMatchesTargetDate == false &&
                insufficient.CurrencyEvaluation?.PurchasePrerequisite is
                {
                    ShopId: "SeedShop",
                    QualifiedItemId: "(O)262",
                    RequiredPurchaseCount: 2,
                    UnitPrice: 80
                } &&
                insufficient.NonMatchingReasons.Contains(
                    "required_currency_amount_unavailable:money",
                    StringComparer.Ordinal),
            "Insufficient machine-input purchase funds were admitted or lost their exact quote binding.");
    }

    private static void VerifyPurchasePass(
        AcquisitionRouteCalendarResolution route,
        AcquisitionRouteTargetDateFacility facility,
        int availableInputQuantity,
        int money,
        int expectedPurchases,
        int expectedMaterialClaim,
        int expectedCurrencyClaim)
    {
        var resources = MachineResourceState(
            MachineResourceSlot(0, "(O)262", availableInputQuantity));
        var resource = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            facility,
            route,
            resources);
        Require(resource.ResourceInputsMatchTargetDate == false,
            "Machine-input purchase fixture unexpectedly had enough material.");

        var currencies = MachineInputPurchaseCurrencyState(money);
        var currency = AcquisitionRouteTargetDateCurrencyBuilder.Evaluate(
            resource,
            route,
            currencies);
        Require(currency.CurrencyBudgetMatchesTargetDate == true &&
                currency.CurrencyRequirementKind ==
                    "current_native_machine_input_purchase_quote" &&
                currency.CurrencyEvaluation is
                {
                    RequiredAmount: var requiredAmount,
                    PurchasePrerequisite:
                    {
                        InputKind: "machine_primary_input",
                        ShopId: "SeedShop",
                        StockId: "wheat-seed",
                        QualifiedItemId: "(O)262",
                        UnitPrice: 80,
                        OutputStackPerPurchase: 1,
                        RequiredPurchaseCount: var purchaseCount
                    }
                } &&
                requiredAmount == expectedCurrencyClaim &&
                purchaseCount == expectedPurchases,
            "Machine-input purchase prerequisite did not select the cheapest exact current native quote: " +
            JsonSerializer.Serialize(currency, JsonDefaults.Compact));

        var stateHash = new string('a', 64);
        var ledger = new AcquisitionStrategyLedgerState(
            new StrategyCommitmentLedger
            {
                LedgerId = "machine-input-purchase-ledger",
                SaveId = "machine-input-purchase-save",
                PlayerId = "42",
                Revision = 0,
                SourceStateHash = stateHash
            },
            42);
        var reservation = AcquisitionRouteTargetDateReservationBuilder
            .Evaluate(
                currency,
                "grandpa.stage1.21_points",
                stateHash,
                ledger,
                resources,
                currencies);
        Require(reservation.InventoryReservationMatchesTargetDate == true &&
                reservation.ClaimSet is
                {
                    AtomicCommitRequired: true,
                    MaterialClaims: [var material],
                    CurrencyClaims: [var funds]
                } &&
                material.QualifiedItemId == "(O)262" &&
                material.Quantity == expectedMaterialClaim &&
                funds.CurrencyId == NativeShopCurrencies.Money &&
                funds.Amount == expectedCurrencyClaim,
            "Machine-input purchase reservation did not separate current material from future purchases.");

        var processing = AcquisitionRouteTargetDateProcessingBuilder.Evaluate(
            reservation,
            route,
            MachineProcessingState(900,
                Array.Empty<Dictionary<string, object?>>()),
            targetTotalDay: 1);
        Require(processing.ProcessingLeadTimeAxisResolved &&
                processing.ProcessingLeadTimeMatchesTargetDate == false &&
                processing.ProcessingLeadTimeRequirementKind ==
                    "upstream_machine_input_purchase" &&
                processing.NonMatchingReasons.SequenceEqual(new[]
                {
                    "machine_input_purchase_required_before_processing"
                }),
            "Machine processing advanced before its reserved purchase prerequisite completed.");
    }

    private static AcquisitionShopQuoteSnapshotState
        MachineInputPurchaseCurrencyState(int money)
    {
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                money = Field(money),
                shop_currency_balances = Field(new
                {
                    schema_version = "shop_currency_balances.v1",
                    projection_status =
                        "complete_locked_base_1.6.15_shop_menu_currency_domain",
                    supported_currency_ids = new[] { 0, 1, 2, 4 },
                    rows = new object[]
                    {
                        new { currency_id = 0, currency_key = "money", balance = money },
                        new { currency_id = 1, currency_key = "star_tokens", balance = 0 },
                        new { currency_id = 2, currency_key = "club_coins", balance = 0 },
                        new { currency_id = 4, currency_key = "qi_gems", balance = 0 }
                    }
                })
            },
            locations = new
            {
                shops = Field(new
                {
                    shop_count = 2,
                    shops = new object[]
                    {
                        Shop("JojaMart", "joja-wheat-seed", 100),
                        Shop("SeedShop", "wheat-seed", 80)
                    }
                })
            }
        }, JsonDefaults.Options);
        using var document = JsonDocument.Parse(json);
        return new AcquisitionShopQuoteSnapshotState(
            document.RootElement.Clone());

        static object Field(object value) => new
        {
            status = "available",
            confidence = 1d,
            value
        };

        static object Shop(string shopId, string stockId, int price) => new
        {
            shop_id = shopId,
            stock_preview = new
            {
                kind = "shop_stock_preview",
                shop_id = shopId,
                currency = 0,
                entry_count = 1,
                entries = new[]
                {
                    new
                    {
                        synced_key = stockId,
                        qualified_item_id = "(O)262",
                        stack = 1,
                        quality = 0,
                        currency = 0,
                        price,
                        stock = int.MaxValue,
                        infinite_stock = true,
                        can_buy_item = true
                    }
                }
            }
        };
    }
}
