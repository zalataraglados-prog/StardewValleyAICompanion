using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.MakeoverOutfits;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string PlayerCustomizationNativeContract =
        "wizard_shrine:shared_route->WizardShrine_checkAction->answerDialogue(Yes)->CharacterCustomization(Source.Wizard)_native_controls->OK;desert_makeover:shared_route->walk_onto_DesertMakeover_TouchAction->native_skippable_Event->onEventFinished_ReceiveMakeOver";

    private static object ReadPlayerCustomization(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
            return new { schema_version = "player_customization.v1", projection_status = "unavailable_world_or_player" };

        var basement = Game1.getLocationFromName("WizardHouseBasement");
        var wizardTiles = ReadCustomizationActionTiles(basement, "Action", "Buildings", "WizardShrine");
        var hairStyles = Farmer.GetAllHairstyleIndices().ToArray();
        var eyeHsv = ReadCustomizationHsv(player.newEyeColor.Value);
        var hairHsv = ReadCustomizationHsv(player.hairstyleColor.Value);
        var wizardPoints = player.friendshipData.TryGetValue("Wizard", out var wizardFriendship)
            ? wizardFriendship.Points
            : 0;

        var desert = Game1.getLocationFromName("DesertFestival") as DesertFestival;
        var desertTiles = ReadCustomizationActionTiles(desert, "TouchAction", "Back", "DesertMakeover");
        var stylist = desert?.GetStylist();
        var equippedCount = (player.hat.Value is null ? 0 : 1) +
            (player.shirtItem.Value is null ? 0 : 1) + (player.pantsItem.Value is null ? 0 : 1);
        var makeover = ReadDesertMakeoverProjection(player);
        var festivalDay = Utility.GetDayOfPassiveFestival("DesertFestival");
        var desertReady = desert is not null && festivalDay > 0 && stylist is not null &&
            !player.activeDialogueEvents.ContainsKey("DesertMakeover") &&
            player.freeSpotsInInventory() >= equippedCount && desertTiles.Length > 0 && makeover.ExpectedOutfitAvailable;

        var projectionBody = new
        {
            current = new
            {
                player.Name,
                favorite_thing = player.favoriteThing.Value,
                gender = player.IsMale ? "male" : "female",
                skin_index = player.skin.Value,
                hair_style_id = player.hair.Value,
                accessory_index = player.accessory.Value,
                eye_hsv = eyeHsv,
                hair_hsv = hairHsv,
                hat_qid = player.hat.Value?.QualifiedItemId ?? string.Empty,
                shirt_qid = player.shirtItem.Value?.QualifiedItemId ?? string.Empty,
                pants_qid = player.pantsItem.Value?.QualifiedItemId ?? string.Empty
            },
            hairStyles,
            wizard = new { player.Money, wizardPoints, wizardTiles },
            desert = new
            {
                festivalDay,
                stylist = stylist?.Name ?? string.Empty,
                alreadyStyled = player.activeDialogueEvents.ContainsKey("DesertMakeover"),
                freeSlots = player.freeSpotsInInventory(),
                equippedCount,
                desertTiles,
                makeover.QualifyingOutfits,
                makeover.ExpectedParts,
                makeover.ExpectedOutfitIndex,
                makeover.UsesPlayerSeed,
                makeover.SpecialLaurelOutfit
            }
        };

        return new
        {
            schema_version = "player_customization.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = PlayerCustomizationSha256(JsonSerializer.Serialize(projectionBody)),
            invocation_policy = "player_command_only",
            training_policy = "excluded_from_autonomous_candidates_and_strategy_training",
            current = projectionBody.current,
            wizard_shrine = new
            {
                mode = "wizard_shrine",
                location_id = "WizardHouseBasement",
                price_gold = 500,
                money_before = player.Money,
                wizard_friendship_points = wizardPoints,
                normal_hatch_unlock_points = 1000,
                access_policy = "shared_route_graph_is_authoritative;WizardHatch_requires_1000_points_but_WitchHut_magic_route_may_also_resolve",
                service_status = basement is null || wizardTiles.Length == 0
                    ? "blocked_shrine_endpoint_missing"
                    : player.Money < 500
                        ? "blocked_money_below_500"
                        : ReferenceEquals(Game1.currentLocation, basement) ? "ready" : "route_to_wizard_shrine_required",
                action_tiles = wizardTiles,
                hair_style_ids = hairStyles,
                skin_index_min = 0,
                skin_index_max = 23,
                accessory_index_min = -1,
                accessory_index_max = 29,
                color_slider_min = 0,
                color_slider_max = 100,
                editable_fields = new[] { "name", "favorite_thing", "gender", "skin_index", "hair_style_id", "accessory_index", "eye_hsv", "hair_hsv" },
                excluded_fields = new[] { "shirt", "pants_style", "pants_color", "pet", "farm_name" },
                random_button_policy = "not_used;explicit_exact_targets_only"
            },
            desert_makeover = new
            {
                mode = "desert_makeover",
                location_id = "DesertFestival",
                passive_festival_day = festivalDay,
                stylist_name = stylist?.Name ?? string.Empty,
                daily_flag_key = "DesertMakeover",
                already_styled_today = player.activeDialogueEvents.ContainsKey("DesertMakeover"),
                equipped_item_count = equippedCount,
                free_inventory_slots = player.freeSpotsInInventory(),
                service_status = desertReady
                    ? ReferenceEquals(Game1.currentLocation, desert) ? "ready" : "route_to_desert_makeover_required"
                    : desert is null || festivalDay <= 0 ? "blocked_desert_festival_inactive"
                    : stylist is null ? "blocked_no_native_stylist"
                    : player.activeDialogueEvents.ContainsKey("DesertMakeover") ? "blocked_already_styled_today"
                    : player.freeSpotsInInventory() < equippedCount ? "blocked_inventory_space_below_equipped_count"
                    : desertTiles.Length == 0 ? "blocked_touch_action_missing"
                    : "blocked_no_qualifying_outfit",
                touch_tiles = desertTiles,
                qualifying_outfit_count = makeover.QualifyingOutfits.Length,
                qualifying_outfits = makeover.QualifyingOutfits,
                expected_outfit_index = makeover.ExpectedOutfitIndex,
                expected_parts = makeover.ExpectedParts,
                uses_player_seed = makeover.UsesPlayerSeed,
                special_laurel_outfit = makeover.SpecialLaurelOutfit,
                expected_outfit_available = makeover.ExpectedOutfitAvailable,
                inventory_overflow_policy = "native_returnedDonations_and_lost_and_found",
                selection_policy = "exact_ReceiveMakeOver_locked_1.6.15_filter_and_day_save_rng_replay"
            },
            active_menu_status = Game1.activeClickableMenu is CharacterCustomization customization
                ? customization.source == CharacterCustomization.Source.Wizard ? "wizard_character_customization" : "other_character_customization"
                : "none",
            active_event_status = Game1.eventUp ? Game1.currentLocation?.currentEvent is { } activeEvent
                ? "event:" + activeEvent.id : "event_up_without_current_event" : "none",
            native_contract = PlayerCustomizationNativeContract,
            source_boundaries = new[]
            {
                "ClothesDye_and_DyePots_belong_to_tailoring.dye_item",
                "NewGame_HostNewFarm_NewFarmhand_are_onboarding_control_plane",
                "Dresser_constructor_not_present_in_locked_base_call_sites"
            },
            direct_mutation_policy = "production_executor_must_not_write_money_appearance_equipment_or_daily_flag_and_must_not_call_ReceiveMakeOver_directly"
        };
    }

    private static object[] ReadCustomizationActionTiles(GameLocation? location, string property, string layerName, string token)
    {
        var layer = location?.map?.GetLayer(layerName);
        if (location is null || layer is null)
            return Array.Empty<object>();
        var rows = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var raw = location.doesTileHaveProperty(x, y, property, layerName);
            if (string.Equals(raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), token, StringComparison.Ordinal))
                rows.Add(new { tile_x = x, tile_y = y, property_name = property, layer_name = layerName, action_raw = raw, action_token = token });
        }
        return rows.ToArray();
    }

    private static object ReadCustomizationHsv(Color color)
    {
        ColorPicker.RGBtoHSV(color.R, color.G, color.B, out var hue, out var saturation, out var value);
        return new
        {
            hue = float.IsNaN(hue) || hue < 0 ? 0 : (int)(hue / 360f * 100f),
            saturation = float.IsNaN(saturation) ? 0 : (int)(saturation * 100f),
            value = float.IsNaN(value) ? 0 : (int)(value / 255f * 100f)
        };
    }

    private static DesertMakeoverProjection ReadDesertMakeoverProjection(Farmer player)
    {
        var source = DataLoader.MakeoverOutfits(Game1.content) ?? new List<MakeoverOutfit>();
        var qualifying = new List<(int Index, MakeoverOutfit Outfit)>();
        var rows = new List<object>();
        for (var index = 0; index < source.Count; index++)
        {
            var outfit = source[index];
            var genderMismatch = outfit.Gender.HasValue && outfit.Gender.Value != player.Gender;
            var currentHatOrShirtMatch = !genderMismatch && (outfit.OutfitParts ?? new List<MakeoverItem>())
                .Where(part => part.MatchesGender(player.Gender))
                .Select(part => ItemRegistry.GetDataOrErrorItem(part.ItemId).QualifiedItemId)
                .Any(qid => qid == player.hat.Value?.QualifiedItemId || qid == player.shirtItem.Value?.QualifiedItemId);
            var eligible = !genderMismatch && !currentHatOrShirtMatch;
            if (eligible)
                qualifying.Add((index, outfit));
            rows.Add(new
            {
                source_index = index,
                gender = outfit.Gender?.ToString() ?? "any",
                eligible,
                exclusion_reason = genderMismatch ? "gender_mismatch" : currentHatOrShirtMatch ? "matches_current_hat_or_shirt" : string.Empty,
                parts = ReadMakeoverParts(outfit.OutfitParts, player)
            });
        }
        if (qualifying.Count == 0)
            return new(rows.ToArray(), Array.Empty<object>(), -1, false, false, false);

        var random = Utility.CreateDaySaveRandom(Game1.year);
        var usesPlayerSeed = random.NextDouble() < 0.75;
        if (usesPlayerSeed)
            random = Utility.CreateDaySaveRandom(Game1.year, (int)player.UniqueMultiplayerID);
        var selected = qualifying[random.Next(qualifying.Count)];
        var specialRandom = Utility.CreateDaySaveRandom();
        var special = Utility.GetDayOfPassiveFestival("DesertFestival") == 2 && specialRandom.NextDouble() < 0.03;
        var expectedParts = special
            ? new object[]
            {
                new { slot = "hat", qualified_item_id = "(H)LaurelWreathCrown", color = string.Empty },
                new { slot = "pants", qualified_item_id = "(P)3", color = "247 245 205" },
                new { slot = "shirt", qualified_item_id = "(S)1199", color = string.Empty }
            }
            : ReadMakeoverParts(selected.Outfit.OutfitParts, player);
        return new(rows.ToArray(), expectedParts, selected.Index, usesPlayerSeed, special, true);
    }

    private static object[] ReadMakeoverParts(IList<MakeoverItem>? parts, Farmer player) =>
        (parts ?? Array.Empty<MakeoverItem>()).Where(part => part.MatchesGender(player.Gender)).Select(part =>
        {
            var qid = ItemRegistry.GetDataOrErrorItem(part.ItemId).QualifiedItemId;
            var slot = qid.StartsWith("(H)", StringComparison.Ordinal) ? "hat"
                : qid.StartsWith("(S)", StringComparison.Ordinal) ? "shirt"
                : qid.StartsWith("(P)", StringComparison.Ordinal) ? "pants" : "unsupported";
            return (object)new { slot, qualified_item_id = qid, color = part.Color ?? string.Empty };
        }).ToArray();

    private static string PlayerCustomizationSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record DesertMakeoverProjection(
        object[] QualifyingOutfits,
        object[] ExpectedParts,
        int ExpectedOutfitIndex,
        bool UsesPlayerSeed,
        bool SpecialLaurelOutfit,
        bool ExpectedOutfitAvailable);
}
