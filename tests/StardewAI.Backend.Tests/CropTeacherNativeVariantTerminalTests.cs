using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Declared synthetic native protocol fixtures, never a live shipping or whole-cycle claim.
    [Fact]
    public async Task NativeVariantTerminalShipsFiveNormalAndOneSilverThroughOriginalCompilerAndOwner()
    {
        using var f = new NativeVariantTerminalFixture();
        await using var owner = await f.Shared.Files.Open();
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(f.Target, JsonOptions));
        Assert.NotNull(document.RootElement.GetProperty("nativeVariants"));
        Assert.Equal(f.Target, JsonSerializer.Deserialize<CropTeacherTerminalShippingTarget>(document.RootElement.GetRawText(), JsonOptions));
        var source = f.Shared.Source.Before;
        for (var index = 0; index < 6; index++)
        {
            var target = f.Target with { AcceptedDepositCount = index };
            var model = CropTeacherTerminalShipping.Preparation.BuildOriginalAction(source, target);
            var action = Assert.Single(model.Actions);
            Assert.Contains(action.Parameters, row => row.Name == "slot_index" && row.Value == (index == 5 ? f.SilverSlot : f.NormalSlot).ToString());
            Assert.Contains(action.Parameters, row => row.Name == "expected_unit_price" && row.Value == (index == 5 ? "218" : "175"));
            var queue = new ActionQueueCompiler().Compile(model, source, ReadLedger(owner.CurrentLedgerPath));
            var prefix = Path.Combine(f.Root, "variant-unit-" + index);
            var modelPath = prefix + "-model.json"; var queuePath = prefix + "-queue.json";
            var receiptPath = prefix + "-receipt.json"; var afterPath = prefix + "-after.json";
            Fixture.Save(modelPath, model); Fixture.Save(queuePath, queue);
            var after = f.Deposit(source, index);
            var candidate = model.ModelOutputId["model-output:".Length..];
            var receipt = f.Receipt(queue, source, after, candidate, index);
            var operation = await owner.CompileAndAdmitOriginalTerminalAsync(target, () => Task.FromResult(
                new CropTeacherOriginalTerminalCompilation(modelPath, queuePath, receiptPath, afterPath,
                    Path.Combine(f.Shared.Files.Native, "variant-" + index))));
            await owner.ExecuteOriginalTerminalAsync(operation, f.Shared.Runtime.Client, () =>
            {
                Fixture.Save(receiptPath, receipt); Fixture.Save(afterPath, after);
                f.Shared.Runtime.Store.Snapshots[after.StateHash] = after;
                return Task.FromResult(new JsonObject { ["execution_path"] = receiptPath, ["after_snapshot_path"] = afterPath });
            });
            await owner.RunTerminalShippingAsync(operation, f.Shared.Runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions,
                    diagnostic, request), f.Shared.Files.Post);
            Assert.Equal(index + 1, owner.AcceptedTerminalDepositCount);
            Assert.Equal(index + 1, f.Shared.Runtime.Handler.Commits);
            var accepted = OwnedControlAccepted(f.Shared, operation);
            Assert.Equal(target.PendingValueAt(index + 1), accepted["terminal_pending_value_after"]!.GetValue<long>());
            Assert.False(accepted["terminal_settlement_verified"]!.GetValue<bool>());
            Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
            source = after;
        }
        Assert.Equal(1093, f.Target.PendingValueAt(6));
        f.Target.VerifyNativeVariantSource(source, 6, 1050);
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(6, reopened.AcceptedTerminalDepositCount);
        Assert.Equal(6, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("quality")]
    [InlineData("quantity")]
    [InlineData("actor")]
    [InlineData("day")]
    [InlineData("bin")]
    [InlineData("cursor")]
    public void NativeVariantTerminalRejectsDriftFromPinnedWholePrefix(string fault)
    {
        using var f = new NativeVariantTerminalFixture();
        var source = NativeVariantTerminalFixture.Copy(f.Shared.Source.Before);
        if (fault is "price" or "quality" or "quantity") ChangeState(source, "player", node =>
        {
            var row = node["inventory"]!["value"]!.AsArray().Single(row => row!["slot_index"]!.GetValue<int>() == f.SilverSlot)!;
            row[fault == "price" ? "sell_to_store_price" : fault == "quality" ? "quality" : "stack"] = 999;
        });
        if (fault == "actor") source.PlayerId.Value += ":foreign";
        if (fault == "day") ChangeState(source, "time", node => node["total_days"]!["value"] = f.Target.TotalDay + 1);
        if (fault == "bin") ChangeState(source, "farm", node => node["shipping_bins"]!["value"]![0]!["player_id"] = "foreign");
        Assert.Throws<InvalidDataException>(() => f.Target.VerifyNativeVariantSource(source, fault == "cursor" ? 1 : 0, 1050));
    }

    [Fact]
    public void NativeVariantTerminalOldDefaultRejectsMarkedTargetAndLegacyByteShapeStaysUnmarked()
    {
        using var f = new TerminalOutcomeFixture();
        var legacy = JsonSerializer.Serialize(f.Target, JsonOptions);
        Assert.DoesNotContain("nativeVariants", legacy, StringComparison.Ordinal);
        var marked = MarkTerminalVariant(f.Target, f.Before);
        Assert.Throws<InvalidDataException>(() => f.Prepare(marked));
        Assert.Equal(f.Target, JsonSerializer.Deserialize<CropTeacherTerminalShippingTarget>(legacy, JsonOptions));
    }

    [Theory]
    [InlineData("native_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    [InlineData("unknown_native")]
    public async Task NativeVariantTerminalRetainsOneClaimAcrossMissingAcknowledgementAndReopen(string mode)
    {
        using var f = new TerminalOutcomeFixture();
        var target = MarkTerminalVariant(f.Target, f.Before);
        var loseWrite = true;
        await using var owner = await f.Shared.Files.Open(writer: async (path, json) =>
        {
            if (mode == "marker_reply_lost" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Declared missing marker reply."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "acceptance_ack_lost" && loseWrite && Path.GetFileName(path) == "operation-000001-accepted.json")
            { loseWrite = false; throw new IOException("Declared missing acceptance acknowledgement."); }
        });
        var operation = await owner.CompileAndAdmitOriginalTerminalAsync(target, () => Task.FromResult(
            new CropTeacherOriginalTerminalCompilation(f.ModelPath, f.QueuePath, f.Shared.ReceiptPath, f.Shared.AfterPath, f.Shared.Files.Native)));
        var calls = 0;
        Task<JsonObject> Native()
        {
            calls++;
            if (mode == "unknown_native") throw new IOException("Unknown dispatch result.");
            var value = EmitTerminal(f);
            if (mode == "native_ack_lost") throw new IOException("Declared missing native reply.");
            return Task.FromResult(value);
        }
        if (mode is "native_ack_lost" or "unknown_native")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalTerminalAsync(operation, f.Shared.Runtime.Client, Native));
        else
        {
            await owner.ExecuteOriginalTerminalAsync(operation, f.Shared.Runtime.Client, Native);
            await Assert.ThrowsAsync<IOException>(() => RebindTerminal(owner, operation, f));
        }
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        if (mode == "unknown_native")
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalTerminalAsync(reopened.Pending!, f.Shared.Runtime.Client, Native));
            Assert.Equal(0, reopened.AcceptedTerminalDepositCount); Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        }
        else
        {
            if (mode == "acceptance_ack_lost") await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
            else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
            else await RebindTerminal(reopened, reopened.Pending!, f);
            Assert.Equal(1, reopened.AcceptedTerminalDepositCount); Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
            await Assert.ThrowsAsync<InvalidDataException>(() => reopened.CompileAndAdmitOriginalTerminalAsync(target,
                () => throw new InvalidOperationException("Replayed target must not compile.")));
        }
        Assert.Equal(1, calls);
    }

    private static CropTeacherTerminalShippingTarget MarkTerminalVariant(CropTeacherTerminalShippingTarget target, SnapshotEnvelope initial) =>
        target with { NativeVariants = new("crop_teacher_native_terminal_variants.v1",
            initial.State["farm"].GetProperty("shipping_bins").GetProperty("value")[0].GetProperty("bin_scope").GetString()!,
            new(target.SlotIndex, target.Quality, target.UnitPrice, target.InitialQuantity)) };
    private static StardewAI.Contracts.Strategy.StrategyCommitmentLedger ReadLedger(string path) =>
        JsonSerializer.Deserialize<StardewAI.Contracts.Strategy.StrategyCommitmentLedger>(File.ReadAllText(path), JsonOptions)!;

    private sealed class NativeVariantTerminalFixture : IDisposable
    {
        private readonly ShippingResourcesFixture resources = new(stack: 6);
        internal OwnedRouteFixture Shared { get; }
        internal int NormalSlot => resources.Index;
        internal int SilverSlot { get; }
        internal string Root => Shared.Runtime.LiveOptions.SnapshotDir;
        internal CropTeacherTerminalShippingTarget Target { get; }

        internal NativeVariantTerminalFixture()
        {
            SilverSlot = resources.Before.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray()
                .Max(row => row.GetProperty("slot_index").GetInt32()) + 1;
            Shared = new(prepareSource: source =>
            {
                foreach (var field in resources.Before.State) source.Before.State[field.Key] = field.Value.Clone();
                ChangeState(source.Before, "player", node =>
                {
                    node["tile_x"]!["value"] = 4; node["tile_y"]!["value"] = 4;
                    var inventory = node["inventory"]!["value"]!.AsArray();
                    var row = inventory.Single(row => row!["slot_index"]!.GetValue<int>() == NormalSlot)!;
                    row["stack"] = 5; row["sell_to_store_price"] = 175; row["can_be_shipped"] = true;
                    var silver = row.DeepClone(); silver["slot_index"] = SilverSlot; silver["stack"] = 1;
                    silver["quality"] = 1; silver["sell_to_store_price"] = 218; inventory.Add(silver);
                    var capacity = node["inventory_capacity"]!["value"]!;
                    capacity["occupied_item_stacks"] = capacity["occupied_item_stacks"]!.GetValue<int>() + 1;
                    capacity["empty_slots"] = capacity["empty_slots"]!.GetValue<int>() - 1;
                });
                ChangeState(source.Before, "farm", node =>
                {
                    var graph = node["material_inventory_graph"]!["value"]!;
                    graph["physical_inventory_count"] = graph["inventory_nodes"]!.AsArray().Count;
                    var slots = graph["inventory_nodes"]![0]!["slots"]!.AsArray();
                    var slot = slots.Single(row => row!["slot_index"]!.GetValue<int>() == NormalSlot)!;
                    slot["stack"] = 5;
                    var silver = slot.DeepClone(); silver["slot_index"] = SilverSlot; silver["stack"] = 1; silver["quality"] = 1; slots.Add(silver);
                    foreach (var bin in node["shipping_bins"]!["value"]!.AsArray())
                    { bin!["interaction_stand_tile_x"] = 4; bin["interaction_stand_tile_y"] = 4; bin["interaction_stand_tile_blocked_reason"] = ""; }
                });
            });
            var initialPath = Path.Combine(Root, "mixed-retained-source.json"); Fixture.Save(initialPath, Shared.Source.Before);
            var proofPath = Path.Combine(Root, "mixed-retention.json");
            var sourceSnapshot = Shared.Source.Before;
            var day = sourceSnapshot.State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
            Fixture.Save(proofPath, new MoneyRevenueCropOutputRetentionProof
            {
                Status = "verified_native_crop_output_retention", NativeOutputRetentionVerified = true, CommitmentId = Id,
                HarvestItemQualifiedId = resources.Item, MinimumUnits = 6, MinimumGrossRevenue = 1050, HarvestTotalDay = day,
                CropProductionPlanPath = Shared.Inputs.Request.CropProductionPlanPath,
                CropProductionPlanSha256 = Hash(Shared.Inputs.Request.CropProductionPlanPath),
                AfterSnapshotPath = initialPath, AfterSnapshotSha256 = Hash(initialPath), AfterStateHash = sourceSnapshot.StateHash, InventoryQuantityAfter = 6
            });
            Target = new(proofPath, Hash(proofPath), sourceSnapshot.SaveId.Value!, sourceSnapshot.PlayerId.Value!, sourceSnapshot.GameVersion,
                day, resources.Item, NormalSlot, 0, 175, 71, 14, 4, 4, 5, 6, 0)
            { NativeVariants = new("crop_teacher_native_terminal_variants.v1", "shared", new(NormalSlot, 0, 175, 5, new(SilverSlot, 1, 218, 1))) };
        }

        internal SnapshotEnvelope Deposit(SnapshotEnvelope source, int index)
        {
            var after = Copy(source); after.GameTick++;
            var selected = Target.VariantAt(index);
            ChangeState(after, "player", node =>
            {
                var row = node["inventory"]!["value"]!.AsArray().Single(row => row!["slot_index"]!.GetValue<int>() == selected.SlotIndex)!.AsObject();
                var stack = row["stack"]!.GetValue<int>();
                if (stack > 1) row["stack"] = stack - 1;
                else
                {
                    foreach (var key in row.Select(pair => pair.Key).ToArray())
                        if (key is not "slot_index" and not "shipping_unknown") row[key] = null;
                    foreach (var key in new[] { "is_object", "is_furniture", "is_wallpaper", "protected_from_auto_sell" })
                        if (row.ContainsKey(key)) row[key] = false;
                    if (row.ContainsKey("auto_sell_protection_reasons")) row["auto_sell_protection_reasons"] = new JsonArray();
                    if (row.ContainsKey("donate_color_context")) row["donate_color_context"] = new JsonObject {
                        ["is_colored_object"] = false, ["preserved_parent_item_id"] = null,
                        ["preserved_parent_base_context_tags"] = new JsonArray(), ["projection_status"] = "not_applicable_not_colored_object" };
                    row["is_empty"] = true;
                    var capacity = node["inventory_capacity"]!["value"]!;
                    capacity["occupied_item_stacks"] = capacity["occupied_item_stacks"]!.GetValue<int>() - 1;
                    capacity["empty_slots"] = capacity["empty_slots"]!.GetValue<int>() + 1; capacity["has_empty_slot"] = true;
                }
            });
            ChangeState(after, "farm", node =>
            {
                var graph = node["material_inventory_graph"]!["value"]!;
                var slots = graph["inventory_nodes"]![0]!["slots"]!.AsArray();
                var slot = slots.Single(row => row!["slot_index"]!.GetValue<int>() == selected.SlotIndex)!;
                if (slot["stack"]!.GetValue<int>() == 1) slots.Remove(slot); else slot["stack"] = slot["stack"]!.GetValue<int>() - 1;
                var quantities = graph["quantity_rows"]!.AsArray();
                var quantity = quantities.Single(row => row!["qualified_item_id"]!.GetValue<string>() == resources.Item)!;
                if (index == 5) quantities.Remove(quantity); else quantity["available_quantity"] = 5 - index;
                foreach (var bin in node["shipping_bins"]!["value"]!.AsArray())
                {
                    var count = Math.Min(index + 1, 5); var silver = index == 5;
                    var contents = new JsonArray(new JsonObject { ["item_id"] = resources.Item[3..], ["qualified_item_id"] = resources.Item,
                        ["quality"] = 0, ["count"] = count, ["unit_sell_to_store_price"] = 175, ["total_sell_to_store_price"] = count * 175 });
                    if (silver) contents.Add(new JsonObject { ["item_id"] = resources.Item[3..], ["qualified_item_id"] = resources.Item,
                        ["quality"] = 1, ["count"] = 1, ["unit_sell_to_store_price"] = 218, ["total_sell_to_store_price"] = 218 });
                    bin!["contents"] = contents; bin["contents_total_count"] = index + 1; bin["contents_distinct_item_count"] = 1;
                    bin["contents_distinct_settlement_variant_count"] = silver ? 2 : 1; bin["pending_settlement_value"] = Target.PendingValueAt(index + 1);
                    bin["contents_signature"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                        $"{resources.Item}|0|175|{count}\n" + (silver ? $"{resources.Item}|1|218|1\n" : "")))).ToLowerInvariant();
                }
            });
            after.StateHash = SnapshotHash.ComputeStateHash(after.State); return after;
        }

        internal QueueExecutionReceiptEnvelope Receipt(ActionQueueEnvelope queue, SnapshotEnvelope before, SnapshotEnvelope after, string candidate, int index)
        {
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("MoneyCropQueueReceipt", queue, before, after,
                candidate, "ship_inventory_item_to_bin", new[] {
                    new { path = "player.inventory." + resources.Item + ".count", before = 6 - index, after = 5 - index },
                    new { path = "farm.shipping_bin." + resources.Item + ".count", before = index, after = index + 1 } });
            receipt.RunId = Shared.Files.Options.RunId; return receipt;
        }
        internal static SnapshotEnvelope Copy(SnapshotEnvelope value) => JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        public void Dispose() { Shared.Dispose(); resources.Dispose(); }
    }
}
