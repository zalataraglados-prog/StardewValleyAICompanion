using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MenuReadAdapter
{
    private static object ReadMineElevatorMenuState(MineElevatorMenu menu)
    {
        var currentLevel = Game1.CurrentMineLevel;
        var inMineShaft = Game1.currentLocation is MineShaft;
        var entries = menu.elevators
            .Select(component =>
            {
                var parsed = int.TryParse(component.name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor);
                return new
                {
                    floor = parsed ? floor : -1,
                    bounds = new
                    {
                        x = component.bounds.X,
                        y = component.bounds.Y,
                        width = component.bounds.Width,
                        height = component.bounds.Height
                    },
                    visible = component.visible,
                    selectable = parsed && component.visible && floor != currentLevel && (floor != 0 || inMineShaft)
                };
            })
            .OrderBy(entry => entry.floor)
            .ToArray();
        var identitySource = string.Join("\n", new[]
        {
            currentLevel.ToString(CultureInfo.InvariantCulture),
            MineShaft.lowestLevelReached.ToString(CultureInfo.InvariantCulture),
            inMineShaft.ToString(),
            string.Join(";", entries.Select(entry => $"{entry.floor}:{entry.visible}:{entry.selectable}:{entry.bounds.x},{entry.bounds.y},{entry.bounds.width},{entry.bounds.height}"))
        });

        return new
        {
            kind = "mine_elevator",
            current_mine_level = currentLevel,
            lowest_level_reached = MineShaft.lowestLevelReached,
            is_current_location_mineshaft = inMineShaft,
            entries,
            menu_identity_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identitySource))).ToLowerInvariant()
        };
    }
}
