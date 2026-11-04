using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingCustody(string root, List<string> passed)
    {
        foreach (var remote in new[] { false, true })
        {
            var fixture = new RustyKeyPortfolioFixture();
            var reservation = fixture.EnableStoredCustody(remote);
            var original = JsonSerializer.Serialize(fixture.Ledger);
            var result = fixture.RunRolling(() => throw new InvalidOperationException("Owned custody must not rebuild acquisition axes."));
            var kind = remote ? "custody_navigation" : "custody_transfer";
            Require(result.RollingStepReady && result.SelectedCustodyTransfer is { } selection && selection.Kind == kind &&
                selection.ReservationId == reservation && selection.DestinationNodeId == "player:1" &&
                result.SelectedAcquisitionProposal is null && result.CompleteMuseumItemDenominator.Length == 95 &&
                result.PendingDonationRequirementIds is { Length: 1 } && !result.TeacherPreferenceLabelEligible && !result.FormalTrainingAuthorized,
                "Stored custody must select one original retrieval stage without reacquisition: " + JsonSerializer.Serialize(result.CandidateEvaluations));
            Require(remote ? result.CompiledQueue is not null && result.SelectedCustodyTransfer!.AtomicCommitRequest is null :
                result.CompiledQueue is null && result.SelectedCustodyTransfer!.AtomicCommitRequest is not null,
                "Only the actual transfer may prepare inventory; navigation is one original route queue.");
            Require(original == JsonSerializer.Serialize(fixture.Ledger), "Rolling selection cannot mutate custody.");
            passed.Add(kind + "_read_only_original_control_no_acquisition");
            var directory = Path.Combine(root, kind);
            var inputs = new AcquisitionRoutePortfolioInputs
            {
                RequirementInventoryPath = Path.Combine(directory, "inventory.json"), SnapshotPath = Path.Combine(directory, "snapshot.json"),
                StrategyLedgerPath = Path.Combine(directory, "ledger.json")
            };
            Write(inputs.RequirementInventoryPath, fixture.Inventory);
            Write(inputs.SnapshotPath, fixture.Snapshot);
            Write(inputs.StrategyLedgerPath, fixture.Ledger);
            var control = RustyKeyRollingSupplyProposalBuilder.Build(inputs);
            var controlPath = Path.Combine(directory, "control.json");
            Write(controlPath, control);
            var preparation = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
            Require(preparation.StepReady && preparation.Kind == kind && preparation.SupportInputs is null &&
                !preparation.ExecutionAuthorized && !preparation.FormalTrainingAuthorized,
                "Original step preparation must retain the explicit custody stage without requiring a ranking.");
            passed.Add(kind + "_original_preparation_without_axes_or_ranking");
            var forged = JsonSerializer.SerializeToNode(control, JsonDefaults.Options)!;
            forged["selected_custody_transfer"]!["position_sha256"] = new string('0', 64);
            Write(controlPath, forged);
            var rejected = false;
            try { RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Step preparation must independently reject changed custody position authority.");
            passed.Add(kind + "_tampered_control_rejected");
            Write(controlPath, control);
            if (!remote)
            {
                var selected = control.SelectedCustodyTransfer!;
                var commit = new ReservationPortfolioLedgerService().Commit(fixture.Ledger, fixture.Snapshot,
                    preparation.AtomicCommitRequest!, "rolling-selected");
                var compiled = MuseumOutputCustodyTransferRequestBuilder.Compile(fixture.Snapshot, fixture.Ledger,
                    selected.ReservationId, selected.DestinationNodeId, commit);
                Require(compiled.Queue is not null && compiled.Errors.Length == 0, "Selected atomic request must compile via the original custody builder.");
                passed.Add("rolling_custody_selected_request_compiles_original_queue");
            }
        }
    }
}
