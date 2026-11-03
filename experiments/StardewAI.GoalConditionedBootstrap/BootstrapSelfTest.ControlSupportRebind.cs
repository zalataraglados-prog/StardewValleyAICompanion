using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunControlSupportRebind(string outputRoot) => new StageOneCollectionFrontierFixture(outputRoot).RunControlSupportRebind();

    private sealed partial class StageOneCollectionFrontierFixture
    {
        public void RunControlSupportRebind()
        {
            var template = PrepareControlExecution(shopOpen: false);
            var portfolio = AcquisitionRouteExecutionBindingBuilder.PortfolioInputs(template);
            var control = RustyKeyRollingSupplyProposalBuilder.Build(portfolio);
            Require(control.RollingStepReady && control.SelectedAcquisitionProposal is not null,
                "A current shop preparation must retain a safe rolling route.");
            var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(template.BeforeSnapshotPath, "Control support before");
            var ledger = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(template.StrategyLedgerPath, "Control support ledger");
            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(before, new[] { "economy.buy_supplies" }, commitmentLedger: ledger);
            Write(rankingPath, CollectionRanking(before.StateHash,
                new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, control.GoalId)));
            var support = SupportingTransitionProofInputs(portfolio, rankingPath, control.SelectedCandidateId);
            var supportNode = JsonSerializer.SerializeToNode(support, JsonDefaults.Options)!;
            supportNode["use_control_selection"] = true;
            support = supportNode.Deserialize<AcquisitionRouteSupportingTransitionRequestInputs>(JsonDefaults.Options)!;
            var proof = BuildFileBackedSupportingTransitionProof(Path.Combine(root, "support"), support, ledger, before,
                (compilation, afterPath) =>
                {
                    var after = CloneSnapshot(before);
                    var menus = JsonSerializer.SerializeToNode(after.State["menus"])!;
                    menus["active_menu"]!["value"]!["is_open"] = true;
                    menus["active_menu"]!["value"]!["type"] = "ShopMenu";
                    var locations = JsonSerializer.SerializeToNode(after.State["locations"])!;
                    menus["shop_stock"] = JsonSerializer.SerializeToNode(NativeField(
                        locations["shops"]!["value"]!["shops"]![0]!["stock_preview"]!, "test"));
                    after.State["menus"] = JsonSerializer.SerializeToElement(menus);
                    after.GameTick++;
                    after.StateHash = SnapshotHash.ComputeStateHash(after.State);
                    Write(afterPath, after);
                    return new FileBackedSupportingTransitionExecution(after,
                        ControlQueueReceipt(compilation.ActionQueue!, before, after.StateHash, compilation.SelectedCandidateId));
                });
            Require(proof.Compilation.ActionQueue!.SourceModel == AcquisitionRouteControlSelectionVerifier.DispatchSource &&
                !proof.Compilation.TerminalReceiptEligible && proof.Compilation.FreshReplanRequiredAfterSuccess,
                "A native preparation is control-only, not a terminal outcome or Teacher preference.");
            var nextRoot = Path.Combine(root, "fresh");
            Directory.CreateDirectory(nextRoot);
            var freshInputs = BuildTargetDatePortfolioInputs(template, proof.AfterSnapshotPath, proof.SettledLedgerPath,
                Path.Combine(nextRoot, "proposal.json"), nextRoot, 0);
            var replan = AcquisitionRouteSupportingTransitionReplanBuilder.Build(support, proof.Proof, freshInputs);
            var replanPath = Path.Combine(nextRoot, "replan.json");
            Write(replanPath, replan);
            var proofPath = Path.Combine(nextRoot, "support-proof.json");
            Write(proofPath, new AcquisitionRoutePortfolioSupportingTransitionInitialProof
            {
                RequestInputs = support, SettlementProof = proof.Proof, ReplanAdmissionPath = replanPath
            });
            var rebound = RustyKeyRollingSupplyProposalBuilder.BuildAfterSupportingTransition(freshInputs, proofPath);
            Require(rebound.RollingStepReady && rebound.SelectedAcquisitionProposal!.PriorSupportingTransitionReplanSha256 == HashFile(replanPath) &&
                !rebound.TeacherPreferenceLabelEligible && !rebound.FormalTrainingAuthorized,
                "Rebound rolling selection must carry independently verified support lineage without Teacher labels.");
            var controlPath = Path.Combine(nextRoot, "control.json");
            var admissionPath = Path.Combine(nextRoot, "admission.json");
            Write(controlPath, rebound);
            Write(freshInputs.ProposalPath, rebound.SelectedAcquisitionProposal!);
            Write(admissionPath, rebound.SelectedAcquisitionAdmission!);
            var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(proof.AfterSnapshotPath, "After support");
            var settled = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(proof.SettledLedgerPath, "Settled support ledger");
            var preparationRankingPath = Path.Combine(nextRoot, "pre-commit-ranking.json");
            var preparationAvailability = new CandidateOptionAvailabilityEvaluator().Evaluate(after,
                new[] { "economy.buy_supplies" }, commitmentLedger: settled);
            Write(preparationRankingPath, CollectionRanking(after.StateHash,
                new EventCandidateRanker().Rank(new BaselineTrainingReport(), preparationAvailability, control.GoalId)));
            var prepared = RustyKeyRollingStepPreparationBuilder.Build(freshInputs, controlPath, preparationRankingPath, proofPath);
            Require(prepared.StepReady && prepared.Kind == "terminal_acquisition" &&
                EqualJson(prepared.AtomicCommitRequest!, rebound.SelectedAcquisitionAdmission!.AtomicCommitRequest!) &&
                !prepared.ExecutionAuthorized, "Fresh support lineage must select the terminal stage before its real commit.");
            var commit = new ReservationPortfolioLedgerService().Commit(settled, after,
                rebound.SelectedAcquisitionAdmission!.AtomicCommitRequest!, "2026-11-04T00:02:00Z");
            Require(commit.Accepted && commit.Ledger is not null, "Rebound control atomic commit failed: " + string.Join(",", commit.Errors));
            var commitPath = Path.Combine(nextRoot, "commit.json");
            var committedPath = Path.Combine(nextRoot, "committed-ledger.json");
            var commitReceiptPath = Path.Combine(nextRoot, "commit-receipt.json");
            Write(commitPath, commit);
            Write(committedPath, commit.Ledger!);
            var receipt = AcquisitionRoutePortfolioCommitReceiptBuilder.BuildAfterSupportingTransition(freshInputs,
                HashFile(replanPath), admissionPath, committedPath, commitPath);
            Write(commitReceiptPath, receipt);
            var queuePath = Path.Combine(nextRoot, "queue.json");
            var inputs = SupportingTransitionTerminalExecutionInputs(freshInputs, "", "", admissionPath,
                commitReceiptPath, committedPath, commitPath, queuePath, rebound.SelectedCandidateId);
            inputs = ChangeInputs(inputs, node =>
            {
                node["portfolio_control_selection_path"] = controlPath;
                node["control_supporting_transition_proof_path"] = proofPath;
            });
            var freshRankingPath = Path.Combine(nextRoot, "ranking.json");
            var freshAvailability = new CandidateOptionAvailabilityEvaluator().Evaluate(after, new[] { "economy.buy_supplies" }, commitmentLedger: commit.Ledger);
            Write(freshRankingPath, CollectionRanking(after.StateHash,
                new EventCandidateRanker().Rank(new BaselineTrainingReport(), freshAvailability, control.GoalId)));
            var compiled = AcquisitionRouteDispatchCompilationBuilder.Build(inputs, freshRankingPath);
            Require(compiled.DispatchReady && compiled.TerminalReceiptEligible, "Rebound control terminal compilation failed: " + string.Join(",", compiled.BlockingReasons));
            Write(queuePath, compiled.ActionQueue!);
            var binding = AcquisitionRouteExecutionBindingBuilder.Build(inputs);
            Require(binding.DispatchBindingReady && !binding.PortfolioTeacherPreferenceVerified &&
                binding.PriorSupportingTransitionReplanSha256 == HashFile(replanPath),
                "Control binding lost support lineage or obtained a Teacher claim.");
            Write(targetDateExecutionBindingPath, binding);
            VerifyControlExecutionCli(inputs, Path.Combine(nextRoot, "cli-binding.json"), binding);
            VerifyControlSupplyCli(inputs, Path.Combine(nextRoot, "cli-control.json"), rebound);
            var passed = new List<string> { "shared_native_preparation", "fresh_axis_rebuild", "independent_support_lineage",
                "atomic_terminal_commit", "control_terminal_compiler", "control_terminal_binding", "actual_rebound_cli" };
            Require(EqualJson(binding, AcquisitionRouteExecutionBindingBuilder.BuildAfterSupportingTransition(
                support, proof.Proof, replanPath, inputs)), "The existing support-rebind entrypoint must reuse the same control binding.");
            passed.Add("shared_support_rebind_entrypoint");
            VerifyControlNativeReceipt(inputs, binding, compiled.ActionQueue!, after, passed);
            var tampered = JsonSerializer.SerializeToNode(replan, JsonDefaults.Options)!;
            tampered["prior_queue_invalidated"] = false;
            Write(replanPath, tampered);
            var rejected = false;
            try { AcquisitionRouteExecutionBindingBuilder.Build(inputs); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "A stale prior queue cannot authorize a control continuation.");
            passed.Add("tampered_replan_rejected");
            Write(replanPath, replan);
            rejected = false;
            try { AcquisitionRouteExecutionBindingBuilder.Build(ChangeInputs(inputs, node => node["control_supporting_transition_proof_path"] = null)); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Lineage cannot survive removal of its retained independent proof.");
            passed.Add("missing_support_proof_rejected");
            var native = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(
                proof.Proof.ExecutionReceiptPath, "Original native support receipt");
            var alteredNative = JsonSerializer.SerializeToNode(native, JsonDefaults.Options)!;
            alteredNative["success"] = false;
            Write(proof.Proof.ExecutionReceiptPath, alteredNative);
            rejected = false;
            try { AcquisitionRouteExecutionBindingBuilder.Build(inputs); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "A warm control binding cache cannot hide a changed native support receipt.");
            passed.Add("warm_cache_native_receipt_tampering_rejected");
            Write(proof.Proof.ExecutionReceiptPath, native);
            Require(AcquisitionRouteExecutionBindingBuilder.Build(inputs).DispatchBindingReady,
                "Restoring the exact native proof must restore the same valid admission.");
            passed.Add("restored_proof_rebuilds_original_binding");
            Write(Path.Combine(root, "control-support-self-test.json"), new
            {
                status = "passed", passed_count = passed.Count, passed, native_game_run = false,
                teacher_preference_label_eligible = false, formal_training_authorized = false
            });
            Console.WriteLine("Control support rebind self-test passed: " + passed.Count);
        }
    }
}
