using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingCustodyNextDay(string root, List<string> passed)
    {
        foreach (var mode in new[] { "fresh", "stale_date", "missing_stock", "broken_lineage" })
        {
            var fixture = new RustyKeyPortfolioFixture();
            var reservationId = fixture.EnableStoredCustody(remote: true);
            ConfigureRollingDonationRouteSchedule(fixture, "ready");
            var custody = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == reservationId);
            Require(MaterialOutputCustodyContract.TryGetPositionBinding(custody, fixture.Ledger, out var position),
                "The synthetic day-zero storage must have its original complete position history.");
            var acquiredReceipt = custody.OutputCustody!.FreshTerminalReceiptSha256;
            var positionState = custody.SourceStateHash;
            var oldSnapshotState = fixture.Snapshot.StateHash;
            var time = JsonSerializer.SerializeToNode(fixture.Snapshot.State["time"])!;
            time["total_days"]!["value"] = 1;
            fixture.Snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
            var locations = JsonSerializer.SerializeToNode(fixture.Snapshot.State["locations"])!;
            if (mode != "stale_date") locations["social_route_date_evidence"]!["value"]!["capture_total_days"] = 1;
            fixture.Snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
            if (mode == "missing_stock")
            {
                var farm = JsonSerializer.SerializeToNode(fixture.Snapshot.State["farm"])!;
                farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![1]!["slots"] = new JsonArray();
                fixture.Snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
            }
            if (mode == "broken_lineage") custody.OutputCustody.InitialPosition!.BindingSha256 = new string('f', 64);
            fixture.Rehash();
            var directory = Path.Combine(root, "custody-next-day-" + mode);
            var inputs = new AcquisitionRoutePortfolioInputs
            {
                RequirementInventoryPath = Path.Combine(directory, "inventory.json"), SnapshotPath = Path.Combine(directory, "snapshot.json"),
                StrategyLedgerPath = Path.Combine(directory, "ledger.json"), RouteTimingCalibrationPath = Path.Combine(directory, "timing.json")
            };
            Write(inputs.RequirementInventoryPath, fixture.Inventory);
            Write(inputs.SnapshotPath, fixture.Snapshot);
            Write(inputs.StrategyLedgerPath, fixture.Ledger);
            Write(inputs.RouteTimingCalibrationPath, StageOneRouteTimingCalibration(1));
            var ledgerBytes = File.ReadAllBytes(inputs.StrategyLedgerPath);
            var control = RustyKeyRollingSupplyProposalBuilder.Build(inputs);
            if (mode != "broken_lineage")
            {
                var probed = fixture.RunRolling(() => throw new InvalidOperationException(
                    "Verified owned stock or an unreadable live slot must not fall through to duplicate acquisition."), inputs.RouteTimingCalibrationPath);
                Require(probed.RollingStepReady == control.RollingStepReady && probed.SelectedAcquisitionProposal is null,
                    "The owned-supply branch must not depend on missing acquisition inputs to avoid reacquisition: " + mode);
            }
            else
            {
                var refused = MuseumOutputCustodyTransferRequestBuilder.Build(fixture.Snapshot, fixture.Ledger, reservationId, "player:1");
                Require(refused.Request is null && refused.Errors.Contains("museum_custody_transfer_source_not_authorized"),
                    "Broken lineage must not authorize this custody transfer; unrelated legal supply policy is unchanged.");
            }
            var controlPath = Path.Combine(directory, "control.json");
            Write(controlPath, control);
            var prepared = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
            var ready = mode is "fresh" or "stale_date";
            Require(control.RollingStepReady == ready && prepared.StepReady == ready && control.SourceTotalDay == 1 &&
                control.SelectedAcquisitionProposal is null && !control.ThresholdSatisfied && !control.FormalTrainingAuthorized &&
                control.CompleteMuseumItemDenominator.Length == 95 && File.ReadAllBytes(inputs.StrategyLedgerPath).SequenceEqual(ledgerBytes),
                "A new day must re-observe stock/authority without rewriting custody: " + mode);
            if (ready)
            {
                var reloaded = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(inputs.StrategyLedgerPath, "Retained next-day custody");
                var row = reloaded.MaterialReservations.Single(value => value.ReservationId == reservationId);
                Require(MaterialOutputCustodyContract.TryGetPositionBinding(row, reloaded, out var retainedPosition) && retainedPosition == position &&
                    row.SourceStateHash == positionState && row.OutputCustody!.FreshTerminalReceiptSha256 == acquiredReceipt &&
                    !MaterialOutputCustodyContract.IsAuthorizedForConsumption(row, reloaded, MaterialOutputCustodyContract.MuseumConsumer) &&
                    prepared is { Kind: "custody_navigation", AtomicCommitRequest: null, CompiledQueue: not null } &&
                    control.SelectedCustodyTransfer?.PositionSha256 == position &&
                    control.SelectedCustodyDonationSchedule?.CurrentDayCustodyDonationRouteReady == (mode == "fresh") &&
                    fixture.Snapshot.StateHash != oldSnapshotState,
                    "Fresh date evidence may update the forecast, not the stored unit's acquisition/position origin: " + mode);
                if (mode == "stale_date") Require(control.SelectedCustodyDonationSchedule!.BlockingReasons.Length > 0,
                    "Yesterday's map-date proof cannot become today's full-route certificate.");
            }
            else Require(control.SelectedCustodyTransfer is null && prepared.CompiledQueue is null && prepared.AtomicCommitRequest is null,
                "Missing stock or broken original custody history must not produce a transfer/navigation queue.");
            passed.Add("custody_next_day_" + mode);
        }
    }
}
