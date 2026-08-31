using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string MasteryClaimNativeContract =
        "Forest.MasteryRoom(all_five_base_skills_10)->MasteryCave;MasteryCave_skill_action->MasteryTrackerMenu(skill)->mainButton->claimReward(recipes,direct_inventory_else_debris,mastery_stat,masteryLevelsSpent,combat_trinket_slot,all_plaque_finale)";

    private static MasteryClaimProjectionRef? ReadMasteryClaim(Farmer? player)
    {
        if (player is null) return null;

        var masteryExperience = checked((int)Game1.stats.Get("MasteryExp"));
        var currentMasteryLevel = MasteryTrackerMenu.getCurrentMasteryLevel();
        var spent = checked((int)Game1.stats.Get("masteryLevelsSpent"));
        var unspent = Math.Max(0, currentMasteryLevel - spent);
        var skillLevels = new[]
        {
            player.farmingLevel.Value,
            player.fishingLevel.Value,
            player.foragingLevel.Value,
            player.miningLevel.Value,
            player.combatLevel.Value
        };
        var allSkillsLevelTen = skillLevels.Sum(level => level / 10) >= 5;
        var options = Enumerable.Range(0, 5)
            .Select(skillId => ReadMasteryClaimOption(player, skillId, skillLevels[skillId], unspent, allSkillsLevelTen))
            .ToArray();
        var claimable = options.Where(option => option.Claimable).ToArray();
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var currentMatches = string.Equals(Game1.currentLocation?.NameOrUniqueName, "MasteryCave", StringComparison.OrdinalIgnoreCase);
        var reasons = new List<string>();
        if (!allSkillsLevelTen) reasons.Add("mastery_cave_requires_all_five_base_skills_level_ten");
        if (unspent <= 0) reasons.Add("mastery_claim_no_unspent_mastery_level");
        if (claimable.Length == 0) reasons.Add("mastery_claim_no_unclaimed_plaque");
        if (claimable.Any(option => option.ActionTile is null)) reasons.Add("mastery_claim_native_action_endpoint_unavailable");
        if (!menuClear) reasons.Add("mastery_claim_menu_or_dialogue_not_clear");

        var projection = new MasteryClaimProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15",
            NativeContract = MasteryClaimNativeContract,
            CurrentLocationMatches = currentMatches,
            MenuClear = menuClear,
            AllBaseSkillsLevelTen = allSkillsLevelTen,
            MasteryExperience = masteryExperience,
            CurrentMasteryLevel = currentMasteryLevel,
            MasteryLevelsSpent = spent,
            UnspentMasteryLevels = unspent,
            AllPlaquesCompleted = options.All(option => option.Claimed),
            TrinketSlots = checked((int)player.stats.Get("trinketSlots")),
            Skills = options,
            ClaimableOptions = claimable,
            GameId = Game1.uniqueIDForThisGame,
            PlayerId = player.UniqueMultiplayerID,
            ServiceStatus = reasons.Count > 0 ? "blocked" : currentMatches ? "ready" : "route_required",
            BlockedDiagnostics = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
        projection.ProjectionFingerprint = MasteryClaimIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static MasteryClaimOptionRef ReadMasteryClaimOption(
        Farmer player,
        int skillId,
        int skillLevel,
        int unspent,
        bool allSkillsLevelTen)
    {
        var masteryStatKey = StatKeys.Mastery(skillId);
        var masteryStatValue = checked((int)player.stats.Get(masteryStatKey));
        var masteryCave = Game1.getLocationFromName("MasteryCave");
        var directRewards = MasteryDirectRewardIds(skillId)
            .Select(qualifiedId =>
            {
                var item = ItemRegistry.Create(qualifiedId);
                return new MasteryClaimDirectRewardRef
                {
                    QualifiedItemId = item.QualifiedItemId,
                    ItemId = item.ItemId,
                    DisplayName = item.DisplayName,
                    Stack = item.Stack,
                    RuntimeType = item.GetType().FullName ?? string.Empty,
                    InventoryCountBefore = player.Items.CountId(item.QualifiedItemId),
                    MasteryCaveDebrisCountBefore = CountMasteryRewardDebris(masteryCave, item.QualifiedItemId)
                };
            }).ToArray();
        var recipeRewards = MasteryRecipeRewardNames(skillId)
            .Select(recipe => new MasteryClaimRecipeRewardRef
            {
                RecipeName = recipe,
                KnownBefore = player.craftingRecipes.ContainsKey(recipe)
            }).ToArray();
        var option = new MasteryClaimOptionRef
        {
            SkillId = skillId,
            SkillKey = MasterySkillKey(skillId),
            SkillLevel = skillLevel,
            MasteryStatKey = masteryStatKey,
            MasteryStatValue = masteryStatValue,
            Claimed = masteryStatValue != 0,
            Claimable = allSkillsLevelTen && unspent > 0 && masteryStatValue == 0,
            ActionTile = FindMasteryClaimActionTile(MasteryActionToken(skillId)),
            DirectRewards = directRewards,
            RecipeRewards = recipeRewards,
            GrantsTrinketSlot = skillId == 4
        };
        option.OptionFingerprint = MasteryClaimIdentity.ComputeOptionFingerprint(option);
        return option;
    }

    private static MasteryClaimActionTileRef? FindMasteryClaimActionTile(string token)
    {
        var location = Game1.getLocationFromName("MasteryCave");
        var buildings = location?.map?.GetLayer("Buildings");
        if (location is null || buildings is null) return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
        {
            var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (!string.Equals(action, token, StringComparison.Ordinal)) continue;
            return new MasteryClaimActionTileRef
            {
                LocationId = location.NameOrUniqueName,
                TileX = x,
                TileY = y,
                ActionRaw = action
            };
        }
        return null;
    }

    internal static string MasterySkillKey(int skillId) => skillId switch
    {
        0 => "farming",
        1 => "fishing",
        2 => "foraging",
        3 => "mining",
        4 => "combat",
        _ => "unknown"
    };

    internal static string MasteryActionToken(int skillId) => "MasteryCave_" + (skillId switch
    {
        0 => "Farming",
        1 => "Fishing",
        2 => "Foraging",
        3 => "Mining",
        4 => "Combat",
        _ => "Unknown"
    });

    internal static string[] MasteryDirectRewardIds(int skillId) => skillId switch
    {
        0 => new[] { "(W)66" },
        1 => new[] { "(T)AdvancedIridiumRod" },
        _ => Array.Empty<string>()
    };

    private static int CountMasteryRewardDebris(GameLocation? location, string qualifiedItemId) => location?.debris
        .Where(debris => string.Equals(
            debris.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(debris.itemId.Value) ?? debris.itemId.Value,
            qualifiedItemId,
            StringComparison.Ordinal))
        .Sum(debris => Math.Max(1, debris.item?.Stack ?? debris.Chunks.Count)) ?? 0;

    internal static string[] MasteryRecipeRewardNames(int skillId) => skillId switch
    {
        0 => new[] { "Statue Of Blessings" },
        1 => new[] { "Challenge Bait" },
        2 => new[] { "Mystic Tree Seed", "Treasure Totem" },
        3 => new[] { "Statue Of The Dwarf King", "Heavy Furnace" },
        4 => new[] { "Anvil", "Mini-Forge" },
        _ => Array.Empty<string>()
    };
}
