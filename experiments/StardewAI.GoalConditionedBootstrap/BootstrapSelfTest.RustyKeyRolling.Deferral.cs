using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingOwnedSupplyDeferral(string root, List<string> passed)
    {
        foreach (var mode in new[] { "evening", "last_legal_day", "too_late", "daytime", "missing_clock", "missing_home",
                     "bed_unconfirmed", "missing_calibration", "progress_drift", "insufficient_owned", "unknown_menu",
                     "sleep_prompt", "foreign_prompt", "native_settle", "unsafe_settle", "later_gate_closed",
                     "first_gate_closed", "unknown_route", "closed_missing_calibration", "morning" })
        {
            var fixture = new RustyKeyPortfolioFixture(mode == "insufficient_owned" ? 58 : 59);
            fixture.EnableHeldDonation();
            ConfigureRollingDonationRouteSchedule(fixture, "ready");
            ConfigureOwnedSupplyHome(fixture, mode);
            var directory = Path.Combine(root, "owned-deferral-" + mode);
            var inputs = new AcquisitionRoutePortfolioInputs
            {
                RequirementInventoryPath = Path.Combine(directory, "inventory.json"), SnapshotPath = Path.Combine(directory, "snapshot.json"),
                StrategyLedgerPath = Path.Combine(directory, "ledger.json"),
                RouteTimingCalibrationPath = mode is "missing_calibration" or "closed_missing_calibration" ? "" : Path.Combine(directory, "timing.json")
            };
            Write(inputs.RequirementInventoryPath, fixture.Inventory);
            Write(inputs.SnapshotPath, fixture.Snapshot);
            Write(inputs.StrategyLedgerPath, fixture.Ledger);
            if (inputs.RouteTimingCalibrationPath.Length > 0) Write(inputs.RouteTimingCalibrationPath,
                StageOneRouteTimingCalibration(mode is "last_legal_day" ? 221 : mode is "too_late" ? 222 : mode == "morning" ? 1 : 0));
            var snapshotBefore = JsonSerializer.Serialize(fixture.Snapshot, JsonDefaults.Options);
            var ledgerBefore = JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options);
            var control = RustyKeyRollingSupplyProposalBuilder.Build(inputs);
            var expected = mode is "evening" or "last_legal_day" or "sleep_prompt" or "native_settle" or "later_gate_closed" or "first_gate_closed";
            Require((control.SelectedOwnedSupplyDeferral is not null) == expected,
                "Only exact, source-bound ordinary recovery may defer owned supply: " + mode + ":" +
                JsonSerializer.Serialize(control.CandidateEvaluations, JsonDefaults.Options));
            Require(control.SelectedAcquisitionProposal is null && control.CompleteMuseumItemDenominator.Length == 95 &&
                !control.ThresholdSatisfied && !control.FormalTrainingAuthorized &&
                snapshotBefore == JsonSerializer.Serialize(fixture.Snapshot, JsonDefaults.Options) &&
                ledgerBefore == JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options),
                "Deferral cannot reacquire stock, mutate sources or promote evidence: " + mode);
            if (mode == "morning") Require(control.RollingStepReady && control.SelectedDonationRouteSchedule is { CurrentDayRouteReady: true } &&
                control.SourceTotalDay == 1 && control.CompiledQueue is not null,
                "An independent synthetic next-day source must resume ordinary held donation without acquisition or another sleep.");
            if (expected)
            {
                var deferral = control.SelectedOwnedSupplyDeferral!;
                Require(control.RollingStepReady && control.CompiledQueue is not null &&
                    !deferral.FutureDateRouteVerified && deferral.FreshSnapshotReplanRequired &&
                    deferral.EarliestResumeTotalDay == control.SourceTotalDay + (mode == "native_settle" ? 0 : 1) &&
                    deferral.OwnedRequirementIds.SequenceEqual(control.PendingDonationRequirementIds!) &&
                    control.CompiledQueue.Items.All(item => item.OptionId is "executor.sleep" or "executor.wait_ticks"),
                    "Recovery must reuse the original terminal/observer, not a future donation queue: " + mode);
                var controlPath = Path.Combine(directory, "control.json");
                Write(controlPath, control);
                var preparation = RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath);
                Require(preparation is { StepReady: true, Kind: "owned_supply_deferral", AtomicCommitRequest: null } &&
                    preparation.OwnedSupplyDeferral?.RecoveryCandidateId == deferral.RecoveryCandidateId &&
                    EqualJson(preparation, RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath)),
                    "Recovery preparation must independently rebuild the same typed stage and stable queue.");
                var forged = JsonSerializer.SerializeToNode(control, JsonDefaults.Options)!;
                forged["selected_owned_supply_deferral"]!["earliest_resume_total_day"] = 223;
                Write(controlPath, forged);
                var rejected = false;
                try { RustyKeyRollingStepPreparationBuilder.Build(inputs, controlPath); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Caller-edited future-date claims must fail whole-control rebuilding.");
                Write(controlPath, control);
                Write(Path.Combine(directory, "preparation.json"), preparation);
            }
            passed.Add("owned_supply_deferral_" + mode);
        }
    }

    private static void ConfigureOwnedSupplyHome(RustyKeyPortfolioFixture fixture, string mode)
    {
        JsonNode Field(object value) => JsonSerializer.SerializeToNode(new
        {
            value, status = "available", confidence = 1, adapter = "test", read_at_tick = 1,
            source = new { kind = "game_object", path = "synthetic_owned_supply_deferral" }
        })!;
        var day = mode == "last_legal_day" ? 221 : mode == "too_late" ? 222 : mode == "morning" ? 1 : 0;
        var clock = JsonSerializer.SerializeToNode(fixture.Snapshot.State["time"])!.AsObject();
        clock["time"] = Field(mode is "native_settle" or "unsafe_settle" ? 600 : mode == "daytime" ? 1853 : mode == "morning" ? 1000 : 1900);
        clock["total_days"] = Field(day);
        if (mode == "missing_clock") clock.Remove("time");
        fixture.Snapshot.State["time"] = JsonSerializer.SerializeToElement(clock);
        var player = JsonSerializer.SerializeToNode(fixture.Snapshot.State["player"])!.AsObject();
        player["location_id"]!["value"] = "FarmHouse";
        player["tile_x"]!["value"] = 3;
        player["tile_y"]!["value"] = 9;
        var movement = player["movement_timing_context"]!["value"]!.AsObject();
        if (mode is "native_settle" or "unsafe_settle")
        {
            foreach (var name in new[] { "player_using_tool_now", "immobilizing_buff_19_active", "current_event_active", "event_up",
                         "game_paused", "freeze_controls", "is_warping", "dialogue_up", "global_fade", "player_can_move_now", "route_timing_ready_now" })
                movement[name] = false;
            movement["native_control_gate_open"] = true;
            movement["player_freeze_pause"] = 0;
            if (mode == "unsafe_settle") movement["player_using_tool_now"] = true;
        }
        fixture.Snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        var locations = JsonSerializer.SerializeToNode(fixture.Snapshot.State["locations"])!.AsObject();
        locations["collision_grid"]!["value"]!["location_id"] = "FarmHouse";
        locations["route_connectors"]!["value"]!["location_id"] = "FarmHouse";
        foreach (var edge in locations["route_graph"]!["value"]!["edges"]!.AsArray())
            if (edge!["from_location"]!.GetValue<string>() == "Town") edge["from_location"] = "FarmHouse";
        var date = locations["social_route_date_evidence"]!["value"]!;
        date["capture_total_days"] = day;
        foreach (var map in date["locations"]!.AsArray())
        {
            if (map!["location_id"]!.GetValue<string>() == "Town") map["location_id"] = "FarmHouse";
            foreach (var gate in map["action_gates"]!.AsArray()) gate!["effective_close_time"] = 2400;
        }
        if (mode is "first_gate_closed" or "closed_missing_calibration")
        {
            var gate = new { kind = "warp", tile_x = 10, tile_y = 10, target_location = "Road", allowed_on_capture_date = true,
                time_unrestricted_on_capture_date = false, effective_open_time = 900, effective_close_time = 1700 };
            date["locations"]![0]!["action_gates"] = JsonSerializer.SerializeToNode(new[] { gate });
            locations["route_gate_context"] = Field(new { action_gates = new[] { new
            {
                kind = "warp", tile_x = 10, tile_y = 10, target_location = "Road", allowed_now = false,
                effective_open_time = 900, close_time = 1700, friendship_allowed = true, unresolved_reason = ""
            } } });
        }
        if (mode == "later_gate_closed") date["locations"]![2]!["action_gates"]![0]!["effective_close_time"] = 1700;
        if (mode == "unknown_route") locations.Remove("social_route_date_evidence");
        fixture.Snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        var home = new JsonObject { ["home_context"] = Field(new
        {
            home_available = true, home_location_id = "FarmHouse", current_location_id = "FarmHouse", current_location_is_home = true,
            entry_tile_x = 3, entry_tile_y = 9, bed_tile_x = 3, bed_tile_y = 8,
            bed_tile_has_bed = mode != "bed_unconfirmed", sleep_executor_enabled = true
        }) };
        if (mode == "missing_home") home.Remove("home_context");
        fixture.Snapshot.State["current_location"] = JsonSerializer.SerializeToElement(home);
        var prompt = mode is "sleep_prompt" or "foreign_prompt";
        fixture.Snapshot.State["menus"] = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["active_menu"] = Field(new { is_open = prompt || mode == "unknown_menu", type = prompt ? "DialogueBox" : "none",
                last_question_key = prompt ? (mode == "foreign_prompt" ? "Other" : "Sleep") : "", is_sleep_prompt = prompt, event_up = false }),
            ["sleep_prompt_context"] = Field(new { prompt_open = prompt, active_menu_open = prompt, active_menu_type = "DialogueBox",
                last_question_key = "Sleep", can_confirm_sleep = prompt, confirm_executor_enabled = prompt, confirm_action_key = "Sleep_Yes" }),
            ["menu_specific_state"] = Field(new { kind = "none" })
        });
        if (mode == "progress_drift")
        {
            var world = JsonSerializer.SerializeToNode(fixture.Snapshot.State["world_progress"])!;
            world["museum"]!["value"]!["donation_candidates"]![0]!["donated_count_before"] = 58;
            fixture.Snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(world);
        }
        fixture.Rehash();
    }
}
