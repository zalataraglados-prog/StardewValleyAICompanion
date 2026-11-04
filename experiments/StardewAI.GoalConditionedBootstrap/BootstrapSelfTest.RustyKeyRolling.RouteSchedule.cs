using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingDonationRouteSchedule(string root, List<string> passed)
    {
        foreach (var mode in new[] { "ready", "later_gate_closed", "alternative", "deferred", "missing_calibration" })
        {
            var fixture = new RustyKeyPortfolioFixture();
            fixture.EnableHeldDonation();
            ConfigureRollingDonationRouteSchedule(fixture, mode);
            var directory = Path.Combine(root, "donation-route-" + mode);
            var inputs = new AcquisitionRoutePortfolioInputs
            {
                RequirementInventoryPath = Path.Combine(directory, "inventory.json"),
                SnapshotPath = Path.Combine(directory, "snapshot.json"),
                StrategyLedgerPath = Path.Combine(directory, "ledger.json"),
                RouteTimingCalibrationPath = mode == "missing_calibration" ? string.Empty : Path.Combine(directory, "timing.json")
            };
            Write(inputs.RequirementInventoryPath, fixture.Inventory);
            Write(inputs.SnapshotPath, fixture.Snapshot);
            Write(inputs.StrategyLedgerPath, fixture.Ledger);
            if (inputs.RouteTimingCalibrationPath.Length > 0) Write(inputs.RouteTimingCalibrationPath, StageOneRouteTimingCalibration(0));
            var control = RustyKeyRollingSupplyProposalBuilder.Build(inputs);
            var ready = mode is "ready" or "alternative" or "deferred";
            Require(control.RollingStepReady == ready && control.SelectedAcquisitionProposal is null &&
                (control.CompiledQueue is not null) == ready && control.CompleteMuseumItemDenominator.Length == 95 &&
                !control.ThresholdSatisfied && !control.FormalTrainingAuthorized,
                "Donation routing must use the complete sequential date-bound schedule, not unrelated acquisition: " + mode);
            if (ready)
            {
                Require(control.SelectedDonationRouteSchedule is { CurrentDayRouteReady: true } &&
                    control.CompiledQueue!.Items.Length == 1 && control.CompiledQueue.Items[0].OptionId ==
                        (mode == "deferred" ? "executor.wait_ticks" : "executor.traverse_connector"),
                    "A complete forecast must still dispatch only one original connector or bounded wait then fresh-replan.");
                if (mode == "deferred") Require(control.SelectedDonationRouteSchedule!.RouteScenario!.SegmentEvidence![0].OpenTime == 1100,
                    "The sequential proof must include the real first-door opening instead of treating bounded wait as arrival.");
                if (mode == "alternative") Require(control.SelectedDonationRouteSchedule!.RouteScenario!.ProducedPaths![0][0].TargetLocation == "AltRoad",
                    "Full counter arrival must beat the cheaper first connector's longer remaining route.");
                var controlPath = Path.Combine(directory, "control.json");
                Write(controlPath, control);
                var prepared = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
                Require(prepared.StepReady && prepared.Kind == "held_donation" && prepared.AtomicCommitRequest is null,
                    "Original step preparation must independently rebuild the schedule without a resource commit.");
                var forged = JsonSerializer.SerializeToNode(control, JsonDefaults.Options)!;
                forged["selected_donation_route_schedule"]!["donation_completion_by_time"] = 600;
                Write(controlPath, forged);
                var rejected = false;
                try { RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Caller-edited route timing must not survive original control identity rebuilding.");
            }
            else Require(control.PendingDonationRequirementIds is { Length: 1 } && control.CandidateEvaluations.Any(row => !row.Ready),
                "Untimely or unknown routing must retain owned pending supply without inventing another copy.");
            passed.Add("rolling_donation_joint_route_" + mode);
        }

    }

    private static void ConfigureRollingDonationRouteSchedule(RustyKeyPortfolioFixture fixture, string mode)
    {
        var player = JsonSerializer.SerializeToNode(fixture.Snapshot.State["player"])!.AsObject();
        player["location_id"]!["value"] = "Town";
        player["tile_x"]!["value"] = 9;
        player["tile_y"]!["value"] = 10;
        player["energy"] = Field(270);
        player["movement_timing_context"] = Field(new
        {
            projection_status = "exact_current_player_native_cardinal_movement_context",
            runtime_calibration_compatible = true,
            route_timing_ready_now = true,
            real_milliseconds_per_game_minute = 700,
            theoretical_upper_bound_game_minutes_per_tile = 0.7,
            scope = "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay"
        });
        fixture.Snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        var museum = JsonSerializer.SerializeToNode(fixture.Snapshot.State["world_progress"])!;
        museum["museum"]!["value"]!["museum_is_current_location"] = false;
        foreach (var candidate in museum["museum"]!["value"]!["donation_candidates"]!.AsArray().OfType<JsonObject>())
            candidate["action_status"] = "museum_not_current_location";
        fixture.Snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(museum);
        var locations = JsonSerializer.SerializeToNode(fixture.Snapshot.State["locations"])!.AsObject();
        locations["collision_grid"]!["value"]!["location_id"] = "Town";
        var firstKind = mode == "deferred" ? "locked_door_warp" : "warp";
        var edges = new List<object> { Edge(firstKind, "Town", 10, 10, "Road"), Edge("locked_door_warp", "Road", 40, 40, "ArchaeologyHouse") };
        var connectors = new List<object> { new
            {
                kind = firstKind, tile_x = 10, tile_y = 10, target_location = "Road", target_x = 5, target_y = 12, resolved = true,
                source_property = mode == "deferred" ? "Buildings.Action" : ""
            } };
        var maps = new List<object>
            {
                Map("Town", mode == "deferred" ? new object[] { new
                {
                    kind = "locked_door_warp", tile_x = 10, tile_y = 10, target_location = "Road",
                    allowed_on_capture_date = true, time_unrestricted_on_capture_date = false, effective_open_time = 1100, effective_close_time = 1700
                } } : Array.Empty<object>()), Map("ArchaeologyHouse", Array.Empty<object>()),
                Map("Road", new object[] { new
                {
                    kind = "locked_door_warp", tile_x = 40, tile_y = 40, target_location = "ArchaeologyHouse",
                    allowed_on_capture_date = true, time_unrestricted_on_capture_date = false, effective_open_time = 900,
                    effective_close_time = mode == "later_gate_closed" ? 1010 : 1700
                } })
            };
        if (mode == "alternative")
        {
            edges.Add(Edge("warp", "Town", 11, 10, "AltRoad"));
            edges.Add(Edge("warp", "AltRoad", 10, 10, "ArchaeologyHouse"));
            connectors.Add(new { kind = "warp", tile_x = 11, tile_y = 10, target_location = "AltRoad", target_x = 5, target_y = 12, resolved = true });
            maps.Add(Map("AltRoad", Array.Empty<object>()));
        }
        locations["route_graph"] = Field(new { status = "complete", edges });
        locations["route_connectors"] = Field(new { location_id = "Town", connectors });
        locations["route_action_branch_coverage"] = Field(new { rows = Array.Empty<object>() });
        if (mode == "deferred") locations["route_gate_context"] = Field(new
        {
            action_gates = new[] { new
                {
                    kind = "locked_door_warp", tile_x = 10, tile_y = 10, target_location = "Road", allowed_now = false,
                    effective_open_time = 1100, close_time = 1700, friendship_allowed = true, unresolved_reason = ""
                } }
        });
        locations["social_route_date_evidence"] = Field(new
        {
            schema_version = "social_route_date_evidence.v2",
            capture_total_days = 0,
            all_location_static_walkability_complete = true,
            projection_status = "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            location_count = maps.Count,
            locations = maps
        });
        fixture.Snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        fixture.Rehash();

        static JsonNode Field(object value) => JsonSerializer.SerializeToNode(new
        {
            value,
            status = "available",
            confidence = 1,
            adapter = "test",
            read_at_tick = 1,
            source = new { kind = "game_object", path = "synthetic_rolling_route_fixture" }
        })!;
        static object Edge(string kind, string from, int x, int y, string target) => new
        {
            kind,
            from_location = from,
            from_x = x,
            from_y = y,
            target_location = target,
            target_x = 5,
            target_y = 12,
            resolved = true
        };
        static object Map(string id, object[] gates) => new
        {
            location_id = id,
            map_width = 64,
            map_height = 64,
            projection_status = "exact_current_date_static_native_walkability",
            static_walkable_tile_count = 4096,
            static_walkable_tile_ranges = Enumerable.Range(0, 64).Select(y => new { y, start_x = 0, end_x = 63 }).ToArray(),
            build_conditions = (string?)null,
            build_conditions_met = (bool?)null,
            unsupported_route_action_record_count = 0,
            unsupported_route_action_tile_count = 0,
            unsupported_route_action_tiles = Array.Empty<object>(),
            action_gates = gates
        };
    }
}
