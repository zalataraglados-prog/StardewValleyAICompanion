using System.Text.Json;
using System.Text.Json.Nodes;
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
    public static void RunControlExecution(string outputRoot) => new StageOneCollectionFrontierFixture(outputRoot).RunControlExecution();

    private sealed partial class StageOneCollectionFrontierFixture
    {
        public void RunControlExecution()
        {
            var inputs = PrepareControlExecution();
            var portfolioInputs = AcquisitionRouteExecutionBindingBuilder.PortfolioInputs(inputs);
            var control = RustyKeyRollingSupplyProposalBuilder.Build(portfolioInputs);
            Write(inputs.PortfolioControlSelectionPath!, control);
            Require(control.RollingStepReady && control.SelectedAcquisitionProposal is not null,
                "File-based rolling control must select one executable acquisition: " + string.Join(",", control.BlockingReasons));
            inputs = ChangeInputs(inputs, node => node["route_occurrence_id"] = control.SelectedCandidateId);
            Write(inputs.PortfolioProposalPath, control.SelectedAcquisitionProposal!);
            Write(inputs.PortfolioAdmissionPath, control.SelectedAcquisitionAdmission!);
            var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(inputs.BeforeSnapshotPath, "Control before");
            var ledger = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(inputs.StrategyLedgerPath, "Control ledger");
            var commit = new ReservationPortfolioLedgerService().Commit(ledger, before,
                control.SelectedAcquisitionAdmission!.AtomicCommitRequest!, "2026-11-04T00:00:00Z");
            Require(commit.Accepted && commit.Ledger is not null, "Control atomic commit failed.");
            Write(inputs.PortfolioCommitResultPath, commit);
            Write(inputs.CommittedStrategyLedgerPath, commit.Ledger!);
            Write(inputs.PortfolioCommitReceiptPath, AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
                portfolioInputs, inputs.PortfolioAdmissionPath, inputs.CommittedStrategyLedgerPath, inputs.PortfolioCommitResultPath));
            var ranking = AcquisitionRouteDispatchCompilationBuilder.BuildCurrentControlRanking(
                portfolioInputs, inputs.PortfolioControlSelectionPath!, currentLedgerPath: inputs.CommittedStrategyLedgerPath);
            Write(rankingPath, ranking);
            var compilation = AcquisitionRouteDispatchCompilationBuilder.Build(inputs, rankingPath);
            Write(Path.Combine(root, "control-compilation.json"), compilation);
            Require(compilation.DispatchReady && compilation.ActionQueue is not null,
                "Public control compiler failed: " + string.Join(",", compilation.BlockingReasons));
            var queue = compilation.ActionQueue!;
            Require(queue.SourceModel == AcquisitionRouteControlSelectionVerifier.DispatchSource &&
                !compilation.UsesLearnerRankOrScore && !compilation.FormalTrainingAuthorized,
                "Control dispatch must not impersonate Teacher or learner ranking.");
            Write(inputs.ActionQueuePath, queue);
            var binding = AcquisitionRouteExecutionBindingBuilder.Build(inputs);
            Write(targetDateExecutionBindingPath, binding);
            Require(binding.DispatchBindingReady && binding.PortfolioReservationCommitVerified &&
                binding.PortfolioControlSelection is { Verified: true } && !binding.PortfolioTeacherPreferenceVerified &&
                binding.PortfolioTeacherPreferenceSha256.Length == 0 && !binding.FormalTrainingAuthorized,
                "Control selection must execute without obtaining Teacher authorization: " + string.Join(",", binding.BlockingReasons));
            Require(EqualJson(binding, AcquisitionRouteExecutionBindingBuilder.ReadVerifiedArtifact(inputs, targetDateExecutionBindingPath)),
                "Persisted control binding must independently rebuild.");
            var passed = new List<string> { "public_control_source_rebuild", "atomic_commit_without_teacher", "public_dispatch_without_teacher",
                "control_source_not_teacher", "public_execution_binding_without_teacher", "persisted_binding_independent_rebuild" };
            var currentRankingCli = Path.Combine(root, "control-current-ranking-cli.json");
            RunControlCli(inputs, "build-rusty-key-current-ranking", currentRankingCli,
                "--current-ranking-ledger", inputs.CommittedStrategyLedgerPath);
            Require(EqualJson(ranking, CurrentTeacherFrontierSupport.Read<AvailabilityAwarePolicyPredictionEnvelope>(currentRankingCli, "Current ranking CLI")),
                "Current source ranking CLI must use the same deterministic native intent builder.");
            passed.Add("actual_current_ranking_cli");
            var commitCli = Path.Combine(root, "control-commit-cli.json");
            RunControlCli(inputs, "build-acquisition-route-portfolio-commit-receipt", commitCli);
            Require(EqualJson(CurrentTeacherFrontierSupport.Read<AcquisitionRoutePortfolioCommitReceipt>(commitCli, "Control commit CLI"),
                CurrentTeacherFrontierSupport.Read<AcquisitionRoutePortfolioCommitReceipt>(inputs.PortfolioCommitReceiptPath, "Control commit")),
                "Control commit CLI must preserve the canonical receipt.");
            passed.Add("actual_control_commit_receipt_cli");
            VerifyControlExecutionCli(inputs, Path.Combine(root, "control-cli-binding.json"), binding);
            passed.Add("actual_cli_without_teacher_flags");
            foreach (var field in new[] { "teacher_preference_label_eligible", "formal_training_authorized", "full_threshold_supply_verified" })
            {
                Reject("forged_" + field, () =>
                {
                    var node = JsonSerializer.SerializeToNode(control, JsonDefaults.Options)!;
                    node[field] = true;
                    Write(inputs.PortfolioControlSelectionPath!, node);
                    AcquisitionRouteExecutionBindingBuilder.Build(inputs);
                });
                Write(inputs.PortfolioControlSelectionPath!, control);
            }
            Reject("mixed_teacher_control", () => AcquisitionRouteExecutionBindingBuilder.Build(
                ChangeInputs(inputs, node => node["portfolio_teacher_preference_path"] = "not-a-teacher.json")));
            Reject("wrong_selected_route", () => AcquisitionRouteExecutionBindingBuilder.Build(
                ChangeInputs(inputs, node => node["route_occurrence_id"] = "foreign-route")));
            var changedProposal = JsonSerializer.SerializeToNode(control.SelectedAcquisitionProposal, JsonDefaults.Options)!;
            changedProposal["proposal_id"] = "unselected-proposal";
            Write(inputs.PortfolioProposalPath, changedProposal);
            Reject("caller_proposal_drift", () => AcquisitionRouteExecutionBindingBuilder.Build(inputs));
            Write(inputs.PortfolioProposalPath, control.SelectedAcquisitionProposal!);
            var forged = JsonSerializer.SerializeToNode(binding, JsonDefaults.Options)!;
            forged["portfolio_teacher_preference_verified"] = true;
            Write(targetDateExecutionBindingPath, forged);
            Reject("binding_cannot_promote_control_to_teacher", () =>
                AcquisitionRouteExecutionBindingBuilder.ReadVerifiedArtifact(inputs, targetDateExecutionBindingPath));
            forged = JsonSerializer.SerializeToNode(binding, JsonDefaults.Options)!;
            forged["selected_from_complete_pareto_frontier"] = false;
            Write(targetDateExecutionBindingPath, forged);
            Reject("control_comparison_scope_requires_source_rebuild", () =>
                AcquisitionRouteExecutionBindingBuilder.ReadVerifiedArtifact(inputs, targetDateExecutionBindingPath));
            Write(targetDateExecutionBindingPath, binding);
            VerifyControlNativeReceipt(inputs, binding, queue, before, passed);
            Write(Path.Combine(root, "control-execution-self-test.json"), new
            {
                status = "passed", passed_count = passed.Count, passed, native_game_run = false,
                teacher_preference_label_eligible = false, formal_training_authorized = false
            });
            Console.WriteLine("Control execution self-test passed: " + passed.Count);

            void Reject(string name, Action action)
            {
                var rejected = false;
                try { action(); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Control admission did not reject " + name);
                passed.Add(name);
            }
        }

        private static AcquisitionRouteExecutionBindingInputs ChangeInputs(AcquisitionRouteExecutionBindingInputs inputs, Action<JsonNode> change)
        {
            var node = JsonSerializer.SerializeToNode(inputs, JsonDefaults.Options)!;
            change(node);
            return node.Deserialize<AcquisitionRouteExecutionBindingInputs>(JsonDefaults.Options)!;
        }

        private void VerifyControlNativeReceipt(AcquisitionRouteExecutionBindingInputs inputs, AcquisitionRouteExecutionBinding binding,
            ActionQueueEnvelope queue, SnapshotEnvelope before, List<string> passed)
        {
            Require(queue.Items.Select(item => item.OptionId).SequenceEqual(new[] { "executor.buy_shop_item", "executor.close_menu" }),
                "Control shop fixture must use the shared purchase and menu cleanup queue.");
            _ = WriteTargetDateInventoryAfterSnapshot(inputs.BeforeSnapshotPath, targetDateAfterSnapshotPath, "(O)96", 1, 0);
            var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(targetDateAfterSnapshotPath, "Control after");
            var farm = JsonSerializer.SerializeToNode(after.State["farm"])!;
            farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]!.AsArray().Add(
                JsonSerializer.SerializeToNode(new { slot_index = 10, qualified_item_id = "(O)96", stack = 1, quality = 0 }));
            after.State["farm"] = JsonSerializer.SerializeToElement(farm);
            after.GameTick = before.GameTick + queue.Items.Length;
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            Write(targetDateAfterSnapshotPath, after);
            var receipt = ControlQueueReceipt(queue, before, after.StateHash, binding.SelectedCandidateId);
            Write(targetDateExecutionReceiptPath, receipt);
            var fresh = AcquisitionRouteFreshTerminalReceiptBuilder.Build(inputs, targetDateExecutionBindingPath,
                targetDateExecutionReceiptPath, targetDateAfterSnapshotPath, receipt.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
            var freshPath = Path.Combine(root, "control-fresh-terminal.json");
            Write(freshPath, fresh);
            Require(fresh.FreshTerminalReceiptVerified && fresh.QueueExecutionVerified && !fresh.RouteTrainingEvidenceEligible &&
                !fresh.FormalTrainingAuthorized, "A control native receipt must verify without becoming preference evidence: " + string.Join(",", fresh.BlockingReasons));
            passed.Add("fresh_control_outcome_not_training_preference");
            var request = AcquisitionRoutePortfolioSettlementBuilder.BuildRequest(inputs, targetDateExecutionBindingPath,
                targetDateExecutionReceiptPath, targetDateAfterSnapshotPath, freshPath, receipt.RunId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
            Require(EqualJson(request, AcquisitionRoutePortfolioSettlementBuilder.BuildRequestFromVerifiedArtifacts(inputs,
                targetDateExecutionBindingPath, targetDateExecutionReceiptPath, targetDateAfterSnapshotPath, freshPath,
                receipt.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor)), "Control fast settlement must retain all source verification.");
            passed.Add("verified_artifact_settlement_same_request");
            var committed = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(inputs.CommittedStrategyLedgerPath, "Committed control ledger");
            var result = new ReservationPortfolioLedgerService().SettleCompletedRoute(committed, after, request, "2026-11-04T00:01:00Z");
            Require(result.Accepted && result.OutputCustodyReservationIds is { Length: 1 } &&
                result.Ledger!.MaterialReservations.Single(row => row.ReservationId == result.OutputCustodyReservationIds[0])
                    .OutputCustody!.ConsumerOptionId == "executor.donate_museum_item",
                "Control outcome must use the existing atomic museum custody settlement.");
            passed.Add("control_settlement_retains_exact_donation_custody");
            var requestPath = Path.Combine(root, "control-settlement-request.json");
            var resultPath = Path.Combine(root, "control-settlement-result.json");
            var settledPath = Path.Combine(root, "control-settled-ledger.json");
            Write(requestPath, request);
            Write(resultPath, result);
            Write(settledPath, result.Ledger!);
            var settledReceipt = AcquisitionRoutePortfolioSettlementBuilder.BuildReceipt(inputs, targetDateExecutionBindingPath,
                targetDateExecutionReceiptPath, targetDateAfterSnapshotPath, freshPath, receipt.RunId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, requestPath, resultPath, settledPath);
            Require(settledReceipt.ReservationLifecycleVerified && settledReceipt.ExactSettlementReplayVerified &&
                settledReceipt.FreshReplanRequired && !settledReceipt.FormalTrainingAuthorized,
                "Control settlement must produce the canonical independently replayed receipt.");
            passed.Add("public_settlement_receipt_independent_replay");
            var forgedFresh = JsonSerializer.SerializeToNode(fresh, JsonDefaults.Options)!;
            forgedFresh["route_training_evidence_eligible"] = true;
            Write(freshPath, forgedFresh);
            var rejected = false;
            try
            {
                AcquisitionRoutePortfolioSettlementBuilder.BuildRequest(inputs, targetDateExecutionBindingPath,
                    targetDateExecutionReceiptPath, targetDateAfterSnapshotPath, freshPath, receipt.RunId,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
            }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "A caller cannot promote control outcome to Teacher evidence.");
            passed.Add("forged_fresh_training_flag_rejected");
            Write(freshPath, fresh);
            var invalidAfter = WriteTargetDateInventoryAfterSnapshot(inputs.BeforeSnapshotPath, targetDateInsufficientAfterSnapshotPath, "(O)96", 0, 0);
            var invalidReceipt = ControlQueueReceipt(queue, before, invalidAfter, binding.SelectedCandidateId);
            Write(targetDateExecutionReceiptPath, invalidReceipt);
            var failed = AcquisitionRouteFreshTerminalReceiptBuilder.Build(inputs, targetDateExecutionBindingPath,
                targetDateExecutionReceiptPath, targetDateInsufficientAfterSnapshotPath, receipt.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
            Require(!failed.FreshTerminalReceiptVerified && !failed.RouteTrainingEvidenceEligible &&
                failed.BlockingReasons.Contains("fresh_terminal_transition_not_verified"), "Control must still require the exact native gain.");
            passed.Add("missing_native_gain_rejected");
            Write(targetDateExecutionReceiptPath, receipt);
        }

        private static QueueExecutionReceiptEnvelope ControlQueueReceipt(ActionQueueEnvelope queue, SnapshotEnvelope before,
            string afterHash, string candidateId)
        {
            var receipt = TargetDateQueueReceipt(queue, queue.Items[0], before, afterHash, before.GameTick + queue.Items.Length, candidateId);
            receipt.PlannedItemCount = receipt.ExecutedItemCount = queue.Items.Length;
            receipt.MaxQueueItemAttempts = queue.Items.Length;
            var facts = receipt.StepResults[0].ChangedFacts;
            receipt.StepResults = queue.Items.Select((item, index) => SequentialReceiptStep(queue, item, index,
                index == 0 ? before.StateHash : "synthetic-control-intermediate-" + index,
                index == queue.Items.Length - 1 ? afterHash : "synthetic-control-intermediate-" + (index + 1),
                before.GameTick + index, index == queue.Items.Length - 1, facts)).ToArray();
            return receipt;
        }
    }
}
