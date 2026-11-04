using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingAcquisitionDonationSchedule(string root, List<string> passed)
    {
        var calibrationPath = Path.Combine(root, "acquisition-donation-timing.json");
        Write(calibrationPath, StageOneRouteTimingCalibration(0));
        foreach (var mode in new[] { "ready", "terminal_sequence", "late", "missing_endpoint", "clock_mismatch", "missing_calibration", "endpoint_mismatch" })
        {
            var fixture = new RustyKeyPortfolioFixture();
            var originalFarm = fixture.Snapshot.State["farm"];
            fixture.EnableHeldDonation();
            ConfigureRollingDonationRouteSchedule(fixture, "ready");
            fixture.Snapshot.State["farm"] = originalFarm;
            var player = JsonSerializer.SerializeToNode(fixture.Snapshot.State["player"])!;
            player["inventory"]!["value"] = new JsonArray();
            fixture.Snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
            fixture.ChangeMuseum("donation_candidates", new JsonArray());
            var sequential = mode is "terminal_sequence" or "endpoint_mismatch";
            var x = sequential ? 6 : 5;
            fixture.ChangeRoute(59, route =>
            {
                var daily = route["upstream_route"]!;
                daily["daily_time_energy_axis_resolved"] = true;
                daily["daily_time_energy_matches_target_date"] = true;
                daily["evaluation"] = JsonSerializer.SerializeToNode(new
                {
                    target_location_id = "Road",
                    target_tile_x = x,
                    target_tile_y = 11,
                    stand_tile_x = mode == "missing_endpoint" ? (int?)null : x,
                    stand_tile_y = 12,
                    snapshot_start_time = mode == "clock_mismatch" ? 900 : 1000,
                    guaranteed_arrival_by_time = 1030,
                    guaranteed_completion_by_time = mode == "late" ? 1800 : 1040,
                    time_budget_matches = true,
                    energy_budget_matches = true,
                    timing_evidence_id = "synthetic_acquisition_budget",
                    terminal_route_steps = sequential ? new[]
                    {
                        Step(1, 5, 1000, 1030, 5, 1035),
                        Step(2, 6, 1035, 1037, 3, mode == "endpoint_mismatch" ? 1041 : 1040)
                    } : Array.Empty<AcquisitionDailyTerminalRouteStep>()
                }, JsonDefaults.Options);
            });
            var before = JsonSerializer.Serialize(fixture.Snapshot, JsonDefaults.Options);
            var ledgerBefore = JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options);
            var control = fixture.RunRolling(routeTimingCalibrationPath: mode == "missing_calibration" ? "" : calibrationPath);
            var forecast = control.SelectedAcquisitionDonationSchedule;
            Require(control.RollingStepReady && control.SelectedCandidateId == "route-59" &&
                control.SelectedAcquisitionAdmission is { PortfolioAdmissionReady: true } && control.CompiledQueue is null &&
                forecast is not null && !forecast.NativeAcquisitionVerified && !forecast.JointExecutionScheduleVerified &&
                !control.FormalTrainingAuthorized && control.CompleteMuseumItemDenominator.Length == 95 &&
                before == JsonSerializer.Serialize(fixture.Snapshot, JsonDefaults.Options) &&
                ledgerBefore == JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options),
                "The selected acquisition must retain its original admission and immutable stock/ledger, not fabricate held output: " + mode);
            var ready = mode is "ready" or "terminal_sequence";
            Require(forecast!.CurrentDaySupplyDonationScheduleReady == ready &&
                forecast.RouteOccurrenceId == control.SelectedCandidateId &&
                forecast.AcquisitionDailyBudgetSha256 == AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                    fixture.Opportunity.Routes.Single(route => route.RouteOccurrenceId == control.SelectedCandidateId).UpstreamRoute.Evaluation!),
                "The forecast must bind the selected daily budget and exact endpoint: " + mode);
            if (ready)
            {
                var route = forecast.DonationRouteSchedule!;
                Require(route.RouteScenario!.StartLocation == "Road" && route.RouteScenario.StartTileX == x &&
                    route.RouteScenario.StartTileY == 12 && route.DepartureTime == 1040 && route.CounterArrivalByTime > 1040,
                    "Donation travel must start at the final acquisition stand and completion, not the original player position/time.");
            }
            else
            {
                var expected = mode switch
                {
                    "missing_calibration" => "future_route_timing_calibration_input_invalid",
                    "endpoint_mismatch" => "rusty_key_acquisition_terminal_schedule_endpoint_mismatch",
                    "late" => "future_route_connector_closed_before_arrival",
                    _ => "rusty_key_acquisition_terminal_schedule_unavailable"
                };
                Require(forecast.BlockingReasons.Any(reason => reason.StartsWith(expected, StringComparison.Ordinal)),
                    "A missing current-day join must stay explicit without prohibiting legal acquisition/later donation: " + mode);
            }
            Write(Path.Combine(root, "acquisition-donation-" + mode + ".json"), control);
            passed.Add("rolling_acquisition_donation_schedule_" + mode);
        }

        static AcquisitionDailyTerminalRouteStep Step(int ordinal, int x, int departure, int arrival, int minutes, int completion) =>
            new(ordinal, "Road", x, 11, x, 12, departure, arrival, arrival, minutes, completion, "synthetic_acquisition_budget");
    }
}
