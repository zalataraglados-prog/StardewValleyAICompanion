using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void SaleStockTransferPreflightDoesNotActivateOnAMissingLegacyCollection()
    {
        var fixture = SaleStockTransferFixture(); var ledger = fixture.BaseLedger;
        ledger.MaterialReservations = null!;
        Assert.Empty(CropSaleStockTransferPreflight.Evaluate("executor.transfer_material", [], fixture.Before, ledger));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaleStockTransferPreflightReusesOriginalPreparationAcrossOrdinaryMovementWithoutGrantingDispatch(bool moved)
    {
        var fixture = SaleStockTransferFixture(); var ledger = fixture.PreparedLedger;
        var snapshot = SaleStockTransferPlanningSnapshot(fixture.Before);
        if (moved)
        {
            snapshot.GameTick++;
            var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
            player["tile_y"]!["value"] = 6;
            snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        var parameters = SaleStockTransferParameters(fixture);
        var original = JsonSerializer.Serialize(ledger);
        Assert.Empty(CropSaleStockTransferPreflight.Evaluate("executor.transfer_material", parameters, snapshot, ledger));
        var queue = new ActionQueueCompiler().Compile(SaleStockTransferAction(snapshot, parameters), snapshot, ledger);
        var item = Assert.Single(queue.Items);
        Assert.DoesNotContain(item.BlockingReasons, reason => reason.StartsWith("crop_sale_stock_transfer_", StringComparison.Ordinal));
        // A resource preflight does not invent the original owner's acquisition/runtime control binding.
        var dispatch = new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, snapshot.StateHash);
        Assert.False(dispatch.Ready);
        Assert.Contains("dispatch_material_transfer_preparation_binding_drifted", dispatch.BlockingReasons);
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    [Theory]
    [InlineData("missing_binding")]
    [InlineData("duplicate_binding")]
    [InlineData("wrong_binding")]
    [InlineData("missing_preparation")]
    [InlineData("intent")]
    [InlineData("revision")]
    [InlineData("actor")]
    public void SaleStockTransferFailsAtCandidateAndFreshCompilationBeforeNativeDispatch(string fault)
    {
        var fixture = SaleStockTransferFixture(); var ledger = fixture.PreparedLedger;
        var snapshot = SaleStockTransferPlanningSnapshot(fixture.Before);
        var parameters = SaleStockTransferParameters(fixture);
        if (fault == "missing_binding") parameters = parameters.Where(row => row.Name != PreparedMaterialTransferContract.BindingParameter).ToArray();
        if (fault == "duplicate_binding") parameters = parameters.Append(parameters.Single(row => row.Name == PreparedMaterialTransferContract.BindingParameter)).ToArray();
        if (fault == "wrong_binding") parameters.Single(row => row.Name == PreparedMaterialTransferContract.BindingParameter).Value = new string('a', 64);
        if (fault == "missing_preparation") ledger.PreparedMaterialTransfer = null;
        if (fault == "intent") parameters.Single(row => row.Name == "quantity").Value = "1";
        if (fault == "revision") ledger.Revision++;
        if (fault == "actor") snapshot.SaveId.Value = "other-save";
        var original = JsonSerializer.Serialize(ledger);
        var reasons = CropSaleStockTransferPreflight.Evaluate("executor.transfer_material", parameters, snapshot, ledger);
        Assert.NotEmpty(reasons);
        var queue = new ActionQueueCompiler().Compile(SaleStockTransferAction(snapshot, parameters), snapshot, ledger);
        Assert.All(reasons, reason => Assert.Contains(reason, Assert.Single(queue.Items).BlockingReasons));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { new OptionAvailabilityCandidate
            { OptionId = "inventory.transfer_item", Parameters = parameters } }, commitmentLedger: ledger);
        var candidate = Assert.Single(availability.Options);
        Assert.False(candidate.Available);
        Assert.All(reasons, reason => Assert.Contains(reason, candidate.BlockingReasons));
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    private static SmallModelActionParameter[] SaleStockTransferParameters(PreparedMaterialTransferTestFixture fixture)
    {
        var intent = fixture.PreparedLedger.PreparedMaterialTransfer!.Intent;
        return new[]
        {
            new SmallModelActionParameter { Name = "source_node_id", Value = intent.SourceNodeId },
            new() { Name = "destination_node_id", Value = intent.DestinationNodeId },
            new() { Name = "source_slot_index", Value = intent.SourceSlotIndex.ToString(CultureInfo.InvariantCulture) },
            new() { Name = "qualified_item_id", Value = intent.QualifiedItemId },
            new() { Name = "quality", Value = intent.Quality.ToString(CultureInfo.InvariantCulture) },
            new() { Name = "quantity", Value = intent.Quantity.ToString(CultureInfo.InvariantCulture) },
            new() { Name = "expected_source_stack", Value = intent.ExpectedSourceStack.ToString(CultureInfo.InvariantCulture) },
            new() { Name = "stand_tile_x", Value = "3" }, new() { Name = "stand_tile_y", Value = "5" },
            new() { Name = PreparedMaterialTransferContract.BindingParameter, Value = fixture.PreparedLedger.PreparedMaterialTransfer.BindingSha256 }
        };
    }

    private static SmallModelActionEnvelope SaleStockTransferAction(SnapshotEnvelope source, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "declared-sale-stock-transfer", SourceModel = "declared-fixture", StateHash = source.StateHash,
        GoalId = "earn_money", ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
        Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer),
        Actions = [new() { ActionId = "transfer", OptionId = "executor.transfer_material", Parameters = parameters }]
    };

    private static SnapshotEnvelope SaleStockTransferPlanningSnapshot(SnapshotEnvelope original)
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(original))!;
        object Field<T>(T value) => new { status = "available", value, read_at_tick = snapshot.GameTick, confidence = 1d,
            source = new { kind = "test", path = "declared-sale-stock-transfer" }, adapter = "test" };
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["location_id"] = JsonSerializer.SerializeToNode(Field("Farm"));
        player["tile_x"] = JsonSerializer.SerializeToNode(Field(3)); player["tile_y"] = JsonSerializer.SerializeToNode(Field(5));
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        farm["material_inventory_graph"] = JsonSerializer.SerializeToNode(Field(farm["material_inventory_graph"]!["value"]!.DeepClone()));
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        snapshot.State["current_location"] = JsonSerializer.SerializeToElement(new { map = Field(new { location_id = "Farm", width = 80, height = 65 }) });
        snapshot.State["locations"] = JsonSerializer.SerializeToElement(new
        {
            collision_grid = Field(new { location_id = "Farm", width = 80, height = 65, notable_tiles = Array.Empty<object>() }),
            route_action_branch_coverage = Field(new { rows = Array.Empty<object>() })
        });
        snapshot.State["menus"] = JsonSerializer.SerializeToElement(new { active_menu = Field(new { is_open = false, type = "none" }) });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
