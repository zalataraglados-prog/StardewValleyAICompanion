using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineInputMaterialStagingTransition()
    {
        var before = MachineMaterialStagingSnapshot(afterTransfer: false);
        var requirement = BushRequirement() with
        {
            RouteOccurrenceId = "full_shipment:machine:keg-wheat:staging",
            RequirementId = "ship_beer_staging",
            QualifiedItemId = "(O)346",
            RouteKind = "machine_output",
            SourceId = "machine:(BC)12:rule:keg_wheat",
            UncertaintyMode = "native_outcome_domain_and_retry_bound"
        };
        var lowering = BushLowering() with
        {
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            SupervisionMode = "policy_option",
            UncertaintyMode = requirement.UncertaintyMode,
            EndpointOptionIds = new[] { "farm.collect_machine_outputs" },
            SupportingOptionIds = new[]
            {
                "inventory.transfer_item",
                "farm.process_machines"
            }
        };
        const string routeDecision =
            "target-date-acquisition-route:full_shipment:machine:keg-wheat:staging";
        var claim = new MaterialReservationUpsertRequest
        {
            StateHash = before.StateHash,
            ExpectedLedgerRevision = 3,
            ReservationId = "reservation:machine-staging:material:0:0",
            SourceDecisionId = routeDecision,
            GoalId = "grandpa.stage1.21_points",
            NodeId = "chest:Farm:4,5",
            SlotIndex = 0,
            QualifiedItemId = "(O)262",
            Quantity = 2,
            Purpose = "reserve target-date acquisition material input"
        };
        var reservation = new AcquisitionRouteTargetDateReservation(
            requirement.RouteOccurrenceId,
            null!,
            "resolved_inventory_reservation_match",
            true,
            true,
            "claim_proposed",
            new AcquisitionRouteReservationClaimSet(
                routeDecision,
                before.StateHash,
                3,
                true,
                false,
                Array.Empty<string>(),
                new[] { claim },
                Array.Empty<CurrencyReservationUpsertRequest>()),
            Array.Empty<string>(),
            Array.Empty<string>());
        var schedule = new AcquisitionMachineProcessingScheduleBinding(
            "manual_input_processing",
            "(BC)12",
            64,
            15,
            "idle",
            -1,
            2,
            0,
            1750,
            null,
            null,
            1020,
            null);
        var processing = new AcquisitionRouteTargetDateProcessing(
            requirement.RouteOccurrenceId,
            reservation,
            "resolved_processing_lead_time_miss",
            true,
            false,
            "native_machine_processing_schedule",
            new[]
            {
                new AcquisitionProcessingLeadTimeEvaluation(
                    "Farm",
                    "manual_input_processing",
                    "resolved_machine_no_new_output_ready_on_target_date",
                    "exact_native_machine_timer_lower_bound",
                    null,
                    1,
                    1,
                    0,
                    0,
                    false,
                    new[] { "state.farm.machines.value[]" },
                    Array.Empty<string>(),
                    schedule)
            },
            new[] { "machine_processing_capacity_before_day_end_shortfall:0:2" },
            Array.Empty<string>());
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.machine-staging.self-test",
            SaveId = "machine-staging-save",
            PlayerId = "123",
            Revision = 3,
            SourceStateHash = before.StateHash
        };
        var stagingCandidates =
            AcquisitionMachineInputMaterialStaging.BuildCandidates(
                before,
                reservation,
                out var stagingReasons);
        var support = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowering,
                stagingCandidates);
        Require(stagingReasons.Length == 0 &&
                support is
                [
                    {
                        Candidate:
                        {
                            OptionId: "inventory.transfer_item",
                            Kind: "transfer_inventory_item",
                            Available: true
                        },
                        RouteOptionRole: "supporting_transition"
                    }
                ],
            "Reserved chest material did not produce one deterministic transfer candidate: " +
            string.Join(",", stagingReasons));

        var splitBefore = MachineMaterialStagingSnapshot(
            afterTransfer: false,
            initialPlayerStack: 998);
        var splitClaim = new MaterialReservationUpsertRequest
        {
            StateHash = splitBefore.StateHash,
            ExpectedLedgerRevision = claim.ExpectedLedgerRevision,
            ReservationId = claim.ReservationId,
            SourceDecisionId = claim.SourceDecisionId,
            GoalId = claim.GoalId,
            NodeId = claim.NodeId,
            SlotIndex = claim.SlotIndex,
            QualifiedItemId = claim.QualifiedItemId,
            Quantity = claim.Quantity,
            Purpose = claim.Purpose
        };
        var splitReservation = reservation with
        {
            ClaimSet = reservation.ClaimSet! with
            {
                SourceStateHash = splitBefore.StateHash,
                MaterialClaims = new[] { splitClaim }
            }
        };
        var splitCandidates =
            AcquisitionMachineInputMaterialStaging.BuildCandidates(
                splitBefore,
                splitReservation,
                out var splitReasons);
        Require(splitCandidates.Length == 0 &&
                splitReasons.Contains(
                    "machine_input_staging_requires_single_destination_slot",
                    StringComparer.Ordinal),
            "Machine input staging admitted a transfer spanning multiple player slots.");

        var request = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                reservation,
                processing,
                before,
                ledger,
                support,
                supportDeadlineTotalDay: 2,
                rankingSha256: new string('5', 64));
        Require(request.SupportRequestReady &&
                request.SupportTransitionKind ==
                    "machine_input_material_transfer" &&
                request.SupportMaterialConsumptions.Length == 0 &&
                request.SupportMaterialRelocations is
                [
                    {
                        ReservationId:
                            "reservation:machine-staging:material:0:0",
                        SourceNodeId: "chest:Farm:4,5",
                        DestinationNodeId: "player:123",
                        DestinationSlotIndex: 0,
                        Quantity: 2
                    }
                ] &&
                request.MaterialTransferIntent is
                {
                    SourceNodeId: "chest:Farm:4,5",
                    DestinationNodeId: "player:123",
                    Quantity: 2
                },
            "Machine input staging request was not reservation-bound: " +
            string.Join(",", request.BlockingReasons));

        var service = new ReservationPortfolioLedgerService();
        var commit = service.Commit(
            ledger,
            before,
            request.AtomicCommitRequest!,
            "2026-09-27T03:00:00Z");
        Require(commit.Accepted && commit.Ledger is not null,
            "Machine staging reservation commit failed: " +
            string.Join(",", commit.Errors));
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder
                .BuildCore(
                    request,
                    ledger,
                    commit.Ledger!,
                    before,
                    commit,
                    new string('6', 64));
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.BuildCore(
                request,
                commitReceipt,
                requirement,
                lowering,
                before,
                commit.Ledger!,
                support,
                new string('5', 64),
                new string('6', 64),
                new string('7', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue?.Items.Select(item => item.OptionId)
                    .SequenceEqual(new[]
                    {
                        "executor.move_to_tile",
                        "executor.transfer_material"
                    }, StringComparer.Ordinal) == true,
            "Machine staging did not compile through the existing transfer queue: " +
            string.Join(",", compilation.BlockingReasons));

        var after = MachineMaterialStagingSnapshot(afterTransfer: true);
        var execution = MaterialStagingExecutionReceipt(
            compilation,
            before,
            after);
        var transition = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                execution,
                after,
                "run.machine-staging.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(transition.SupportingTransitionVerified &&
                transition.MaterialTransferTransition is
                {
                    Verified: true,
                    ReservationId:
                        "reservation:machine-staging:material:0:0",
                    SourceQuantityBefore: 2,
                    SourceQuantityAfter: 0,
                    DestinationQuantityBefore: 0,
                    DestinationQuantityAfter: 2
                },
            "Machine staging receipt did not verify the exact native move: " +
            string.Join(",", transition.BlockingReasons));

        var transitionHash = new string('8', 64);
        var settlementRequest =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildRequestCore(
                    request,
                    commitReceipt,
                    compilation,
                    transition,
                    after,
                    commit.Ledger!,
                    transitionHash);
        var settlement = service.SettleSupportingTransition(
            commit.Ledger!,
            after,
            settlementRequest,
            "2026-09-27T03:00:02Z");
        Require(settlement.Accepted &&
                settlement.Ledger?.MaterialReservations.Single() is
                {
                    Status: StrategyCommitmentStatuses.Active,
                    NodeId: "player:123",
                    SlotIndex: 0,
                    Quantity: 2
                },
            "Machine staging settlement did not relocate the active claim: " +
            string.Join(",", settlement.Errors));
        var settlementReceipt =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildReceiptCore(
                    request,
                    commitReceipt,
                    compilation,
                    transition,
                    after,
                    commit.Ledger!,
                    settlementRequest,
                    settlement,
                    settlement.Ledger!,
                    transitionHash);
        Require(settlementReceipt is
                {
                    ReservationLifecycleVerified: true,
                    ExactSettlementReplayVerified: true,
                    FreshReplanRequired: true,
                    FormalTrainingAuthorized: false
                } &&
                settlementReceipt.RelocatedMaterialReservationIds
                    .SequenceEqual(new[]
                    {
                        "reservation:machine-staging:material:0:0"
                    }, StringComparer.Ordinal),
            "Machine staging relocation did not close with exact replay: " +
            string.Join(",", settlementReceipt.BlockingReasons));
    }

    private static SnapshotEnvelope MachineMaterialStagingSnapshot(
        bool afterTransfer,
        int initialPlayerStack = 0)
    {
        var playerStack = afterTransfer
            ? initialPlayerStack + 2
            : initialPlayerStack;
        var chestStack = afterTransfer ? 0 : 2;
        var playerInventory = playerStack == 0
            ? Array.Empty<object>()
            : new object[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "262",
                    qualified_item_id = "(O)262",
                    stack = playerStack,
                    quality = 0,
                    sale_price = 25
                }
            };
        var playerGraphSlots = playerStack == 0
            ? Array.Empty<object>()
            : new object[]
            {
                MaterialStagingSlot(playerStack)
            };
        var chestGraphSlots = chestStack == 0
            ? Array.Empty<object>()
            : new object[]
            {
                MaterialStagingSlot(chestStack)
            };
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new
            {
                time = new
                {
                    time_of_day = MaterialStagingField(900),
                    total_days = MaterialStagingField(0)
                },
                player = new
                {
                    location_id = MaterialStagingField("Farm"),
                    tile_x = MaterialStagingField(afterTransfer ? 4 : 8),
                    tile_y = MaterialStagingField(afterTransfer ? 6 : 8),
                    inventory = MaterialStagingField(playerInventory),
                    inventory_capacity = MaterialStagingField(new
                    {
                        occupied_stacks = playerStack > 0 ? 1 : 0,
                        empty_slots = playerStack > 0 ? 11 : 12,
                        has_empty_slot = true
                    })
                },
                farm = new
                {
                    machines = MaterialStagingField(new[]
                    {
                        new
                        {
                            location_id = "Farm",
                            location_kind = "farm_outdoor",
                            location_is_current = true,
                            machine_has_input = true,
                            machine_has_output = true,
                            tile_x = 64,
                            tile_y = 15,
                            qualified_item_id = "(BC)12",
                            display_name = "Keg",
                            ready_for_harvest = false,
                            minutes_until_ready = -1,
                            held_item = (object?)null,
                            loadable_inputs = Array.Empty<object>()
                        }
                    }),
                    material_inventory_graph = MaterialStagingField(new
                    {
                        schema_version = "material_inventory_graph.v1",
                        status = "available",
                        player_id = 123,
                        inventory_nodes = new object[]
                        {
                            new
                            {
                                node_id = "player:123",
                                inventory_kind = "player_inventory",
                                supply_state = "available",
                                location_id = "Farm",
                                owner_player_id = 123,
                                actor_use_authorized = true,
                                capacity = 12,
                                slots = playerGraphSlots
                            },
                            new
                            {
                                node_id = "chest:Farm:4,5",
                                inventory_kind = "chest",
                                supply_state = "available",
                                location_id = "Farm",
                                tile_x = 4,
                                tile_y = 5,
                                owner_player_id = 123,
                                actor_use_authorized = true,
                                capacity = 36,
                                slots = chestGraphSlots
                            }
                        },
                        access_points = new[]
                        {
                            new
                            {
                                access_point_id = "access:chest:Farm:4,5",
                                node_id = "chest:Farm:4,5",
                                access_kind = "placed_chest",
                                location_id = "Farm",
                                location_is_current = true,
                                tile_x = 4,
                                tile_y = 5,
                                special_chest_type = "None",
                                owner_player_id = 123,
                                is_player_chest = true,
                                locked_by_other_player = false
                            }
                        }
                    })
                },
                current_location = new
                {
                    map = MaterialStagingField(new
                    {
                        location_id = "Farm",
                        width = 100,
                        height = 100
                    })
                },
                menus = new
                {
                    active_menu = MaterialStagingField(new
                    {
                        is_open = false,
                        type = "none"
                    })
                },
                locations = new
                {
                    collision_grid = MaterialStagingField(new
                    {
                        location_id = "Farm",
                        width = 100,
                        height = 100,
                        notable_tiles = Array.Empty<object>()
                    }),
                    route_action_branch_coverage =
                        MaterialStagingField(new
                        {
                            rows = Array.Empty<object>()
                        })
                }
            }, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Machine staging snapshot is null.");
        return new SnapshotEnvelope
        {
            GameVersion = "1.6.15",
            SaveId = new FieldEnvelope<string?>
            {
                Value = "machine-staging-save",
                Status = "available"
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = "available"
            },
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = afterTransfer ? 3 : 1,
            RealTimestamp = afterTransfer
                ? "2026-09-27T03:00:01Z"
                : "2026-09-27T03:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static object MaterialStagingSlot(int stack) => new
    {
        slot_index = 0,
        item_id = "262",
        qualified_item_id = "(O)262",
        runtime_type = "StardewValley.Object",
        context_tags = Array.Empty<string>(),
        context_tags_projection_status = "exact_item_get_context_tags",
        edibility = -300,
        edibility_projection_status = "exact_object_edibility",
        stack,
        maximum_stack_size = 999,
        quality = 0,
        sale_price = 25
    };

    private static object MaterialStagingField<T>(T value) => new
    {
        value,
        status = "available",
        source = new
        {
            kind = "game_object",
            path = "test"
        },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1d
    };

    private static QueueExecutionReceiptEnvelope
        MaterialStagingExecutionReceipt(
            AcquisitionRouteDispatchCompilation compilation,
            SnapshotEnvelope before,
            SnapshotEnvelope after)
    {
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Machine staging queue is null.");
        var intermediateHash = new string('9', 64);
        var first = queue.Items[0];
        var second = queue.Items[1];
        var reboundSecond = JsonSerializer.Deserialize<ActionQueueItem>(
            JsonSerializer.Serialize(second, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Machine staging rebound item is null.");
        reboundSecond.NormalizedCommand!.StateHash = intermediateHash;
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run.machine-staging.self-test",
            QueueId = queue.QueueId,
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = 2,
            ExecutedItemCount = 2,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = 2,
            SelectedCandidateId = compilation.SelectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = new[]
            {
                new QueueExecutionStepReceipt
                {
                    QueueItemIndex = 0,
                    QueueItemCount = 2,
                    OriginalPlannedItemCount = 2,
                    QueueId = queue.QueueId,
                    QueueItemId = first.QueueItemId,
                    OptionId = first.OptionId,
                    SourceStateHash = before.StateHash,
                    CompiledCommandStateHash = before.StateHash,
                    SelectedQueueCandidateCompleted = false,
                    AfterStateHash = intermediateHash,
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick,
                    AfterGameTick = before.GameTick + 1,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = "move_to_tile",
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_movement_target_observed"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        first,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(new[]
                    {
                        "player.tile=4,6"
                    })
                },
                new QueueExecutionStepReceipt
                {
                    QueueItemIndex = 1,
                    QueueItemCount = 2,
                    OriginalPlannedItemCount = 2,
                    QueueId = queue.QueueId,
                    QueueItemId = second.QueueItemId,
                    OptionId = second.OptionId,
                    SourceStateHash = intermediateHash,
                    CompiledCommandStateHash = before.StateHash,
                    SelectedQueueCandidateCompleted = true,
                    AfterStateHash = after.StateHash,
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick + 1,
                    AfterGameTick = after.GameTick,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = "transfer_material",
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_chest_transfer_observed"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        reboundSecond,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(new[]
                    {
                        "farm.material_inventory_graph[chest:Farm:4,5,0].stack=0",
                        "farm.material_inventory_graph[player:123,0].stack=2"
                    })
                }
            }
        };
    }
}
