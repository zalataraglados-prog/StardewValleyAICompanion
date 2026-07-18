using StardewValley;
using System.Text.Json;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly string[] WellReadBookStatKeys =
    {
        "Book_Trash", "Book_Crabbing", "Book_Bombs", "Book_Roe", "Book_WildSeeds", "Book_Woodcutting",
        "Book_Defense", "Book_Friendship", "Book_Void", "Book_Speed", "Book_Marlon", "Book_PriceCatalogue",
        "Book_Diamonds", "Book_Mystery", "Book_AnimalCatalogue", "Book_Speed2", "Book_Artifact", "Book_Horse", "Book_Grass"
    };

    private static object[] ReadBookCandidates(Farmer? player)
    {
        if (player is null)
        {
            return Array.Empty<object>();
        }

        return player.Items
            .Select((item, slotIndex) => item is StardewValley.Object book &&
                (book.Category == StardewValley.Object.booksCategory || book.Category == StardewValley.Object.skillBooksCategory)
                    ? ReadBookCandidate(player, book, slotIndex)
                    : null)
            .Where(candidate => candidate is not null)
            .Cast<object>()
            .ToArray();
    }

    private static object ReadBookCandidate(Farmer player, StardewValley.Object book, int slotIndex)
    {
        var contextTagsNativeOrder = book.GetContextTags().ToArray();
        var alreadyReadCount = player.stats.Get(book.ItemId);
        var branch = BookBranch(book, alreadyReadCount, contextTagsNativeOrder);
        var calls = BookExperienceCalls(book, branch, contextTagsNativeOrder, out var matchedExperienceTag, out var branchStatus);
        var useMethod = book.GetType().GetMethod(nameof(StardewValley.Object.performUseAction), new[] { typeof(GameLocation) });
        if (useMethod?.DeclaringType != typeof(StardewValley.Object))
        {
            branchStatus = "blocked_custom_perform_use_action_override";
        }
        var experience = ProjectBookExperience(player, calls);
        var newLevelsBefore = player.newLevels
            .Select(level => new BookNewLevelQueueEntry(level.X, level.Y))
            .ToArray();
        var newLevelsAfter = newLevelsBefore
            .Concat(experience.LevelDeltas.SelectMany(delta => delta.NewLevelsQueued
                .Select(level => new BookNewLevelQueueEntry(delta.SkillIndex, level))))
            .ToArray();
        var nativeFeedbackCallbacks = BookNativeFeedbackCallbacks(branch, newLevelsBefore.Length, newLevelsAfter.Length);
        var recipesAdded = branch == "queen_of_sauce_first_read"
            ? DataLoader.Tv_CookingChannel(Game1.content)
                .Values
                .Select(value => value.Split('/')[0])
                .Where(recipe => !player.cookingRecipes.ContainsKey(recipe))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(recipe => recipe, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        var incrementsBookStat = branch is "power_book_first_read" or "queen_of_sauce_first_read";
        var addsReadBookMail = branch is "power_book_first_read" or "power_book_repeated_skill" or "power_book_repeated_all_skills";
        var wellReadBefore = player.achievements.Contains(35);
        var projectedStatAfter = incrementsBookStat ? unchecked(alreadyReadCount + 1u) : alreadyReadCount;
        var wellReadCriteriaAfter = branch == "power_book_first_read" && WellReadBookStatKeys.All(
            key => string.Equals(key, book.ItemId, StringComparison.Ordinal) ? projectedStatAfter != 0 : player.stats.Get(key) != 0);
        var wellReadWillUnlock = !wellReadBefore && wellReadCriteriaAfter && Game1.gameMode == 3 &&
            Game1.achievements?.ContainsKey(35) == true;
        var wellReadAfter = wellReadBefore || wellReadWillUnlock;
        var hatterMailBefore = player.hasOrWillReceiveMail("hatter");
        var achievementDialogueBefore = player.hasSeenActiveDialogueEvent("achievement_35");
        var gateReasons = BookUseGateReasons(player, book, branchStatus, experience.Status);

        return new
        {
            slot_index = slotIndex,
            item_id = book.ItemId,
            qualified_item_id = book.QualifiedItemId,
            display_name = book.DisplayName,
            runtime_type = book.GetType().FullName ?? book.GetType().Name,
            category = book.Category,
            stack_before = book.Stack,
            stack_after = Math.Max(0, book.Stack - 1),
            temporarily_invisible = book.isTemporarilyInvisible,
            context_tags_native_order = contextTagsNativeOrder,
            context_tags_native_order_json = JsonSerializer.Serialize(contextTagsNativeOrder),
            matched_book_experience_tag = matchedExperienceTag,
            already_read_stat_key = book.ItemId,
            already_read_stat_before = alreadyReadCount,
            native_branch = branch,
            native_branch_status = branchStatus,
            experience_calls = calls,
            experience_deltas = experience.Deltas,
            experience_deltas_json = JsonSerializer.Serialize(experience.Deltas),
            skill_level_deltas = experience.LevelDeltas,
            skill_level_deltas_json = JsonSerializer.Serialize(experience.LevelDeltas),
            new_levels_before = newLevelsBefore,
            new_levels_before_json = JsonSerializer.Serialize(newLevelsBefore),
            new_levels_after = newLevelsAfter,
            new_levels_after_json = JsonSerializer.Serialize(newLevelsAfter),
            native_feedback_callbacks = nativeFeedbackCallbacks,
            mastery_experience_delta = experience.MasteryExperienceDelta,
            experience_projection_status = experience.Status,
            book_stat_key = incrementsBookStat ? book.ItemId : string.Empty,
            book_stat_before = incrementsBookStat ? alreadyReadCount : (uint?)null,
            book_stat_after = incrementsBookStat ? projectedStatAfter : (uint?)null,
            read_a_book_mail_before = player.mailReceived.Contains("read_a_book"),
            read_a_book_mail_after = addsReadBookMail || player.mailReceived.Contains("read_a_book"),
            well_read_achievement_before = wellReadBefore,
            well_read_achievement_after = wellReadAfter,
            well_read_achievement_will_unlock = wellReadWillUnlock,
            well_read_achievement_definition_loaded = Game1.achievements?.ContainsKey(35) == true,
            well_read_achievement_game_mode_allows_unlock = Game1.gameMode == 3,
            well_read_hatter_mail_before = hatterMailBefore,
            well_read_hatter_mail_after = hatterMailBefore || wellReadWillUnlock,
            well_read_dialogue_event_seen_before = achievementDialogueBefore,
            well_read_dialogue_event_seen_after = achievementDialogueBefore || wellReadWillUnlock,
            well_read_ui_sound_platform_callbacks = wellReadWillUnlock ? "native_runtime_callbacks_expected" : "not_triggered",
            cooking_recipes_added = recipesAdded,
            cooking_recipes_added_json = JsonSerializer.Serialize(recipesAdded),
            cooking_recipes_added_count = recipesAdded.Length,
            player_can_move = player.canMove,
            event_up = Game1.eventUp,
            festival_active = Game1.isFestival(),
            fade_to_black = Game1.fadeToBlack,
            swimming = player.swimming.Value,
            bathing_clothes = player.bathingClothes.Value,
            on_bridge = player.onBridge.Value,
            active_menu_clear = Game1.activeClickableMenu is null,
            available = gateReasons.Length == 0,
            block_reasons = gateReasons,
            projection_basis = "Object.performUseAction category gate and Object.readBook decompile"
        };
    }

    private static string BookBranch(StardewValley.Object book, uint alreadyReadCount, string[] contextTagsNativeOrder)
    {
        if (book.ItemId.StartsWith("SkillBook_", StringComparison.Ordinal))
        {
            return "skill_book";
        }
        if (alreadyReadCount != 0 && book.ItemId is not "Book_PriceCatalogue" and not "Book_AnimalCatalogue")
        {
            return contextTagsNativeOrder.Any(tag => tag.StartsWith("book_xp_", StringComparison.OrdinalIgnoreCase))
                ? "power_book_repeated_skill"
                : "power_book_repeated_all_skills";
        }
        if (book.ItemId == "PurpleBook")
        {
            return "purple_book";
        }
        if (book.ItemId == "Book_QueenOfSauce")
        {
            return "queen_of_sauce_first_read";
        }
        return "power_book_first_read";
    }

    private static BookExperienceCall[] BookExperienceCalls(
        StardewValley.Object book,
        string branch,
        string[] contextTagsNativeOrder,
        out string matchedExperienceTag,
        out string status)
    {
        matchedExperienceTag = string.Empty;
        status = "exact";
        if (branch == "skill_book")
        {
            var last = book.ItemId.LastOrDefault();
            if (last is < '0' or > '5')
            {
                status = "blocked_invalid_skill_book_item_id";
                return Array.Empty<BookExperienceCall>();
            }
            return new[] { new BookExperienceCall(SkillId(last - '0'), last - '0', 250) };
        }
        if (branch == "power_book_repeated_skill")
        {
            matchedExperienceTag = contextTagsNativeOrder.First(tag => tag.StartsWith("book_xp_", StringComparison.OrdinalIgnoreCase));
            var tokens = matchedExperienceTag.Split('_');
            var skillIndex = tokens.Length > 2 ? Farmer.getSkillNumberFromName(tokens[2]) : -1;
            if (skillIndex is < 0 or > 5)
            {
                status = "blocked_invalid_book_xp_context_tag";
                return Array.Empty<BookExperienceCall>();
            }
            return new[] { new BookExperienceCall(SkillId(skillIndex), skillIndex, 100) };
        }
        if (branch == "power_book_repeated_all_skills")
        {
            return Enumerable.Range(0, 5).Select(index => new BookExperienceCall(SkillId(index), index, 20)).ToArray();
        }
        if (branch == "purple_book")
        {
            return Enumerable.Range(0, 5).Select(index => new BookExperienceCall(SkillId(index), index, 250)).ToArray();
        }
        return Array.Empty<BookExperienceCall>();
    }

    private static BookExperienceProjection ProjectBookExperience(Farmer player, BookExperienceCall[] calls)
    {
        var aggregate = new Dictionary<int, int>();
        var experience = Enumerable.Range(0, 6).Select(index => player.experiencePoints[index]).ToArray();
        var levels = Enumerable.Range(0, 6).Select(player.GetUnmodifiedSkillLevel).ToArray();
        var levelsBefore = levels.ToArray();
        var queuedLevels = new Dictionary<int, List<int>>();
        var masteryDelta = 0;
        try
        {
            foreach (var call in calls)
            {
                var effectiveAmount = call.SkillIndex == Farmer.luckSkill || call.Amount <= 0 ? 0 : call.Amount;
                var levelBeforeCall = levels.Sum() / 2;
                aggregate[call.SkillIndex] = aggregate.TryGetValue(call.SkillIndex, out var current)
                    ? checked(current + effectiveAmount)
                    : effectiveAmount;
                if (effectiveAmount <= 0)
                {
                    continue;
                }
                if (levelBeforeCall >= 25)
                {
                    masteryDelta = checked(masteryDelta + Math.Max(1, call.SkillIndex == Farmer.farmingSkill ? effectiveAmount / 2 : effectiveAmount));
                }

                var oldExperience = experience[call.SkillIndex];
                var newExperience = checked(oldExperience + effectiveAmount);
                var gainedLevel = Farmer.checkForLevelGain(oldExperience, newExperience);
                experience[call.SkillIndex] = newExperience;
                if (gainedLevel != -1)
                {
                    var oldLevel = levels[call.SkillIndex];
                    levels[call.SkillIndex] = gainedLevel;
                    if (gainedLevel > oldLevel)
                    {
                        queuedLevels[call.SkillIndex] = Enumerable.Range(oldLevel + 1, gainedLevel - oldLevel).ToList();
                    }
                }
            }
        }
        catch (OverflowException)
        {
            return new BookExperienceProjection(
                Array.Empty<BookExperienceDelta>(),
                Array.Empty<BookSkillLevelDelta>(),
                0,
                "blocked_integer_overflow_in_modded_book_data");
        }

        var levelDeltas = aggregate.Keys
            .OrderBy(index => index)
            .Select(index => new BookSkillLevelDelta(
                SkillId(index),
                index,
                levelsBefore[index],
                levels[index],
                queuedLevels.TryGetValue(index, out var entries) ? entries.ToArray() : Array.Empty<int>()))
            .ToArray();
        return new BookExperienceProjection(
            aggregate.OrderBy(pair => pair.Key).Select(pair => new BookExperienceDelta(SkillId(pair.Key), pair.Key, pair.Value)).ToArray(),
            levelDeltas,
            masteryDelta,
            "exact_native_gain_experience_order");
    }

    private static string[] BookUseGateReasons(
        Farmer player,
        StardewValley.Object book,
        string branchStatus,
        string experienceStatus)
    {
        var reasons = new List<string>();
        if (!player.canMove) reasons.Add("book_use_player_cannot_move");
        if (book.isTemporarilyInvisible) reasons.Add("book_use_item_temporarily_invisible");
        if (Game1.eventUp) reasons.Add("book_use_event_active");
        if (Game1.isFestival()) reasons.Add("book_use_festival_active");
        if (Game1.fadeToBlack) reasons.Add("book_use_fade_to_black");
        if (player.swimming.Value) reasons.Add("book_use_player_swimming");
        if (player.bathingClothes.Value) reasons.Add("book_use_bathing_clothes");
        if (player.onBridge.Value) reasons.Add("book_use_player_on_bridge");
        if (Game1.activeClickableMenu is not null) reasons.Add("book_use_active_menu_open");
        if (!string.Equals(branchStatus, "exact", StringComparison.Ordinal)) reasons.Add(branchStatus);
        if (!experienceStatus.StartsWith("exact_", StringComparison.Ordinal)) reasons.Add(experienceStatus);
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string BookNativeFeedbackCallbacks(string branch, int newLevelsBefore, int newLevelsAfter)
    {
        var branchFeedback = branch switch
        {
            "skill_book" when newLevelsAfter == newLevelsBefore || (newLevelsAfter > 1 && newLevelsBefore >= 1) =>
                "delayed_skill_book_message_expected",
            "skill_book" => "skill_book_message_suppressed_for_new_level_menu",
            "power_book_first_read" => "delayed_learned_new_power_message_expected",
            "queen_of_sauce_first_read" => "immediate_queen_of_sauce_recipe_count_message_expected",
            _ => "no_branch_specific_message"
        };
        return "native_book_animation_1000ms;music_duck_4000ms;book_read_sound;" + branchFeedback;
    }

    private static string SkillId(int skillIndex) => Farmer.getSkillNameFromIndex(skillIndex).ToLowerInvariant();

    private sealed record BookExperienceCall(string SkillId, int SkillIndex, int Amount);
    private sealed record BookExperienceDelta(string SkillId, int SkillIndex, int Delta);
    private sealed record BookSkillLevelDelta(
        string SkillId,
        int SkillIndex,
        int LevelBefore,
        int LevelAfter,
        int[] NewLevelsQueued);
    private sealed record BookNewLevelQueueEntry(int SkillIndex, int Level);
    private sealed record BookExperienceProjection(
        BookExperienceDelta[] Deltas,
        BookSkillLevelDelta[] LevelDeltas,
        int MasteryExperienceDelta,
        string Status);
}
