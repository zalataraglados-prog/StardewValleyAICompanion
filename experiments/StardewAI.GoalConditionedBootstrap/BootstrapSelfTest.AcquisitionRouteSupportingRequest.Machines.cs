using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineInputSupportingRequest(
        StardewAI.Contracts.State.SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowering,
        AcquisitionRouteDispatchCandidateMatch[] support)
    {
        const string routeDecision =
            "target-date-acquisition-route:full_shipment:machine:keg-wheat";
        var materialClaim = new MaterialReservationUpsertRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = ledger.Revision,
            ReservationId =
                "reservation:full_shipment:machine:keg-wheat:material:0:0",
            SourceDecisionId = routeDecision,
            GoalId = "grandpa.stage1.21_points",
            NodeId = "player:123",
            SlotIndex = 0,
            QualifiedItemId = "(O)262",
            Quantity = 2,
            Purpose = "reserve target-date acquisition material input"
        };
        var claimSet = new AcquisitionRouteReservationClaimSet(
            routeDecision,
            snapshot.StateHash,
            ledger.Revision,
            true,
            false,
            Array.Empty<string>(),
            new[] { materialClaim },
            Array.Empty<CurrencyReservationUpsertRequest>());
        var reservation = new AcquisitionRouteTargetDateReservation(
            requirement.RouteOccurrenceId,
            null!,
            "resolved_inventory_reservation_match",
            true,
            true,
            "claim_proposed",
            claimSet,
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
            new[]
            {
                "machine_processing_capacity_before_day_end_shortfall:0:2"
            },
            Array.Empty<string>());
        var request = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                reservation,
                processing,
                snapshot,
                ledger,
                support,
                supportDeadlineTotalDay: 2,
                rankingSha256: new string('b', 64));
        Require(request.SupportRequestReady &&
                request.SupportTransitionKind == "machine_input_load" &&
                request.DeadlineProofVerified &&
                request.ReservationClaimBoundToCandidate &&
                request.AtomicCommitPreflightPassed &&
                request.ExpectedReadyTotalDay == 1 &&
                request.InputQualifiedItemId == "(O)262" &&
                request.InputSlotIndex == 0 &&
                request.InputRequiredQuantity == 1 &&
                request.MachineQualifiedItemId == "(BC)12" &&
                request.PredictedProcessingMinutes == 1750 &&
                request.AtomicCommitRequest?.MaterialClaims.Single().Quantity ==
                    2,
            "Deadline-safe machine input did not produce an atomic support request: " +
            request.Status + ":" + string.Join(",", request.BlockingReasons));

        var commitResult = new StardewAI.Core.Strategy
            .ReservationPortfolioLedgerService().Commit(
                ledger,
                snapshot,
                request.AtomicCommitRequest!,
                "2026-09-27T01:00:00Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "The shared reservation service rejected a valid machine support request.");
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                commitResult.Ledger!,
                snapshot,
                commitResult,
                requestSha256: new string('c', 64));
        Require(commitReceipt.SupportReservationCommitVerified,
            "A valid machine support reservation commit was not verified: " +
            string.Join(",", commitReceipt.BlockingReasons));
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.BuildCore(
                request,
                commitReceipt,
                requirement,
                lowering,
                snapshot,
                commitResult.Ledger!,
                support,
                new string('b', 64),
                new string('c', 64),
                new string('d', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue?.Items.Single().OptionId ==
                    "executor.load_machine_input" &&
                compilation.SupportReservationCommitVerified &&
                compilation.FreshReplanRequiredAfterSuccess &&
                !compilation.TerminalReceiptEligible,
            "Committed machine support request did not compile through the shared queue: " +
            string.Join(",", compilation.BlockingReasons));
        VerifyMachineInputSupportingReceipt(
            compilation,
            snapshot,
            request,
            commitReceipt,
            commitResult.Ledger!,
            requirement);

        var driftedDuration = CloneCandidate(support[0].Candidate);
        driftedDuration.Parameters = driftedDuration.Parameters.Select(parameter =>
            parameter.Name == "predicted_processing_minutes"
                ? Parameter(parameter.Name, "1740")
                : parameter).ToArray();
        var durationRejected =
            AcquisitionRouteSupportingTransitionRequestBuilder.BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                reservation,
                processing,
                snapshot,
                ledger,
                new[]
                {
                    support[0] with { Candidate = driftedDuration }
                },
                supportDeadlineTotalDay: 2);
        Require(!durationRejected.SupportRequestReady &&
                durationRejected.BlockingReasons.Contains(
                    "machine_input_processing_duration_drifted",
                    StringComparer.Ordinal),
            "A machine input candidate with drifted native duration was admitted.");

        var wrongSlotClaim = materialClaim.WithSlot(1);
        var wrongReservation = reservation.WithClaim(new[] { wrongSlotClaim });
        var wrongSlot = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                wrongReservation,
                processing.WithReservation(wrongReservation),
                snapshot,
                ledger,
                support,
                supportDeadlineTotalDay: 2);
        Require(!wrongSlot.SupportRequestReady &&
                wrongSlot.BlockingReasons.Contains(
                    "machine_input_candidate_material_claim_mismatch",
                    StringComparer.Ordinal),
            "A machine support candidate escaped its exact reserved input slot.");

        var wrongNodeClaim = materialClaim.WithNode("chest:Farm:10:10");
        var wrongNodeReservation = reservation.WithClaim(
            new[] { wrongNodeClaim });
        var wrongNode = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                wrongNodeReservation,
                processing.WithReservation(wrongNodeReservation),
                snapshot,
                ledger,
                support,
                supportDeadlineTotalDay: 2);
        Require(!wrongNode.SupportRequestReady &&
                wrongNode.BlockingReasons.Contains(
                    "machine_input_candidate_material_claim_mismatch",
                    StringComparer.Ordinal),
            "A machine load consumed a player slot while binding a chest claim.");
    }

    private static void VerifyMachineInputSupportingReceipt(
        AcquisitionRouteDispatchCompilation compilation,
        SnapshotEnvelope before,
        AcquisitionRouteSupportingTransitionRequest request,
        AcquisitionRouteSupportingTransitionCommitReceipt commitReceipt,
        StrategyCommitmentLedger committedLedger,
        AcquisitionRouteTargetDateUnlock requirement)
    {
        var after = LoadedMachineSnapshot(
            before,
            materialStack: 1,
            sourceId: "machine:(BC)12:rule:keg_wheat");
        var execution = MachineSupportingExecutionReceipt(
            compilation,
            before,
            after);
        var verified = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                execution,
                after,
                "run.machine-support.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(verified.SupportingTransitionVerified &&
                verified.QueueExecutionVerified &&
                verified.FreshReplanRequired &&
                !verified.TerminalReceiptEligible &&
                !verified.FormalTrainingAuthorized &&
                verified.CropPlantingTransition is null &&
                verified.MachineInputTransition is
                {
                    Verified: true,
                    BeforeCapacityState: "idle",
                    AfterCapacityState: "processing",
                    AfterMinutesUntilReady: 1750
                } &&
                verified.MachineInputTransition.MaterialConsumptions is
                [
                    {
                        ReservationId:
                            "reservation:full_shipment:machine:keg-wheat:material:0:0",
                        BeforeQuantity: 2,
                        AfterQuantity: 1,
                        ObservedConsumedQuantity: 1,
                        Verified: true
                    }
                ],
            "Exact machine load did not produce a verified nonterminal receipt: " +
            string.Join(",", verified.BlockingReasons));
        Require(request.SupportMaterialConsumptions.Length == 1 &&
                request.SupportMaterialConsumptions[0].ConsumedQuantity == 1,
            "Machine support request lost its per-action consumption plan.");

        var transitionHash = new string('e', 64);
        var settlementRequest =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildRequestCore(
                    request,
                    commitReceipt,
                    compilation,
                    verified,
                    after,
                    committedLedger,
                    transitionHash);
        Require(settlementRequest.MaterialConsumptions is
                [
                    {
                        MaterialReservationId:
                            "reservation:full_shipment:machine:keg-wheat:material:0:0",
                        ConsumedQuantity: 1
                    }
                ] &&
                string.IsNullOrEmpty(
                    settlementRequest.MaterialReservationId),
            "Machine settlement did not use the canonical consumption set.");
        var settlementResult = new StardewAI.Core.Strategy
            .ReservationPortfolioLedgerService()
            .SettleSupportingTransition(
                committedLedger,
                after,
                settlementRequest,
                "2026-09-27T01:00:02Z");
        Require(settlementResult.Accepted &&
                settlementResult.Ledger is not null &&
                settlementResult.CompletedMaterialReservationIds.Length == 0 &&
                settlementResult.ActiveMaterialReservationIds.SequenceEqual(
                    new[]
                    {
                        "reservation:full_shipment:machine:keg-wheat:material:0:0"
                    },
                    StringComparer.Ordinal) &&
                settlementResult.Ledger.MaterialReservations.Single(
                    row => row.ReservationId ==
                        settlementRequest.MaterialConsumptions[0]
                            .MaterialReservationId) is
                    {
                        Status: StrategyCommitmentStatuses.Active,
                        Quantity: 1
                    },
            "Machine support settlement did not preserve the unconsumed claim: " +
            string.Join(",", settlementResult.Errors));
        var settlementReceipt =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildReceiptCore(
                    request,
                    commitReceipt,
                    compilation,
                    verified,
                    after,
                    committedLedger,
                    settlementRequest,
                    settlementResult,
                    settlementResult.Ledger!,
                    transitionHash);
        Require(settlementReceipt is
                {
                    Status:
                        "verified_supporting_transition_claim_settlement",
                    ExactSettlementReplayVerified: true,
                    ReservationLifecycleVerified: true,
                    FreshReplanRequired: true,
                    RouteTerminalCompletionRecorded: false,
                    TerminalReceiptEligible: false,
                    FormalTrainingAuthorized: false,
                    ConsumedQuantity: 1
                } &&
                settlementReceipt.ActiveMaterialReservationIds.SequenceEqual(
                    settlementResult.ActiveMaterialReservationIds,
                    StringComparer.Ordinal),
            "Machine partial settlement receipt failed exact replay: " +
            string.Join(",", settlementReceipt.BlockingReasons));

        var settledLedgerSha256 = new string('f', 64);
        settlementReceipt.SettledLedgerSha256 = settledLedgerSha256;
        var replan = AcquisitionRouteSupportingTransitionReplanBuilder
            .BuildCore(
                settlementReceipt,
                request,
                compilation,
                new AcquisitionRouteSupportingTransitionFreshContext(
                    request.GoalId,
                    after.StateHash,
                    new string('1', 64),
                    settlementResult.Ledger!.Revision,
                    settledLedgerSha256,
                    new string('2', 64),
                    new string('3', 64),
                    requirement.RouteOccurrenceId,
                    requirement.RequirementSetId,
                    requirement.RequirementId,
                    true),
                new string('4', 64));
        Require(replan.FreshTeacherRequestReady &&
                replan.PriorQueueInvalidated &&
                replan.AllTargetDateAxesRebuilt &&
                !replan.FormalTrainingAuthorized &&
                replan.NextTeacherPreferenceRequest?.ExpectedLedgerRevision ==
                    settlementResult.Ledger.Revision,
            "Machine partial settlement did not re-enter complete replanning: " +
            string.Join(",", replan.BlockingReasons));

        var wrongQuantityAfter = LoadedMachineSnapshot(
            before,
            materialStack: 2,
            sourceId: "machine:(BC)12:rule:keg_wheat");
        var wrongQuantity =
            AcquisitionRouteSupportingTransitionReceiptBuilder.BuildCore(
                compilation,
                before,
                MachineSupportingExecutionReceipt(
                    compilation,
                    before,
                    wrongQuantityAfter),
                wrongQuantityAfter,
                "run.machine-support.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(!wrongQuantity.SupportingTransitionVerified &&
                wrongQuantity.BlockingReasons.Contains(
                    "supporting_transition_machine_material_delta_mismatch",
                    StringComparer.Ordinal),
            "A machine load receipt accepted a missing inventory decrement.");

        var wrongSourceAfter = LoadedMachineSnapshot(
            before,
            materialStack: 1,
            sourceId: "machine:(BC)12:rule:wrong");
        var wrongSource =
            AcquisitionRouteSupportingTransitionReceiptBuilder.BuildCore(
                compilation,
                before,
                MachineSupportingExecutionReceipt(
                    compilation,
                    before,
                    wrongSourceAfter),
                wrongSourceAfter,
                "run.machine-support.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(!wrongSource.SupportingTransitionVerified &&
                wrongSource.BlockingReasons.Contains(
                    "supporting_transition_machine_after_state_mismatch",
                    StringComparer.Ordinal),
            "A machine load receipt accepted the wrong native output source.");
    }

    private static SnapshotEnvelope LoadedMachineSnapshot(
        SnapshotEnvelope before,
        int materialStack,
        string sourceId)
    {
        var state = before.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = SupportingTransitionField("Farm"),
            tile_x = SupportingTransitionField(63),
            tile_y = SupportingTransitionField(15),
            inventory = SupportingTransitionField(new[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "262",
                    qualified_item_id = "(O)262",
                    stack = materialStack,
                    quality = 0,
                    sale_price = 25
                }
            }),
            inventory_capacity = SupportingTransitionField(new
            {
                occupied_stacks = 1,
                empty_slots = 11,
                has_empty_slot = true
            })
        }, JsonDefaults.Options);
        state["farm"] = JsonSerializer.SerializeToElement(new
        {
            machines = SupportingTransitionField(new[]
            {
                new
                {
                    location_id = "Farm",
                    tile_x = 64,
                    tile_y = 15,
                    qualified_item_id = "(BC)12",
                    ready_for_harvest = false,
                    minutes_until_ready = 1750,
                    last_output_rule_id = "keg_wheat",
                    last_input_item = new
                    {
                        item_id = "262",
                        qualified_item_id = "(O)262",
                        stack = 2,
                        quality = 0
                    },
                    held_item = new
                    {
                        item_id = "346",
                        qualified_item_id = "(O)346",
                        stack = 1,
                        quality = 0
                    },
                    active_output_authoritative_route_sources = new[]
                    {
                        new
                        {
                            route_kind = "machine_output",
                            source_id = sourceId,
                            qualified_item_id = "(O)346"
                        }
                    }
                }
            }),
            material_inventory_graph = SupportingTransitionField(new
            {
                schema_version = "material_inventory_graph.v1",
                status = "available",
                player_id = 123,
                inventory_nodes = new[]
                {
                    new
                    {
                        node_id = "player:123",
                        inventory_kind = "player_inventory",
                        supply_state = "available",
                        actor_use_authorized = true,
                        slots = new[]
                        {
                            new
                            {
                                slot_index = 0,
                                qualified_item_id = "(O)262",
                                stack = materialStack,
                                quality = 0,
                                sale_price = 25
                            }
                        }
                    }
                }
            })
        }, JsonDefaults.Options);
        return new SnapshotEnvelope
        {
            GameVersion = before.GameVersion,
            SaveId = before.SaveId,
            PlayerId = before.PlayerId,
            GameTick = before.GameTick + 1,
            RealTimestamp = "2026-09-27T01:00:01Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static QueueExecutionReceiptEnvelope
        MachineSupportingExecutionReceipt(
            AcquisitionRouteDispatchCompilation compilation,
            SnapshotEnvelope before,
            SnapshotEnvelope after)
    {
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Machine supporting-transition queue is null.");
        var item = queue.Items.Single();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run.machine-support.self-test",
            QueueId = queue.QueueId,
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = 1,
            ExecutedItemCount = 1,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = 1,
            SelectedCandidateId = compilation.SelectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = new[]
            {
                new QueueExecutionStepReceipt
                {
                    QueueItemIndex = 0,
                    QueueItemCount = 1,
                    OriginalPlannedItemCount = 1,
                    QueueId = queue.QueueId,
                    QueueItemId = item.QueueItemId,
                    OptionId = item.OptionId,
                    SourceStateHash = before.StateHash,
                    CompiledCommandStateHash = before.StateHash,
                    SelectedQueueCandidateCompleted = true,
                    AfterStateHash = after.StateHash,
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick,
                    AfterGameTick = after.GameTick,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = "load_machine_input",
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_machine_input_state_transition_observed"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        item,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(new[]
                    {
                        "farm.material_inventory_graph[player:123,0].stack=1",
                        "farm.machines[Farm,64,15].minutes_until_ready=1750"
                    })
                }
            }
        };
    }
}
