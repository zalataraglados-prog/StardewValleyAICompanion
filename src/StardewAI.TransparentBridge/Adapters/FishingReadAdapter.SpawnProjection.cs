using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FishingReadAdapter : ReadAdapterBase
{
    private static SpawnOutputProjection ReadSpawnOutput(
        string raw,
        int outputIndex,
        SpawnFishData spawn,
        Farmer player,
        GameLocation location,
        IReadOnlyDictionary<string, string> fishData,
        IReadOnlyList<FishingTileReadRow> eligibleTiles,
        bool hasMagicBait,
        bool hasCuriosityLure,
        string? targetedFishId,
        bool usesTrainingRod,
        bool isTutorialCatch,
        int seed)
    {
        var queryParts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (queryParts.Length > 0 && string.Equals(queryParts[0], "SECRET_NOTE_OR_ITEM", StringComparison.Ordinal))
        {
            if (queryParts.Length > 1 && !string.IsNullOrWhiteSpace(queryParts[1]))
            {
                return new SpawnOutputProjection(new
                {
                    output_index = outputIndex,
                    raw,
                    resolution_complete = false,
                    resolution_status = "vanilla_secret_note_alternate_query_not_enumerated",
                    item_query_key = queryParts[0],
                    alternate_item_query = queryParts[1],
                    reason = "alternate_item_query_requires_separate_side_effect_free_resolver_audit"
                }, false);
            }

            var islandNotes = location.InIslandContext();
            var noteQualifiedItemId = islandNotes ? "(O)842" : "(O)79";
            var unlocked = location.HasUnlockedAreaSecretNotes(player);
            var unseenNoteIds = Utility.GetUnseenSecretNotes(player, islandNotes, out var totalNotes);
            var inventoryCount = player.Items.CountId(noteQualifiedItemId);
            var availableNoteCount = Math.Max(0, unseenNoteIds.Length - inventoryCount);
            var festivalEventBlocks = location.currentEvent?.isFestival == true;
            var noteChance = availableNoteCount <= 0
                ? 0f
                : GameLocation.LAST_SECRET_NOTE_CHANCE +
                  (GameLocation.FIRST_SECRET_NOTE_CHANCE - GameLocation.LAST_SECRET_NOTE_CHANCE) *
                  ((float)(availableNoteCount - 1) / Math.Max(1, totalNotes - 1));
            var eligible = unlocked && !festivalEventBlocks && availableNoteCount > 0 && !isTutorialCatch;
            return new SpawnOutputProjection(new
            {
                output_index = outputIndex,
                raw,
                resolution_complete = true,
                resolution_status = "vanilla_secret_note_or_item",
                item_query_key = queryParts[0],
                item_id = islandNotes ? "842" : "79",
                qualified_item_id = noteQualifiedItemId,
                island_journal_scrap = islandNotes,
                area_secret_notes_unlocked = unlocked,
                festival_event_blocks = festivalEventBlocks,
                unseen_note_ids = unseenNoteIds,
                total_note_count = totalNotes,
                matching_notes_in_inventory = inventoryCount,
                available_note_count = availableNoteCount,
                output_local_chance_preview = noteChance,
                output_local_chance_roll_pending = eligible,
                output_eligible_before_random_rolls = eligible,
                output_blocking_reasons = new[]
                {
                    !unlocked ? "area_secret_notes_not_unlocked" : null,
                    festivalEventBlocks ? "festival_event_blocks_secret_note" : null,
                    availableNoteCount <= 0 ? "no_unseen_secret_note_available" : null,
                    isTutorialCatch ? "secret_note_not_valid_for_tutorial_catch" : null
                }.Where(reason => reason is not null).ToArray(),
                data_fish_chance_roll_pending = false,
                data_fish_chance_by_water_depth = Array.Empty<object>()
            }, true);
        }

        var parsedItem = ItemRegistry.GetData(raw);
        if (parsedItem is null)
        {
            var queryKey = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return new SpawnOutputProjection(new
            {
                output_index = outputIndex,
                raw,
                resolution_complete = false,
                resolution_status = "item_query_output_not_enumerated",
                item_query_key = queryKey,
                item_query_registered = queryKey is not null && ItemQueryResolver.ItemResolvers.ContainsKey(queryKey),
                per_item_condition = spawn.PerItemCondition,
                reason = "arbitrary_item_query_resolvers_are_not_executed_by_the_read_adapter"
            }, false);
        }

        string? rawFishData = null;
        var hasFishData = parsedItem.QualifiedItemId.StartsWith("(O)", StringComparison.Ordinal)
            && fishData.TryGetValue(parsedItem.ItemId, out rawFishData);
        var requirements = hasFishData
            ? FishingDataFishRuleParser.Parse(rawFishData!)
            : null;
        FishingDataFishEligibilityRead genericEligibility;
        if (requirements is null)
        {
            genericEligibility = new FishingDataFishEligibilityRead
            {
                EligibleBeforeRandomRoll = !isTutorialCatch,
                BlockingReasons = isTutorialCatch
                    ? new[] { "item_missing_data_fish_tutorial_entry" }
                    : Array.Empty<string>()
            };
        }
        else
        {
            genericEligibility = FishingDataFishRuleParser.Evaluate(
                requirements,
                new FishingDataFishEligibilityContext
                {
                    TimeOfDay = Game1.timeOfDay,
                    IsRaining = location.IsRainingHere(),
                    FishingLevel = player.FishingLevel,
                    HasMagicBait = hasMagicBait,
                    UsesTrainingRod = usesTrainingRod,
                    IsTutorialCatch = isTutorialCatch
                },
                spawn.CanUseTrainingRod,
                spawn.IgnoreFishDataRequirements);
        }

        var catchCount = player.fishCaught.TryGetValue(parsedItem.QualifiedItemId, out var caught)
            && caught.Length > 0
                ? caught[0]
                : 0;
        var catchLimitReached = spawn.CatchLimit > -1 && catchCount >= spawn.CatchLimit;
        var targetedByBait = spawn.ItemId == targetedFishId;
        var effectiveFishDifficulty = EffectiveNextCatchDifficulty(requirements?.Difficulty, spawn.IsBossFish, player);
        var genericChanceByTile = requirements is not null
            && requirements.ParseStatus == "parsed"
            && !spawn.IgnoreFishDataRequirements
                ? eligibleTiles
                    .Select(tile => tile.WaterDepth)
                    .Distinct()
                    .OrderBy(waterDepth => waterDepth)
                    .Select(waterDepth => new
                {
                    water_depth = waterDepth,
                    chance_preview = CalculateDataFishChance(
                        requirements,
                        waterDepth,
                        spawn,
                        player,
                        location,
                        usesTrainingRod,
                        hasCuriosityLure,
                        targetedByBait,
                        seed ^ waterDepth)
                }).ToArray()
                : Array.Empty<object>();

        return new SpawnOutputProjection(new
        {
            output_index = outputIndex,
            raw,
            resolution_complete = true,
            resolution_status = "direct_item",
            item_id = parsedItem.ItemId,
            qualified_item_id = parsedItem.QualifiedItemId,
            internal_name = parsedItem.InternalName,
            display_name = parsedItem.DisplayName,
            category = parsedItem.Category,
            object_type = parsedItem.ObjectType,
            base_fish_difficulty = requirements?.Difficulty,
            effective_fish_difficulty = effectiveFishDifficulty,
            fishing_experience_inputs_complete = hasFishData && requirements?.ParseStatus == "parsed" && effectiveFishDifficulty.HasValue,
            catch_count = catchCount,
            catch_limit_reached = catchLimitReached,
            output_eligible_before_random_rolls = genericEligibility.EligibleBeforeRandomRoll && !catchLimitReached,
            output_blocking_reasons = genericEligibility.BlockingReasons
                .Concat(catchLimitReached ? new[] { "catch_limit_reached" } : Array.Empty<string>())
                .ToArray(),
            data_fish_requirements = requirements,
            data_fish_chance_roll_pending = requirements is not null && !spawn.IgnoreFishDataRequirements,
            data_fish_chance_by_water_depth = genericChanceByTile
        }, true);
    }

    private static int? EffectiveNextCatchDifficulty(int? baseDifficulty, bool isBossFish, Farmer player)
    {
        if (!baseDifficulty.HasValue)
        {
            return null;
        }

        var difficulty = (float)baseDifficulty.Value;
        if (player.stats.Get("blessingOfWaters") != 0 && difficulty > 20f)
        {
            difficulty = isBossFish ? difficulty * 0.75f : difficulty / 2f;
        }

        if (player.fishCaught.Length == 0 && difficulty < 50f)
        {
            difficulty = 50f;
        }

        return (int)difficulty;
    }

    private static float? CalculateDataFishChance(
        FishingDataFishRequirementsRead requirements,
        int waterDepth,
        SpawnFishData spawn,
        Farmer player,
        GameLocation location,
        bool usesTrainingRod,
        bool hasCuriosityLure,
        bool targetedByBait,
        int seed)
    {
        if (!requirements.BaseChance.HasValue
            || !requirements.MaxDepth.HasValue
            || !requirements.DepthMultiplier.HasValue)
        {
            return null;
        }

        var chance = requirements.BaseChance.Value;
        var depthPenalty = requirements.DepthMultiplier.Value * chance;
        chance -= Math.Max(0, requirements.MaxDepth.Value - waterDepth) * depthPenalty;
        chance += player.FishingLevel / 50f;
        if (usesTrainingRod)
        {
            chance *= 1.1f;
        }
        chance = Math.Min(chance, 0.9f);
        if (chance < 0.25f && hasCuriosityLure)
        {
            chance = spawn.CuriosityLureBuff > -1f
                ? chance + spawn.CuriosityLureBuff
                : (0.25f - 0.08f) / 0.25f * chance + (0.25f - 0.08f) / 2f;
        }
        if (targetedByBait)
        {
            chance *= 1.66f;
        }
        if (spawn.ApplyDailyLuck)
        {
            chance += (float)player.DailyLuck;
        }
        if (spawn.ChanceModifiers is { Count: > 0 })
        {
            chance = Utility.ApplyQuantityModifiers(
                chance,
                spawn.ChanceModifiers,
                spawn.ChanceModifierMode,
                location,
                player,
                null,
                null,
                new Random(seed));
        }
        return chance;
    }

    private static object[] ReadQuantityModifiers(IReadOnlyList<QuantityModifier>? modifiers)
    {
        return modifiers?.Select(modifier => (object)new
        {
            id = modifier.Id,
            condition = modifier.Condition,
            modification = modifier.Modification.ToString(),
            amount = modifier.Amount,
            random_amount = modifier.RandomAmount?.ToArray() ?? Array.Empty<float>()
        }).ToArray() ?? Array.Empty<object>();
    }

    private static FishingRectangleRead? ReadRectangle(Rectangle? rectangle)
    {
        return rectangle.HasValue
            ? new FishingRectangleRead
            {
                X = rectangle.Value.X,
                Y = rectangle.Value.Y,
                Width = rectangle.Value.Width,
                Height = rectangle.Value.Height
            }
            : null;
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

}
