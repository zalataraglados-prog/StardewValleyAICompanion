using System.Reflection;
using System.Text.Json;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static readonly JsonSerializerOptions PetSpawnDataJsonOptions = new()
    {
        IncludeFields = true
    };

    private static object[] ReadPets()
    {
        var player = Game1.player;
        var safeSlot = PlayerReadAdapter.FindSafeItemSlot(player);
        var petLoveMailBefore = player.mailReceived.Contains("petLoveMessage");
        var marnieAdoptionMailBefore = player.hasOrWillReceiveMail("MarniePetAdoption");

        return Utility.getAllPets()
            .GroupBy(pet => pet.petId.Value)
            .Select(group => group.First())
            .OrderBy(pet => pet.petId.Value)
            .Select(pet =>
            {
                var data = pet.GetPetData();
                var lastPetDay = pet.lastPetDay.TryGetValue(player.UniqueMultiplayerID, out var value)
                    ? value
                    : (int?)null;
                var grantAvailable = !pet.grantedFriendshipForPet.Value;
                var alreadyPettedToday = lastPetDay == Game1.Date.TotalDays;
                var willGrantDailyInteraction = data is not null && grantAvailable && !alreadyPettedToday;
                var friendshipBefore = pet.friendshipTowardFarmer.Value;
                var friendshipAfter = willGrantDailyInteraction
                    ? Math.Min(Pet.maxFriendship, friendshipBefore + 12)
                    : friendshipBefore;
                var giftTrigger = willGrantDailyInteraction &&
                    Utility.CreateDaySaveRandom(pet.timesPet.Value, 71928.0, pet.petId.Value.GetHashCode()).NextDouble() < data!.GiftChance;
                var eligibleGifts = (data?.Gifts ?? new())
                    .Where(gift => friendshipAfter >= gift.MinimumFriendshipThreshold && GameStateQuery.CheckConditions(gift.Condition))
                    .Select(gift => (object)new
                    {
                        gift.Id,
                        gift.ItemId,
                        gift.RandomItemId,
                        gift.MaxItems,
                        gift.MinStack,
                        gift.MaxStack,
                        gift.Quality,
                        gift.IsRecipe,
                        gift.Condition,
                        gift.PerItemCondition,
                        gift.MinimumFriendshipThreshold,
                        gift.Weight,
                        spawn_data_json = JsonSerializer.Serialize(gift, PetSpawnDataJsonOptions)
                    })
                    .ToArray() ?? Array.Empty<object>();
                var bowl = pet.GetPetBowl();
                var projectedNextDayFriendship = bowl is null
                    ? friendshipBefore - 10
                    : bowl.watered.Value
                        ? Math.Min(Pet.maxFriendship, friendshipBefore + 6)
                        : friendshipBefore;
                var nativeMethod = pet.GetType().GetMethod(
                    nameof(Pet.checkAction),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    new[] { typeof(Farmer), typeof(GameLocation) },
                    modifiers: null);
                var nativeCheckActionSupported = nativeMethod?.DeclaringType == typeof(Pet) &&
                    pet.GetType() is var petRuntimeType &&
                    (petRuntimeType == typeof(Pet) || petRuntimeType == typeof(Cat) || petRuntimeType == typeof(Dog));
                var actionStatus = data is null
                    ? "pet_data_unavailable"
                    : !nativeCheckActionSupported
                        ? "custom_pet_check_action_override"
                        : alreadyPettedToday
                            ? "already_petted_today"
                            : !grantAvailable
                                ? "daily_friendship_already_granted"
                                : friendshipBefore >= Pet.maxFriendship
                                    ? "pet_love_already_maximum"
                                    : !safeSlot.HasValue
                                        ? "safe_toolbar_slot_unavailable"
                                        : "ready";

                return new
                {
                    pet_id = pet.petId.Value.ToString("D"),
                    runtime_type = pet.GetType().FullName,
                    native_check_action_declaring_type = nativeMethod?.DeclaringType?.FullName ?? string.Empty,
                    native_check_action_supported = nativeCheckActionSupported,
                    location_id = pet.currentLocation?.NameOrUniqueName ?? string.Empty,
                    tile_x = pet.TilePoint.X,
                    tile_y = pet.TilePoint.Y,
                    name = pet.Name,
                    display_name = pet.displayName,
                    pet_type = pet.petType.Value,
                    breed_id = pet.whichBreed.Value,
                    home_location_name = pet.homeLocationName.Value,
                    current_behavior = pet.CurrentBehavior,
                    sleeping_on_farmer_bed = pet.isSleepingOnFarmerBed.Value,
                    friendship_toward_farmer = friendshipBefore,
                    friendship_after_daily_interaction = friendshipAfter,
                    friendship_maximum = Pet.maxFriendship,
                    granted_friendship_for_pet = pet.grantedFriendshipForPet.Value,
                    granted_friendship_after_daily_interaction = willGrantDailyInteraction || pet.grantedFriendshipForPet.Value,
                    last_pet_day_for_player = lastPetDay,
                    current_total_days = Game1.Date.TotalDays,
                    petted_by_player_today = alreadyPettedToday,
                    times_pet_before = pet.timesPet.Value,
                    times_pet_after_daily_interaction = willGrantDailyInteraction ? pet.timesPet.Value + 1 : pet.timesPet.Value,
                    daily_interaction_friendship_delta = willGrantDailyInteraction ? friendshipAfter - friendshipBefore : 0,
                    safe_slot_index = safeSlot,
                    pet_love_mail_before = petLoveMailBefore,
                    pet_love_mail_after_daily_interaction = petLoveMailBefore || willGrantDailyInteraction && friendshipAfter >= Pet.maxFriendship,
                    marnie_pet_adoption_mail_before_or_pending = marnieAdoptionMailBefore,
                    marnie_pet_adoption_mail_after_daily_interaction = marnieAdoptionMailBefore || willGrantDailyInteraction && friendshipAfter >= Pet.maxFriendship,
                    pet_love_feedback_delivery_after_daily_interaction = !petLoveMailBefore && willGrantDailyInteraction && friendshipAfter >= Pet.maxFriendship
                        ? Game1.newDay ? "morning_fluff_global_message" : "immediate_global_message"
                        : "none",
                    gift_chance = data?.GiftChance,
                    gift_trigger_will_succeed = giftTrigger,
                    gift_trigger_rng = "Utility.CreateDaySaveRandom(timesPet,71928,petId.GetHashCode)",
                    gift_selection_status = giftTrigger ? "runtime_observed_global_rng_selection" : "not_triggered",
                    eligible_gifts = eligibleGifts,
                    all_gift_spawn_data_json = data is null ? string.Empty : JsonSerializer.Serialize(data.Gifts ?? new(), PetSpawnDataJsonOptions),
                    assigned_bowl_present = bowl is not null,
                    assigned_bowl_location_id = bowl?.GetParentLocation()?.NameOrUniqueName ?? string.Empty,
                    assigned_bowl_tile_x = bowl?.tileX.Value,
                    assigned_bowl_tile_y = bowl?.tileY.Value,
                    assigned_bowl_watered = bowl?.watered.Value,
                    projected_next_day_friendship_from_current_bowl_state = projectedNextDayFriendship,
                    projected_next_day_pet_love_mail = petLoveMailBefore || projectedNextDayFriendship >= Pet.maxFriendship,
                    projected_next_day_marnie_pet_adoption_mail = marnieAdoptionMailBefore || projectedNextDayFriendship >= Pet.maxFriendship,
                    action_status = actionStatus
                };
            })
            .ToArray();
    }

    private static object[] ReadPetBowls(Farm farm)
    {
        var player = Game1.player;
        var canEntry = player.Items
            .Select((item, index) => new { item, index })
            .FirstOrDefault(entry => entry.item is WateringCan);
        var can = canEntry?.item as WateringCan;
        var wateringEnergyCost = can is null
            ? (double?)null
            : can.IsEfficient ? 0d : Math.Max(0d, 2d - player.FarmingLevel * 0.1d);
        var expectedWaterAfter = can is null
            ? (int?)null
            : can.IsBottomless ? can.WaterLeft : can.WaterLeft - 1;
        var petById = Utility.getAllPets()
            .GroupBy(pet => pet.petId.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var petLoveMailBefore = player.mailReceived.Contains("petLoveMessage");
        var marnieAdoptionMailBefore = player.hasOrWillReceiveMail("MarniePetAdoption");
        var locations = Game1.locations.Append(farm).Distinct().ToArray();

        return locations
            .SelectMany(location => location.buildings
                .OfType<PetBowl>()
                .Select(bowl => new { Location = location, Bowl = bowl }))
            .DistinctBy(entry => entry.Bowl)
            .Select(entry =>
            {
                var actionTile = FindPetBowlActionTile(entry.Bowl);
                Pet? pet = null;
                var hasAssignedPet = entry.Bowl.HasPet() && petById.TryGetValue(entry.Bowl.petId.Value, out pet);
                var friendshipBefore = hasAssignedPet ? pet!.friendshipTowardFarmer.Value : (int?)null;
                var projectedAfterFillAndDayUpdate = friendshipBefore.HasValue
                    ? Math.Min(Pet.maxFriendship, friendshipBefore.Value + 6)
                    : (int?)null;
                var status = !entry.Bowl.HasPet()
                    ? "pet_bowl_unassigned"
                    : !hasAssignedPet
                        ? "assigned_pet_unavailable"
                        : entry.Bowl.GetType() != typeof(PetBowl)
                            ? "custom_pet_bowl_runtime"
                        : entry.Bowl.watered.Value
                            ? "pet_bowl_already_watered"
                            : actionTile is null
                                ? "pet_bowl_action_tile_unavailable"
                                : can is null
                                    ? "watering_can_missing"
                                    : can.GetType() != typeof(WateringCan)
                                        ? "custom_watering_can_runtime"
                                    : can.WaterLeft <= 0 && !player.hasWateringCanEnchantment
                                        ? "watering_can_empty"
                                    : player.Stamina < wateringEnergyCost!.Value
                                            ? "insufficient_stamina"
                                            : friendshipBefore!.Value >= Pet.maxFriendship
                                                ? "pet_love_already_maximum"
                                                : "ready";

                return new
                {
                    location_id = entry.Location.NameOrUniqueName,
                    runtime_type = entry.Bowl.GetType().FullName,
                    building_tile_x = entry.Bowl.tileX.Value,
                    building_tile_y = entry.Bowl.tileY.Value,
                    building_tiles_wide = entry.Bowl.tilesWide.Value,
                    building_tiles_high = entry.Bowl.tilesHigh.Value,
                    action_tile_x = actionTile?.X,
                    action_tile_y = actionTile?.Y,
                    pet_spot_tile_x = entry.Bowl.GetPetSpot().X,
                    pet_spot_tile_y = entry.Bowl.GetPetSpot().Y,
                    watered = entry.Bowl.watered.Value,
                    assigned_pet_id = entry.Bowl.petId.Value.ToString("D"),
                    assigned_pet_present = hasAssignedPet,
                    assigned_pet_name = hasAssignedPet ? pet!.displayName : string.Empty,
                    friendship_before_next_day = friendshipBefore,
                    friendship_after_fill_and_next_day_update = projectedAfterFillAndDayUpdate,
                    delayed_friendship_delta = friendshipBefore.HasValue ? projectedAfterFillAndDayUpdate - friendshipBefore : null,
                    pet_love_mail_before = petLoveMailBefore,
                    pet_love_mail_after_fill_and_next_day_update = petLoveMailBefore ||
                        projectedAfterFillAndDayUpdate.HasValue && projectedAfterFillAndDayUpdate.Value >= Pet.maxFriendship,
                    marnie_pet_adoption_mail_before_or_pending = marnieAdoptionMailBefore,
                    marnie_pet_adoption_mail_after_fill_and_next_day_update = marnieAdoptionMailBefore ||
                        projectedAfterFillAndDayUpdate.HasValue && projectedAfterFillAndDayUpdate.Value >= Pet.maxFriendship,
                    delayed_settlement = "Pet.dayUpdate consumes watered=true and applies min(1000,friendship+6)",
                    current_location_raining = entry.Location.IsRainingHere(),
                    rain_fill_rule = "new_day_outdoor_rain_sets_watered_before_location_day_updates",
                    watering_can_slot_index = canEntry?.index,
                    watering_can_runtime_type = can?.GetType().FullName ?? string.Empty,
                    watering_can_upgrade_level = can?.UpgradeLevel,
                    watering_can_water_left = can?.WaterLeft,
                    watering_can_bottomless = can?.IsBottomless,
                    farmer_has_watering_can_enchantment = player.hasWateringCanEnchantment,
                    watering_can_efficient = can?.IsEfficient,
                    expected_watering_can_water_after = expectedWaterAfter,
                    energy_before = player.Stamina,
                    watering_energy_cost = wateringEnergyCost,
                    expected_energy_after = wateringEnergyCost.HasValue ? (double?)(player.Stamina - wateringEnergyCost.Value) : null,
                    energy_delta_projection_status = wateringEnergyCost.HasValue ? "exact_vanilla_power_zero" : "watering_can_unavailable",
                    action_status = status
                };
            })
            .OrderBy(row => row.location_id, StringComparer.Ordinal)
            .ThenBy(row => row.building_tile_y)
            .ThenBy(row => row.building_tile_x)
            .ToArray();
    }

    private static Microsoft.Xna.Framework.Point? FindPetBowlActionTile(PetBowl bowl)
    {
        for (var y = bowl.tileY.Value; y < bowl.tileY.Value + bowl.tilesHigh.Value; y++)
        {
            for (var x = bowl.tileX.Value; x < bowl.tileX.Value + bowl.tilesWide.Value; x++)
            {
                string propertyValue = null!;
                if (bowl.doesTileHaveProperty(x, y, "PetBowl", "Buildings", ref propertyValue))
                {
                    return new Microsoft.Xna.Framework.Point(x, y);
                }
            }
        }
        return null;
    }
}
