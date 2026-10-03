using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineCapacityPlacementContinuation(
        string goalId,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowering,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionRouteTargetDateProcessing processing,
        AcquisitionMachineCapacitySupportBinding binding,
        OptionAvailabilityCandidate capacityOption,
        SnapshotEnvelope before,
        StrategyCommitmentLedger ledger)
    {
        SetParameter(
            capacityOption.Parameters,
            "machine_capacity_requested_candidate_kind",
            "place_machine_item");
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                before,
                [capacityOption],
                includeExecutorCalibrationOptions: true,
                commitmentLedger: ledger),
            goalId);
        var support = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowering,
                ranked,
                binding.MachineQualifiedItemId);
        Require(support is [{ Candidate.Kind: "place_machine_item" }],
            "Crafted acquisition machine did not enter the shared placement chain.");

        var request = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                goalId,
                requirement,
                lowering,
                reservation,
                processing,
                before,
                ledger,
                support,
                supportDeadlineTotalDay: 2,
                rankingSha256: new string('e', 64),
                capacityBinding: binding);
        Require(request.SupportRequestReady &&
                request.SupportTransitionKind ==
                    "machine_capacity_establishment" &&
                request.MachineSupportIntentId == binding.IntentId &&
                request.MachineSupportIntentStage ==
                    MachineSupportIntentStages.PlacementBound &&
                request.AtomicCommitRequest?.MachineSupportIntent is
                    { Stage: MachineSupportIntentStages.PlacementBound },
            "Machine placement request did not advance the existing intent: " +
            string.Join(",", request.BlockingReasons));

        var service = new ReservationPortfolioLedgerService();
        var commitResult = service.Commit(
            ledger,
            before,
            request.AtomicCommitRequest!,
            "2026-09-27T02:00:03Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null &&
                commitResult.Ledger.MachineSupportIntents.Single().Stage ==
                    MachineSupportIntentStages.PlacementBound,
            "Machine placement intent commit was rejected: " +
            string.Join(",", commitResult.Errors));
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                commitResult.Ledger!,
                before,
                commitResult,
                new string('f', 64));
        Require(commitReceipt.SupportReservationCommitVerified,
            "Machine placement commit receipt was not verified: " +
            string.Join(",", commitReceipt.BlockingReasons));

        var committedRanked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                before,
                [capacityOption],
                includeExecutorCalibrationOptions: true,
                commitmentLedger: commitResult.Ledger),
            goalId);
        var committedSupport = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowering,
                committedRanked,
                binding.MachineQualifiedItemId);
        Require(committedSupport is
                [{ Candidate.Kind: "place_machine_item" }],
            "Committed placement intent did not rebuild the exact candidate.");
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.BuildCore(
                request,
                commitReceipt,
                requirement,
                lowering,
                before,
                commitResult.Ledger!,
                committedSupport,
                new string('e', 64),
                new string('f', 64),
                new string('1', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue?.Items.Select(item => item.OptionId)
                    .SequenceEqual(
                        new[]
                        {
                            "executor.move_to_tile",
                            "executor.place_machine"
                        },
                        StringComparer.Ordinal) == true,
            "Machine placement did not compile through the shared move/place queue: " +
            string.Join(",", compilation.BlockingReasons));

        var after = PlacedMachineCapacitySnapshot(before);
        var execution = MachineCapacityPlacementExecutionReceipt(
            compilation,
            before,
            after);
        var transition = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                execution,
                after,
                "run.machine-capacity-placement.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(transition.SupportingTransitionVerified &&
                transition.MachineCapacityTransition is
                {
                    Verified: true,
                    Stage: MachineSupportIntentStages.PlacementBound,
                    ObservedInventoryDelta: -1,
                    BeforeTargetMachinePresent: false,
                    AfterTargetMachinePresent: true
                },
            "Machine placement receipt was not verified: " +
            string.Join(",", transition.BlockingReasons));

        var wrongAfter = PlacedMachineCapacitySnapshot(before, targetX: 8);
        var wrongExecution = MachineCapacityPlacementExecutionReceipt(
            compilation,
            before,
            wrongAfter);
        var rejected = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                wrongExecution,
                wrongAfter,
                "run.machine-capacity-placement.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(!rejected.SupportingTransitionVerified &&
                rejected.BlockingReasons.Contains(
                    "supporting_transition_machine_placement_delta_mismatch",
                    StringComparer.Ordinal),
            "A machine placed at the wrong tile entered the proof chain.");

        var transitionHash = new string('2', 64);
        var settlementRequest =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildRequestCore(
                    request,
                    commitReceipt,
                    compilation,
                    transition,
                    after,
                    commitResult.Ledger!,
                    transitionHash);
        var settlementResult = service.SettleSupportingTransition(
            commitResult.Ledger,
            after,
            settlementRequest,
            "2026-09-27T02:00:05Z");
        Require(settlementResult.Accepted &&
                settlementResult.Ledger is not null &&
                settlementResult.ReboundMachineSupportIntentId ==
                    binding.IntentId,
            "Machine placement marker settlement was rejected: " +
            string.Join(",", settlementResult.Errors));
        var settlementReceipt =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildReceiptCore(
                    request,
                    commitReceipt,
                    compilation,
                    transition,
                    after,
                    commitResult.Ledger!,
                    settlementRequest,
                    settlementResult,
                    settlementResult.Ledger!,
                    transitionHash);
        var settledLedgerSha256 = new string('3', 64);
        settlementReceipt.SettledLedgerSha256 = settledLedgerSha256;
        var replan = AcquisitionRouteSupportingTransitionReplanBuilder
            .BuildCore(
                settlementReceipt,
                request,
                compilation,
                new AcquisitionRouteSupportingTransitionFreshContext(
                    goalId,
                    after.StateHash,
                    new string('4', 64),
                    settlementResult.Ledger!.Revision,
                    settledLedgerSha256,
                    new string('5', 64),
                    new string('6', 64),
                    requirement.RouteOccurrenceId,
                    requirement.RequirementSetId,
                    requirement.RequirementId,
                    true),
                new string('7', 64));
        Require(settlementReceipt.ReservationLifecycleVerified &&
                settlementReceipt.ExactSettlementReplayVerified &&
                settlementReceipt.FreshReplanRequired &&
                replan.FreshTeacherRequestReady &&
                replan.PriorQueueInvalidated &&
                replan.NextTeacherPreferenceRequest is not null &&
                replan.NextTeacherPreferenceRequest.SnapshotStateHash ==
                    after.StateHash &&
                replan.NextTeacherPreferenceRequest.ExpectedLedgerRevision ==
                    settlementResult.Ledger.Revision &&
                !replan.FormalTrainingAuthorized,
            "Placed machine did not rejoin the shared portfolio replan boundary: " +
            string.Join(",", settlementReceipt.BlockingReasons) + ":" +
            string.Join(",", replan.BlockingReasons));
    }

    private static void SetParameter(
        SmallModelActionParameter[] parameters,
        string name,
        string value)
    {
        var matches = parameters.Where(parameter => parameter.Name == name)
            .ToArray();
        Require(matches.Length == 1,
            "Machine-capacity option parameter is missing or ambiguous: " +
            name);
        matches[0].Value = value;
    }

    private static SnapshotEnvelope PlacedMachineCapacitySnapshot(
        SnapshotEnvelope before,
        int targetX = 7)
    {
        var state = before.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        var player = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            state["player"].GetRawText(),
            JsonDefaults.Options)!;
        player["tile_x"] = JsonSerializer.SerializeToElement(new
        {
            value = 6,
            status = "available"
        });
        player["inventory"] = JsonSerializer.SerializeToElement(new
        {
            value = Array.Empty<object>(),
            status = "available"
        });
        player["machine_placement"] = JsonSerializer.SerializeToElement(new
        {
            value = new
            {
                projection_status =
                    "complete_all_inventory_machines_across_loaded_persistent_locations",
                static_projection_fingerprint =
                    "capacity-self-test-placement-complete",
                rows = Array.Empty<object>()
            },
            status = "available"
        });
        state["player"] = JsonSerializer.SerializeToElement(player);

        var farm = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            state["farm"].GetRawText(),
            JsonDefaults.Options)!;
        farm["machines"] = JsonSerializer.SerializeToElement(new
        {
            value = new[]
            {
                new
                {
                    location_id = "FarmHouse",
                    tile_x = targetX,
                    tile_y = 5,
                    item_id = "12",
                    qualified_item_id = "(BC)12",
                    ready_for_harvest = false,
                    minutes_until_ready = 0,
                    held_item = (object?)null
                }
            },
            status = "available"
        });
        farm["material_inventory_graph"] =
            JsonSerializer.SerializeToElement(new
            {
                value = new
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
                            owner_player_id = 123,
                            actor_use_authorized = true,
                            slots = Array.Empty<object>()
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
                },
                status = "available",
                confidence = 1
            });
        state["farm"] = JsonSerializer.SerializeToElement(farm);
        return new SnapshotEnvelope
        {
            SaveId = before.SaveId,
            PlayerId = before.PlayerId,
            GameTick = before.GameTick + 2,
            RealTimestamp = "2026-09-27T02:00:05Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static QueueExecutionReceiptEnvelope
        MachineCapacityPlacementExecutionReceipt(
            AcquisitionRouteDispatchCompilation compilation,
            SnapshotEnvelope before,
            SnapshotEnvelope after)
    {
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Machine-capacity placement queue is null.");
        Require(queue.Items.Length == 2,
            "Machine-capacity placement queue must contain move and place.");
        var intermediateStateHash = PlacementIntermediateStateHash(before);
        var stepSources = new[]
        {
            before.StateHash,
            intermediateStateHash
        };
        var stepTargets = new[]
        {
            intermediateStateHash,
            after.StateHash
        };
        var stepTicks = new[]
        {
            before.GameTick,
            before.GameTick + 1,
            after.GameTick
        };
        var steps = queue.Items.Select((item, index) =>
        {
            var effective = JsonSerializer.Deserialize<ActionQueueItem>(
                JsonSerializer.Serialize(item, JsonDefaults.Options),
                JsonDefaults.Options) ?? throw new InvalidDataException(
                "Machine-capacity placement queue item clone failed.");
            effective.NormalizedCommand.StateHash = stepSources[index];
            return new QueueExecutionStepReceipt
            {
                QueueItemIndex = index,
                QueueItemCount = queue.Items.Length,
                OriginalPlannedItemCount = queue.Items.Length,
                QueueId = queue.QueueId,
                QueueItemId = item.QueueItemId,
                OptionId = item.OptionId,
                SourceStateHash = stepSources[index],
                CompiledCommandStateHash = before.StateHash,
                TeacherPreferenceStateRebound = true,
                SelectedQueueCandidateCompleted =
                    index == queue.Items.Length - 1,
                AfterStateHash = stepTargets[index],
                StateHashChanged = true,
                BeforeGameTick = stepTicks[index],
                AfterGameTick = stepTicks[index + 1],
                AfterSnapshotFresh = true,
                Status = "applied",
                PrimitiveKind = item.NormalizedCommand.Steps.Single().StepType,
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons =
                [
                    index == 0
                        ? "native_movement_transition_observed"
                        : "native_machine_placement_transition_observed"
                ],
                EffectiveQueueItem = JsonSerializer.SerializeToElement(
                    effective,
                    JsonDefaults.Options),
                ChangedFacts = JsonSerializer.SerializeToElement(new[]
                {
                    index == 0
                        ? "player.tile=6,5"
                        : "farm.machines[FarmHouse:7,5]=(BC)12"
                })
            };
        }).ToArray();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run.machine-capacity-placement.self-test",
            QueueId = queue.QueueId,
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = queue.Items.Length,
            ExecutedItemCount = queue.Items.Length,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = queue.Items.Length,
            SelectedCandidateId = compilation.SelectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = steps
        };
    }

    private static string PlacementIntermediateStateHash(
        SnapshotEnvelope before)
    {
        var state = before.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        var player = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            state["player"].GetRawText(),
            JsonDefaults.Options)!;
        player["tile_x"] = JsonSerializer.SerializeToElement(new
        {
            value = 6,
            status = "available"
        });
        state["player"] = JsonSerializer.SerializeToElement(player);
        return SnapshotHash.ComputeStateHash(state);
    }
}
