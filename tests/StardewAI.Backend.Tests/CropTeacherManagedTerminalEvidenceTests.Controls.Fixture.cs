using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedTerminalEvidenceTests
{
    private static readonly Lazy<Task<Golden>> ControlGolden = new(() => CreateControlGolden(false));
    private static readonly Lazy<Task<Golden>> CloseControlGolden = new(() => CreateControlGolden(true));
    private static readonly Lazy<Task<Golden>> PlainNativeGolden = new(() => CreateControlGolden(false, includeControls: false));

    private static async Task<Golden> CreateControlGolden(bool closeFirst, bool includeControls = true, bool nativeWireSurface = false)
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture(nativeTerminalSurface: true, initialSafeMenu: closeFirst,
            nativeWireSurface: nativeWireSurface);
        var production = await f.Build();
        var retentionPath = Assert.Single(production.OutputRetentionProofPaths);
        var retention = Read<MoneyRevenueCropOutputRetentionProof>(retentionPath);
        var current = Read<SnapshotEnvelope>(retention.AfterSnapshotPath);
        var item = current.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray()
            .Single(row => row.GetProperty("qualified_item_id").GetString() == retention.HarvestItemQualifiedId);
        var bin = current.State["farm"].GetProperty("shipping_bins").GetProperty("value").EnumerateArray().Single();
        var target = new CropTeacherTerminalShippingTarget(retentionPath, Hash(retentionPath), current.SaveId.Value!, current.PlayerId.Value!,
            current.GameVersion, retention.HarvestTotalDay, retention.HarvestItemQualifiedId, item.GetProperty("slot_index").GetInt32(),
            item.GetProperty("quality").GetInt32(), item.GetProperty("sell_to_store_price").GetInt32(),
            bin.GetProperty("tile_x").GetInt32(), bin.GetProperty("tile_y").GetInt32(),
            bin.GetProperty("interaction_stand_tile_x").GetInt32(), bin.GetProperty("interaction_stand_tile_y").GetInt32(),
            item.GetProperty("stack").GetInt32(), retention.MinimumUnits, retention.MinimumUnits)
        { NativeVariants = new("crop_teacher_native_terminal_variants.v1", bin.GetProperty("bin_scope").GetString()!,
            new(item.GetProperty("slot_index").GetInt32(), item.GetProperty("quality").GetInt32(),
                item.GetProperty("sell_to_store_price").GetInt32(), item.GetProperty("stack").GetInt32())) };
        var root = Path.Combine(Path.GetTempPath(), "managed-terminal-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var transitions = f.Transitions.ToList();
        var previous = retention.AfterSnapshotPath;
        var run = nativeWireSurface ? "declared-observed-terminal" : "declared-managed-terminal-controls";
        void Add(ActionQueueEnvelope queue, SnapshotEnvelope after, string candidate, string operation, string kind, string primitive)
        {
            Assert.True(queue.Status == "pending" && queue.Items.All(row => row.Status == "pending"), JsonSerializer.Serialize(queue, JsonOptions));
            var time = JsonNode.Parse(after.State["time"].GetRawText())!;
            time["time"]!["read_at_tick"] = after.GameTick;
            after.State["time"] = JsonSerializer.SerializeToElement(time);
            if (nativeWireSurface)
            {
                CropTeacherNativeSupportFixture.CompleteEnvelopeMetadata(after, "declared.observed.terminal");
                foreach (var domain in after.State.Keys.ToArray())
                {
                    var fields = JsonNode.Parse(after.State[domain].GetRawText())!.AsObject();
                    foreach (var field in fields)
                        if (field.Value is JsonObject envelope && envelope.ContainsKey("status"))
                            envelope["read_at_tick"] = after.GameTick;
                    after.State[domain] = JsonSerializer.SerializeToElement(fields);
                }
            }
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            var prefix = Path.Combine(root, "native-" + transitions.Count);
            var queuePath = prefix + "-queue.json"; var receiptPath = prefix + "-receipt.json"; var afterPath = prefix + "-after.json";
            File.WriteAllText(queuePath, JsonSerializer.Serialize(queue, JsonOptions));
            object facts = new[] { "declared_terminal_control_sequence" };
            if (primitive == "sleep") facts = new[] { new { path = "player.money",
                before = current.State["player"].GetProperty("money").GetProperty("value").GetInt32(),
                after = after.State["player"].GetProperty("money").GetProperty("value").GetInt32() } };
            if (primitive == "ship_inventory_item_to_bin")
            {
                var count = MoneyCropTeacherFixture.Create<int>("MoneyCropInventoryCount", current, target.QualifiedItemId);
                facts = new[]
                {
                    new { path = "player.inventory." + target.QualifiedItemId + ".count", before = count, after = count - 1 },
                    new { path = "farm.shipping_bin." + target.QualifiedItemId + ".count",
                        before = target.RequiredQuantity - count, after = target.RequiredQuantity - count + 1 }
                };
            }
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("MoneyCropQueueReceipt",
                queue, current, after, candidate, primitive, facts);
            receipt.RunId = run;
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, JsonOptions));
            File.WriteAllText(afterPath, JsonSerializer.Serialize(after, JsonOptions));
            transitions.Add(new(transitions.Count + 1, operation, new(kind, target.TotalDay, queuePath, previous,
                receiptPath, afterPath, run, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, candidate)));
            previous = afterPath; current = after;
        }
        void Control(bool close = false)
        {
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(current, JsonOptions), JsonOptions)!;
            ActionQueueEnvelope queue;
            string candidate;
            if (close)
            {
                var menu = JsonNode.Parse(after.State["menus"].GetRawText())!;
                menu["active_menu"]!["value"]!["is_open"] = false;
                menu["active_menu"]!["value"]!["type"] = "none";
                after.State["menus"] = JsonSerializer.SerializeToElement(menu);
                queue = MoneyCropTeacherFixture.Create<ActionQueueEnvelope>("LifecycleQueue", "close-" + transitions.Count,
                    "executor.close_menu", current, new[] { 5 }, "");
                candidate = "declared-safe-close";
            }
            else
            {
                var source = Assert.IsType<EventCandidate>(NativeControlSettleRecovery.TryCreate(current));
                var availability = new OptionAvailabilityEnvelope { StateHash = current.StateHash,
                    Options = [new() { OptionId = "recovery.stabilize_day", Available = true, EventCandidates = [source] }] };
                var ranked = Assert.Single(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "goal.grandpa_max_score_year3"));
                queue = new ActionQueueCompiler().Compile(new DailyPlanCompiler().Compile([ranked], current.StateHash, maxCandidates: 1), current);
                candidate = source.CandidateId;
            }
            after.GameTick = current.GameTick + (close ? 1 : NativeControlSettleRecovery.WaitTicks);
            var player = JsonNode.Parse(after.State["player"].GetRawText())!;
            player["movement_timing_context"]!["read_at_tick"] = after.GameTick;
            after.State["player"] = JsonSerializer.SerializeToElement(player);
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            Add(queue, after, candidate, close ? "control_recovery" : "supporting_route",
                close ? "control_recovery" : "supporting_route_to_sleep", close ? "close_menu" : "wait_ticks");
        }
        if (closeFirst) Control(close: true);
        if (includeControls) Control();
        for (var index = 0; index < target.RequiredQuantity; index++)
        {
            var after = MoneyCropTeacherFixture.CreateSnapshot("MoneyCropShippingDepositAfter", current, target.QualifiedItemId, target.UnitPrice);
            var player = JsonNode.Parse(current.State["player"].GetRawText())!;
            var raw = player["inventory"]!["value"]!.AsArray().OfType<JsonObject>().Single(row => row["slot_index"]!.GetValue<int>() == target.SlotIndex);
            var remaining = raw["stack"]!.GetValue<int>() - 1;
            if (remaining > 0) raw["stack"] = remaining;
            else
            {
                foreach (var key in new[] { "item_id", "qualified_item_id", "display_name", "stack", "quality", "maximum_stack_size",
                             "can_be_shipped", "sell_to_store_price", "sale_price", "runtime_type", "context_tags" })
                    if (raw.ContainsKey(key)) raw[key] = null;
                raw["is_empty"] = true;
            }
            after.State["player"] = JsonSerializer.SerializeToElement(player);
            CropTeacherProductionEvidenceTests.SynchronizeDeclaredTerminalStock(after);
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            var model = nativeWireSurface ? CropTeacherTerminalShipping.Preparation.BuildOriginalAction(current,
                target with { AcceptedDepositCount = index }) : null;
            var queue = model is null ? MoneyCropTeacherFixture.Create<ActionQueueEnvelope>("MoneyCropShippingDepositQueue", current, target.QualifiedItemId,
                target.UnitPrice, "deposit-" + index) : new ActionQueueCompiler().Compile(model, current);
            Add(queue, after, model is null ? "candidate.deposit-" + index : model.ModelOutputId["model-output:".Length..],
                "terminal_shipping", "shipping_deposit", "ship_inventory_item_to_bin");
            if (includeControls) Control();
        }
        var ingress = MoneyCropTeacherFixture.Create<object>("BuildMoneyCropHomeIngressTransition", root, "home", current);
        var ingressType = ingress.GetType();
        var home = (MoneyRevenueTerminalQueueTransitionProof)ingressType.GetProperty("Transition")!.GetValue(ingress)!;
        Add(Read<ActionQueueEnvelope>(home.QueuePath), (SnapshotEnvelope)ingressType.GetProperty("After")!.GetValue(ingress)!,
            home.SelectedCandidateId, "supporting_route", "supporting_route_to_sleep", "traverse_connector");
        if (includeControls) Control();
        Add(MoneyCropTeacherFixture.Create<ActionQueueEnvelope>("MoneyCropShippingSettlementQueue", current),
            MoneyCropTeacherFixture.CreateSnapshot("MoneyCropShippingSettlementAfter", current, (int)target.PendingValueAt(target.RequiredQuantity)),
            "candidate.declared-settlement", "overnight_control", "native_day_rollover", "sleep");
        var hashes = Read<JsonElement>(production.ManifestPath).GetProperty("input_artifact_hashes")
            .EnumerateObject().ToDictionary(row => row.Name, row => row.Value.GetString()!, StringComparer.OrdinalIgnoreCase);
        foreach (var row in transitions)
            foreach (var path in new[] { row.NativeTransition.QueuePath, row.NativeTransition.BeforeSnapshotPath,
                         row.NativeTransition.ExecutionReceiptPath, row.NativeTransition.AfterSnapshotPath }) hashes[path] = Hash(path);
        var ledgerPath = Path.Combine(root, "final-ledger.json");
        var ledger = Read<StrategyCommitmentLedger>(f.LedgerPath); ledger.SourceStateHash = current.StateHash;
        File.WriteAllText(ledgerPath, JsonSerializer.Serialize(ledger, JsonOptions));
        foreach (var path in new[] { f.NativePlanPath, f.LedgerPath, production.ManifestPath, retentionPath, ledgerPath }) hashes[path] = Hash(path);
        return new(f, production, target, transitions.ToArray(), hashes, previous, ledgerPath);
    }
}
