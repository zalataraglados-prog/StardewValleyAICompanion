using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunRustyKeyRollingStep(string outputRoot)
    {
        var passed = new List<string>();
        foreach (var shopOpen in new[] { false, true })
            new StageOneCollectionFrontierFixture(Path.Combine(outputRoot, shopOpen ? "terminal" : "support"))
                .VerifyRustyKeyRollingStep(shopOpen, passed);
        var root = Path.GetFullPath(outputRoot);
        var held = new RustyKeyPortfolioFixture();
        held.EnableHeldDonation();
        var inputs = new AcquisitionRoutePortfolioInputs
        {
            RequirementInventoryPath = Path.Combine(root, "inventory.json"), SnapshotPath = Path.Combine(root, "snapshot.json"),
            StrategyLedgerPath = Path.Combine(root, "ledger.json")
        };
        Write(inputs.RequirementInventoryPath, held.Inventory);
        Write(inputs.SnapshotPath, held.Snapshot);
        Write(inputs.StrategyLedgerPath, held.Ledger);
        var controlPath = Path.Combine(root, "held-control.json");
        Write(controlPath, RustyKeyRollingSupplyProposalBuilder.Build(inputs));
        var prepared = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
        Require(prepared.StepReady && prepared.Kind == "held_donation" && prepared.CompiledQueue is not null &&
            prepared.AtomicCommitRequest is null && !prepared.ExecutionAuthorized,
            "Held donation must reuse its original queue without acquisition axes or a speculative resource commit.");
        passed.Add("held_donation_no_acquisition_or_commit");
        Require(EqualJson(prepared, RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath)),
            "Held plan, queue, items and primitive IDs must all rebuild exactly from the same sources.");
        passed.Add("held_compilation_identity_rebuilds_exactly");
        held.ChangeMuseum("museum_mutex_locked", JsonValue.Create(true));
        Write(inputs.SnapshotPath, held.Snapshot);
        Write(controlPath, RustyKeyRollingSupplyProposalBuilder.Build(inputs));
        prepared = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
        Require(!prepared.StepReady && prepared.AtomicCommitRequest is null &&
            prepared.BlockingReasons.Contains("rusty_key_owned_supply_pending_donation_or_transfer"),
            "Pending donation must not yield any speculative acquisition/support commit.");
        passed.Add("pending_donation_prevents_speculative_commit");
        var satisfied = new RustyKeyPortfolioFixture(60);
        Write(inputs.SnapshotPath, satisfied.Snapshot);
        Write(controlPath, RustyKeyRollingSupplyProposalBuilder.Build(inputs));
        var handoff = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
        Require(handoff.Kind == "native_key_handoff" && !handoff.StepReady && handoff.AtomicCommitRequest is null &&
            handoff.CompiledQueue is null, "Threshold satisfaction must hand off, not invent a key or execute extra supply.");
        passed.Add("threshold_requires_native_handoff");
        VerifyRollingCustody(root, passed);
        VerifyRollingCustodyDonationSchedule(root, passed);
        VerifyRollingDayRecovery(root, passed);
        VerifyRollingCustodyNextDay(root, passed);
        Write(Path.Combine(root, "rolling-step-self-test.json"), new
        {
            status = "passed", passed_count = passed.Count, passed, native_game_run = false,
            teacher_preference_label_eligible = false, formal_training_authorized = false
        });
        Console.WriteLine("Rolling step self-test passed: " + passed.Count);
    }

    private sealed partial class StageOneCollectionFrontierFixture
    {
        public void VerifyRustyKeyRollingStep(bool shopOpen, List<string> passed)
        {
            var inputs = PrepareControlExecution(shopOpen);
            Write(Path.Combine(root, "runtime-inputs.json"), inputs);
            var portfolio = AcquisitionRouteExecutionBindingBuilder.PortfolioInputs(inputs);
            var control = RustyKeyRollingSupplyProposalBuilder.Build(portfolio);
            Write(inputs.PortfolioControlSelectionPath!, control);
            var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(inputs.BeforeSnapshotPath, "Step before");
            var ledger = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(inputs.StrategyLedgerPath, "Step ledger");
            var originalLedger = File.ReadAllText(inputs.StrategyLedgerPath);
            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "economy.buy_supplies" }, commitmentLedger: ledger);
            var ranking = CollectionRanking(snapshot.StateHash,
                new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, control.GoalId));
            Write(rankingPath, ranking);
            var prepared = RustyKeyRollingStepPreparationBuilder.Build(portfolio, inputs.PortfolioControlSelectionPath!, rankingPath);
            Write(Path.Combine(root, "preparation.json"), prepared);
            Require(prepared.StepReady && prepared.Kind == (shopOpen ? "terminal_acquisition" : "supporting_transition") &&
                prepared.AtomicCommitRequest is not null && prepared.CompiledQueue is null &&
                prepared.RouteOccurrenceId == control.SelectedCandidateId && !prepared.ExecutionAuthorized &&
                !prepared.TeacherPreferenceLabelEligible && !prepared.FormalTrainingAuthorized,
                "Preparation must choose the shared executable current stage before any commit: " + string.Join(",", prepared.BlockingReasons));
            Require(shopOpen ? prepared.SupportRequest is null : prepared.SupportRequest is { SupportRequestReady: true } &&
                prepared.SupportInputs is { UseControlSelection: true }, "Only preparation may carry the existing support request.");
            passed.Add(shopOpen ? "terminal_preferred_before_commit" : "native_support_selected_before_commit");
            Require(File.ReadAllText(inputs.StrategyLedgerPath) == originalLedger, "Preparation must not mutate the real ledger.");
            passed.Add("read_only_preparation_" + shopOpen);
            var cliPath = Path.Combine(root, "cli-preparation.json");
            RunControlCli(inputs, "prepare-rusty-key-rolling-step", cliPath, "--ranking", rankingPath);
            Require(EqualJson(prepared, CurrentTeacherFrontierSupport.Read<RustyKeyRollingStepPreparation>(cliPath, "CLI preparation")),
                "Actual CLI must select exactly the same stage and immutable request.");
            passed.Add("actual_step_cli_" + shopOpen);
            var noRanking = RustyKeyRollingStepPreparationBuilder.Build(portfolio, inputs.PortfolioControlSelectionPath!);
            Require(!noRanking.StepReady && noRanking.AtomicCommitRequest is null, "No ranking must mean no speculative commit.");
            passed.Add("missing_ranking_no_commit_" + shopOpen);
            var savedCandidates = ranking.RankedEventCandidates;
            ranking.RankedEventCandidates = Array.Empty<PolicyEventCandidatePrediction>();
            Write(rankingPath, ranking);
            var missingCandidates = RustyKeyRollingStepPreparationBuilder.Build(portfolio, inputs.PortfolioControlSelectionPath!, rankingPath);
            Require(!missingCandidates.StepReady && missingCandidates.AtomicCommitRequest is null && missingCandidates.SupportInputs is null &&
                missingCandidates.BlockingReasons.Contains("rebuilt_endpoint_candidate_missing_from_ranking"),
                "A failed candidate denominator check must not be reinterpreted as a reason to execute preparation.");
            passed.Add("missing_current_candidates_no_fallback_" + shopOpen);
            ranking.RankedEventCandidates = savedCandidates;
            ranking.Availability.StateHash = "stale";
            Write(rankingPath, ranking);
            Require(!RustyKeyRollingStepPreparationBuilder.Build(portfolio, inputs.PortfolioControlSelectionPath!, rankingPath).StepReady,
                "Stale ranking must not trigger support fallback.");
            passed.Add("stale_ranking_no_fallback_" + shopOpen);
            var forged = JsonSerializer.SerializeToNode(control, JsonDefaults.Options)!;
            forged["formal_training_authorized"] = true;
            Write(inputs.PortfolioControlSelectionPath!, forged);
            var rejected = false;
            try { RustyKeyRollingStepPreparationBuilder.Build(portfolio, inputs.PortfolioControlSelectionPath!, rankingPath); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Raw control claims must be independently verified even at pre-commit preparation.");
            passed.Add("forged_control_rejected_" + shopOpen);
            Require(control.SelectedAcquisitionDonationSchedule is not null,
                "Public acquisition control must retain the source-bound continuation forecast, including missing evidence.");
            forged = JsonSerializer.SerializeToNode(control, JsonDefaults.Options)!;
            forged["selected_acquisition_donation_schedule"]!["acquisition_completion_by_time"] = 600;
            Write(inputs.PortfolioControlSelectionPath!, forged);
            rejected = false;
            try { RustyKeyRollingStepPreparationBuilder.Build(portfolio, inputs.PortfolioControlSelectionPath!, rankingPath); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Precommit must rebuild the selected acquisition's continuation instead of trusting caller-edited timing.");
            passed.Add("forged_acquisition_donation_schedule_rejected_" + shopOpen);
        }
    }
}
