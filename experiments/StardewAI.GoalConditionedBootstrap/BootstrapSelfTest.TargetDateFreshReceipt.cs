using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyTargetDateFreshTerminalReceipt(
        AcquisitionRouteExecutionBindingInputs inputs,
        string bindingPath,
        string afterSnapshotPath,
        string executionReceiptPath,
        string insufficientAfterSnapshotPath)
    {
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            inputs.TargetDateOpportunityCostPath,
            "Target-date opportunity cost");
        var selected = opportunity.Routes.Single(route => string.Equals(
            route.RouteOccurrenceId,
            inputs.RouteOccurrenceId,
            StringComparison.Ordinal));
        var requirement = TargetDateRequirementRoute(selected);
        var selectedCandidateId =
            AcquisitionRouteExecutionBindingBuilder.SelectedCandidateId(
                selected.RouteOccurrenceId);
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            inputs.AcquisitionLoweringPath,
            "Acquisition route lowering");
        var lowered = lowering.RequirementSets.Single(set => string.Equals(
                set.RequirementSetId,
                requirement.RequirementSetId,
                StringComparison.Ordinal))
            .Groups.Single(group => string.Equals(
                group.RequirementId,
                requirement.RequirementId,
                StringComparison.Ordinal))
            .Alternatives[requirement.AlternativeIndex]
            .Routes[requirement.RouteIndex];
        var optionId = lowered.EndpointOptionIds.First();
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            inputs.BeforeSnapshotPath,
            "Target-date before snapshot");
        var portfolioReceipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioCommitReceipt>(
            inputs.PortfolioCommitReceiptPath,
            "Target-date portfolio commit receipt");
        var actor = ExecutionTargetProfiles.CreateActor(
            ExecutionTargetProfiles.TrainingSingleplayer);
        var queueItem = new ActionQueueItem
        {
            QueueItemId = "queue-item.target-date-shop.0",
            SourceActionId = "action.target-date-shop.0",
            OptionId = optionId,
            Status = "pending",
            PermissionRequired = "executor",
            BehaviorCategory = "mechanical",
            CompilerResponsibility = "deterministic",
            TrainingRole = "strategy_value",
            NormalizedCommand = new NormalizedCommand
            {
                CommandType = "option_request",
                OptionId = optionId,
                BehaviorCategory = "mechanical",
                CompilerResponsibility = "deterministic",
                TrainingRole = "strategy_value",
                StateHash = before.StateHash,
                ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
                Actor = actor,
                Parameters = AcquisitionRouteExecutionBindingBuilder
                    .RouteBindingParameters(
                        requirement,
                        portfolioReceipt.PortfolioId,
                        portfolioReceipt.CommittedLedgerRevision),
                Steps = new[]
                {
                    new CompiledActionStep
                    {
                        StepId = "primitive.target-date-shop.0",
                        StepType = "fixture_native_shop_purchase",
                        Target = requirement.QualifiedItemId,
                        ExpectedEffect = "exact_inventory_quantity_increase",
                        EstimatedTicks = 1
                    }
                }
            }
        };
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.target-date-shop",
            SourceModelOutputId = "teacher.target-date-shop",
            SourceModel = "deterministic_teacher.fixture",
            StateHash = before.StateHash,
            GoalId = opportunity.GoalId,
            ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
            Actor = actor,
            Status = "pending",
            Items = new[] { queueItem },
            CandidateAudit = new[]
            {
                new SmallModelPlanCandidateAudit
                {
                    CandidateId = selectedCandidateId,
                    Kind = "target_date_acquisition_route",
                    Decision = "accepted"
                }
            }
        };
        Write(inputs.ActionQueuePath, queue);

        var binding = AcquisitionRouteExecutionBindingBuilder.Build(inputs);
        Write(bindingPath, binding);
        Require(binding.Status == "ready_for_exact_route_dispatch" &&
                binding.DispatchBindingReady &&
                binding.SelectedFromCompleteParetoFrontier &&
                binding.QueueOptionsBoundToRoute &&
                binding.PortfolioReservationCommitVerified &&
                binding.PortfolioTeacherPreferenceVerified &&
                !binding.FormalTrainingAuthorized &&
                binding.BlockingReasons.Length == 0 &&
                binding.RouteOccurrenceId == inputs.RouteOccurrenceId &&
                binding.ReservationPortfolioId ==
                    portfolioReceipt.PortfolioId &&
                binding.CommittedStrategyLedgerRevision ==
                    portfolioReceipt.CommittedLedgerRevision &&
                binding.PrimitiveOptionIds.SequenceEqual(
                    new[] { optionId },
                    StringComparer.Ordinal) &&
                binding.TerminalReceiptKind ==
                    "exact_player_inventory_quantity_increase",
            "Target-date route execution binding drifted.");
        Require(JsonSerializer.Serialize(binding, JsonDefaults.Options) ==
                JsonSerializer.Serialize(
                    AcquisitionRouteExecutionBindingBuilder.Build(inputs),
                    JsonDefaults.Options),
            "Target-date route execution binding is not deterministic.");

        var routeParameters = queueItem.NormalizedCommand.Parameters;
        queueItem.NormalizedCommand.Parameters = routeParameters
            .Where(parameter => parameter.Name != "acquisition_source_id")
            .ToArray();
        Write(inputs.ActionQueuePath, queue);
        var incompleteIdentity =
            AcquisitionRouteExecutionBindingBuilder.Build(inputs);
        Require(!incompleteIdentity.DispatchBindingReady &&
                incompleteIdentity.BlockingReasons.Contains(
                    "route_queue_command_binding_invalid",
                    StringComparer.Ordinal),
            "Queue missing exact route identity was not rejected.");
        queueItem.NormalizedCommand.Parameters = routeParameters;
        Write(inputs.ActionQueuePath, queue);

        queueItem.NormalizedCommand.Parameters = routeParameters
            .Where(parameter => parameter.Name !=
                "acquisition_reservation_portfolio_id")
            .ToArray();
        Write(inputs.ActionQueuePath, queue);
        var missingPortfolioOwnership =
            AcquisitionRouteExecutionBindingBuilder.Build(inputs);
        Require(!missingPortfolioOwnership.DispatchBindingReady &&
                missingPortfolioOwnership.BlockingReasons.Contains(
                    "route_queue_command_binding_invalid",
                    StringComparer.Ordinal),
            "Queue missing committed portfolio ownership was not rejected.");
        queueItem.NormalizedCommand.Parameters = routeParameters;
        Write(inputs.ActionQueuePath, queue);

        var afterStateHash = "target-date-route-after-state";
        WriteTargetDateInventoryAfterSnapshot(
            inputs.BeforeSnapshotPath,
            afterSnapshotPath,
            afterStateHash,
            requirement.QualifiedItemId,
            requirement.RequiredAmount,
            requirement.MinimumQuality);
        Write(
            executionReceiptPath,
            TargetDateQueueReceipt(
                queue,
                queueItem,
                before,
                afterStateHash,
                before.GameTick + 1,
                selectedCandidateId));
        const string runId = "run.target-date-shop";
        var receiptNode = JsonNode.Parse(
            File.ReadAllText(executionReceiptPath))!.AsObject();
        receiptNode["run_id"] = runId;
        File.WriteAllText(
            executionReceiptPath,
            receiptNode.ToJsonString(JsonDefaults.Options));
        var admitted = AcquisitionRouteFreshTerminalReceiptBuilder.Build(
            inputs,
            bindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            runId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Write(
            Path.Combine(
                Path.GetDirectoryName(bindingPath)!,
                "target-date-fresh-terminal-receipt.json"),
            admitted);
        Require(admitted.Status == "verified_fresh_terminal_receipt" &&
                admitted.QueueExecutionVerified &&
                admitted.FreshTerminalReceiptVerified &&
                admitted.RouteTrainingEvidenceEligible &&
                !admitted.FormalTrainingAuthorized &&
                admitted.BlockingReasons.Length == 0 &&
                admitted.TerminalTransition is
                {
                    Resolved: true,
                    Verified: true,
                    QuantityIncrease: not null
                } &&
                admitted.TerminalTransition.QuantityIncrease ==
                    requirement.RequiredAmount,
            "Target-date fresh terminal receipt was not admitted.");

        var insufficientStateHash =
            "target-date-route-insufficient-after-state";
        WriteTargetDateInventoryAfterSnapshot(
            inputs.BeforeSnapshotPath,
            insufficientAfterSnapshotPath,
            insufficientStateHash,
            requirement.QualifiedItemId,
            requirement.RequiredAmount - 1,
            requirement.MinimumQuality);
        var insufficientReceiptPath =
            insufficientAfterSnapshotPath + ".receipt.json";
        Write(
            insufficientReceiptPath,
            TargetDateQueueReceipt(
                queue,
                queueItem,
                before,
                insufficientStateHash,
                before.GameTick + 1,
                selectedCandidateId,
                runId));
        var insufficient = AcquisitionRouteFreshTerminalReceiptBuilder.Build(
            inputs,
            bindingPath,
            insufficientReceiptPath,
            insufficientAfterSnapshotPath,
            runId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(insufficient.Status == "blocked_fresh_terminal_receipt" &&
                insufficient.QueueExecutionVerified &&
                !insufficient.FreshTerminalReceiptVerified &&
                !insufficient.RouteTrainingEvidenceEligible &&
                !insufficient.FormalTrainingAuthorized &&
                insufficient.TerminalTransition is
                {
                    Resolved: true,
                    Verified: false
                } &&
                insufficient.BlockingReasons.Contains(
                    "fresh_terminal_transition_not_verified",
                    StringComparer.Ordinal),
            "Partial target-date quantity increase was not rejected.");
        VerifyExactCommunityCenterPaymentReceipt(inputs.BeforeSnapshotPath);
    }

    private static void VerifyExactCommunityCenterPaymentReceipt(
        string snapshotPath)
    {
        var beforeNode = JsonNode.Parse(
            File.ReadAllText(snapshotPath))!.AsObject();
        beforeNode["state"]!["player"]!["money"]!["value"] = 5000;
        beforeNode["state"]!["world_progress"]!["community_center"] =
            JsonSerializer.SerializeToNode(new
            {
                status = "available",
                value = new
                {
                    bundle_rows = new[]
                    {
                        new
                        {
                            bundle_data_key = "Vault/0",
                            ingredients = new[]
                            {
                                new
                                {
                                    ingredient_index = 0,
                                    completed = false
                                }
                            }
                        }
                    }
                }
            }, JsonDefaults.Options);
        var afterNode = JsonNode.Parse(
            beforeNode.ToJsonString(JsonDefaults.Options))!.AsObject();
        afterNode["state"]!["player"]!["money"]!["value"] = 2500;
        afterNode["state"]!["world_progress"]!["community_center"]![
            "value"]!["bundle_rows"]![0]!["ingredients"]![0]![
            "completed"] = true;
        var before = JsonSerializer.Deserialize<SnapshotEnvelope>(
            beforeNode.ToJsonString(JsonDefaults.Options),
            JsonDefaults.Options)!;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(
            afterNode.ToJsonString(JsonDefaults.Options),
            JsonDefaults.Options)!;
        var exact = ExactCommunityCenterPaymentReceiptVerifier.Verify(
            before,
            after,
            "community_center:bundle:Vault/0",
            0,
            2500);
        Require(exact.Resolved && exact.Verified &&
                exact.MoneyDecrease == 2500 &&
                exact.BeforeNativeCompletion == false &&
                exact.AfterNativeCompletion == true,
            "Exact community-center payment receipt was not verified.");

        afterNode["state"]!["player"]!["money"]!["value"] = 2501;
        var underpaidAfter = JsonSerializer.Deserialize<SnapshotEnvelope>(
            afterNode.ToJsonString(JsonDefaults.Options),
            JsonDefaults.Options)!;
        var underpaid = ExactCommunityCenterPaymentReceiptVerifier.Verify(
            before,
            underpaidAfter,
            "community_center:bundle:Vault/0",
            0,
            2500);
        Require(underpaid.Resolved && !underpaid.Verified &&
                underpaid.MoneyDecrease == 2499,
            "Inexact community-center money decrease was not rejected.");
    }

}
