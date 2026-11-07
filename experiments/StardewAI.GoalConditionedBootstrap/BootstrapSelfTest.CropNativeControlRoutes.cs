using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCropNativeControlRoutes(SnapshotEnvelope source)
    {
        var root = TempArtifactRoot("crop-native-control-routes");
        var before = MutateContextBoundary(source, state =>
        {
            state["player"]!["location_id"] = ControlField(JsonValue.Create("FarmHouse")!);
            state["player"]!["tile_x"] = ControlField(JsonValue.Create(3)!);
            state["player"]!["tile_y"] = ControlField(JsonValue.Create(11)!);
            state["player"]!["movement_timing_context"] = ControlField(JsonNode.Parse("""
                {"projection_status":"exact_current_player_native_cardinal_movement_context",
                "scope":"ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
                "runtime_calibration_compatible":true,"route_timing_ready_now":false,
                "player_using_tool_now":false,"immobilizing_buff_19_active":false,
                "current_event_active":false,"event_up":false,"game_paused":false,
                "freeze_controls":false,"is_warping":false,"dialogue_up":false,
                "player_freeze_pause":0,"native_control_gate_open":true,
                "player_can_move_now":false,"global_fade":false}
                """)!);
            state["menus"] = new JsonObject
            {
                ["active_menu"] = ControlField(new JsonObject { ["is_open"] = false })
            };
            state["current_location"] = new JsonObject
            {
                ["home_context"] = ControlField(new JsonObject { ["home_location_id"] = "FarmHouse" })
            };
        });
        var after = MutateContextBoundary(before, state =>
        {
            var movement = state["player"]!["movement_timing_context"]!["value"]!;
            movement["player_can_move_now"] = true;
            movement["route_timing_ready_now"] = true;
        });
        after.GameTick = before.GameTick + NativeControlSettleRecovery.WaitTicks;
        var queue = LifecycleQueue("control", "executor.wait_ticks", before, new[] { 3 }, "FarmHouse");
        queue.Items[0].NormalizedCommand.Parameters = new[]
        {
            SeedAcquisitionParameter("wait_ticks", "30"),
            SeedAcquisitionParameter("precondition", "candidate_id:" + NativeControlSettleRecovery.CandidateId)
        };
        queue.Items[0].NormalizedCommand.Steps[0].Target = "30";
        queue.Items[0].NormalizedCommand.Steps[0].EstimatedTicks = 30;

        VerifyControlRouteCase(root, "valid", before, after, queue,
            NativeControlSettleRecovery.CandidateId, true);
        VerifyControlRouteCase(root, "generic-wait", before, after, queue, "other-wait", false);
        var wrongTicks = CloneQueue(queue);
        wrongTicks.Items[0].NormalizedCommand.Parameters[0].Value = "60";
        VerifyControlRouteCase(root, "wrong-ticks", before, after, wrongTicks,
            NativeControlSettleRecovery.CandidateId, false);
        var noBinding = CloneQueue(queue);
        noBinding.Items[0].NormalizedCommand.Parameters = noBinding.Items[0].NormalizedCommand.Parameters.Take(1).ToArray();
        VerifyControlRouteCase(root, "no-binding", before, after, noBinding,
            NativeControlSettleRecovery.CandidateId, false);
        var resourceChange = MutateContextBoundary(after, state => state["player"]!["money"]!["value"] = 1);
        VerifyControlRouteCase(root, "resource-change", before, resourceChange, queue,
            NativeControlSettleRecovery.CandidateId, false);
        var moved = MutateContextBoundary(after, state => state["player"]!["tile_x"]!["value"] = 4);
        VerifyControlRouteCase(root, "moved", before, moved, queue,
            NativeControlSettleRecovery.CandidateId, false);
        var staleTick = CloneSnapshot(after);
        staleTick.GameTick = before.GameTick + 1;
        VerifyControlRouteCase(root, "short-wait", before, staleTick, queue,
            NativeControlSettleRecovery.CandidateId, false);
        var ready = CloneSnapshot(after);
        ready.GameTick = before.GameTick;
        var readyQueue = CloneQueue(queue);
        readyQueue.StateHash = ready.StateHash;
        readyQueue.Items[0].NormalizedCommand.StateHash = ready.StateHash;
        VerifyControlRouteCase(root, "not-control-gap", ready, after, readyQueue,
            NativeControlSettleRecovery.CandidateId, false);
        Console.WriteLine("PASS: exact control observations through both original crop route proof paths; seven forged/unsafe cases rejected.");
    }

    private static JsonObject ControlField(JsonNode value) =>
        new() { ["status"] = "available", ["value"] = value };

    private static void VerifyControlRouteCase(string root, string name,
        SnapshotEnvelope before, SnapshotEnvelope after, ActionQueueEnvelope queue,
        string candidateId, bool accepted)
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(folder);
        var queuePath = Path.Combine(folder, "queue.json");
        var beforePath = Path.Combine(folder, "before.json");
        var afterPath = Path.Combine(folder, "after.json");
        var receiptPath = Path.Combine(folder, "receipt.json");
        var receipt = LifecycleReceipt(queue, before, after, "control." + name, candidateId, "wait_ticks");
        receipt.StepResults[0].AfterGameTick = after.GameTick;
        Write(queuePath, queue); Write(beforePath, before); Write(afterPath, after); Write(receiptPath, receipt);
        var request = new MoneyRevenueTerminalQueueTransitionProof
        {
            TransitionId = name, QueuePath = queuePath, BeforeSnapshotPath = beforePath,
            AfterSnapshotPath = afterPath, ExecutionReceiptPath = receiptPath,
            RunId = receipt.RunId, ExecutorVersion = PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            SelectedCandidateId = candidateId
        };
        bool Passes(Action action)
        {
            try { action(); return true; }
            catch (InvalidDataException) when (!accepted) { return false; }
            catch (TargetInvocationException error) when (!accepted && error.InnerException is InvalidDataException) { return false; }
        }
        Require(Passes(() => MoneyRevenueResourceNeutralTerminalRouteVerifier.VerifyHomeIngress(before, new[] { request })) == accepted,
            "Shipping control route acceptance drifted: " + name);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(MoneyRevenueCropNativeLifecycleProofBuilder);
        var loaded = type.GetMethod("LoadTransition", flags)!.Invoke(null, new object[]
        {
            new MoneyRevenueCropNativeLifecycleTransitionRequest("supporting_route_to_sleep",
                before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
                queuePath, beforePath, receiptPath, afterPath, receipt.RunId, request.ExecutorVersion, candidateId)
        })!;
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(loaded.GetType()))!;
        list.Add(loaded);
        Require(Passes(() => type.GetMethod("ValidateSupportingRoutes", flags)!.Invoke(null, new object[]
            { before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(), list, "FarmHouse" })) == accepted,
            "Lifecycle control route acceptance drifted: " + name);
    }
}
