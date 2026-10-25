namespace StardewAI.Core.Tests;

public sealed class ShopAccessRouteGraphSourceGuardTests
{
    [Fact]
    public void NativeBusTransitionsUseTheDedicatedAuditedTransportConnector()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.RouteGraph.cs"));

        Assert.Contains("IsNativeBusTransportWarp", source,
            StringComparison.Ordinal);
        Assert.Contains("location.NameOrUniqueName", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("location is Desert", source,
            StringComparison.Ordinal);
        Assert.Contains("TryReadNativeBusTransport(location, warp, out var busTransport)",
            source, StringComparison.Ordinal);
        Assert.Contains("edges.Add(busTransport)", source,
            StringComparison.Ordinal);
        var bridge = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.BusTransport.cs"));
        Assert.Contains("native_bus_transport", bridge, StringComparison.Ordinal);
        Assert.Contains("BusTicketTileIndex = 1057", bridge, StringComparison.Ordinal);
        Assert.Contains("mailReceived.Contains(", bridge, StringComparison.Ordinal);
        Assert.Contains("\"ccVault\"", bridge, StringComparison.Ordinal);
        Assert.Contains("canDriveYourselfToday", bridge, StringComparison.Ordinal);
        Assert.Contains("pam.TilePoint == new Point(21, 10)", bridge, StringComparison.Ordinal);
        Assert.Contains("Money >= ticketPrice", bridge, StringComparison.Ordinal);
        Assert.Contains("isRidingHorse()", bridge, StringComparison.Ordinal);
        Assert.Contains("native_bus_transport", File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.Core",
            "Execution",
            "ActionQueueCompiler.Routing.cs")), StringComparison.Ordinal);
        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.NativeBusTransport.cs"));
        Assert.Contains("native_bus_transport", runtime, StringComparison.Ordinal);
        Assert.Contains("checkAction(", runtime, StringComparison.Ordinal);
        Assert.Contains("performTouchAction(parts, Game1.player.Position)", runtime,
            StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.lastQuestionKey", runtime,
            StringComparison.Ordinal);
        Assert.Contains("response.responseKey", runtime, StringComparison.Ordinal);
        Assert.Contains("\"Yes\"", runtime, StringComparison.Ordinal);

        var receipt = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs"));
        Assert.Contains("native_bus_yes_response_not_observed", receipt,
            StringComparison.Ordinal);
        Assert.Contains("native_bus_money_receipt_mismatch", receipt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FishShopDayOneClosureMatchesTheNativeBeachOverride()
    {
        var rule = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "NativeShopGateRules.cs"));
        Assert.Contains("Game1.Date.TotalDays < 1", rule,
            StringComparison.Ordinal);
        Assert.Contains("fromLocation is Beach", rule,
            StringComparison.Ordinal);
        Assert.Contains("FishShopGoneFishingDoorTileIndex = 496", rule,
            StringComparison.Ordinal);
        Assert.Contains("\"Buildings\"", rule, StringComparison.Ordinal);
        Assert.Contains("\"untitled tile sheet\"", rule,
            StringComparison.Ordinal);

        var gateRead = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.GatesBlockers.cs"));
        var currentLocationRead = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CurrentLocationReadAdapter.cs"));
        var purchaseGate = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.Core",
            "OptionRegistry",
            "CandidateOptionAvailabilityEvaluator.PurchaseRouting.Gates.cs"));
        var routeGate = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.Core",
            "OptionRegistry",
            "CandidateOptionAvailabilityEvaluator.Routing.cs"));

        Assert.Contains("fish_shop_gone_fishing_closed", gateRead,
            StringComparison.Ordinal);
        Assert.Contains("fish_shop_gone_fishing_closed", currentLocationRead,
            StringComparison.Ordinal);
        Assert.Contains("fish_shop_gone_fishing_closed", purchaseGate,
            StringComparison.Ordinal);
        Assert.Contains("route_gate_fish_shop_gone_fishing_closed", routeGate,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
