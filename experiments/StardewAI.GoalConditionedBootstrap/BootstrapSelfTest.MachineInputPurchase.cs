using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

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
        VerifyExecutorEligibleQuoteSelection(route);

        var insufficient = AcquisitionRouteTargetDateCurrencyBuilder.Evaluate(
            AcquisitionRouteTargetDateResourceBuilder.Evaluate(
                facility,
                route,
                MachineResourceState(
                    MachineResourceSlot(0, "(O)262", 1))),
            route,
            MachineInputPurchaseCurrencyState(
                10,
                new MachineInputPurchaseQuoteFixture(
                    "SeedShop",
                    "wheat-seed",
                    80,
                    1,
                    false,
                    new[] { "insufficient_currency_for_purchase" })));
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
        VerifyPurchaseDoesNotMaskOtherMissingInput(route, facility);
        VerifyMachineInputPurchaseReceipt();
        VerifyMachineInputPurchaseSupportChain();
    }

    private static void VerifyPurchaseDoesNotMaskOtherMissingInput(
        AcquisitionRouteCalendarResolution route,
        AcquisitionRouteTargetDateFacility facility)
    {
        var resources = MachineResourceState(
            MachineResourceSlot(0, "(O)262", 1));
        var baseResource = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            facility,
            route,
            resources);
        var resource = baseResource with
        {
            InputEvaluations = baseResource.InputEvaluations
                .Append(new AcquisitionResourceInputEvaluation(
                    "zz_unresolved_machine_input",
                    "(O)388",
                    5,
                    0,
                    "resolved_resource_input_miss",
                    new[] { "fixture" },
                    Array.Empty<string>()))
                .ToArray()
        };
        var currency = AcquisitionRouteTargetDateCurrencyBuilder.Evaluate(
            resource,
            route,
            MachineInputPurchaseCurrencyState(1_000));
        Require(currency.CurrencyBudgetMatchesTargetDate == true,
            "The fixture did not bind its primary-input purchase quote.");

        var stateHash = new string('f', 64);
        var reservation = AcquisitionRouteTargetDateReservationBuilder
            .Evaluate(
                currency,
                "grandpa.stage1.21_points",
                stateHash,
                new AcquisitionStrategyLedgerState(
                    new StrategyCommitmentLedger
                    {
                        LedgerId = "multi-input-purchase-ledger",
                        SaveId = "multi-input-purchase-save",
                        PlayerId = "42",
                        SourceStateHash = stateHash
                    },
                    42),
                resources,
                MachineInputPurchaseCurrencyState(1_000));
        Require(!reservation.ReservationAxisResolved &&
                reservation.BlockingReasons.Contains(
                    "machine_input_purchase_other_input_unresolved:" +
                    "zz_unresolved_machine_input:(O)388",
                    StringComparer.Ordinal),
            "A bound purchase incorrectly masked another missing machine input.");
    }

    private static void VerifyExecutorEligibleQuoteSelection(
        AcquisitionRouteCalendarResolution route)
    {
        var largeRoute = route with { RequiredAmount = 5 };
        var resource = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            MachineResourceFacilityRoute(largeRoute),
            largeRoute,
            MachineResourceState(
                MachineResourceSlot(0, "(O)262", 1)));
        var currency = AcquisitionRouteTargetDateCurrencyBuilder.Evaluate(
            resource,
            largeRoute,
            MachineInputPurchaseCurrencyState(
                1_000,
                new MachineInputPurchaseQuoteFixture(
                    "CheapUnsafeShop",
                    "cheap-unsafe-wheat",
                    20,
                    1,
                    false,
                    new[] { "actions_on_purchase_present" }),
                new MachineInputPurchaseQuoteFixture(
                    "SafeShop",
                    "safe-double-wheat",
                    70,
                    2,
                    true,
                    Array.Empty<string>())));
        Require(currency.CurrencyBudgetMatchesTargetDate == true &&
                currency.CurrencyEvaluation is
                {
                    RequiredAmount: 140,
                    OutputStackPerPurchase: 2,
                    RequiredPurchaseCount: 2,
                    PurchasePrerequisite:
                    {
                        ShopId: "SafeShop",
                        StockId: "safe-double-wheat",
                        OutputStackPerPurchase: 2,
                        RequiredPurchaseCount: 2
                    }
                },
            "Executor-blocked cheapest quote displaced the safe exact stock row or its output-stack purchase count.");
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
        MachineInputPurchaseCurrencyState(
            int money,
            params MachineInputPurchaseQuoteFixture[] quoteFixtures)
    {
        if (quoteFixtures.Length == 0)
        {
            quoteFixtures = new[]
            {
                new MachineInputPurchaseQuoteFixture(
                    "JojaMart",
                    "joja-wheat-seed",
                    100,
                    1,
                    true,
                    Array.Empty<string>()),
                new MachineInputPurchaseQuoteFixture(
                    "SeedShop",
                    "wheat-seed",
                    80,
                    1,
                    true,
                    Array.Empty<string>())
            };
        }
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
                    shop_count = quoteFixtures.Length,
                    shops = quoteFixtures.Select(Shop).ToArray()
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

        static object Shop(MachineInputPurchaseQuoteFixture quote) => new
        {
            shop_id = quote.ShopId,
            stock_preview = new
            {
                kind = "shop_stock_preview",
                shop_id = quote.ShopId,
                currency = 0,
                entry_count = 1,
                entries = new[]
                {
                    new
                    {
                        synced_key = quote.StockId,
                        qualified_item_id = "(O)262",
                        stack = quote.OutputStack,
                        quality = 0,
                        currency = 0,
                        price = quote.Price,
                        stock = int.MaxValue,
                        infinite_stock = true,
                        can_buy_item = true,
                        executor_purchase_preview_enabled =
                            quote.ExecutorPurchaseEnabled,
                        executor_block_reasons = quote.ExecutorBlockReasons
                    }
                }
            }
        };
    }

    private sealed record MachineInputPurchaseQuoteFixture(
        string ShopId,
        string StockId,
        int Price,
        int OutputStack,
        bool ExecutorPurchaseEnabled,
        string[] ExecutorBlockReasons);

    private static void VerifyMachineInputPurchaseReceipt()
    {
        var binding = new AcquisitionPurchasePrerequisiteBinding(
            "machine_primary_input",
            "SeedShop",
            "wheat-seed",
            "(O)262",
            1,
            2,
            1,
            0,
            NativeShopCurrencies.Money,
            "money",
            80,
            2);
        var before = MachineInputPurchaseReceiptSnapshot(1, 1_000);
        var purchased = AcquisitionRouteSupportingTransitionReceiptBuilder
            .VerifyPurchaseTransition(
                PurchaseQueue("purchase", binding),
                before,
                MachineInputPurchaseReceiptSnapshot(2, 920));
        Require(purchased.Verified &&
                purchased.ObservedCurrencyDecrease == 80 &&
                purchased.ObservedItemIncrease == 1,
            "Exact machine-input purchase receipt was not verified.");

        var routed = AcquisitionRouteSupportingTransitionReceiptBuilder
            .VerifyPurchaseTransition(
                PurchaseQueue("route_connector", binding),
                before,
                MachineInputPurchaseReceiptSnapshot(1, 1_000));
        Require(routed.Verified &&
                routed.ObservedCurrencyDecrease == 0 &&
                routed.ObservedItemIncrease == 0,
            "Non-purchase continuation progress mutated reserved resources.");

        var mismatched = AcquisitionRouteSupportingTransitionReceiptBuilder
            .VerifyPurchaseTransition(
                PurchaseQueue("purchase", binding),
                before,
                MachineInputPurchaseReceiptSnapshot(1, 920));
        Require(!mismatched.Verified &&
                mismatched.BlockingReasons.Contains(
                    "machine_input_purchase_inventory_delta_mismatch",
                    StringComparer.Ordinal),
            "A charged purchase without the exact item increase was admitted.");
    }

    private static ActionQueueEnvelope PurchaseQueue(
        string stage,
        AcquisitionPurchasePrerequisiteBinding binding) => new()
        {
            Items = new[]
            {
                new ActionQueueItem
                {
                    NormalizedCommand = new NormalizedCommand
                    {
                        Parameters = new[]
                        {
                            Parameter(
                                "acquisition_support_purchase_stage",
                                stage),
                            Parameter(
                                "acquisition_support_purchase_prerequisite_json",
                                JsonSerializer.Serialize(
                                    binding,
                                    JsonDefaults.Options))
                        }
                    }
                }
            }
        };

    private static SnapshotEnvelope MachineInputPurchaseReceiptSnapshot(
        int inputQuantity,
        int money,
        bool shopMenuOpen = true)
    {
        var graph = new MaterialInventoryGraph
        {
            PlayerId = 42,
            InventoryNodes = new[]
            {
                new MaterialInventoryNode
                {
                    NodeId = "player:42",
                    InventoryKind = "player_inventory",
                    SupplyState = "available",
                    OwnershipClass = "actor_owned",
                    ActorUseAuthorized = true,
                    OwnerPlayerId = 42,
                    LocationId = "Farm",
                    Capacity = 36,
                    Slots = new[]
                    {
                        MachineResourceSlot(0, "(O)262", inputQuantity)
                    }
                }
            },
            PhysicalInventoryCount = 1
        };
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new
            {
                player = new
                {
                    money = Field(money),
                    location_id = Field("SeedShop"),
                    tile_x = Field(10),
                    tile_y = Field(10),
                    inventory = Field(Array.Empty<object>()),
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
                farm = new
                {
                    material_inventory_graph = Field(graph)
                },
                locations = new
                {
                    shops = Field(Array.Empty<object>())
                },
                menus = new
                {
                    active_menu = Field(new
                    {
                        is_open = shopMenuOpen,
                        type = shopMenuOpen ? "ShopMenu" : "none"
                    }),
                    sleep_prompt_context = Field(new
                    {
                        prompt_open = false,
                        can_confirm_sleep = false,
                        confirm_executor_enabled = false,
                        confirm_action_key = "Sleep_Yes"
                    }),
                    shop_stock = Field(new
                    {
                        kind = "shop_stock",
                        shop_id = "SeedShop",
                        read_only = false,
                        safety_timer = 0,
                        entry_count = 1,
                        entries = new[]
                        {
                            new
                            {
                                item_id = "262",
                                qualified_item_id = "(O)262",
                                display_name = "Wheat Seeds",
                                stack = 1,
                                quality = 0,
                                synced_key = "wheat-seed",
                                price = 80,
                                stock = int.MaxValue,
                                infinite_stock = true,
                                currency_balance = money,
                                can_buy_item = true,
                                can_afford_one_with_currency = money >= 80,
                                can_afford_one_with_trade_item = true,
                                could_inventory_accept = true,
                                executor_purchase_enabled = true,
                                executor_block_reasons = Array.Empty<string>()
                            }
                        }
                    })
                },
                time = new
                {
                    total_days = Field(1),
                    time = Field(900),
                    season = Field("spring")
                }
            }, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Machine-input purchase receipt state is invalid.");
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "machine-input-purchase-save",
                Status = "available"
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "42",
                Status = "available"
            },
            StateHash = SnapshotHash.ComputeStateHash(state),
            State = state
        };

        static object Field(object value) => new
        {
            status = "available",
            confidence = 1d,
            value
        };
    }

}
