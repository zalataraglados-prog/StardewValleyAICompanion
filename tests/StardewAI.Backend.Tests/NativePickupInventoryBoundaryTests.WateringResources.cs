using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    // This leaf composes an already verified pickup with watering resource checks.
    // It does not stand in for the selected-water wire or native tool XML witness.
    [Theory]
    [InlineData(false, false, 20, false, false, false, 0)]
    [InlineData(true, false, 20, false, false, false, 0)]
    [InlineData(true, true, 0, false, false, false, 9)]
    [InlineData(false, false, 20, true, false, false, 9)]
    [InlineData(false, false, 0, false, true, false, 9)]
    [InlineData(false, false, 20, false, false, true, 0)]
    public void WateringResourceCompositionCombinesVerifiedPrimaryEffectsAndPhysicalPickup(
        bool newSlot, bool newKind, int water, bool bottomless, bool enchanted, bool efficient, int level)
    {
        var f = WateringResourceFixture(newSlot, newKind, water, bottomless, enchanted, efficient, level);
        var raw = WateringResourceRaw(f);
        VerifyWateringResources(f);
        Assert.Single(f.Pickup.Slots);
        Assert.Equal(6, f.Pickup.Evidence.ObservedPickupUnits);
        Assert.False(f.Pickup.ResourceCreditAuthorized);
        Assert.False(f.Pickup.NativeOutcomeVerified);
        Assert.False(f.Pickup.FormalProductTrainingAuthorized);

        var legacy = Assert.Throws<InvalidDataException>(() => ResourceBoundaryInvoke("VerifyWatering",
            f.Raw.Before, f.Raw.After, "Farm", new MoneyRevenueCropPlantingTile(71, 16), true));
        Assert.Contains("Crop watering changed unrelated persistent resources", legacy.Message);
        Assert.Throws<InvalidDataException>(() => Resources(f.Raw));
        Assert.Equal(raw, WateringResourceRaw(f));
        Assert.Equal(f.Raw.Before.StateHash, SnapshotHash.ComputeStateHash(f.Raw.Before.State));
        Assert.Equal(f.Raw.After.StateHash, SnapshotHash.ComputeStateHash(f.Raw.After.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WateringResourceCompositionKeepsBlockedWateringAndRefillWithoutCropCredit(bool refill)
    {
        var f = WateringResourceFixture(water: refill ? 0 : 20, applied: false, refillOnly: refill);
        var raw = WateringResourceRaw(f);
        VerifyWateringResources(f, applied: false);
        Assert.Equal(raw, WateringResourceRaw(f));
        Assert.False(f.Raw.After.State["farm"].GetProperty("crops").GetProperty("value")[0].GetProperty("watered").GetBoolean());
    }

    [Fact]
    public void WateringResourceCompositionAlsoAcceptsAnObservedZeroPickupWithoutRelaxingLegacyWatering()
    {
        var f = WateringResourceFixture();
        foreach (var (domain, field) in new[]
        {
            ("player", "inventory"), ("player", "inventory_capacity"),
            ("farm", "material_inventory_graph"), ("world_progress", "marriage_house")
        })
            Put(f.Raw.After, domain, field, JsonNode.Parse(f.Raw.Before.State[domain].GetProperty(field).GetProperty("value").GetRawText())!);
        var receipt = f.Raw.Receipt;
        receipt.Collections = [];
        receipt.InventoryAfter = JsonSerializer.Deserialize<ActionOriginInventoryCount[]>(JsonSerializer.Serialize(receipt.InventoryBefore))!;
        receipt.InventoryPayloadAfter = JsonSerializer.Deserialize<ActionOriginInventoryPayload>(JsonSerializer.Serialize(receipt.InventoryPayloadBefore))!;
        receipt.InventoryPayloadAfter.GameTick = f.Raw.After.GameTick;
        Rebind(f.Raw);
        f = new(f.Raw, f.Raw.Read());
        var raw = WateringResourceRaw(f);
        VerifyWateringResources(f);
        ResourceBoundaryInvoke("VerifyWatering", f.Raw.Before, f.Raw.After, "Farm", new MoneyRevenueCropPlantingTile(71, 16), true);
        Assert.Empty(f.Pickup.Slots);
        Assert.Equal(0, f.Pickup.Evidence.ObservedPickupUnits);
        Assert.Equal(raw, WateringResourceRaw(f));
    }

    private sealed record WateringResourceLeaf(PickupFixture Raw, NativePickupInventorySummary Pickup);

    private static WateringResourceLeaf WateringResourceFixture(bool newSlot = false, bool newKind = false,
        int water = 20, bool bottomless = false, bool enchanted = false, bool efficient = false,
        int level = 0, bool applied = true, bool refillOnly = false)
    {
        var f = ResourceFixture(newSlot, newKind);
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            var isAfter = ReferenceEquals(snapshot, f.After);
            var cost = efficient ? 0f : 2f - level * 0.1f;
            var projectedCost = efficient ? 0d : 2d - level * 0.1d;
            var available = water <= 0 && !enchanted ? 40 : water;
            var left = isAfter ? applied ? bottomless ? available : available - 1 : refillOnly ? 40 : water : water;
            var energy = isAfter && applied ? 100f - cost : 100f;
            Put(snapshot, "farm", "shipping_bins", new JsonArray());
            Put(snapshot, "player", "club_coins", JsonValue.Create(0)!);
            Put(snapshot, "player", "total_money_earned", JsonValue.Create(500)!);
            Put(snapshot, "player", "energy", JsonValue.Create(energy)!);
            Put(snapshot, "player", "watering_can_state", new JsonObject
            {
                ["projection_status"] = "exact_vanilla_power_zero", ["runtime_type"] = "StardewValley.Tools.WateringCan",
                ["slot_index"] = 1, ["upgrade_level"] = 0, ["water_left"] = left, ["capacity"] = 40,
                ["is_bottomless"] = bottomless, ["farmer_has_watering_can_enchantment"] = enchanted,
                ["is_efficient"] = efficient, ["farming_level"] = level, ["energy_cost_per_power_zero_action"] = projectedCost
            });
            Put(snapshot, "farm", "crops", new JsonArray(Crop(71, isAfter && applied), Crop(72, false)));
            Put(snapshot, "farm", "pet_bowls", new JsonArray(new JsonObject
            {
                ["location_id"] = "Farm", ["tile_x"] = 4, ["tile_y"] = 5, ["watered"] = false,
                ["friendship_before_next_day"] = 10, ["friendship_after_fill_and_next_day_update"] = 16,
                ["unknown_native_bowl_detail"] = "retained", ["pet_love_mail_before"] = false,
                ["watering_can_slot_index"] = 1, ["watering_can_runtime_type"] = "StardewValley.Tools.WateringCan",
                ["watering_can_upgrade_level"] = 0, ["watering_can_bottomless"] = bottomless,
                ["farmer_has_watering_can_enchantment"] = enchanted, ["watering_can_efficient"] = efficient,
                ["watering_can_water_left"] = left, ["expected_watering_can_water_after"] = bottomless ? left : left - 1,
                ["energy_before"] = energy, ["watering_energy_cost"] = projectedCost,
                ["expected_energy_after"] = energy - projectedCost,
                ["energy_delta_projection_status"] = "exact_vanilla_power_zero",
                ["action_status"] = left <= 0 && !enchanted ? "watering_can_empty" : energy < cost ? "insufficient_stamina" : "ready"
            }));
        }
        Rebind(f);
        return new(f, f.Read());

        static JsonObject Crop(int x, bool watered) => new()
        {
            ["location_id"] = "Farm", ["tile_x"] = x, ["tile_y"] = 16,
            ["requires_watering_for_growth"] = true, ["watered"] = watered, ["needs_watering"] = !watered,
            ["unknown_native_crop_detail"] = "retained"
        };
    }

    private static string WateringResourceRaw(WateringResourceLeaf f) =>
        JsonSerializer.Serialize(new { f.Raw.Request, f.Raw.Receipt, f.Raw.Before, f.Raw.After });

    private static void VerifyWateringResources(WateringResourceLeaf f, bool applied = true) =>
        ResourceBoundaryInvoke("VerifyObservedWateringResources", f.Raw.Before, f.Raw.After, "Farm",
            new MoneyRevenueCropPlantingTile(71, 16), applied, f.Pickup);
}
