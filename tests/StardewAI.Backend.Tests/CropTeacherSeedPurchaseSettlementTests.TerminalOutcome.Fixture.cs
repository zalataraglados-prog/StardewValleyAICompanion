using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Declared leaf certificate and receipts only: not whole-wave, owner, or native acceptance.
    private sealed class TerminalOutcomeFixture : IDisposable
    {
        private readonly ShippingResourcesFixture resources;
        internal OwnedRouteFixture Shared { get; }
        internal SnapshotEnvelope Before => Shared.Source.Before;
        internal SnapshotEnvelope After { get; }
        internal CropTeacherTerminalShippingTarget Target { get; }
        internal SmallModelActionEnvelope Model { get; }
        internal ActionQueueEnvelope Queue { get; }
        internal string Root => Shared.Runtime.LiveOptions.SnapshotDir;
        internal string ModelPath => Path.Combine(Root, "terminal-model.json");
        internal string QueuePath => Path.Combine(Root, "terminal-queue.json");
        internal string AfterPath => Path.Combine(Root, "terminal-after.json");
        internal string ReceiptPath => Path.Combine(Root, "terminal-receipt.json");
        internal string InitialPath => Path.Combine(Root, "terminal-initial.json");
        internal string ProofPath => Path.Combine(Root, "terminal-retention.json");
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;
        private readonly bool approach;
        private readonly int cursor;
        private readonly int quantity;
        internal string Candidate => FormattableString.Invariant($"ship:Farm:5,4:{resources.Index}:{resources.Item}") +
            (approach ? ":approach" : ":deposit:" + (cursor + 1).ToString(CultureInfo.InvariantCulture));

        internal TerminalOutcomeFixture(bool approach = false, int cursor = 0, int quantity = 2)
        {
            this.approach = approach; this.cursor = cursor; this.quantity = quantity;
            resources = new(stack: quantity, bankCount: cursor);
            Shared = new(prepareSource: source =>
            {
                foreach (var field in resources.Before.State) source.Before.State[field.Key] = field.Value.Clone();
                Geometry(source.Before, approach, resources.Index);
            });
            After = approach ? Copy(Before) : Copy(resources.After);
            Geometry(After, false, resources.Index); After.GameTick = Before.GameTick + 1;
            After.StateHash = SnapshotHash.ComputeStateHash(After.State);
            var initial = Copy(Before);
            ChangeState(initial, "player", node => node["inventory"]!["value"]!.AsArray()
                .Single(row => row!["slot_index"]!.GetValue<int>() == resources.Index)!["stack"] = quantity + cursor);
            ChangeState(initial, "farm", node =>
            {
                foreach (var bin in node["shipping_bins"]!["value"]!.AsArray())
                {
                    bin!["contents"] = new JsonArray(); bin["contents_total_count"] = 0; bin["contents_distinct_item_count"] = 0;
                    bin["contents_distinct_settlement_variant_count"] = 0; bin["pending_settlement_value"] = 0;
                    bin["contents_signature"] = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant();
                }
                var graph = node["material_inventory_graph"]!["value"]!;
                graph["inventory_nodes"]![0]!["slots"]!.AsArray().Single(row => row!["slot_index"]!.GetValue<int>() == resources.Index)!["stack"] = quantity + cursor;
                graph["quantity_rows"]!.AsArray().Single(row => row!["qualified_item_id"]!.GetValue<string>() == resources.Item)!["available_quantity"] = quantity + cursor;
            });
            Fixture.Save(InitialPath, initial);
            Fixture.Save(ProofPath, new MoneyRevenueCropOutputRetentionProof
            {
                Status = "verified_native_crop_output_retention", NativeOutputRetentionVerified = true,
                CommitmentId = Id, HarvestItemQualifiedId = resources.Item, MinimumUnits = Math.Min(2, quantity + cursor), MinimumGrossRevenue = 50 * Math.Min(2, quantity + cursor),
                HarvestTotalDay = Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
                CropProductionPlanPath = Shared.Inputs.Request.CropProductionPlanPath,
                CropProductionPlanSha256 = Hash(Shared.Inputs.Request.CropProductionPlanPath),
                AfterSnapshotPath = InitialPath, AfterSnapshotSha256 = Hash(InitialPath), AfterStateHash = initial.StateHash,
                InventoryQuantityAfter = quantity + cursor
            });
            Target = new(ProofPath, Hash(ProofPath), Before.SaveId.Value!, Before.PlayerId.Value!, Before.GameVersion,
                Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(), resources.Item, resources.Index,
                0, 50, 5, 4, 4, 4, quantity + cursor, Math.Min(2, quantity + cursor), cursor);
            var parameters = new List<SmallModelActionParameter>();
            void Add(string name, object value) => parameters.Add(new() { Name = name, Value = Convert.ToString(value, CultureInfo.InvariantCulture)! });
            if (approach) { Add("target_tile_x", 4); Add("target_tile_y", 4); }
            else
            {
                Add("target_location", "Farm"); Add("target_tile_x", 5); Add("target_tile_y", 4);
                Add("stand_tile_x", 4); Add("stand_tile_y", 4); Add("slot_index", resources.Index);
                Add("qualified_item_id", resources.Item); Add("quantity", 1); Add("expected_unit_price", 50);
            }
            Add("continuation.option_id", "executor.ship_inventory_item_to_bin"); Add("continuation.target_location", "Farm");
            Add("continuation.qualified_item_id", resources.Item); Add("continuation.slot_index", resources.Index);
            Add("continuation.quantity", 1); Add("continuation.expected_unit_price", 50); Add("precondition", "candidate_id:" + Candidate);
            Model = new()
            {
                ModelOutputId = "model-output:" + Candidate, SourceModel = "authoritative_crop_terminal_continuation.v1",
                StateHash = Before.StateHash, GoalId = "daily.closed_loop", ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
                Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer),
                Actions = new[] { new SmallModelAction { ActionId = Candidate + ":action", Rationale = "authoritative_crop_terminal_continuation",
                    OptionId = approach ? "executor.move_to_tile" : "executor.ship_inventory_item_to_bin", Parameters = parameters.ToArray() } }
            };
            Queue = new ActionQueueCompiler().Compile(Model, Before, Shared.Source.Ledger);
            Fixture.Save(ModelPath, Model); Fixture.Save(QueuePath, Queue);
            SaveNative();
        }

        internal CropTeacherTerminalShipping.Preparation Prepare(CropTeacherTerminalShippingTarget? target = null) =>
            CropTeacherTerminalShipping.Preparation.Open(Shared.Inputs.Plan.RequestPath, Shared.Source.SourcePath,
                Shared.Source.LedgerPath, Shared.Inputs.Plan.CoveredCommitmentIds, "terminal-leaf.original", target ?? Target, ModelPath, QueuePath);

        internal void SaveNative()
        {
            After.StateHash = SnapshotHash.ComputeStateHash(After.State);
            Fixture.Save(AfterPath, After);
            var receipt = approach
                ? MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", Queue, Before, After,
                    Shared.Files.Options.RunId, Candidate, "move_to_tile")
                : MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("MoneyCropQueueReceipt", Queue, Before, After,
                Candidate, approach ? "move_to_tile" : "ship_inventory_item_to_bin", approach ? Array.Empty<object>() : new object[] {
                    new { path = "player.inventory." + resources.Item + ".count", before = quantity, after = quantity - 1 },
                    new { path = "farm.shipping_bin." + resources.Item + ".count", before = cursor, after = cursor + 1 } });
            receipt.RunId = Shared.Files.Options.RunId;
            Fixture.Save(ReceiptPath, receipt);
            Transition = new(approach ? "shipping_approach" : "shipping_deposit", Target.TotalDay, QueuePath, Shared.Source.SourcePath,
                ReceiptPath, AfterPath, receipt.RunId, Shared.Files.Options.ExecutorVersion, Candidate);
        }

        internal SnapshotEnvelope DepositAfter(SnapshotEnvelope current)
        {
            var after = Copy(resources.After);
            Geometry(after, false, resources.Index);
            after.GameTick = current.GameTick + 1;
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            return after;
        }

        private static void Geometry(SnapshotEnvelope snapshot, bool approaching, int slotIndex)
        {
            ChangeState(snapshot, "player", node =>
            {
                node["tile_x"]!["value"] = approaching ? 0 : 4; node["tile_y"]!["value"] = approaching ? 0 : 4;
                var row = node["inventory"]!["value"]!.AsArray().Single(row => row!["slot_index"]!.GetValue<int>() == slotIndex)!;
                row["can_be_shipped"] = row["is_empty"]?.GetValue<bool>() == true ? null : JsonValue.Create(true);
            });
            ChangeState(snapshot, "farm", node =>
            {
                // ShippingResourcesFixture adds a retained foreign node; full source admission
                // also requires the graph's declared physical denominator to cover that node.
                var graph = node["material_inventory_graph"]!["value"]!;
                graph["physical_inventory_count"] = graph["inventory_nodes"]!.AsArray().Count;
                var bins = node["shipping_bins"]!["value"]!.AsArray();
                bins[0]!["tile_x"] = 5; bins[0]!["tile_y"] = 4;
                bins[0]!["interaction_stand_tile_x"] = 4; bins[0]!["interaction_stand_tile_y"] = 4;
                bins[0]!["interaction_stand_tile_blocked_reason"] = "";
                bins[1]!["tile_x"] = 10; bins[1]!["tile_y"] = 8;
            });
        }
        private static SnapshotEnvelope Copy(SnapshotEnvelope snapshot) => JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        public void Dispose() { Shared.Dispose(); resources.Dispose(); }
    }
}
