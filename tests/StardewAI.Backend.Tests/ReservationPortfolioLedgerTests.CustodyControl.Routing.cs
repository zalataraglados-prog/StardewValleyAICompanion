using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void CustodyControlApproachReusesOneOriginalConnectorWithoutPreparingOrTransferring()
    {
        var fixture = RemoteCustodyFixture();
        var original = JsonSerializer.Serialize(fixture.Ledger);
        var result = MuseumOutputCustodyTransferRequestBuilder.BuildApproach(fixture.Snapshot, fixture.Ledger,
            fixture.CustodyId, "player:123");
        Assert.True(result.Errors.Length == 0, string.Join("\n", result.Errors));
        Assert.NotNull(result.Queue);
        Assert.Contains(result.Candidate!.Parameters, row => row.Name == "expected_target_location" && row.Value == "Farm");
        Assert.DoesNotContain(result.Candidate.Parameters, row => row.Name == "continuation.option_id");
        Assert.DoesNotContain(result.Queue.Items, row => row.OptionId == "executor.transfer_material");
        Assert.Contains(result.Queue.Items, row => row.OptionId == "executor.traverse_connector");
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Ledger));
        Assert.Null(fixture.Ledger.PreparedMaterialTransfer);
        Assert.Null(MuseumOutputCustodyTransferRequestBuilder.Build(fixture.Snapshot, fixture.Ledger,
            fixture.CustodyId, "player:123").Request);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(MuseumOutputCustodyTransferRequestBuilder.BuildApproach(
            fixture.Snapshot, fixture.Ledger, fixture.CustodyId, "player:123")));
    }

    [Theory]
    [InlineData("route")]
    [InlineData("connector")]
    [InlineData("menu")]
    [InlineData("access")]
    [InlineData("position")]
    [InlineData("clock")]
    [InlineData("source-stack")]
    public void CustodyControlApproachRejectsUnreadableOrUnauthorizedSource(string mutation)
    {
        var fixture = RemoteCustodyFixture();
        var snapshot = fixture.Snapshot;
        var locations = JsonSerializer.SerializeToNode(snapshot.State["locations"])!;
        switch (mutation)
        {
            case "route": locations["route_graph"]!["status"] = "unavailable"; break;
            case "connector": locations["route_connectors"]!["value"]!["connectors"] = new JsonArray(); break;
            case "menu": snapshot.State["menus"] = JsonSerializer.SerializeToElement(new { active_menu = Field(new { is_open = true, type = "ItemGrabMenu" }) }); break;
            case "access":
            case "source-stack":
                Assert.True(MaterialInventoryGraphReader.TryRead(snapshot, out var graph));
                if (mutation == "access") graph!.AccessPoints.Single(row => row.NodeId == CustodyTransferFixture.Chest).ActorUseAuthorized = false;
                else graph!.InventoryNodes.Single(row => row.NodeId == CustodyTransferFixture.Chest).Slots.Single(row => row.QualifiedItemId == "(O)96").Stack = 0;
                snapshot = CustodyTransferFixture.WithGraph(snapshot, graph!, snapshot.GameTick + 1);
                break;
            case "position": fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId).SlotIndex = 9; break;
            case "clock": snapshot.State.Remove("time"); break;
        }
        snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var result = MuseumOutputCustodyTransferRequestBuilder.BuildApproach(snapshot, fixture.Ledger, fixture.CustodyId, "player:123");
        Assert.Null(result.Queue);
        Assert.NotEmpty(result.Errors);
        Assert.Null(fixture.Ledger.PreparedMaterialTransfer);
    }

    private static CustodyTransferFixture RemoteCustodyFixture()
    {
        var fixture = new CustodyTransferFixture();
        fixture.Transfer(withdraw: false);
        Assert.True(MaterialInventoryGraphReader.TryRead(fixture.Snapshot, out var graph));
        graph!.InventoryNodes.Single(row => row.NodeId == "player:123").LocationId = "Town";
        fixture.Snapshot = CustodyTransferFixture.WithGraph(fixture.Snapshot, graph, fixture.Snapshot.GameTick + 1);
        var locations = JsonSerializer.SerializeToNode(fixture.Snapshot.State["locations"])!;
        locations["route_action_branch_coverage"] = JsonSerializer.SerializeToNode(Field(new { rows = Array.Empty<object>() }));
        locations["route_graph"] = JsonSerializer.SerializeToNode(Field(new { edges = new[] {
            new { kind = "warp", from_location = "Town", from_x = 9, from_y = 9, target_location = "Farm", target_x = 3, target_y = 4, resolved = true } } }));
        locations["route_connectors"] = JsonSerializer.SerializeToNode(Field(new { location_id = "Town", connectors = new[] {
            new { kind = "warp", tile_x = 9, tile_y = 9, target_location = "Farm", target_x = 3, target_y = 4, resolved = true } } }));
        fixture.Snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        var player = JsonSerializer.SerializeToNode(fixture.Snapshot.State["player"])!;
        player["energy"] = JsonSerializer.SerializeToNode(Field(270));
        fixture.Snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        fixture.Snapshot.StateHash = SnapshotHash.ComputeStateHash(fixture.Snapshot.State);
        return fixture;
    }

    [Theory]
    [InlineData(false, "warp")]
    [InlineData(true, "warp")]
    [InlineData(false, "locked_door_warp")]
    [InlineData(true, "locked_door_warp")]
    public void CustodyControlApproachPreservesOriginalOpeningWindowAndBoundedWait(bool opensLater, string kind)
    {
        var fixture = RemoteCustodyFixture();
        var locations = JsonSerializer.SerializeToNode(fixture.Snapshot.State["locations"])!;
        locations["route_graph"]!["value"]!["edges"]![0]!["kind"] = kind;
        locations["route_connectors"]!["value"]!["connectors"]![0]!["kind"] = kind;
        locations["route_connectors"]!["value"]!["connectors"]![0]!["source_property"] = "Buildings.Action";
        locations["route_gate_context"] = JsonSerializer.SerializeToNode(Field(new { action_gates = new[] {
            new { kind = kind == "warp" ? "ordinary_warp_active_festival" : kind, tile_x = 9, tile_y = 9, target_location = "Farm",
                allowed_now = false, effective_open_time = 900, close_time = 1700, festival_closed = !opensLater } } }));
        fixture.Snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        fixture.Snapshot.StateHash = SnapshotHash.ComputeStateHash(fixture.Snapshot.State);
        var result = MuseumOutputCustodyTransferRequestBuilder.BuildApproach(fixture.Snapshot, fixture.Ledger, fixture.CustodyId, "player:123");
        if (!opensLater) { Assert.Null(result.Queue); Assert.NotEmpty(result.Errors); }
        else
        {
            Assert.True(result.Errors.Length == 0, string.Join(",", result.Errors));
            Assert.All(result.Queue!.Items, row => Assert.Equal("executor.wait_ticks", row.OptionId));
        }
        Assert.Null(fixture.Ledger.PreparedMaterialTransfer);
    }

    private static object Field(object value) => new { value, status = "available", confidence = 1, adapter = "test", read_at_tick = 1,
        source = new { kind = "game_object", path = "synthetic_custody_routing" } };
}
