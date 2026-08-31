using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Constants;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMasteryClaimFixture(TrainingExecutionRequest request)
    {
        var cases = new Dictionary<string, (int SkillId, bool FullInventory, bool FinalClaim)>(StringComparer.Ordinal)
        {
            ["farming_inventory"] = (0, false, false),
            ["fishing_full_inventory"] = (1, true, false),
            ["foraging_recipes"] = (2, false, false),
            ["mining_recipes"] = (3, false, false),
            ["combat_final"] = (4, false, true)
        };
        var reasons = ValidateExecutionRequest(request);
        if (!cases.TryGetValue(request.MasteryFixtureCase, out var fixture)) reasons.Add("mastery_fixture_case_invalid");
        var cave = Game1.getLocationFromName("MasteryCave");
        if (cave is null) reasons.Add("mastery_fixture_base_location_unavailable");
        if (reasons.Count > 0 || cave is null)
            return BlockedWithPrimitive(request, "debug_setup_mastery_claim", "mastery_fixture=ready", "mastery_fixture=blocked", reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        Game1.currentSpeaker = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.currentMinigame = null;
        StopAllMovement();
        Game1.player.farmingLevel.Value = 10;
        Game1.player.fishingLevel.Value = 10;
        Game1.player.foragingLevel.Value = 10;
        Game1.player.miningLevel.Value = 10;
        Game1.player.combatLevel.Value = 10;
        Game1.stats.Set("MasteryExp", fixture.FinalClaim ? 100000u : 10000u);
        Game1.stats.Set("masteryLevelsSpent", fixture.FinalClaim ? 4u : 0u);
        for (var skillId = 0; skillId < 5; skillId++)
            Game1.player.stats.Set(StatKeys.Mastery(skillId), fixture.FinalClaim && skillId < 4 ? 1u : 0u);
        Game1.player.stats.Set("trinketSlots", 0);
        foreach (var recipe in Enumerable.Range(0, 5).SelectMany(MasteryRecipeRewardNames).Distinct(StringComparer.Ordinal))
            Game1.player.craftingRecipes.Remove(recipe);
        for (var index = 0; index < Math.Min(Game1.player.MaxItems, Game1.player.Items.Count); index++)
            Game1.player.Items[index] = null;
        if (fixture.FullInventory)
        {
            for (var index = 0; index < Math.Min(Game1.player.MaxItems, Game1.player.Items.Count); index++)
                Game1.player.Items[index] = ItemRegistry.Create("(O)388", 999);
        }
        foreach (var directId in MasteryDirectRewardIds(fixture.SkillId))
            for (var index = cave.debris.Count - 1; index >= 0; index--)
                if (string.Equals(DebrisQualifiedItemId(cave.debris[index]), directId, StringComparison.Ordinal))
                    cave.debris.RemoveAt(index);

        var endpoint = RuntimeMasteryActionTile(MasteryActionToken(fixture.SkillId));
        if (endpoint is null)
            return BlockedWithPrimitive(request, "debug_setup_mastery_claim", "mastery_fixture=ready", "mastery_fixture=endpoint_missing", "mastery_fixture_native_endpoint_unavailable");
        var target = new Point(endpoint.TileX, endpoint.TileY);
        var stand = Neighbors(target).FirstOrDefault(tile => IsTileOnMap(cave, tile) && IsTileWalkable(cave, tile) && !IsTileOccupiedByCharacter(cave, tile));
        if (stand == default)
            return BlockedWithPrimitive(request, "debug_setup_mastery_claim", "mastery_fixture=ready", "mastery_fixture=stand_missing", "mastery_fixture_native_stand_unavailable");
        Game1.currentLocation = cave;
        Game1.player.currentLocation = cave;
        cave.currentEvent = null;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        Game1.player.CurrentToolIndex = 0;

        var projection = ReadLiveMasteryClaimProjection();
        var option = projection?.ClaimableOptions.SingleOrDefault(row => row.SkillId == fixture.SkillId);
        var verified = projection is not null && option is not null && projection.ServiceStatus == "ready" &&
            projection.AllBaseSkillsLevelTen && projection.UnspentMasteryLevels == 1 && option.MasteryStatValue == 0 &&
            option.ActionTile?.ActionRaw == MasteryActionToken(fixture.SkillId) &&
            option.RecipeRewards.All(reward => !reward.KnownBefore) &&
            (!fixture.FullInventory || Game1.player.Items.Take(Game1.player.MaxItems).All(item => item is not null));
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mastery_claim",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_native_mastery_fixture_ready:" + request.MasteryFixtureCase } : new[] { "mastery_fixture_receipt_mismatch" },
            RequestedEffect = "mastery_fixture=" + request.MasteryFixtureCase,
            ObservedEffect = "skill=" + fixture.SkillId + ";unspent=" + projection?.UnspentMasteryLevels + ";status=" + projection?.ServiceStatus,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "mastery_fixture_receipt_mismatch" }
        };
    }
}
