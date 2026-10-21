using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter
{
    private const string NativeBusConnectorKind = "native_bus_transport";
    private const int BusTicketTileIndex = 1057;

    private static bool TryReadNativeBusTransport(
        GameLocation location,
        Warp warp,
        out object connector)
    {
        connector = null!;
        if (!IsNativeBusTransportWarp(location, warp.TargetName))
            return false;

        var sourceLocation = location.NameOrUniqueName;
        var toDesert = string.Equals(
            sourceLocation,
            "BusStop",
            StringComparison.OrdinalIgnoreCase);
        var endpoint = toDesert
            ? FindBusTicketMachineTile(location)
            : FindDesertBusTouchTile(location);
        var sourceProperty = toDesert
            ? "BusStop.checkAction Buildings/outdoors tile index 1057"
            : "Back.TouchAction";
        var rawAction = toDesert
            ? "Bus"
            : endpoint.HasValue
                ? location.doesTileHaveProperty(
                    endpoint.Value.X,
                    endpoint.Value.Y,
                    "TouchAction",
                    "Back")
                : null;
        var structuralResolution = endpoint.HasValue &&
            !string.IsNullOrWhiteSpace(warp.TargetName);
        var gate = ReadNativeBusGateState(location, toDesert);

        connector = new
        {
            kind = NativeBusConnectorKind,
            tile_x = endpoint?.X,
            tile_y = endpoint?.Y,
            target_location = warp.TargetName,
            target_x = (int?)warp.TargetX,
            target_y = (int?)warp.TargetY,
            action = rawAction,
            source_property = sourceProperty,
            bus_direction = toDesert ? "to_desert" : "to_bus_stop",
            native_transport_transition = true,
            transport_executor_supported = true,
            ticket_price = gate.TicketPrice,
            service_unlocked = gate.ServiceUnlocked,
            driver_available = gate.DriverAvailable,
            money_sufficient = gate.MoneySufficient,
            mount_clear = gate.MountClear,
            allowed_now = gate.AllowedNow,
            gate_reasons = gate.GateReasons,
            open = gate.AllowedNow,
            resolved = structuralResolution,
            unresolved_reason = structuralResolution
                ? (string?)null
                : endpoint.HasValue
                    ? "native_bus_target_location_missing"
                    : "native_bus_interaction_tile_missing"
        };
        return true;
    }

    private static object ReadNativeBusTransportGate(
        GameLocation location,
        int tileX,
        int tileY,
        bool toDesert)
    {
        var gate = ReadNativeBusGateState(location, toDesert);
        return new
        {
            kind = NativeBusConnectorKind,
            tile_x = tileX,
            tile_y = tileY,
            source_property = toDesert
                ? "BusStop.checkAction Buildings/outdoors tile index 1057"
                : "Back.TouchAction",
            action = toDesert ? "Bus" : "DesertBus",
            target_location = toDesert ? "Desert" : "BusStop",
            target_x = toDesert ? 16 : 22,
            target_y = toDesert ? 24 : 10,
            bus_direction = toDesert ? "to_desert" : "to_bus_stop",
            required_mail_id = toDesert ? "ccVault" : null,
            ticket_price = gate.TicketPrice,
            service_unlocked = gate.ServiceUnlocked,
            driver_available = gate.DriverAvailable,
            money_sufficient = gate.MoneySufficient,
            mount_clear = gate.MountClear,
            allowed_on_capture_date = gate.AllowedNow,
            allowed_now = gate.AllowedNow,
            gate_reasons = gate.GateReasons,
            unresolved_reason = (string?)null
        };
    }

    private static NativeBusGateState ReadNativeBusGateState(
        GameLocation location,
        bool toDesert)
    {
        if (!toDesert)
        {
            var returnAllowed = location is Desert desert &&
                !desert.IgnoreTouchActions();
            return new NativeBusGateState(
                TicketPrice: 0,
                ServiceUnlocked: true,
                DriverAvailable: true,
                MoneySufficient: true,
                MountClear: true,
                AllowedNow: returnAllowed,
                GateReasons: returnAllowed
                    ? Array.Empty<string>()
                    : new[] { "native_bus_return_transition_busy" });
        }

        var busStop = location as BusStop;
        var ticketPrice = busStop?.TicketPrice ?? 500;
        var serviceUnlocked = Game1.MasterPlayer?.mailReceived.Contains(
            "ccVault") == true;
        var pam = Game1.getCharacterFromName("Pam");
        var driverAvailable =
            Game1.netWorldState.Value.canDriveYourselfToday.Value ||
            pam is not null &&
            location.characters.Contains(pam) &&
            pam.TilePoint == new Point(21, 10);
        var moneySufficient = Game1.player?.Money >= ticketPrice;
        var mountClear = Game1.player is not null &&
            (!Game1.player.isRidingHorse() || Game1.player.mount is null);
        var forwardAllowed = busStop is not null &&
            serviceUnlocked &&
            driverAvailable &&
            moneySufficient &&
            mountClear;
        var reasons = new List<string>();
        if (busStop is null) reasons.Add("native_bus_stop_runtime_type_missing");
        if (!serviceUnlocked) reasons.Add("native_bus_cc_vault_missing");
        if (!driverAvailable) reasons.Add("native_bus_driver_unavailable");
        if (!moneySufficient) reasons.Add("native_bus_ticket_money_insufficient");
        if (!mountClear) reasons.Add("native_bus_player_mounted");

        return new NativeBusGateState(
            ticketPrice,
            serviceUnlocked,
            driverAvailable,
            moneySufficient,
            mountClear,
            forwardAllowed,
            reasons.ToArray());
    }

    private static Point? FindBusTicketMachineTile(GameLocation location)
    {
        var layer = location.map?.GetLayer("Buildings");
        if (layer is null)
            return null;

        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (location.getTileIndexAt(
                        x,
                        y,
                        "Buildings",
                        "outdoors") == BusTicketTileIndex)
                    return new Point(x, y);
            }
        }
        return null;
    }

    private static Point? FindDesertBusTouchTile(GameLocation location)
    {
        var action = ReadMapActions(location).FirstOrDefault(row =>
            string.Equals(
                row.branch,
                "DesertBus",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                row.source_property,
                "Back.TouchAction",
                StringComparison.OrdinalIgnoreCase));
        return action is null
            ? null
            : new Point(action.tile_x, action.tile_y);
    }

    private sealed record NativeBusGateState(
        int TicketPrice,
        bool ServiceUnlocked,
        bool DriverAvailable,
        bool MoneySufficient,
        bool MountClear,
        bool AllowedNow,
        string[] GateReasons);
}
