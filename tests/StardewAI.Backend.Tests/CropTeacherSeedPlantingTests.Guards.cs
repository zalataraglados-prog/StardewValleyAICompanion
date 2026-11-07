using System.Text.Json.Nodes;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Theory]
    [InlineData("tile")]
    [InlineData("duplicate_tile")]
    [InlineData("claim")]
    [InlineData("seed")]
    [InlineData("count")]
    [InlineData("location")]
    public void InvalidOriginalBatchIsExcludedBeforeCompilation(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var batch = f.Batch(fault == "count" ? 9 : 8);
        if (fault == "tile") batch.Tiles[0] = new(79, 60);
        if (fault == "duplicate_tile") batch.Tiles[1] = batch.Tiles[0];
        if (fault == "seed") batch = batch with { SeedQualifiedItemId = "(O)472" };
        if (fault == "location") batch = batch with { LocationId = "Town" };
        Assert.Throws<InvalidDataException>(() => marker.BuildActions(batch, fault == "claim" ? "other-seeds" : f.OwnedSlotOne));
    }

    [Fact]
    public void InputArtifactsAreRecheckedBeforeDispatch()
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var queue = f.Compile(marker, marker.BuildActions(f.Batch(), f.OwnedSlotOne));
        Fixture.Save(f.QueuePath, queue);
        File.AppendAllText(f.SourcePath, " ");
        Assert.Throws<InvalidDataException>(() => marker.ValidateQueue(f.QueuePath));
    }

    [Theory]
    [InlineData("raw_quality_type")]
    [InlineData("raw_quality_missing")]
    [InlineData("raw_status")]
    [InlineData("graph_owner")]
    [InlineData("graph_quality_type")]
    public void SelectedNativeSlotRequiresReadableExactTypedGraphAndInventory(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var actions = marker.BuildActions(f.Batch(), f.OwnedSlotOne);
        var player = JsonNode.Parse(f.Before.State["player"].GetRawText())!.AsObject();
        var slot = player["inventory"]!["value"]!.AsArray().Single(row => row!["slot_index"]!.GetValue<int>() == 1)!.AsObject();
        if (fault == "raw_quality_type") slot["quality"] = "zero";
        if (fault == "raw_quality_missing") slot.Remove("quality");
        if (fault == "raw_status") player["inventory"]!["status"] = "unknown";
        f.Before.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
        var farm = JsonNode.Parse(f.Before.State["farm"].GetRawText())!.AsObject();
        var node = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!;
        if (fault == "graph_owner") node["owner_player_id"] = 999;
        if (fault == "graph_quality_type") node["slots"]![1]!["quality"] = "zero";
        f.Before.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
        Assert.Equal("blocked", f.Compile(marker, actions).Status);
    }

    [Theory]
    [InlineData("other_claim")]
    [InlineData("offset")]
    [InlineData("missing_version")]
    [InlineData("duplicate_quality")]
    [InlineData("strip_to_quality_case")]
    [InlineData("wrong_goal")]
    public void OriginalCompilerRejectsOwnedBindingDrift(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var actions = marker.BuildActions(f.Batch(), f.OwnedSlotOne);
        var parameters = actions[0].Parameters;
        if (fault == "other_claim") parameters.Single(row => row.Name == "crop_seed_planting_reservation_id").Value = "other-seeds";
        if (fault == "offset") parameters.Single(row => row.Name == "crop_seed_planting_slot_offset").Value = "8";
        if (fault == "missing_version") actions[0].Parameters = parameters.Where(row => row.Name != "crop_seed_planting_version").ToArray();
        if (fault == "duplicate_quality") actions[0].Parameters = parameters.Append(new() { Name = "seed_inventory_quality", Value = "0" }).ToArray();
        if (fault == "strip_to_quality_case")
        {
            actions[0].Parameters = parameters.Where(row => !row.Name.StartsWith("crop_seed_planting_", StringComparison.Ordinal)).ToArray();
            actions[0].Parameters.Single(row => row.Name == "seed_inventory_quality").Name = "SEED_INVENTORY_QUALITY";
        }
        var queue = f.Compile(marker, actions, fault == "wrong_goal" ? "daily.closed_loop" : "earn_money");
        Assert.Equal("blocked", queue.Status);
        Assert.Contains("crop_seed_planting_source_binding_invalid", queue.Items[0].BlockingReasons);
    }

    [Theory]
    [InlineData("reorder")]
    [InlineData("duplicate")]
    [InlineData("strip")]
    [InlineData("extra_command")]
    [InlineData("target")]
    public void MarkerRejectsWholeQueueDowngradeOrOverconsumption(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var queue = f.Compile(marker, marker.BuildActions(f.Batch(), f.OwnedSlotOne));
        if (fault == "reorder") Array.Reverse(queue.Items);
        if (fault == "duplicate") queue.Items[1] = queue.Items[0];
        if (fault == "strip") queue.Items[0].NormalizedCommand.Parameters = queue.Items[0].NormalizedCommand.Parameters.Where(row =>
            !row.Name.StartsWith("crop_seed_planting_", StringComparison.Ordinal) && row.Name != "seed_inventory_quality").ToArray();
        if (fault == "extra_command") queue.Items[0].NormalizedCommand.Parameters = queue.Items[0].NormalizedCommand.Parameters.Append(new()
            { Name = "execution_option_id", Value = "executor.sell_shop_item" }).ToArray();
        if (fault == "target") queue.Items[0].NormalizedCommand.Parameters.Single(row => row.Name == "target_tile_x").Value = "9";
        Fixture.Save(f.QueuePath, queue);
        Assert.Throws<InvalidDataException>(() => marker.ValidateQueue(f.QueuePath));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("revision")]
    [InlineData("quantity")]
    [InlineData("state")]
    public void OriginalDispatcherRejectsChangedLedgerOrSource(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var queue = f.Compile(marker, marker.BuildActions(f.Batch(), f.OwnedSlotOne));
        var ledger = marker.Ledger;
        var claim = ledger.MaterialReservations.Single(row => row.ReservationId == f.OwnedSlotOne);
        if (fault == "owner") claim.SourceDecisionId = "other-root";
        if (fault == "revision") ledger.Revision++;
        if (fault == "quantity") claim.Quantity = 7;
        var state = fault == "state" ? "other-state" : f.Before.StateHash;
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(queue, queue.Items[0], ledger, state).Ready);
    }
}
