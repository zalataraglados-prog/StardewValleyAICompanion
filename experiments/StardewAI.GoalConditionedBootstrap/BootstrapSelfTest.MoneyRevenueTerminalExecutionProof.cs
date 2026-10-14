using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueTerminalExecutionProof()
    {
        const string fixtureId = "terminal-execution-proof";
        var before = MoneyRevenueTerminalSnapshot(
            gameTick: 100,
            money: 100,
            totalMoneyEarned: 49_900,
            inventoryStack: 1);
        var after = MoneyRevenueTerminalSnapshot(
            gameTick: 110,
            money: 120,
            totalMoneyEarned: 49_920,
            inventoryStack: 0);
        var episodePath = VerifyMoneyRevenueShopRecurrenceArtifact(
            before,
            after,
            fixtureId);
        var root = Path.GetDirectoryName(episodePath) ??
            throw new InvalidDataException(
                "Money Revenue terminal fixture root is missing.");
        var beforeSnapshotPath = Path.Combine(root, "before.json");
        var recurrenceManifestPath = Path.Combine(
            root,
            "shop-revenue-recurrence-manifest.json");
        var recurrenceReceiptPath = Path.Combine(
            root,
            "shop-revenue-recurrence-receipt.json");
        var beforeLedgerPath = Path.Combine(root, "strategy-ledger-before.json");
        var afterLedgerPath = Path.Combine(root, "strategy-ledger-after.json");
        var ledger = MoneyRevenueTerminalLedger(before.StateHash);
        Write(beforeLedgerPath, ledger);
        Write(afterLedgerPath, ledger);

        var request = MoneyRevenueTerminalRequest(
            before,
            beforeSnapshotPath,
            beforeLedgerPath,
            ledger,
            "shop-parsnip");
        var proof = new MoneyRevenueTerminalAlternativeProof
        {
            RequestId = request.RequestId,
            BeforeStrategyLedgerPath = beforeLedgerPath,
            AfterStrategyLedgerPath = afterLedgerPath,
            RouteTransitions =
                Array.Empty<MoneyRevenueTerminalQueueTransitionProof>(),
            ShopSaleEpisodePath = episodePath,
            RevenueRecurrenceManifestPath = recurrenceManifestPath,
            RevenueRecurrenceReceiptPath = recurrenceReceiptPath
        };
        var evidence = MoneyRevenueTerminalExecutionProofBuilder
            .VerifyAlternative(request, proof);
        Require(evidence.ReservationSafetyVerified &&
                evidence.ResourceNeutralRouteVerified &&
                evidence.NativeTerminalTransitionVerified &&
                evidence.RevenueRecurrenceVerified &&
                evidence.TargetInventoryStack == 1 &&
                evidence.TargetReservedQuantity == 0 &&
                evidence.TargetAvailableQuantity == 1 &&
                evidence.ElapsedUpdateTicks == 10 &&
                evidence.ElapsedGameMinutes == 1 &&
                evidence.EvidenceSha256.Length == 64,
            "Money Revenue terminal execution proof was rejected.");

        ledger.MaterialReservations = new[]
        {
            new MaterialReservation
            {
                ReservationId = "reservation.protected-parsnip",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = "decision.protect-parsnip",
                SourceStateHash = before.StateHash,
                GoalId = "goal.protect-parsnip",
                OwnerPlayerId = 42,
                NodeId = "player:42",
                SlotIndex = 0,
                QualifiedItemId = "(O)24",
                Quantity = 1,
                Purpose = "protect hermetic target stack"
            }
        };
        Write(beforeLedgerPath, ledger);
        Write(afterLedgerPath, ledger);
        request.TerminalStrategyLedgerSha256 =
            CurrentTeacherFrontierSupport.HashFile(beforeLedgerPath);
        var rejected = false;
        try
        {
            MoneyRevenueTerminalExecutionProofBuilder.VerifyAlternative(
                request,
                proof);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A fully reserved Money Revenue terminal stack was admitted.");
    }

    private static MoneyRevenueCounterfactualProofRequest
        MoneyRevenueTerminalRequest(
            SnapshotEnvelope before,
            string beforeSnapshotPath,
            string beforeLedgerPath,
            StrategyCommitmentLedger ledger,
            string identitySuffix) => new()
            {
                RequestId = "request.terminal-" + identitySuffix,
                ConcreteAlternativeId = "alternative.terminal-" + identitySuffix,
                SourceStateHash = before.StateHash,
                SourceSnapshotPath = beforeSnapshotPath,
                SourceSnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(beforeSnapshotPath),
                SourceStrategyLedgerPath = beforeLedgerPath,
                SourceStrategyLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(beforeLedgerPath),
                SourceStrategyLedgerId = ledger.LedgerId,
                SourceStrategyLedgerRevision = ledger.Revision,
                TerminalStateHash = before.StateHash,
                TerminalSnapshotPath = beforeSnapshotPath,
                TerminalSnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(beforeSnapshotPath),
                TerminalOptionId = "economy.sell_items",
                TerminalCandidate = new MoneyRevenueCandidateCatalogEntry
                {
                    CatalogCandidateId = "candidate." + identitySuffix,
                    CandidateChannel = "shop_sale",
                    CandidateId = identitySuffix,
                    CandidateKind = "native_shop_sale",
                    Available = true,
                    ComparisonEligible = true,
                    LocationId = "SeedShop",
                    QualifiedItemId = "(O)24",
                    ShopId = "SeedShop",
                    SlotIndex = 0,
                    Quantity = 1,
                    UnitPrice = 20,
                    TotalValue = 20
                },
                RequiredRevenueTransitionProofKind =
                MoneyRevenueRecurrenceProofKinds.NativeShopSale,
                TerminalStrategyLedgerSourceKind = "direct_source_ledger",
                TerminalStrategyLedgerPath = beforeLedgerPath,
                TerminalStrategyLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(beforeLedgerPath),
                TerminalStrategyLedgerId = ledger.LedgerId,
                TerminalStrategyLedgerRevision = ledger.Revision,
                TerminalStrategyLedgerSourceStateHash = ledger.SourceStateHash,
                TerminalOpportunityCostProofRequired = true,
                TerminalReservationClaimProofRequired = true,
                NativeShopSaleProofRequired = true,
                RevenueRecurrenceProofRequired = true
            };

    private static StrategyCommitmentLedger MoneyRevenueTerminalLedger(
        string stateHash) => new()
        {
            LedgerId = "ledger.terminal-shop-parsnip",
            SaveId = "money-progression-save",
            PlayerId = "42",
            Revision = 7,
            UpdatedAt = "2026-10-14T00:00:00Z",
            SourceStateHash = stateHash
        };

    private static SnapshotEnvelope MoneyRevenueTerminalSnapshot(
        long gameTick,
        int money,
        uint totalMoneyEarned,
        int inventoryStack)
    {
        var inventory = inventoryStack > 0
            ? new object[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "24",
                    qualified_item_id = "(O)24",
                    is_empty = false,
                    stack = inventoryStack,
                    quality = 0,
                    sell_to_store_price = 20
                }
            }
            : new object[]
            {
                new
                {
                    slot_index = 0,
                    item_id = string.Empty,
                    qualified_item_id = string.Empty,
                    is_empty = true,
                    stack = 0,
                    quality = 0,
                    sell_to_store_price = 0
                }
            };
        var materialSlots = inventoryStack > 0
            ? new object[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "24",
                    qualified_item_id = "(O)24",
                    display_name = "Parsnip",
                    runtime_type = "StardewValley.Object",
                    context_tags = Array.Empty<string>(),
                    context_tags_projection_status = "available",
                    stack = inventoryStack,
                    maximum_stack_size = 999,
                    quality = 0,
                    sale_price = 20
                }
            }
            : Array.Empty<object>();
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new
            {
                identity = new
                {
                    save_id = MoneyProgressionField(
                        "money-progression-save"),
                    player_id = MoneyProgressionField("42")
                },
                time = new
                {
                    total_days = MoneyProgressionField(10)
                },
                player = new
                {
                    money = MoneyProgressionField(money),
                    total_money_earned =
                        MoneyProgressionField(totalMoneyEarned),
                    inventory = MoneyProgressionField(inventory)
                },
                farm = new
                {
                    shipping_bins = MoneyProgressionField(new[]
                    {
                        new
                        {
                            completed = true,
                            tile_x = 10,
                            tile_y = 12,
                            bin_scope = "shared",
                            player_id = 42L,
                            pending_settlement_value = 0,
                            pending_settlement_value_complete = true,
                            pending_settlement_timing =
                                "next_native_day_transition",
                            contents_signature = "empty",
                            contents_truncated = false
                        }
                    }),
                    material_inventory_graph = MoneyProgressionField(new
                    {
                        schema_version = "material_inventory_graph.v1",
                        status = "available",
                        player_id = 42L,
                        inventory_nodes = new[]
                        {
                            new
                            {
                                node_id = "player:42",
                                inventory_kind = "player_inventory",
                                supply_state = "available",
                                location_id = "FarmHouse",
                                owner_player_id = 42L,
                                ownership_class = "actor",
                                actor_use_authorized = true,
                                global_inventory_id = "player:42",
                                capacity = 36,
                                slots = materialSlots
                            }
                        },
                        access_points = Array.Empty<object>(),
                        workbench_links = Array.Empty<object>(),
                        quantity_rows = Array.Empty<object>(),
                        physical_inventory_count = 1,
                        access_point_count = 0,
                        deduplicated_access_point_count = 0,
                        default_shared_resource_policy =
                            "deny_without_explicit_authorization"
                    })
                }
            }),
            JsonDefaults.Options)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "0.1.0",
            GameVersion = "1.6.15",
            SaveId = MoneyProgressionIdentity("money-progression-save"),
            PlayerId = MoneyProgressionIdentity("42"),
            GameTick = gameTick,
            RealTimestamp = "2026-10-14T00:00:00Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }
}
