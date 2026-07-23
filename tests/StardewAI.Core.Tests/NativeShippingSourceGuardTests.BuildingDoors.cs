using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed partial class NativeShippingSourceGuardTests
{
    [Fact]
    public void FarmBuildingsTransparentRowHasDoorTraversalData()
    {
        var source = FarmReadAdapterSources.All;
        var buildingSlice = Slice(source, "private static object ReadBuildingRow(Building building)", "private static object[] ReadShippingBins");
        Assert.Contains("human_door_relative_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("human_door_absolute_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("exterior_entry_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("exterior_stand_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_location_id", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_arrival_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("has_door_access_resolved", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("door_resolution_status", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("source_label", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("Building.humanDoor", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("Building.GetIndoors()", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("is_locked_by_construction", buildingSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteConnectorsAndWallGraphHaveBuildingDoorEdges()
    {
        var source = ShopAccessReadAdapterSources.All;
        Assert.Contains("ReadBuildingDoorConnectors", source, StringComparison.Ordinal);
        Assert.Contains("ReadBuildingDoorGraphEdge", source, StringComparison.Ordinal);
        Assert.Contains("\"building_door\"", source, StringComparison.Ordinal);
        Assert.Contains("kind = \"building_door\"", source, StringComparison.Ordinal);
        Assert.Contains("Building.humanDoor; Building.GetIndoors()", source, StringComparison.Ordinal);
        Assert.Contains("Building.GetIndoors().warps[0]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasBuildingDoorInActionConnectorKinds()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("building_door", source, StringComparison.Ordinal);
        Assert.Contains("ValidateBuildingDoorConnector", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorBuildingDoorUsesNativeCheckActionNotDirectWarp()
    {
        var source = RuntimeHarnessSource;
        var doorSlice = Slice(source, "private bool TryTriggerConnectorAction", "private static int? ParseIntPart");
        Assert.Contains("Game1.currentLocation.checkAction", doorSlice, StringComparison.Ordinal);
        Assert.Contains("ValidateBuildingDoorConnector", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("building.doAction", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("performAction", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectSetPlayerLocation", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.currentLocation =", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.currentLocation =", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", doorSlice, StringComparison.Ordinal);

        var validationSlice = Slice(source, "private bool ValidateBuildingDoorConnector", "private static Point? FindConnectorActionStandTile");
        Assert.Contains(".humanDoor", validationSlice, StringComparison.Ordinal);
        Assert.Contains("building.GetIndoors()", validationSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("building.doAction", validationSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectSetPlayerLocation", validationSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer", validationSlice, StringComparison.Ordinal);

        var tickSlice = Slice(source, "private void TickTileMove", "private bool TryTriggerConnectorAction");
        Assert.Contains("CompleteConnectorMoveAfterLocationChange", tickSlice, StringComparison.Ordinal);
        Assert.Contains("AllowsLocationChange", tickSlice, StringComparison.Ordinal);
        Assert.Contains("IsStepOntoConnectorKind", tickSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectorTraversalHasNoDirectLocationMutationPath()
    {
        var source = RuntimeHarnessSource;
        var connectorSlice = Slice(source, "private void StartTileMove", "private void StartSleep");
        Assert.DoesNotContain("DirectSetPlayerLocation", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteDirectConnectorTraversal", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.currentLocation =", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.currentLocation =", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", connectorSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction", connectorSlice, StringComparison.Ordinal);
        Assert.Contains("MovePlayerForTick", connectorSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptResolvesFarmhouseBuildingDoorConnector()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("has_door_access_resolved", script, StringComparison.Ordinal);
        Assert.Contains("human_door_absolute_tile_x", script, StringComparison.Ordinal);
        Assert.Contains("indoor_location_id", script, StringComparison.Ordinal);
        Assert.Contains("indoor_arrival_tile_x", script, StringComparison.Ordinal);
        Assert.Contains("connector_kind = \"building_door\"", script, StringComparison.Ordinal);
        Assert.Contains("runtime-ship-inventory-smoke.home-connector", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesHomeLocationAfterConnector()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("post-connector location is not Farmhouse interior", script, StringComparison.Ordinal);
        Assert.Contains("expected $homeIndoorId", script, StringComparison.Ordinal);
        Assert.Contains("$homeLocationName -ne $homeIndoorId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptSleepUsesPostHomeConnectorStateHash()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("$homeConnectorSnapshot.state_hash", script, StringComparison.Ordinal);
        Assert.Contains("runtime-ship-inventory-smoke.sleep", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSettlementHelperScopedToActiveRunId()
    {
        var source = RuntimeHarnessSource;
        var helperSlice = Slice(source, "private void TrySettleActiveRunPendingShippingReceipts", "private void StartShipInventoryItemToBin");
        Assert.Contains("STARDEWAI_TRAINING_RUN_ID", helperSlice, StringComparison.Ordinal);
        Assert.Contains("activeRunId", helperSlice, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(activeRunId)", helperSlice, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(receipt.RunId, activeRunId", helperSlice, StringComparison.Ordinal);
        Assert.Contains("continue;", helperSlice, StringComparison.Ordinal);
        Assert.Contains("IsNullOrWhiteSpace(activeRunId) ||", helperSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsNullOrWhiteSpace(activeRunId) &&", helperSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("!string.IsNullOrWhiteSpace(activeRunId)", helperSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorReconciliationScopesTimeoutToActiveRunId()
    {
        var source = RuntimeHarnessSource;
        var reconcileSlice = Slice(source, "private void ReconcileShippingReceipts", "private static void AtomicWriteReceipt");
        Assert.Contains("STARDEWAI_TRAINING_RUN_ID", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("activeRunId", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(activeRunId)", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(receipt.RunId, activeRunId", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("IsNullOrWhiteSpace(activeRunId) ||", reconcileSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsNullOrWhiteSpace(activeRunId) &&", reconcileSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("!string.IsNullOrWhiteSpace(activeRunId)", reconcileSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildingDoorStandTileEnforcesExactDecompileBackedTile()
    {
        var source = RuntimeHarnessSource;
        var startSlice = Slice(source, "private void StartTileMove", "private void TickTileMove");
        Assert.Contains("connector_building_door_building_not_found", startSlice, StringComparison.Ordinal);
        Assert.Contains("connector_building_door_stand_tile_blocked", startSlice, StringComparison.Ordinal);
        Assert.Contains("requestedTargetTile.X, requestedTargetTile.Y + 1", startSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildingDoorTriggerVerifiesExactStandTileBeforeAction()
    {
        var source = RuntimeHarnessSource;
        var validationSlice = Slice(source, "private bool ValidateBuildingDoorConnector", "private static Point? FindConnectorActionStandTile");
        Assert.Contains("building_door_player_not_on_stand_tile", validationSlice, StringComparison.Ordinal);
        Assert.Contains("actionTile.X, actionTile.Y + 1", validationSlice, StringComparison.Ordinal);

        var triggerSlice = Slice(source, "private bool TryTriggerConnectorAction", "private static int? ParseIntPart");
        Assert.Contains("Game1.player.faceDirection(DirectionTo", triggerSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction", triggerSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildingDoorCheckActionReturnValueChecked()
    {
        var source = RuntimeHarnessSource;
        var triggerSlice = Slice(source, "private bool TryTriggerConnectorAction", "private static int? ParseIntPart");
        Assert.Contains("var handled = Game1.currentLocation.checkAction", triggerSlice, StringComparison.Ordinal);
        Assert.Contains("connector_action_not_handled", triggerSlice, StringComparison.Ordinal);
        Assert.Contains("!handled", triggerSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("building.doAction", triggerSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadBuildingDoorGraphEdgeEmitsUnresolvedRows()
    {
        var source = ShopAccessReadAdapterSources.All;
        var doorSlice = Slice(source, "private static object ReadBuildingDoorGraphEdge", "private static string ClassifyRouteActionBranch");
        Assert.Contains("human_door_unavailable", doorSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_location_unavailable", doorSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_entry_warp_unavailable", doorSlice, StringComparison.Ordinal);
        Assert.Contains("target_location_not_loaded", doorSlice, StringComparison.Ordinal);
        Assert.Contains("building_under_construction", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("return null", doorSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSingleBuildingDoorConnectorEmitsUnresolvedRows()
    {
        var source = ShopAccessReadAdapterSources.All;
        var connSlice = Slice(source, "private static object ReadSingleBuildingDoorConnector", "private static object ReadCollisionGrid");
        Assert.Contains("human_door_unavailable", connSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_location_unavailable", connSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_entry_warp_unavailable", connSlice, StringComparison.Ordinal);
        Assert.Contains("building_under_construction", connSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("return null", connSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesRouteGraphForFarmhouseEdge()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("locations.route_graph", script, StringComparison.Ordinal);
        Assert.Contains("route_graph Farmhouse edge disagrees", script, StringComparison.Ordinal);
        Assert.Contains("no resolved Farmhouse building_door edge in route_graph", script, StringComparison.Ordinal);
        Assert.Contains("route_graph.edges", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossChecksRouteGraphAgainstFarmBuildings()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("route_graph Farmhouse edge disagrees with farm.buildings transparent row", script, StringComparison.Ordinal);
        Assert.Contains("graph_door", script, StringComparison.Ordinal);
        Assert.Contains("building_door", script, StringComparison.Ordinal);
        Assert.Contains("graph_indoor", script, StringComparison.Ordinal);
        Assert.Contains("building_indoor", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesExactLocationEqualityOnly()
    {
        var script = ShippingSmokeSource;
        Assert.DoesNotContain("StartsWith", script, StringComparison.Ordinal);
        Assert.Contains("$homeLocationName -ne $homeIndoorId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossCheckFailsClosedWhenFarmBuildingsAbsent()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("farm.buildings data unavailable", script, StringComparison.Ordinal);
        Assert.Contains("cannot cross-check route_graph Farmhouse edge", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossCheckFailsClosedOnNonSingleFarmhouseCount()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("expected exactly one resolved Farmhouse row", script, StringComparison.Ordinal);
        Assert.Contains("resolved_farmhouse_count", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesPlayerTileEqualsHomeArrivalTileAfterConnector()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("post-connector player tile does not match expected arrival tile", script, StringComparison.Ordinal);
        Assert.Contains("$homeConnectorSnapshot.state.player.tile_x.value", script, StringComparison.Ordinal);
        Assert.Contains("$homeConnectorSnapshot.state.player.tile_y.value", script, StringComparison.Ordinal);
        Assert.Contains("$homePlayerTileX -ne [int]$homeArrivalTileX", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisteredFarmAdapterReadsBuildingDoorTraversal()
    {
        var bridgeSource = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "ModEntry.cs"));
        var farmSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.BuildingsShipping.cs"));

        Assert.Contains("stateCollector?.Adapters", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("building.humanDoor", farmSource, StringComparison.Ordinal);
        Assert.Contains("building.GetIndoors()", farmSource, StringComparison.Ordinal);
        Assert.Contains("indoor_arrival_tile_x", farmSource, StringComparison.Ordinal);
        Assert.Contains("resolved_building_door_connector", farmSource, StringComparison.Ordinal);
    }

}
