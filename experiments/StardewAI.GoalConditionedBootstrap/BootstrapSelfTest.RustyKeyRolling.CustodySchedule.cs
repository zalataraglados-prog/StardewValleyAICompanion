using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingCustodyDonationSchedule(string root, List<string> passed)
    {
        foreach (var mode in new[] { "local", "remote", "later_gate_closed", "deferred", "missing_calibration", "missing_date_evidence" })
        {
            var fixture = new RustyKeyPortfolioFixture();
            var remote = mode != "local";
            var reservation = fixture.EnableStoredCustody(remote);
            ConfigureRollingDonationRouteSchedule(fixture, mode);
            var locations = JsonSerializer.SerializeToNode(fixture.Snapshot.State["locations"])!;
            if (!remote)
            {
                var player = JsonSerializer.SerializeToNode(fixture.Snapshot.State["player"])!;
                player["location_id"]!["value"] = "ArchaeologyHouse";
                player["tile_x"]!["value"] = 8;
                player["tile_y"]!["value"] = 9;
                fixture.Snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
                locations["collision_grid"]!["value"]!["location_id"] = "ArchaeologyHouse";
                locations["route_connectors"]!["value"]!["location_id"] = "ArchaeologyHouse";
                locations["route_connectors"]!["value"]!["connectors"] = new JsonArray();
                fixture.ChangeMuseum("museum_is_current_location", JsonValue.Create(true));
            }
            if (mode == "missing_date_evidence") locations.AsObject().Remove("social_route_date_evidence");
            fixture.Snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
            fixture.Snapshot.State["current_location"] = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["map"] = locations["collision_grid"]!.DeepClone()
            });
            fixture.Rehash();
            var directory = Path.Combine(root, "custody-donation-" + mode);
            var inputs = new AcquisitionRoutePortfolioInputs
            {
                RequirementInventoryPath = Path.Combine(directory, "inventory.json"),
                SnapshotPath = Path.Combine(directory, "snapshot.json"), StrategyLedgerPath = Path.Combine(directory, "ledger.json"),
                RouteTimingCalibrationPath = mode == "missing_calibration" ? "" : Path.Combine(directory, "timing.json")
            };
            Write(inputs.RequirementInventoryPath, fixture.Inventory);
            Write(inputs.SnapshotPath, fixture.Snapshot);
            Write(inputs.StrategyLedgerPath, fixture.Ledger);
            if (inputs.RouteTimingCalibrationPath.Length > 0) Write(inputs.RouteTimingCalibrationPath, StageOneRouteTimingCalibration(0));
            var before = JsonSerializer.Serialize(fixture.Snapshot, JsonDefaults.Options);
            var ledgerBefore = JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options);
            var control = RustyKeyRollingSupplyProposalBuilder.Build(inputs);
            var forecast = control.SelectedCustodyDonationSchedule;
            Require(control.RollingStepReady && control.SelectedCustodyTransfer?.ReservationId == reservation &&
                control.SelectedAcquisitionProposal is null && forecast is not null && !forecast.JointExecutionScheduleVerified &&
                forecast.PositionSha256 == control.SelectedCustodyTransfer.PositionSha256 &&
                forecast.DestinationNodeId == "player:1" && !control.FormalTrainingAuthorized &&
                control.CompleteMuseumItemDenominator.Length == 95 &&
                before == JsonSerializer.Serialize(fixture.Snapshot, JsonDefaults.Options) &&
                ledgerBefore == JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options),
                "The custody forecast must bind the original owned position and leave stock/admission unchanged: " + mode);
            var ready = mode is "local" or "remote" or "deferred";
            Require(forecast!.CurrentDayCustodyDonationRouteReady == ready,
                "Full custody timing did not match the retained source evidence: " + mode + ":" + string.Join(",", forecast.BlockingReasons));
            if (ready)
            {
                var route = forecast.RouteSchedule!;
                Require(route.RequiresFreshTransferPreflight && !route.NativeWithdrawalVerified &&
                    route.DonationRouteSchedule!.DepartureTime == route.WithdrawalCompletionByTime &&
                    StardewAI.Core.Execution.GameClockBudgetPolicy.ClockMinutesBetween(route.ChestArrivalByTime!.Value,
                        route.WithdrawalCompletionByTime!.Value) == 8,
                    "The complete native interaction budget must follow chest arrival, before donation travel.");
                if (mode == "deferred") Require(control.CompiledQueue!.Items.Length == 1 &&
                    control.CompiledQueue.Items[0].OptionId == "executor.wait_ticks" &&
                    route.ChestApproachScenario!.SegmentEvidence![0].OpenTime == 1100,
                    "A full forecast cannot turn the original single bounded wait into a multi-map queue.");
            }
            else Require(forecast.BlockingReasons.Any(reason => reason.StartsWith(mode switch
            {
                "later_gate_closed" => "future_route_connector_closed_before_arrival",
                "missing_calibration" => "future_route_timing_calibration_input_invalid",
                _ => "social_route_date_evidence_unavailable"
            }, StringComparison.Ordinal)), "Missing full timing must remain explicit without reacquisition.");
            var controlPath = Path.Combine(directory, "control.json");
            Write(controlPath, control);
            var preparation = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
            Require(preparation.StepReady && preparation.Kind == (remote ? "custody_navigation" : "custody_transfer") &&
                (remote ? preparation.CompiledQueue is not null && preparation.AtomicCommitRequest is null :
                    preparation.CompiledQueue is null && preparation.AtomicCommitRequest is not null) && !preparation.ExecutionAuthorized,
                "Original preparation must retain the immediate authorized stage, not execute the forecast.");
            var forged = JsonSerializer.SerializeToNode(control, JsonDefaults.Options)!;
            forged["selected_custody_donation_schedule"]!["position_sha256"] = new string('0', 64);
            Write(controlPath, forged);
            var rejected = false;
            try { RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Whole-control rebuilding must reject caller-edited custody forecast identity.");
            passed.Add("custody_donation_schedule_" + mode);
        }
    }
}
