using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.Testing;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private static object LifecycleInvoke(string name, params object[] args) =>
        typeof(MoneyRevenueCropNativeLifecycleProofBuilder).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, args)!;

    private static T LifecycleRead<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)!;

    private static Array LifecycleLoad(IEnumerable<MoneyRevenueCropNativeLifecycleTransitionRequest> requests, bool native = true)
    {
        var loaded = requests.Select(row => LifecycleInvoke(native ? "LoadNativeObservedTransition" : "LoadTransition", row)).ToArray();
        var result = Array.CreateInstance(loaded[0].GetType(), loaded.Length);
        for (var i = 0; i < loaded.Length; i++) result.SetValue(loaded[i], i);
        return result;
    }

    private static IList LifecycleList(Array rows)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rows.GetType().GetElementType()!))!;
        foreach (var row in rows) list.Add(row);
        return list;
    }

    // Rebuilds synthetic fixture wires only; never edits the shared golden or claims a native-v5 capacity proof.
    private sealed class LifecycleControlsFixture(bool combinedRefill = false)
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "native-lifecycle-controls-" + Guid.NewGuid().ToString("N"));
        internal List<MoneyRevenueCropNativeLifecycleTransitionRequest> Rows { get; } = [];
        internal object Contexts { get; private set; } = null!;
        internal int SplitDay { get; private set; }
        internal IReadOnlyDictionary<string, string> PreparationInputs { get; private set; } = new Dictionary<string, string>();
        private SnapshotEnvelope source = null!;
        private MoneyRevenueCropDailyCapacityProof capacity = null!;
        private SnapshotEnvelope current = null!;
        private int sequence;

        internal async Task Build()
        {
            Directory.CreateDirectory(Root);
            var golden = combinedRefill ? new CropTeacherProductionEvidenceTests.Fixture(6, Id) : new CropTeacherProductionEvidenceTests.Fixture();
            var evidence = await golden.Build();
            var proof = LifecycleRead<MoneyRevenueCropNativeLifecycleProof>(Assert.Single(evidence.NativeLifecycleProofPaths));
            var request = LifecycleRead<MoneyRevenueCropNativeLifecycleProofRequest>(proof.RequestPath);
            var plan = LifecycleRead<MoneyRevenueCropProductionObligationPlan>(request.CropProductionPlanPath);
            source = LifecycleRead<SnapshotEnvelope>(plan.SnapshotPath);
            capacity = LifecycleRead<MoneyRevenueCropDailyCapacityProof>(request.DailyCapacityProofPath);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(request.CropProductionPlanPath))).ToLowerInvariant();
            Contexts = LifecycleInvoke("LoadLifecycleContexts", plan, request.CropProductionPlanPath, hash, capacity, source);
            current = LifecycleRead<SnapshotEnvelope>(request.Transitions[0].BeforeSnapshotPath);
            SplitDay = request.Transitions[0].TotalDay + (combinedRefill ? 7 : 1);
            var xs = Enumerable.Range(5, combinedRefill ? 6 : 2).ToArray();
            foreach (var original in request.Transitions)
            {
                var queue = LifecycleRead<ActionQueueEnvelope>(original.QueuePath);
                var option = queue.Items[0].OptionId;
                if (original.TotalDay == SplitDay && option == "executor.water_crop")
                {
                    AddWait(); // Original day1-shaped pre-water wait.
                    Add(original.TotalDay, option, combinedRefill ? xs[..4] : [5]);
                    if (combinedRefill) await AddOriginalRefill(golden);
                    else AddWait(); // Control between homogeneous crop batches.
                    Add(original.TotalDay, option, combinedRefill ? xs[4..] : [6]);
                }
                else Add(original.TotalDay, option, xs, original.TransitionKind == "supporting_route_to_crop" ? "Farm" : "FarmHouse");
                if (original.TotalDay == SplitDay && original.TransitionKind == "supporting_route_to_sleep") AddWait();
            }
            AddWait(); // Final control tail, after the last harvest.
        }

        private async Task AddOriginalRefill(CropTeacherProductionEvidenceTests.Fixture production)
        {
            ChangeState(current, "player", node => node["watering_can_state"]!["value"]!["water_left"] = 0);
            current.StateHash = SnapshotHash.ComputeStateHash(current.State);
            PinPreviousAfter();
            // Reuse the existing compiler and Product native-wire fixture with the SAME six-tile plan,
            // commitment, ledger capture and water4 terminal source, all bound BEFORE compilation/verification.
            var native = new NativeRefillProductFixture();
            var approach = native.Trace.Approach; var shared = approach.Watering.Shared;
            shared.Source.Recapture(current);
            var ledger = LifecycleRead<StrategyCommitmentLedger>(production.LedgerPath);
            ledger.SourceStateHash = current.StateHash;
            Fixture.Save(shared.Source.LedgerPath, ledger);
            var runPlan = LifecycleRead<MoneyRevenueCropNativeProductionRunPlan>(production.NativePlanPath);
            Fixture.Save(shared.Inputs.Plan.RequestPath, LifecycleRead<MoneyRevenueCropNativeProductionRunPlanRequest>(runPlan.RequestPath));
            shared.Runtime.Store.Snapshots[current.StateHash] = current;
            await approach.Compile(forceOriginalApproach: true);
            await native.Build(retainCompilation: true);
            var preparation = native.OpenPreparation(selected: false, native: true);
            var row = native.Transition(preparation, PolicyTrajectoryVersionPins.ProductExecutor) with
            {
                NativeRefillPreparation = new(shared.Inputs.Plan.RequestPath, shared.Source.LedgerPath,
                    [Id], Id, "refill-approach.original", approach.ModelPath, "native_resource_neutral")
            };
            var trace = native.OpenTrace(selected: false, PolicyTrajectoryVersionPins.ProductExecutor);
            Assert.False(trace.ResourceCreditAuthorized); Assert.False(trace.FormalProductTrainingAuthorized);
            PreparationInputs = trace.InputHashes;
            Rows.Add(row);
            current = LifecycleRead<SnapshotEnvelope>(row.AfterSnapshotPath);
        }

        private void PinPreviousAfter()
        {
            var previous = Rows[^1];
            Fixture.Save(previous.AfterSnapshotPath, current);
            var receipt = LifecycleRead<QueueExecutionReceiptEnvelope>(previous.ExecutionReceiptPath);
            receipt.AfterStateHash = current.StateHash;
            receipt.StepResults[^1].AfterStateHash = current.StateHash;
            Fixture.Save(previous.ExecutionReceiptPath, receipt);
        }

        private void Add(int day, string option, int[] xs, string target = "")
        {
            object[] args = [Root, Rows, current, day, sequence, option, xs, target, false];
            current = MoneyCropTeacherFixture.CreateSnapshot("AddLifecycleTransition", args);
            sequence = (int)args[4];
        }

        private void AddWait()
        {
            var movement = JsonNode.Parse("""
                {"projection_status":"exact_current_player_native_cardinal_movement_context",
                "scope":"ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
                "runtime_calibration_compatible":true,"route_timing_ready_now":false,"player_using_tool_now":false,
                "immobilizing_buff_19_active":false,"current_event_active":false,"event_up":false,"game_paused":false,
                "freeze_controls":false,"is_warping":false,"dialogue_up":false,"player_freeze_pause":0,
                "native_control_gate_open":true,"player_can_move_now":false,"global_fade":false}
                """)!;
            ChangeState(current, "player", node => node["movement_timing_context"] = new JsonObject
                { ["status"] = "available", ["value"] = movement });
            current.State["menus"] = JsonSerializer.SerializeToElement(new
                { active_menu = new { status = "available", value = new { is_open = false } } });
            Assert.NotNull(NativeControlSettleRecovery.TryCreate(current));
            current.InGameTime.Status = "available";
            current.InGameTime.Value = current.State["time"].GetProperty("time").GetProperty("value").GetInt32();
            current.StateHash = SnapshotHash.ComputeStateHash(current.State);
            // Pin this control gap into the preceding synthetic producer's original terminal capture/receipt.
            var previous = Rows[^1];
            PinPreviousAfter();
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(current, JsonOptions), JsonOptions)!;
            ChangeState(after, "player", node =>
            {
                node["movement_timing_context"]!["value"]!["player_can_move_now"] = true;
                node["movement_timing_context"]!["value"]!["route_timing_ready_now"] = true;
            });
            after.GameTick += NativeControlSettleRecovery.WaitTicks;
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            var prefix = Path.Combine(Root, "wait-" + (++sequence));
            var queue = MoneyCropTeacherFixture.Create<ActionQueueEnvelope>("LifecycleQueue", prefix,
                "executor.wait_ticks", current, new[] { 5 }, "Farm");
            var item = queue.Items.Single();
            item.NormalizedCommand.Parameters = [new() { Name = "wait_ticks", Value = "30" },
                new() { Name = "precondition", Value = "candidate_id:" + NativeControlSettleRecovery.CandidateId }];
            item.NormalizedCommand.Steps[0].Target = "30";
            item.NormalizedCommand.Steps[0].EstimatedTicks = 30;
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue,
                current, after, previous.RunId, NativeControlSettleRecovery.CandidateId, "wait_ticks");
            receipt.StepResults[0].AfterGameTick = after.GameTick;
            Fixture.Save(prefix + "-before.json", current); Fixture.Save(prefix + "-queue.json", queue);
            Fixture.Save(prefix + "-after.json", after); Fixture.Save(prefix + "-receipt.json", receipt);
            Rows.Add(new("supporting_route_to_sleep", previous.TotalDay, prefix + "-queue.json", prefix + "-before.json",
                prefix + "-receipt.json", prefix + "-after.json", receipt.RunId, previous.ExecutorVersion, receipt.SelectedCandidateId));
            current = after;
        }

        internal object Validate(IEnumerable<MoneyRevenueCropNativeLifecycleTransitionRequest>? rows = null, bool native = true) =>
            LifecycleInvoke("ValidateTransitionChain", LifecycleLoad(rows ?? Rows, native), source, Contexts, capacity,
                Array.Empty<MoneyRevenueCropNativeLifecycleCheckpointResumeRequest>());
    }
}
