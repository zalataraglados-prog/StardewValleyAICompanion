using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string BobberSelectionNativeContract =
        "FishShop_Bobbers_checkAction->ChooseFromIconsMenu(bobbers)->receiveLeftClick_exact_unlocked_icon->Farmer.bobberStyle_and_usingRandomizedBobber_receipt->native_close_button";

    private static object ReadBobberSelection(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
        {
            return new
            {
                schema_version = "bobber_selection.v1",
                projection_status = "unavailable_world_or_player",
                styles = Array.Empty<object>(),
                action_tiles = Array.Empty<object>()
            };
        }

        var fishShop = Game1.getLocationFromName("FishShop");
        var actionTiles = ReadBobberActionTiles(fishShop);
        var caughtSpeciesCount = player.fishCaught.Count();
        var nativeUnlockQuotient = caughtSpeciesCount / 2;
        var maxUnlockedFixedStyle = Math.Min(FishingRod.NUM_BOBBER_STYLES - 1, nativeUnlockQuotient);
        var styles = Enumerable.Range(0, FishingRod.NUM_BOBBER_STYLES)
            .Select(style => new
            {
                style_id = style,
                kind = "fixed",
                unlocked = style <= nativeUnlockQuotient,
                gate_status = style <= nativeUnlockQuotient ? "ready" : "blocked_fish_caught_count_required",
                required_fish_caught_species_count = style * 2
            })
            .Cast<object>()
            .Append(new
            {
                style_id = -2,
                kind = "randomized",
                unlocked = true,
                gate_status = "ready",
                required_fish_caught_species_count = 0
            })
            .ToArray();
        var sonarOverride = player.CurrentTool is FishingRod rod &&
            rod.GetTackleQualifiedItemIDs().Contains("(O)789", StringComparer.Ordinal);
        var menu = Game1.activeClickableMenu as ChooseFromIconsMenu;
        var bobberMenuOpen = menu is not null && menu.icons.Count == FishingRod.NUM_BOBBER_STYLES + 1 &&
            menu.icons.Take(FishingRod.NUM_BOBBER_STYLES).Select(icon => icon.name)
                .SequenceEqual(Enumerable.Range(0, FishingRod.NUM_BOBBER_STYLES).Select(value => value.ToString())) &&
            menu.icons[^1].name == "-2";
        var projectionBody = new
        {
            currentStyle = player.bobberStyle.Value,
            randomized = player.usingRandomizedBobber,
            caughtSpeciesCount,
            nativeUnlockQuotient,
            actionTiles,
            sonarOverride,
            bobberMenuOpen
        };

        return new
        {
            schema_version = "bobber_selection.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = BobberSelectionSha256(JsonSerializer.Serialize(projectionBody)),
            invocation_policy = "player_command_only",
            training_policy = "excluded_from_autonomous_candidates_and_strategy_training",
            location_id = "FishShop",
            is_current_location = ReferenceEquals(Game1.currentLocation, fishShop),
            service_status = fishShop is null
                ? "blocked_fish_shop_missing"
                : actionTiles.Length == 0
                    ? "blocked_bobbers_action_missing"
                    : ReferenceEquals(Game1.currentLocation, fishShop) ? "ready" : "route_to_fish_shop_required",
            current_style_id = player.bobberStyle.Value,
            using_randomized_bobber = player.usingRandomizedBobber,
            fish_caught_species_count = caughtSpeciesCount,
            native_unlock_quotient = nativeUnlockQuotient,
            max_unlocked_fixed_style_id = maxUnlockedFixedStyle,
            fixed_style_count = FishingRod.NUM_BOBBER_STYLES,
            random_style_id = -2,
            sonar_tackle_visible_style_override = sonarOverride,
            effective_visible_style_id = sonarOverride ? 39 : player.bobberStyle.Value,
            random_resolution_policy = "native_FishingRod_getBobberStyle_resolves_and_caches_a_style_when_the_rod_is_used;reads_never_advance_Game1.random",
            styles,
            action_tiles = actionTiles,
            active_menu_status = bobberMenuOpen ? "bobbers" : menu is null ? "none" : "other_choose_from_icons_menu",
            native_contract = BobberSelectionNativeContract,
            direct_mutation_policy = "production_executor_must_not_write_bobberStyle_or_usingRandomizedBobber_directly"
        };
    }

    private static object[] ReadBobberActionTiles(GameLocation? fishShop)
    {
        var buildings = fishShop?.map?.GetLayer("Buildings");
        if (fishShop is null || buildings is null)
            return Array.Empty<object>();
        var rows = new List<object>();
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                var action = fishShop.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.Equals(action, "Bobbers", StringComparison.Ordinal))
                    rows.Add(new { tile_x = x, tile_y = y, action_raw = action, action_token = "Bobbers" });
            }
        }
        return rows.ToArray();
    }

    private static string BobberSelectionSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
