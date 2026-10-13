using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunFullShipmentSettlement()
    {
        VerifyFullShipmentSettlementEvidence();
        VerifyFullShipmentTerminalSettlementEvidence();
        VerifyFullShipmentRecoveryTransitionEvidence();
        VerifyFullShipmentRecoveryToSleepBoundary();
        VerifyFullShipmentRecurrenceSequence();
    }

    private static void VerifyFullShipmentRecoveryToSleepBoundary()
    {
        var required = new[] { "(O)24", "(O)60", "(O)80" };
        var recoveryAfter = FullShipmentRecoveryToSleepSnapshot(
            gameTick: 1_341,
            volatileProjectionTick: 1_341,
            locationId: "FarmHouse",
            tileX: 3,
            tileY: 11,
            parsnipBinCount: 1,
            parsnipShipped: false);
        var settlementBefore = FullShipmentRecoveryToSleepSnapshot(
            gameTick: 1_382,
            volatileProjectionTick: 1_382,
            locationId: "FarmHouse",
            tileX: 3,
            tileY: 11,
            parsnipBinCount: 1,
            parsnipShipped: false);
        Require(
            recoveryAfter.StateHash != settlementBefore.StateHash,
            "The recovery-to-sleep fixture did not model transient drift.");
        var admitted = FullShipmentRecurrenceProofBuilder
            .ValidateRecoveryToSleepBoundary(
                recoveryAfter,
                settlementBefore,
                required,
                "(O)24");
        Require(
            admitted.Length == 0,
            "Transient recovery stabilization was rejected: " +
            string.Join(",", admitted));

        var moved = FullShipmentRecoveryToSleepSnapshot(
            gameTick: 1_382,
            volatileProjectionTick: 1_382,
            locationId: "FarmHouse",
            tileX: 4,
            tileY: 11,
            parsnipBinCount: 1,
            parsnipShipped: false);
        Require(
            FullShipmentRecurrenceProofBuilder
                .ValidateRecoveryToSleepBoundary(
                    recoveryAfter,
                    moved,
                    required,
                    "(O)24")
                .Contains(
                    "full_shipment_recovery_sleep_actor_position_drifted"),
            "Recovery-to-sleep actor movement was admitted.");

        var consumed = FullShipmentRecoveryToSleepSnapshot(
            gameTick: 1_382,
            volatileProjectionTick: 1_382,
            locationId: "FarmHouse",
            tileX: 3,
            tileY: 11,
            parsnipBinCount: 0,
            parsnipShipped: true);
        var consumedReasons = FullShipmentRecurrenceProofBuilder
            .ValidateRecoveryToSleepBoundary(
                recoveryAfter,
                consumed,
                required,
                "(O)24");
        Require(
            consumedReasons.Contains(
                "full_shipment_recovery_sleep_progress_or_day_drifted") &&
            consumedReasons.Contains(
                "full_shipment_recovery_sleep_pending_bin_item_drifted"),
            "Recovery-to-sleep progress or bin drift was admitted.");

        settlementBefore.StateHash = "tampered";
        Require(
            FullShipmentRecurrenceProofBuilder
                .ValidateRecoveryToSleepBoundary(
                    recoveryAfter,
                    settlementBefore,
                    required,
                    "(O)24")
                .Contains(
                    "full_shipment_recovery_sleep_snapshot_hash_invalid"),
            "A tampered recovery-to-sleep snapshot was admitted.");
    }

    private static SnapshotEnvelope FullShipmentRecoveryToSleepSnapshot(
        long gameTick,
        int volatileProjectionTick,
        string locationId,
        int tileX,
        int tileY,
        int parsnipBinCount,
        bool parsnipShipped)
    {
        const int inGameTime = 610;
        var snapshot = FullShipmentSettlementSnapshot(
            "pending-hash",
            totalDay: 0,
            parsnipShipped: parsnipShipped,
            obsidianShipped: false,
            parsnipBinCount: parsnipBinCount,
            achievement34: false);
        snapshot.GameTick = gameTick;
        snapshot.InGameTime = new FieldEnvelope<int?>
        {
            Value = inGameTime,
            Status = FieldStatus.Available
        };
        snapshot.State["time"] = JsonSerializer.SerializeToElement(new
        {
            total_days = Available(0),
            time = Available(inGameTime)
        });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = Available(locationId),
            tile_x = Available(tileX),
            tile_y = Available(tileY),
            movement_timing_context = Available(new
            {
                projection_tick = volatileProjectionTick
            })
        });
        snapshot.State["current_location"] =
            JsonSerializer.SerializeToElement(new
            {
                identity = Available(new
                {
                    name = locationId,
                    unique_name = locationId
                })
            });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static void VerifyFullShipmentSettlementEvidence()
    {
        var required = new[] { "(O)24", "(O)60", "(O)80" };
        var before = FullShipmentSettlementSnapshot(
            "full-shipment-settlement-before",
            totalDay: 40,
            parsnipShipped: false,
            obsidianShipped: false,
            parsnipBinCount: 1,
            achievement34: false);
        var after = FullShipmentSettlementSnapshot(
            "full-shipment-settlement-after",
            totalDay: 41,
            parsnipShipped: true,
            obsidianShipped: false,
            parsnipBinCount: 0,
            achievement34: false);
        var evidence = FullShipmentSettlementVerifier.Verify(
            required,
            before,
            after);
        Require(
            evidence.Verified &&
            evidence.SettledQualifiedItemId == "(O)24" &&
            evidence.BeforeShippedItemCount == 1 &&
            evidence.AfterShippedItemCount == 2 &&
            evidence.BeforeMissingItemCount == 2 &&
            evidence.AfterMissingItemCount == 1 &&
            evidence.BeforeSettledItemBinCount == 1 &&
            evidence.AfterSettledItemBinCount == 0 &&
            evidence.BeforeTotalDay == 40 &&
            evidence.AfterTotalDay == 41 &&
            !evidence.TerminalTransition,
            "Full Shipment ordinary settlement was not admitted: " +
            string.Join(",", evidence.BlockingReasons));

        var twoItems = FullShipmentSettlementVerifier.Verify(
            required,
            before,
            FullShipmentSettlementSnapshot(
                "full-shipment-settlement-two-items",
                totalDay: 41,
                parsnipShipped: true,
                obsidianShipped: true,
                parsnipBinCount: 0,
                achievement34: true));
        Require(
            !twoItems.Verified &&
            twoItems.BlockingReasons.Contains(
                "full_shipment_settlement_not_exactly_one_new_item"),
            "A two-item Full Shipment transition entered one recurrence step.");

        var prematureAchievement = FullShipmentSettlementVerifier.Verify(
            required,
            before,
            FullShipmentSettlementSnapshot(
                "full-shipment-settlement-premature-achievement",
                totalDay: 41,
                parsnipShipped: true,
                obsidianShipped: false,
                parsnipBinCount: 0,
                achievement34: true));
        Require(
            !prematureAchievement.Verified &&
            prematureAchievement.BlockingReasons.Contains(
                "full_shipment_settlement_achievement_34_state_mismatch"),
            "A nonterminal Full Shipment step admitted achievement 34.");

        var terminal = FullShipmentSettlementVerifier.Verify(
            required,
            FullShipmentTerminalSnapshot(
                "full-shipment-settlement-terminal-before",
                totalDay: 223,
                terminalShippedCount: 0,
                terminalBinCount: 1,
                achievement34: false),
            FullShipmentTerminalSnapshot(
                "full-shipment-settlement-terminal-after",
                totalDay: 224,
                terminalShippedCount: 1,
                terminalBinCount: 0,
                achievement34: true));
        Require(
            terminal.Verified && terminal.TerminalTransition,
            "The shared verifier did not identify the dedicated terminal boundary.");
    }

    private static SnapshotEnvelope FullShipmentSettlementSnapshot(
        string stateHash,
        int totalDay,
        bool parsnipShipped,
        bool obsidianShipped,
        int parsnipBinCount,
        bool achievement34)
    {
        var items = new[]
        {
            new
            {
                item_id = "24",
                qualified_item_id = "(O)24",
                current_shipped_count = parsnipShipped ? 1 : 0,
                shipped = parsnipShipped
            },
            new
            {
                item_id = "60",
                qualified_item_id = "(O)60",
                current_shipped_count = 2,
                shipped = true
            },
            new
            {
                item_id = "80",
                qualified_item_id = "(O)80",
                current_shipped_count = obsidianShipped ? 1 : 0,
                shipped = obsidianShipped
            }
        };
        var shippedCount = items.Count(item => item.shipped);
        var shippingCollection = new Dictionary<string, int>
        {
            ["60"] = 2
        };
        if (parsnipShipped)
            shippingCollection["24"] = 1;
        if (obsidianShipped)
            shippingCollection["80"] = 1;
        var contents = parsnipBinCount > 0
            ? new[]
            {
                new
                {
                    item_id = "24",
                    qualified_item_id = "(O)24",
                    count = parsnipBinCount
                }
            }
            : Array.Empty<object>();
        var missingIds = items
            .Where(item => !item.shipped)
            .Select(item => item.item_id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var json = JsonSerializer.SerializeToElement(new
        {
            time = new
            {
                total_days = Available(totalDay)
            },
            farm = new
            {
                shipping_bins = Available(new[]
                {
                    new
                    {
                        contents
                    }
                })
            },
            world_progress = new
            {
                shipping_collection = Available(shippingCollection),
                achievements = Available(
                    achievement34 ? new[] { 5, 34 } : new[] { 5 }),
                full_shipment_progress = Available(new
                {
                    eligible_item_count = items.Length,
                    shipped_eligible_item_count = shippedCount,
                    missing_item_count = items.Length - shippedCount,
                    completion_ratio = shippedCount / (double)items.Length,
                    complete = shippedCount == items.Length,
                    items,
                    missing_item_ids = missingIds
                })
            }
        });
        return new SnapshotEnvelope
        {
            GameVersion = "1.6.15",
            SaveId = new FieldEnvelope<string?>
            {
                Value = "full-shipment-save",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "full-shipment-player",
                Status = FieldStatus.Available
            },
            StateHash = stateHash,
            GameTick = totalDay * 100L,
            State = json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal)
        };
    }

    private static void VerifyFullShipmentRecurrenceSequence()
    {
        var rows = new[]
        {
            RecurrenceRow(0, 0, 1, 0, 1, terminal: false),
            RecurrenceRow(1, 1, 2, 100, 101, terminal: false),
            RecurrenceRow(2, 2, 3, 223, 224, terminal: true)
        };
        FullShipmentRecurrenceProofBuilder.VerifySequence(
            rows,
            requiredItemCount: 3,
            initialTotalDay: 0);
        FullShipmentRecurrenceProofBuilder.VerifyPrefixSequence(
            rows.Take(2).ToArray(),
            requiredItemCount: 3,
            initialTotalDay: 0,
            expectComplete: false);

        RequireSequenceRejected(
            new[]
            {
                rows[0],
                RecurrenceRow(1, 1, 3, 100, 101, terminal: false),
                rows[2]
            },
            "A noncontiguous shipped-count sequence was admitted.");
        RequireSequenceRejected(
            new[]
            {
                RecurrenceRow(0, 0, 1, 0, 1, terminal: true),
                rows[1],
                rows[2]
            },
            "An early terminal Full Shipment transition was admitted.");
        RequireSequenceRejected(
            new[]
            {
                RecurrenceRow(0, 0, 1, 100, 101, terminal: false),
                RecurrenceRow(1, 1, 2, 99, 100, terminal: false),
                rows[2]
            },
            "A time-reversing Full Shipment recurrence was admitted.");
        RequireSequenceRejected(
            new[]
            {
                rows[0],
                rows[1],
                RecurrenceRow(2, 2, 3, 224, 225, terminal: true)
            },
            "A Full Shipment terminal transition after the Stage-1 deadline was admitted.");
        RequirePrefixSequenceRejected(
            new[]
            {
                RecurrenceRow(0, 0, 1, 0, 1, terminal: true)
            },
            requiredItemCount: 3,
            expectComplete: false,
            failureMessage:
                "An incomplete recurrence prefix admitted a terminal transition.");
        RequirePrefixSequenceRejected(
            rows.Take(2).ToArray(),
            requiredItemCount: 3,
            expectComplete: true,
            failureMessage:
                "An incomplete recurrence prefix was reported as complete.");
        RequirePrefixSequenceRejected(
            Array.Empty<FullShipmentRecurrenceIterationEvidence>(),
            requiredItemCount: 3,
            expectComplete: false,
            failureMessage: "An empty recurrence prefix was admitted.");
        RequirePrefixSequenceRejected(
            rows,
            requiredItemCount: 3,
            expectComplete: false,
            failureMessage:
                "A complete recurrence was reported as an incomplete prefix.");
        RequirePrefixSequenceRejected(
            new[]
            {
                RecurrenceRow(0, 0, 1, 223, 224, terminal: false)
            },
            requiredItemCount: 3,
            expectComplete: false,
            failureMessage:
                "A nonterminal recurrence prefix consumed the terminal deadline day.");
        RequirePrefixSequenceRejected(
            new[]
            {
                RecurrenceRow(0, 0, 1, 222, 223, terminal: false)
            },
            requiredItemCount: 3,
            expectComplete: false,
            failureMessage:
                "A recurrence prefix with insufficient remaining days was admitted.");
    }

    private static FullShipmentRecurrenceIterationEvidence RecurrenceRow(
        int index,
        int beforeCount,
        int afterCount,
        int startDay,
        int endDay,
        bool terminal) => new(
            index,
            "full_shipment:item:" + index,
            "(O)" + index,
            "acquisition-" + index,
            "deposit-" + index,
            "settlement-" + index,
            beforeCount,
            afterCount,
            startDay,
            endDay,
            terminal);

    private static void RequireSequenceRejected(
        IReadOnlyList<FullShipmentRecurrenceIterationEvidence> rows,
        string failureMessage)
    {
        var rejected = false;
        try
        {
            FullShipmentRecurrenceProofBuilder.VerifySequence(
                rows,
                rows.Count,
                initialTotalDay: 0);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, failureMessage);
    }

    private static void RequirePrefixSequenceRejected(
        IReadOnlyList<FullShipmentRecurrenceIterationEvidence> rows,
        int requiredItemCount,
        bool expectComplete,
        string failureMessage)
    {
        var rejected = false;
        try
        {
            FullShipmentRecurrenceProofBuilder.VerifyPrefixSequence(
                rows,
                requiredItemCount,
                initialTotalDay: 0,
                expectComplete: expectComplete);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, failureMessage);
    }
}
