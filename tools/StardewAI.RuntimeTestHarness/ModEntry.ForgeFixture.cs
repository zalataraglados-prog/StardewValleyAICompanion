using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupForgeFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0) return Blocked(request, reasons.ToArray());
        var allowed = new[] { "gem_forge", "prismatic_enchant", "galaxy_soul", "diamond_forge", "dragon_tooth_innate_reroll", "weapon_appearance", "combine_rings", "unforge_weapon", "unforge_combined_ring" };
        if (!allowed.Contains(request.ForgeOperation, StringComparer.Ordinal))
            return BlockedWithPrimitive(request, "debug_setup_forge_fixture", "forge_fixture=ready", "forge_fixture=blocked", "forge_operation_not_supported_by_fixture");

        var player = Game1.player;
        for (var slot = 0; slot < player.Items.Count; slot++) player.Items[slot] = null;
        player.Equip(null, player.leftRing); player.Equip(null, player.rightRing);
        Game1.activeClickableMenu = null;
        Item left;
        Item? right = null;
        switch (request.ForgeOperation)
        {
            case "gem_forge": left = new MeleeWeapon("4"); right = ItemRegistry.Create("(O)64"); break;
            case "prismatic_enchant": left = new MeleeWeapon("4"); right = ItemRegistry.Create("(O)74"); break;
            case "galaxy_soul": left = new MeleeWeapon("4"); right = ItemRegistry.Create("(O)896"); break;
            case "diamond_forge": left = new MeleeWeapon("4"); right = ItemRegistry.Create("(O)72"); break;
            case "dragon_tooth_innate_reroll": left = new MeleeWeapon("9"); right = ItemRegistry.Create("(O)852"); break;
            case "weapon_appearance": left = new MeleeWeapon("4"); right = new MeleeWeapon("9"); break;
            case "combine_rings": left = new Ring("516"); right = new Ring("517"); break;
            case "unforge_weapon":
                var weapon = new MeleeWeapon("4"); weapon.AddEnchantment(new RubyEnchantment()); left = weapon; break;
            case "unforge_combined_ring": left = new Ring("516").Combine(new Ring("517")); break;
            default: return BlockedWithPrimitive(request, "debug_setup_forge_fixture", "forge_fixture=ready", "forge_fixture=blocked", "forge_fixture_unreachable_branch");
        }
        player.Items[0] = left;
        if (right is not null) player.Items[1] = right;
        player.Items[2] = ItemRegistry.Create("(O)848", 100);

        var farm = Game1.getFarm();
        var tile = FindOpenFixtureInteractionTile(farm);
        if (!tile.HasValue) return BlockedWithPrimitive(request, "debug_setup_forge_fixture", "forge_fixture=ready", "forge_fixture=blocked", "forge_fixture_tile_missing");
        farm.objects[tile.Value.ToVector2()] = ItemRegistry.Create<StardewValley.Object>("(BC)MiniForge");
        var moved = MoveFixtureFarmerToLocationAdjacent(farm, tile.Value, out var stand, out var moveReason);
        var verified = moved && player.Items[0] == left && (right is null || player.Items[1] == right) &&
            player.Items.CountId("(O)848") == 100 && farm.objects.TryGetValue(tile.Value.ToVector2(), out var forge) && forge.QualifiedItemId == "(BC)MiniForge";
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true, PrimitiveKind = "debug_setup_forge_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_save_forge_fixture_ready", "exact_operation_inputs_shards_and_mini_forge_ready" } : new[] { moveReason, "forge_fixture_post_state_mismatch" },
            RequestedEffect = "forge_fixture=ready;operation=" + request.ForgeOperation,
            ObservedEffect = "location=" + farm.NameOrUniqueName + ";target=" + tile.Value.X + "," + tile.Value.Y + ";stand=" + stand.X + "," + stand.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"), CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "forge_fixture_post_state_mismatch" }
        };
    }

    private static Point? FindOpenFixtureInteractionTile(GameLocation location)
    {
        for (var y = 8; y < 40; y++)
        for (var x = 8; x < 70; x++)
        {
            var tile = new Point(x, y);
            if (location.objects.ContainsKey(tile.ToVector2()) || !IsTileOnMap(location, tile)) continue;
            if (Neighbors(tile).Any(value => IsTileOnMap(location, value) && IsTileWalkable(location, value))) return tile;
        }
        return null;
    }
}
