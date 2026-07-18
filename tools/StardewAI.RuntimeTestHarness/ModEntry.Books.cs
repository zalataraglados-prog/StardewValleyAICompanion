using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly string[] WellReadBookStatKeys =
    {
        "Book_Trash", "Book_Crabbing", "Book_Bombs", "Book_Roe", "Book_WildSeeds", "Book_Woodcutting",
        "Book_Defense", "Book_Friendship", "Book_Void", "Book_Speed", "Book_Marlon", "Book_PriceCatalogue",
        "Book_Diamonds", "Book_Mystery", "Book_AnimalCatalogue", "Book_Speed2", "Book_Artifact", "Book_Horse", "Book_Grass"
    };

    private TrainingExecutionResult ExecuteReadBook(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var requested = "player.inventory[" + request.SlotIndex + "].read=" + request.QualifiedItemId;
        if (ValidateExecutionRequest(request).Count > 0 || !request.SlotIndex.HasValue ||
            request.SlotIndex.Value < 0 || request.SlotIndex.Value >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(request, "read_book", requested, "book=unresolved", "read_book_request_invalid");
        }

        var slot = request.SlotIndex.Value;
        if (Game1.activeClickableMenu is not null || !Game1.player.canMove || Game1.eventUp || Game1.isFestival() ||
            Game1.fadeToBlack || Game1.player.swimming.Value || Game1.player.bathingClothes.Value || Game1.player.onBridge.Value)
        {
            return BlockedWithPrimitive(request, "read_book", requested, BookObservedEffect(slot), "read_book_native_use_gate_blocked");
        }

        if (Game1.player.Items[slot] is not StardewValley.Object book ||
            !string.Equals(book.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(book.ItemId, request.ItemId, StringComparison.Ordinal) ||
            !string.Equals(book.GetType().FullName ?? book.GetType().Name, request.BookRuntimeType, StringComparison.Ordinal) ||
            book.Category != request.BookCategory || book.Stack != request.BookStackBefore || book.isTemporarilyInvisible)
        {
            return BlockedWithPrimitive(request, "read_book", requested, BookObservedEffect(slot), "read_book_inventory_identity_drifted");
        }

        if (!TryProjectBookRead(book, out var projection) ||
            !TryReadExpectedSkillExperience(request, out var expectedExperience, out var expectedMasteryDelta) ||
            !TryReadExpectedRecipes(request, out var expectedRecipes) ||
            !string.Equals(request.BookNativeBranchStatus, "exact", StringComparison.Ordinal) ||
            !string.Equals(request.BookNativeBranch, projection.Branch, StringComparison.Ordinal) ||
            !string.Equals(request.BookContextTagsNativeOrderJson, projection.ContextTagsJson, StringComparison.Ordinal) ||
            !string.Equals(request.BookMatchedExperienceTag, projection.MatchedExperienceTag, StringComparison.Ordinal) ||
            !string.Equals(request.BookSkillLevelDeltasJson, projection.SkillLevelDeltasJson, StringComparison.Ordinal) ||
            !string.Equals(request.BookNewLevelsBeforeJson, projection.NewLevelsBeforeJson, StringComparison.Ordinal) ||
            !string.Equals(request.BookNewLevelsAfterJson, projection.NewLevelsAfterJson, StringComparison.Ordinal) ||
            !string.Equals(request.BookNativeFeedbackCallbacks, projection.NativeFeedbackCallbacks, StringComparison.Ordinal) ||
            !expectedExperience.SequenceEqual(projection.ExperienceDeltas) ||
            expectedMasteryDelta != projection.MasteryExperienceDelta ||
            !string.Equals(request.BookStatKey, projection.StatKey, StringComparison.Ordinal) ||
            !string.Equals(request.BookStatBefore, projection.StatBefore, StringComparison.Ordinal) ||
            !string.Equals(request.BookStatAfter, projection.StatAfter, StringComparison.Ordinal) ||
            request.ReadABookMailBefore != projection.ReadMailBefore ||
            request.ReadABookMailAfter != projection.ReadMailAfter ||
            request.WellReadAchievementBefore != projection.WellReadAchievementBefore ||
            request.WellReadAchievementAfter != projection.WellReadAchievementAfter ||
            request.WellReadAchievementWillUnlock != projection.WellReadAchievementWillUnlock ||
            request.WellReadHatterMailBefore != projection.WellReadHatterMailBefore ||
            request.WellReadHatterMailAfter != projection.WellReadHatterMailAfter ||
            request.WellReadDialogueEventSeenBefore != projection.WellReadDialogueEventSeenBefore ||
            request.WellReadDialogueEventSeenAfter != projection.WellReadDialogueEventSeenAfter ||
            !string.Equals(request.WellReadUiSoundPlatformCallbacks, projection.WellReadUiSoundPlatformCallbacks, StringComparison.Ordinal) ||
            !expectedRecipes.SequenceEqual(projection.RecipesAdded, StringComparer.Ordinal) ||
            request.CookingRecipesAddedCount != projection.RecipesAdded.Length ||
            request.BookStackAfter != Math.Max(0, book.Stack - 1))
        {
            return BlockedWithPrimitive(request, "read_book", requested, BookObservedEffect(slot), "read_book_projection_drifted");
        }

        var experienceBefore = Enumerable.Range(0, 6).Select(index => Game1.player.experiencePoints[index]).ToArray();
        var skillLevelsBefore = Enumerable.Range(0, 6).Select(Game1.player.GetUnmodifiedSkillLevel).ToArray();
        var newLevelsBeforeJson = SerializeNewLevelQueue();
        var masteryBefore = (int)Game1.stats.Get("MasteryExp");
        var recipesBefore = Game1.player.cookingRecipes.Keys.ToHashSet(StringComparer.Ordinal);
        var mailBefore = Game1.player.mailReceived.Contains("read_a_book");
        var achievementBefore = Game1.player.achievements.Contains(35);
        var hatterMailBefore = Game1.player.hasOrWillReceiveMail("hatter");
        var dialogueEventBefore = Game1.player.hasSeenActiveDialogueEvent("achievement_35");
        var statBefore = string.IsNullOrWhiteSpace(projection.StatKey)
            ? string.Empty
            : Game1.player.stats.Get(projection.StatKey).ToString(CultureInfo.InvariantCulture);

        Game1.player.CurrentToolIndex = slot;
        var used = book.performUseAction(Game1.player.currentLocation);
        if (used)
        {
            Game1.player.reduceActiveItemByOne();
        }

        var actualExperience = Enumerable.Range(0, 6)
            .Select(index => new SkillExperienceDelta(
                Farmer.getSkillNameFromIndex(index).ToLowerInvariant(),
                index,
                Game1.player.experiencePoints[index] - experienceBefore[index]))
            .Where(delta => delta.Delta != 0 || expectedExperience.Any(expected => expected.SkillIndex == delta.SkillIndex))
            .ToArray();
        var skillLevelsAfter = Enumerable.Range(0, 6).Select(Game1.player.GetUnmodifiedSkillLevel).ToArray();
        var actualSkillLevelDeltasJson = JsonSerializer.Serialize(projection.SkillLevelDeltas.Select(delta => new BookSkillLevelDelta(
            delta.SkillId,
            delta.SkillIndex,
            skillLevelsBefore[delta.SkillIndex],
            skillLevelsAfter[delta.SkillIndex],
            delta.NewLevelsQueued)));
        var newLevelsAfterJson = SerializeNewLevelQueue();
        var actualMasteryDelta = (int)Game1.stats.Get("MasteryExp") - masteryBefore;
        var actualRecipes = Game1.player.cookingRecipes.Keys
            .Where(recipe => !recipesBefore.Contains(recipe))
            .OrderBy(recipe => recipe, StringComparer.Ordinal)
            .ToArray();
        var actualMailAfter = Game1.player.mailReceived.Contains("read_a_book");
        var actualAchievementAfter = Game1.player.achievements.Contains(35);
        var actualHatterMailAfter = Game1.player.hasOrWillReceiveMail("hatter");
        var actualDialogueEventAfter = Game1.player.hasSeenActiveDialogueEvent("achievement_35");
        var actualStatAfter = string.IsNullOrWhiteSpace(projection.StatKey)
            ? string.Empty
            : Game1.player.stats.Get(projection.StatKey).ToString(CultureInfo.InvariantCulture);
        var itemAfter = slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        var stackVerified = request.BookStackAfter == 0
            ? itemAfter is null
            : itemAfter is StardewValley.Object remaining &&
                string.Equals(remaining.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) &&
                remaining.Stack == request.BookStackAfter;
        var verified = used && stackVerified &&
            actualExperience.SequenceEqual(expectedExperience) &&
            string.Equals(actualSkillLevelDeltasJson, projection.SkillLevelDeltasJson, StringComparison.Ordinal) &&
            string.Equals(newLevelsBeforeJson, projection.NewLevelsBeforeJson, StringComparison.Ordinal) &&
            string.Equals(newLevelsAfterJson, projection.NewLevelsAfterJson, StringComparison.Ordinal) &&
            actualMasteryDelta == expectedMasteryDelta &&
            string.Equals(statBefore, projection.StatBefore, StringComparison.Ordinal) &&
            string.Equals(actualStatAfter, projection.StatAfter, StringComparison.Ordinal) &&
            mailBefore == projection.ReadMailBefore && actualMailAfter == projection.ReadMailAfter &&
            achievementBefore == projection.WellReadAchievementBefore && actualAchievementAfter == projection.WellReadAchievementAfter &&
            hatterMailBefore == projection.WellReadHatterMailBefore && actualHatterMailAfter == projection.WellReadHatterMailAfter &&
            dialogueEventBefore == projection.WellReadDialogueEventSeenBefore && actualDialogueEventAfter == projection.WellReadDialogueEventSeenAfter &&
            actualRecipes.SequenceEqual(expectedRecipes, StringComparer.Ordinal);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "read_book",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_book_performUseAction_succeeded", "one_book_consumed", "all_book_effects_verified" }
                : new[] { used ? "performUseAction_returned_true" : "performUseAction_returned_false", stackVerified ? "book_stack_verified" : "book_stack_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = BookObservedEffect(slot) +
                ";skill_experience_deltas_json=" + JsonSerializer.Serialize(actualExperience) +
                ";skill_level_deltas_json=" + actualSkillLevelDeltasJson +
                ";new_levels_after_json=" + newLevelsAfterJson +
                ";mastery_experience_delta=" + actualMasteryDelta +
                ";book_stat_after=" + actualStatAfter +
                ";recipes_added_json=" + JsonSerializer.Serialize(actualRecipes),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "read_book_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slot + "]",
                        Before = request.QualifiedItemId + "x" + request.BookStackBefore,
                        After = request.BookStackAfter == 0 ? "null" : request.QualifiedItemId + "x" + request.BookStackAfter
                    }
                }
                .Concat(actualExperience.Where(delta => delta.Delta != 0).Select(delta => new SimulatedFactChange
                {
                    Path = "player.skills." + delta.SkillId + ".experience",
                    Before = experienceBefore[delta.SkillIndex].ToString(CultureInfo.InvariantCulture),
                    After = Game1.player.experiencePoints[delta.SkillIndex].ToString(CultureInfo.InvariantCulture)
                }))
                .Concat(projection.SkillLevelDeltas
                    .Where(delta => delta.LevelAfter != delta.LevelBefore)
                    .Select(delta => new SimulatedFactChange
                    {
                        Path = "player.skills." + delta.SkillId + ".unmodified_level",
                        Before = delta.LevelBefore.ToString(CultureInfo.InvariantCulture),
                        After = delta.LevelAfter.ToString(CultureInfo.InvariantCulture)
                    }))
                .ToArray()
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static bool TryProjectBookRead(StardewValley.Object book, out BookReadRuntimeProjection projection)
    {
        projection = default!;
        var useMethod = book.GetType().GetMethod(nameof(StardewValley.Object.performUseAction), new[] { typeof(GameLocation) });
        if (useMethod?.DeclaringType != typeof(StardewValley.Object) ||
            book.Category is not StardewValley.Object.booksCategory and not StardewValley.Object.skillBooksCategory)
        {
            return false;
        }

        var tags = book.GetContextTags().ToArray();
        var statCount = Game1.player.stats.Get(book.ItemId);
        string branch;
        if (book.ItemId.StartsWith("SkillBook_", StringComparison.Ordinal))
        {
            branch = "skill_book";
        }
        else if (statCount != 0 && book.ItemId is not "Book_PriceCatalogue" and not "Book_AnimalCatalogue")
        {
            branch = tags.Any(tag => tag.StartsWith("book_xp_", StringComparison.OrdinalIgnoreCase))
                ? "power_book_repeated_skill"
                : "power_book_repeated_all_skills";
        }
        else if (book.ItemId == "PurpleBook")
        {
            branch = "purple_book";
        }
        else if (book.ItemId == "Book_QueenOfSauce")
        {
            branch = "queen_of_sauce_first_read";
        }
        else
        {
            branch = "power_book_first_read";
        }

        var matchedTag = string.Empty;
        var calls = new List<(int SkillIndex, int Amount)>();
        if (branch == "skill_book")
        {
            var last = book.ItemId.LastOrDefault();
            if (last is < '0' or > '5') return false;
            calls.Add((last - '0', 250));
        }
        else if (branch == "power_book_repeated_skill")
        {
            matchedTag = tags.First(tag => tag.StartsWith("book_xp_", StringComparison.OrdinalIgnoreCase));
            var parts = matchedTag.Split('_');
            var skillIndex = parts.Length > 2 ? Farmer.getSkillNumberFromName(parts[2]) : -1;
            if (skillIndex is < 0 or > 5) return false;
            calls.Add((skillIndex, 100));
        }
        else if (branch == "power_book_repeated_all_skills")
        {
            calls.AddRange(Enumerable.Range(0, 5).Select(index => (index, 20)));
        }
        else if (branch == "purple_book")
        {
            calls.AddRange(Enumerable.Range(0, 5).Select(index => (index, 250)));
        }

        if (!TryProjectBookExperience(calls, out var deltas, out var skillLevelDeltas, out var masteryDelta))
        {
            return false;
        }
        var incrementsStat = branch is "power_book_first_read" or "queen_of_sauce_first_read";
        var addsMail = branch is "power_book_first_read" or "power_book_repeated_skill" or "power_book_repeated_all_skills";
        var wellReadBefore = Game1.player.achievements.Contains(35);
        var projectedStatAfter = incrementsStat ? unchecked(statCount + 1u) : statCount;
        var wellReadCriteriaAfter = branch == "power_book_first_read" && WellReadBookStatKeys.All(
            key => string.Equals(key, book.ItemId, StringComparison.Ordinal) ? projectedStatAfter != 0 : Game1.player.stats.Get(key) != 0);
        var wellReadWillUnlock = !wellReadBefore && wellReadCriteriaAfter && Game1.gameMode == 3 &&
            Game1.achievements?.ContainsKey(35) == true;
        var wellReadAfter = wellReadBefore || wellReadWillUnlock;
        var hatterMailBefore = Game1.player.hasOrWillReceiveMail("hatter");
        var dialogueEventBefore = Game1.player.hasSeenActiveDialogueEvent("achievement_35");
        var recipes = branch == "queen_of_sauce_first_read"
            ? DataLoader.Tv_CookingChannel(Game1.content).Values
                .Select(value => value.Split('/')[0])
                .Where(recipe => !Game1.player.cookingRecipes.ContainsKey(recipe))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(recipe => recipe, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        var newLevelsBefore = Game1.player.newLevels
            .Select(level => new BookNewLevelQueueEntry(level.X, level.Y))
            .ToArray();
        var newLevelsAfter = newLevelsBefore
            .Concat(skillLevelDeltas.SelectMany(delta => delta.NewLevelsQueued
                .Select(level => new BookNewLevelQueueEntry(delta.SkillIndex, level))))
            .ToArray();
        projection = new BookReadRuntimeProjection(
            branch,
            JsonSerializer.Serialize(tags),
            matchedTag,
            deltas,
            skillLevelDeltas,
            JsonSerializer.Serialize(skillLevelDeltas),
            JsonSerializer.Serialize(newLevelsBefore),
            JsonSerializer.Serialize(newLevelsAfter),
            BookNativeFeedbackCallbacks(branch, newLevelsBefore.Length, newLevelsAfter.Length),
            masteryDelta,
            incrementsStat ? book.ItemId : string.Empty,
            incrementsStat ? statCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
            incrementsStat ? unchecked(statCount + 1u).ToString(CultureInfo.InvariantCulture) : string.Empty,
            Game1.player.mailReceived.Contains("read_a_book"),
            addsMail || Game1.player.mailReceived.Contains("read_a_book"),
            wellReadBefore,
            wellReadAfter,
            wellReadWillUnlock,
            hatterMailBefore,
            hatterMailBefore || wellReadWillUnlock,
            dialogueEventBefore,
            dialogueEventBefore || wellReadWillUnlock,
            wellReadWillUnlock ? "native_runtime_callbacks_expected" : "not_triggered",
            recipes);
        return true;
    }

    private static bool TryProjectBookExperience(
        IEnumerable<(int SkillIndex, int Amount)> calls,
        out SkillExperienceDelta[] deltas,
        out BookSkillLevelDelta[] levelDeltas,
        out int masteryDelta)
    {
        deltas = Array.Empty<SkillExperienceDelta>();
        levelDeltas = Array.Empty<BookSkillLevelDelta>();
        masteryDelta = 0;
        var aggregate = new Dictionary<int, int>();
        var experience = Enumerable.Range(0, 6).Select(index => Game1.player.experiencePoints[index]).ToArray();
        var levels = Enumerable.Range(0, 6).Select(Game1.player.GetUnmodifiedSkillLevel).ToArray();
        var levelsBefore = levels.ToArray();
        var queuedLevels = new Dictionary<int, List<int>>();
        try
        {
            foreach (var call in calls)
            {
                var effective = call.SkillIndex == Farmer.luckSkill || call.Amount <= 0 ? 0 : call.Amount;
                var levelBefore = levels.Sum() / 2;
                aggregate[call.SkillIndex] = aggregate.TryGetValue(call.SkillIndex, out var current)
                    ? checked(current + effective)
                    : effective;
                if (effective <= 0) continue;
                if (levelBefore >= 25)
                {
                    masteryDelta = checked(masteryDelta + Math.Max(1, call.SkillIndex == Farmer.farmingSkill ? effective / 2 : effective));
                }
                var oldExperience = experience[call.SkillIndex];
                var newExperience = checked(oldExperience + effective);
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
            deltas = Array.Empty<SkillExperienceDelta>();
            levelDeltas = Array.Empty<BookSkillLevelDelta>();
            masteryDelta = 0;
            return false;
        }
        deltas = aggregate.OrderBy(pair => pair.Key)
            .Select(pair => new SkillExperienceDelta(Farmer.getSkillNameFromIndex(pair.Key).ToLowerInvariant(), pair.Key, pair.Value))
            .ToArray();
        levelDeltas = aggregate.Keys.OrderBy(index => index)
            .Select(index => new BookSkillLevelDelta(
                Farmer.getSkillNameFromIndex(index).ToLowerInvariant(),
                index,
                levelsBefore[index],
                levels[index],
                queuedLevels.TryGetValue(index, out var entries) ? entries.ToArray() : Array.Empty<int>()))
            .ToArray();
        return true;
    }

    private static string SerializeNewLevelQueue() => JsonSerializer.Serialize(
        Game1.player.newLevels.Select(level => new BookNewLevelQueueEntry(level.X, level.Y)).ToArray());

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

    private static bool TryReadExpectedRecipes(TrainingExecutionRequest request, out string[] recipes)
    {
        recipes = Array.Empty<string>();
        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(request.CookingRecipesAddedJson, JsonOptions);
            if (parsed is null || parsed.Distinct(StringComparer.Ordinal).Count() != parsed.Length)
            {
                return false;
            }
            recipes = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BookObservedEffect(int slotIndex)
    {
        var item = slotIndex >= 0 && slotIndex < Game1.player.Items.Count ? Game1.player.Items[slotIndex] : null;
        return "slot_index=" + slotIndex +
            ";qualified_item_id=" + (item?.QualifiedItemId ?? "null") +
            ";stack=" + (item?.Stack ?? 0) +
            ";can_move=" + Game1.player.canMove.ToString().ToLowerInvariant();
    }

    private sealed record BookReadRuntimeProjection(
        string Branch,
        string ContextTagsJson,
        string MatchedExperienceTag,
        SkillExperienceDelta[] ExperienceDeltas,
        BookSkillLevelDelta[] SkillLevelDeltas,
        string SkillLevelDeltasJson,
        string NewLevelsBeforeJson,
        string NewLevelsAfterJson,
        string NativeFeedbackCallbacks,
        int MasteryExperienceDelta,
        string StatKey,
        string StatBefore,
        string StatAfter,
        bool ReadMailBefore,
        bool ReadMailAfter,
        bool WellReadAchievementBefore,
        bool WellReadAchievementAfter,
        bool WellReadAchievementWillUnlock,
        bool WellReadHatterMailBefore,
        bool WellReadHatterMailAfter,
        bool WellReadDialogueEventSeenBefore,
        bool WellReadDialogueEventSeenAfter,
        string WellReadUiSoundPlatformCallbacks,
        string[] RecipesAdded);
    private sealed record BookSkillLevelDelta(
        string SkillId,
        int SkillIndex,
        int LevelBefore,
        int LevelAfter,
        int[] NewLevelsQueued);
    private sealed record BookNewLevelQueueEntry(int SkillIndex, int Level);
}
