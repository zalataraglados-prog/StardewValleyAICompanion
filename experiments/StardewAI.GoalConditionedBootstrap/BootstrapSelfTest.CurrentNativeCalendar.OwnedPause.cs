using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private void VerifyCurrentNativeOwnedPauseLocation(List<string> passed)
        {
            var inputs = PrepareCurrentNativeGeode();
            var baseline = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(inputs.BeforeSnapshotPath, "Owned pause fixture");
            var snapshot = CloneSnapshot(baseline);
            var player = JsonSerializer.SerializeToNode(snapshot.State["player"])!;
            var movement = player["movement_timing_context"]!["value"]!;
            movement["route_timing_ready_now"] = false;
            movement["game_paused"] = true;
            movement["player_can_move_now"] = true;
            movement["player_using_tool_now"] = false;
            movement["player_freeze_pause"] = 0;
            movement["player_id"] = snapshot.PlayerId.Value;
            movement["game_unique_id"] = "synthetic-world";
            foreach (var field in new[] { "dialogue_up", "global_fade", "freeze_controls", "is_warping",
                         "current_event_active", "event_up", "immobilizing_buff_19_active" })
                movement[field] = false;
            movement["executor_owned_idle_pause"] = new JsonObject
            {
                ["schema_version"] = "executor_owned_idle_pause.v1",
                ["owner_mod_id"] = "StardewAI.RuntimeTestHarness",
                ["release_policy"] = "release_before_pending_execution_update",
                ["applied"] = true, ["pause_epoch"] = 1,
                ["player_id"] = snapshot.PlayerId.Value, ["game_unique_id"] = "synthetic-world"
            };
            Require(Rebuild().LocationRouteMatchesTargetDate == true,
                "An exactly owned releasable pause must retain the native geode location proof.");
            passed.Add("owned_idle_pause_current_location_proof");
            movement["executor_owned_idle_pause"]!["applied"] = false;
            AssertNotReady("unowned_pause_reason_preserved");
            movement.AsObject().Remove("executor_owned_idle_pause");
            AssertNotReady("legacy_paused_snapshot_still_blocks");

            void AssertNotReady(string name)
            {
                var blocked = Rebuild();
                Require(blocked.LocationRouteMatchesTargetDate is null &&
                    blocked.BlockingReasons.Contains("future_route_movement_timing_context_not_ready") &&
                    !blocked.BlockingReasons.Contains("future_location_route_evidence_input_missing"),
                    "The original timing failure must survive location production: " + name);
                passed.Add(name);
            }

            AcquisitionRouteTargetDateLocation Rebuild()
            {
                snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
                snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
                Write(inputs.BeforeSnapshotPath, snapshot);
                Write(routeCalendarPath, AcquisitionRouteCalendarResolutionBuilder.BuildCurrentSources(
                    inventoryPath, loweringPath, windowsPath, inputs.BeforeSnapshotPath));
                Write(targetDateCalendarPath, AcquisitionRouteTargetDateCalendarBuilder.BuildCurrentSources(
                    inventoryPath, loweringPath, windowsPath, routeCalendarPath, inputs.BeforeSnapshotPath, 0));
                Write(targetDateUnlockPath, AcquisitionRouteTargetDateUnlockBuilder.Build(inventoryPath, loweringPath,
                    windowsPath, routeCalendarPath, targetDateCalendarPath, inputs.BeforeSnapshotPath));
                Write(targetDateFestivalPath, AcquisitionRouteTargetDateFestivalBuilder.Build(inventoryPath, loweringPath,
                    windowsPath, routeCalendarPath, targetDateCalendarPath, targetDateUnlockPath, inputs.BeforeSnapshotPath));
                return AcquisitionRouteTargetDateLocationBuilder.Build(inventoryPath, loweringPath, windowsPath,
                    routeCalendarPath, targetDateCalendarPath, targetDateUnlockPath, targetDateFestivalPath,
                    inputs.BeforeSnapshotPath, inputs.RouteTimingCalibrationPath).Routes
                    .Single(row => row.RouteOccurrenceId == "museum_collection:museum:item:538:0:0");
            }
        }
    }
}
