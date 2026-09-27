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
    private static void VerifyMachineCapacitySupportingTransition()
    {
        const string goalId = "grandpa.stage1.21_points";
        const string routeId = "full_shipment:machine:keg-wheat";
        const string intentId =
            "machine-support:acquisition-route:" + routeId + ":(BC)12";
        var before = AcquisitionMachineCapacitySnapshot();
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.machine-capacity.self-test",
            SaveId = "machine-capacity-save",
            PlayerId = "123",
            Revision = 3,
            SourceStateHash = before.StateHash
        };
        var requirement = BushRequirement() with
        {
            RouteOccurrenceId = routeId,
            RequirementId = "ship_beer",
            QualifiedItemId = "(O)346",
            RouteKind = "machine_output",
            SourceId = "machine:(BC)12:rule:keg_wheat",
            UncertaintyMode = "native_outcome_domain_and_retry_bound"
        };
        var lowering = BushLowering() with
        {
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            SupportingOptionIds =
            [
                "farm.establish_supported_machine_capacity",
                "farm.load_supported_machine_input",
                "farm.process_machines"
            ]
        };
        var supportJson = JsonSerializer.Serialize(new
        {
            goal_id = goalId,
            route_occurrence_id = routeId,
            route_kind = requirement.RouteKind,
            source_id = requirement.SourceId,
            output_qualified_item_id = requirement.QualifiedItemId,
            machine_qualified_item_id = "(BC)12"
        });
        var capacityOption = new StardewAI.Contracts.Options
            .OptionAvailabilityCandidate
        {
            OptionId = "farm.establish_supported_machine_capacity",
            Parameters =
            [
                Parameter("machine_capacity_support_kind", "acquisition_route"),
                Parameter("machine_capacity_goal_id", goalId),
                Parameter("machine_capacity_route_occurrence_id", routeId),
                Parameter("machine_capacity_route_kind", requirement.RouteKind),
                Parameter("machine_capacity_source_id", requirement.SourceId),
                Parameter("machine_capacity_output_qualified_item_id", requirement.QualifiedItemId),
                Parameter("machine_capacity_machine_qualified_item_id", "(BC)12"),
                Parameter("machine_capacity_intent_id", intentId),
                Parameter("machine_capacity_requested_candidate_kind", string.Empty)
            ]
        };
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            before,
            [capacityOption],
            includeExecutorCalibrationOptions: true,
            commitmentLedger: ledger);
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            goalId);
        var support = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowering,
                ranked,
                "(BC)12");
        Require(support is [{ Candidate.Kind: "craft_machine_item" }],
            "Missing machine capacity did not select the shared craft candidate.");

        var binding = new AcquisitionMachineCapacitySupportBinding(
            "(BC)12",
            intentId,
            supportJson,
            "complete_exact_static_machine_source_and_missing_runtime_capacity");
        var location = new AcquisitionRouteTargetDateLocation(
            routeId,
            null!,
            "resolved_location_route_miss",
            true,
            false,
            Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
            ["matching_machine_runtime_location_not_present"],
            Array.Empty<string>());
        var facility = new AcquisitionRouteTargetDateFacility(
            routeId,
            location,
            "not_evaluated_missing_machine",
            false,
            null,
            "machine_capacity",
            Array.Empty<AcquisitionFacilityTargetEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        var resource = new AcquisitionRouteTargetDateResource(
            routeId,
            facility,
            "not_evaluated_missing_machine",
            false,
            null,
            "machine_input",
            Array.Empty<AcquisitionResourceInputEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        var currency = new AcquisitionRouteTargetDateCurrency(
            routeId,
            resource,
            "not_evaluated_missing_machine",
            false,
            null,
            "none",
            null,
            Array.Empty<string>(),
            Array.Empty<string>());
        var reservation = new AcquisitionRouteTargetDateReservation(
            routeId,
            currency,
            "not_evaluated_missing_machine",
            false,
            null,
            "not_applicable",
            null,
            Array.Empty<string>(),
            Array.Empty<string>());
        var processing = new AcquisitionRouteTargetDateProcessing(
            routeId,
            reservation,
            "not_evaluated_missing_machine",
            false,
            null,
            "machine_capacity_required",
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
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
                rankingSha256: new string('a', 64),
                capacityBinding: binding);
        Require(request.SupportRequestReady &&
                request.SupportTransitionKind ==
                    "machine_capacity_establishment" &&
                request.MachineSupportIntentId == intentId &&
                request.MachineSupportIntentStage ==
                    MachineSupportIntentStages.CraftSelected &&
                request.AtomicCommitRequest?.MachineSupportIntent is not null,
            "Machine-capacity support request did not atomically bind the intent: " +
            string.Join(",", request.BlockingReasons));

        var service = new ReservationPortfolioLedgerService();
        var commitResult = service.Commit(
            ledger,
            before,
            request.AtomicCommitRequest!,
            "2026-09-27T02:00:00Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "Machine-capacity support commit was rejected: " +
            string.Join(",", commitResult.Errors));
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                commitResult.Ledger!,
                before,
                commitResult,
                new string('b', 64));
        Require(commitReceipt.SupportReservationCommitVerified,
            "Machine-capacity support commit receipt was not verified: " +
            string.Join(",", commitReceipt.BlockingReasons));
        capacityOption.Parameters.Single(parameter => parameter.Name ==
            "machine_capacity_requested_candidate_kind").Value =
            "craft_machine_item";
        var committedAvailability =
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                before,
                [capacityOption],
                includeExecutorCalibrationOptions: true,
                commitmentLedger: commitResult.Ledger);
        var committedRanked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            committedAvailability,
            goalId);
        var committedSupport = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowering,
                committedRanked,
                "(BC)12");
        Require(committedSupport is
                [{ Candidate: { Kind: "craft_machine_item" } }],
            "Committed machine intent did not rebuild the exact craft candidate.");
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.BuildCore(
                request,
                commitReceipt,
                requirement,
                lowering,
                before,
                commitResult.Ledger!,
                committedSupport,
                new string('a', 64),
                new string('b', 64),
                new string('c', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue?.Items is
                    [{ OptionId: "executor.craft_machine_item" }],
            "Machine-capacity craft did not compile through the shared executor: " +
            string.Join(",", compilation.BlockingReasons) + ":" +
            string.Join("|", compilation.ActionQueue?.Items.Select(item =>
                item.Status + ":" +
                string.Join(",", item.BlockingReasons)) ??
                Array.Empty<string>()));

        var after = CraftedMachineCapacitySnapshot(before);
        var execution = MachineCapacityExecutionReceipt(
            compilation,
            before,
            after);
        var transition = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                execution,
                after,
                "run.machine-capacity.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(transition.SupportingTransitionVerified &&
                transition.MachineCapacityTransition is
                {
                    Verified: true,
                    Stage: MachineSupportIntentStages.CraftSelected,
                    ObservedInventoryDelta: 1
                },
            "Machine-capacity craft receipt was not verified: " +
            string.Join(",", transition.BlockingReasons) + ":" +
            JsonSerializer.Serialize(
                transition.MachineCapacityTransition,
                JsonDefaults.Compact));

        var transitionHash = new string('d', 64);
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
            "2026-09-27T02:00:02Z");
        Require(settlementResult.Accepted &&
                settlementResult.Ledger is not null &&
                settlementResult.ReboundMachineSupportIntentId == intentId,
            "Machine-capacity intent marker did not settle: " +
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
        Require(settlementReceipt.ReservationLifecycleVerified &&
                settlementReceipt.ExactSettlementReplayVerified &&
                settlementReceipt.FreshReplanRequired &&
                settlementReceipt.ReboundMachineSupportIntentId == intentId &&
                !settlementReceipt.TerminalReceiptEligible &&
                !settlementReceipt.FormalTrainingAuthorized,
            "Machine-capacity settlement did not close with a fresh replan: " +
            string.Join(",", settlementReceipt.BlockingReasons));

        VerifyMachineCapacityPlacementContinuation(
            goalId,
            requirement,
            lowering,
            reservation,
            processing,
            binding,
            capacityOption,
            after,
            settlementResult.Ledger!);
    }

    private static SnapshotEnvelope AcquisitionMachineCapacitySnapshot()
    {
        const string json = """
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "tile_x":{"value":5,"status":"available"},
            "tile_y":{"value":5,"status":"available"},
            "inventory":{"value":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available"},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available"},
            "machine_placement":{"value":{"projection_status":"complete_all_inventory_machines_across_loaded_persistent_locations","static_projection_fingerprint":"capacity-self-test-layout","rows":[]},"status":"available"},
            "machine_crafting":{"value":{"projection_status":"complete_known_machine_recipe_projection","rows":[{
              "recipe_name":"Keg","times_crafted":2,"output_item_id":"12","output_qualified_item_id":"(BC)12","output_count_per_craft":1,"output_context_tags":["item_machine"],
              "output_machine_data":{"status":"available","additional_consumed_item_count":0,"output_rules":[]},
              "ingredient_rows":[{"required_count":30,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)388","amount":30,"unit_sale_price":2,"total_sale_value":60}]}],
              "potential_loadable_input_count":0,"potential_loadable_inputs":[],
              "output_inventory_acceptance_after_material_consumption":true,"craft_candidate_status":"ready_for_native_personal_crafting_menu"
            }]},"status":"available"}
          },
          "farm":{
            "machines":{"value":[],"status":"available"},
            "material_inventory_graph":{"value":{"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[{"node_id":"player:123","inventory_kind":"player_inventory","supply_state":"available","owner_player_id":123,"actor_use_authorized":true,"slots":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","context_tags":[],"stack":30}]}],"access_points":[],"workbench_links":[],"quantity_rows":[],"physical_inventory_count":1,"access_point_count":0,"deduplicated_access_point_count":0,"default_shared_resource_policy":"deny_without_explicit_authorization"},"status":"available","confidence":1}
          },
          "current_location":{"map":{"value":{"width":20,"height":20},"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"FarmHouse","width":20,"height":20,"notable_tiles":[]},"status":"available"},"route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
          "time":{"time":{"value":600,"status":"available"},"total_days":{"value":1,"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json,
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Machine-capacity snapshot is null.");
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "machine-capacity-save",
                Status = "available"
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = "available"
            },
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-27T02:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SnapshotEnvelope CraftedMachineCapacitySnapshot(
        SnapshotEnvelope before)
    {
        var state = before.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        var player = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            state["player"].GetRawText(),
            JsonDefaults.Options)!;
        player["inventory"] = JsonSerializer.SerializeToElement(new
        {
            value = new object[]
            {
                new
                {
                    slot_index = 4,
                    item_id = "12",
                    qualified_item_id = "(BC)12",
                    stack = 1,
                    quality = 0,
                    maximum_stack_size = 999,
                    is_empty = false
                }
            },
            status = "available"
        });
        player["machine_placement"] = JsonSerializer.SerializeToElement(new
        {
            value = new
            {
                projection_status =
                    "complete_all_inventory_machines_across_loaded_persistent_locations",
                static_projection_fingerprint =
                    "capacity-self-test-placement-layout",
                rows = new[]
                {
                    new
                    {
                        inventory_slot_index = 4,
                        item_id = "12",
                        qualified_item_id = "(BC)12",
                        stack = 1,
                        locations = new[]
                        {
                            new
                            {
                                location_id = "FarmHouse",
                                location_is_current = true,
                                machine_operational_context_valid = true,
                                placement_probe_status =
                                    "native_legal_tiles_available",
                                static_legal_tile_count = 1,
                                static_legal_tile_ranges = new[]
                                {
                                    new
                                    {
                                        y = 5,
                                        start_x = 7,
                                        end_x = 7
                                    }
                                }
                            }
                        }
                    }
                }
            },
            status = "available"
        });
        state["player"] = JsonSerializer.SerializeToElement(player);
        var farm = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            state["farm"].GetRawText(),
            JsonDefaults.Options)!;
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
                            slots = new[]
                            {
                                new
                                {
                                    slot_index = 4,
                                    item_id = "12",
                                    qualified_item_id = "(BC)12",
                                    context_tags = Array.Empty<string>(),
                                    stack = 1
                                }
                            }
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
            GameTick = before.GameTick + 1,
            RealTimestamp = "2026-09-27T02:00:01Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static QueueExecutionReceiptEnvelope MachineCapacityExecutionReceipt(
        AcquisitionRouteDispatchCompilation compilation,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Machine-capacity queue is null.");
        var item = queue.Items.Single();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run.machine-capacity.self-test",
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
            StepResults =
            [
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
                    PrimitiveKind = "craft_machine_item",
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons =
                    [
                        "native_crafting_inventory_transition_observed"
                    ],
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        item,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(new[]
                    {
                        "player.inventory[(BC)12].stack=1"
                    })
                }
            ]
        };
    }
}
