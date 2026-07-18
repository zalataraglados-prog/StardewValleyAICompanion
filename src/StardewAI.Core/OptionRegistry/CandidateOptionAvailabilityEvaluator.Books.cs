using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] BookReadCandidates(SnapshotEnvelope snapshot)
        {
            var books = ReadStateFieldValue(snapshot, "player", "book_candidates");
            if (!books.HasValue || books.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return books.Value.EnumerateArray()
                .Where(book => book.ValueKind == JsonValueKind.Object)
                .Select(book =>
                {
                    var slotIndex = ReadInt(book, "slot_index", -1);
                    var qualifiedItemId = ReadString(book, "qualified_item_id");
                    var itemId = ReadString(book, "item_id");
                    var branch = ReadString(book, "native_branch");
                    var branchStatus = ReadString(book, "native_branch_status");
                    var experienceStatus = ReadString(book, "experience_projection_status");
                    var experienceJson = ReadString(book, "experience_deltas_json");
                    var masteryDelta = ReadIntOptional(book, "mastery_experience_delta");
                    var evidenceValid = TryReadStructuredSkillExperienceDeltas(
                        book,
                        "experience_deltas",
                        experienceJson,
                        out var experienceDeltas);
                    var levelDeltasJson = ReadString(book, "skill_level_deltas_json");
                    var newLevelsBeforeJson = ReadString(book, "new_levels_before_json");
                    var newLevelsAfterJson = ReadString(book, "new_levels_after_json");
                    var levelProjectionValid = TryReadBookLevelProjection(
                        book,
                        levelDeltasJson,
                        newLevelsBeforeJson,
                        newLevelsAfterJson,
                        experienceDeltas);
                    var recipesJson = ReadString(book, "cooking_recipes_added_json");
                    var recipesValid = TryReadBookRecipeProjection(book, recipesJson);
                    var blockReasons = new List<string>(ReadStringArray(book, "block_reasons"));
                    var stackBefore = ReadInt(book, "stack_before", -1);
                    var stackAfter = ReadInt(book, "stack_after", -1);
                    var category = ReadInt(book, "category");
                    var statKey = ReadString(book, "book_stat_key");
                    var statBefore = NullableNumberText(book, "book_stat_before");
                    var statAfter = NullableNumberText(book, "book_stat_after");
                    var tagsValid = TryReadExactStringArray(book, "context_tags_native_order", ReadString(book, "context_tags_native_order_json"), out _);
                    var mailBeforeValid = TryReadBoolean(book, "read_a_book_mail_before", out _);
                    var mailAfterValid = TryReadBoolean(book, "read_a_book_mail_after", out _);
                    var achievementBeforeValid = TryReadBoolean(book, "well_read_achievement_before", out _);
                    var achievementAfterValid = TryReadBoolean(book, "well_read_achievement_after", out _);
                    var achievementUnlockValid = TryReadBoolean(book, "well_read_achievement_will_unlock", out _);
                    var hatterBeforeValid = TryReadBoolean(book, "well_read_hatter_mail_before", out _);
                    var hatterAfterValid = TryReadBoolean(book, "well_read_hatter_mail_after", out _);
                    var dialogueBeforeValid = TryReadBoolean(book, "well_read_dialogue_event_seen_before", out _);
                    var dialogueAfterValid = TryReadBoolean(book, "well_read_dialogue_event_seen_after", out _);
                    var nativeFeedbackCallbacks = ReadString(book, "native_feedback_callbacks");
                    if (slotIndex < 0 || string.IsNullOrWhiteSpace(qualifiedItemId) || string.IsNullOrWhiteSpace(itemId) ||
                        string.IsNullOrWhiteSpace(ReadString(book, "runtime_type")))
                    {
                        blockReasons.Add("book_inventory_identity_unavailable");
                    }
                    if (stackBefore <= 0 || stackAfter != stackBefore - 1 || category is not -102 and not -103)
                    {
                        blockReasons.Add("book_inventory_consumption_projection_invalid");
                    }
                    if (!string.Equals(branchStatus, "exact", StringComparison.Ordinal) || !KnownBookBranch(branch))
                    {
                        blockReasons.Add("book_native_branch_unavailable");
                    }
                    if (!experienceStatus.StartsWith("exact_", StringComparison.Ordinal) || !masteryDelta.HasValue || !evidenceValid ||
                        !levelProjectionValid)
                    {
                        blockReasons.Add("book_experience_projection_unavailable");
                    }
                    if (!recipesValid)
                    {
                        blockReasons.Add("book_recipe_projection_unavailable");
                    }
                    if (!tagsValid || !mailBeforeValid || !mailAfterValid || !achievementBeforeValid || !achievementAfterValid ||
                        !achievementUnlockValid || !hatterBeforeValid || !hatterAfterValid || !dialogueBeforeValid || !dialogueAfterValid ||
                        string.IsNullOrWhiteSpace(nativeFeedbackCallbacks) ||
                        (string.IsNullOrWhiteSpace(statKey) ? statBefore.Length > 0 || statAfter.Length > 0 : statBefore.Length == 0 || statAfter.Length == 0))
                    {
                        blockReasons.Add("book_state_projection_internally_inconsistent");
                    }
                    if (ReadBool(book, "available") != true)
                    {
                        blockReasons.Add("book_native_use_gate_blocked");
                    }

                    var parameters = BookParameters(book, experienceJson, masteryDelta ?? 0, recipesJson);
                    var positiveExperience = experienceDeltas.Where(delta => delta.Delta > 0).ToArray();
                    if (positiveExperience.Length == 1)
                    {
                        parameters.Add(Parameter("skill_experience_skill_id", positiveExperience[0].SkillId));
                        parameters.Add(Parameter("skill_experience_on_success_min", positiveExperience[0].Delta.ToString()));
                        parameters.Add(Parameter("skill_experience_on_success_max", positiveExperience[0].Delta.ToString()));
                    }

                    return new EventCandidate
                    {
                        CandidateId = "read-book:" + slotIndex + ":" + qualifiedItemId + ":" + branch,
                        Kind = "read_inventory_book",
                        Available = blockReasons.Count == 0,
                        LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                        ItemId = itemId,
                        QualifiedItemId = qualifiedItemId,
                        SlotIndex = slotIndex,
                        Quantity = 1,
                        EstimatedTicks = 90,
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_native_book_read",
                        ExpectedEffect = "player.inventory[" + slotIndex + "].stack=" + ReadInt(book, "stack_after") +
                            ";book_native_branch=" + branch +
                            ";expected_skill_experience_deltas_json=" + experienceJson +
                            ";expected_mastery_experience_delta=" + (masteryDelta ?? 0) +
                            ";book_stat_key=" + ReadString(book, "book_stat_key") +
                            ";book_stat_after=" + NullableNumberText(book, "book_stat_after") +
                            ";cooking_recipes_added_json=" + recipesJson,
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = parameters.ToArray()
                    };
                })
                .OrderBy(candidate => candidate.SlotIndex ?? int.MaxValue)
                .ThenBy(candidate => candidate.QualifiedItemId, StringComparer.Ordinal)
                .ToArray();
        }

        private static List<SmallModelActionParameter> BookParameters(
            JsonElement book,
            string experienceJson,
            int masteryDelta,
            string recipesJson)
        {
            return new List<SmallModelActionParameter>
            {
                Parameter("slot_index", ReadInt(book, "slot_index").ToString()),
                Parameter("qualified_item_id", ReadString(book, "qualified_item_id")),
                Parameter("item_id", ReadString(book, "item_id")),
                Parameter("book_runtime_type", ReadString(book, "runtime_type")),
                Parameter("book_category", ReadInt(book, "category").ToString()),
                Parameter("book_stack_before", ReadInt(book, "stack_before").ToString()),
                Parameter("book_stack_after", ReadInt(book, "stack_after").ToString()),
                Parameter("book_native_branch", ReadString(book, "native_branch")),
                Parameter("book_native_branch_status", ReadString(book, "native_branch_status")),
                Parameter("book_context_tags_native_order_json", ReadString(book, "context_tags_native_order_json")),
                Parameter("book_matched_experience_tag", ReadString(book, "matched_book_experience_tag")),
                Parameter("expected_skill_experience_deltas_json", experienceJson),
                Parameter("book_skill_level_deltas_json", ReadString(book, "skill_level_deltas_json")),
                Parameter("book_new_levels_before_json", ReadString(book, "new_levels_before_json")),
                Parameter("book_new_levels_after_json", ReadString(book, "new_levels_after_json")),
                Parameter("book_native_feedback_callbacks", ReadString(book, "native_feedback_callbacks")),
                Parameter("expected_mastery_experience_delta", masteryDelta.ToString()),
                Parameter("skill_experience_projection_status", ReadString(book, "experience_projection_status")),
                Parameter("skill_experience_condition", "native_book_read"),
                Parameter("book_stat_key", ReadString(book, "book_stat_key")),
                Parameter("book_stat_before", NullableNumberText(book, "book_stat_before")),
                Parameter("book_stat_after", NullableNumberText(book, "book_stat_after")),
                Parameter("read_a_book_mail_before", BoolText(book, "read_a_book_mail_before")),
                Parameter("read_a_book_mail_after", BoolText(book, "read_a_book_mail_after")),
                Parameter("well_read_achievement_before", BoolText(book, "well_read_achievement_before")),
                Parameter("well_read_achievement_after", BoolText(book, "well_read_achievement_after")),
                Parameter("well_read_achievement_will_unlock", BoolText(book, "well_read_achievement_will_unlock")),
                Parameter("well_read_hatter_mail_before", BoolText(book, "well_read_hatter_mail_before")),
                Parameter("well_read_hatter_mail_after", BoolText(book, "well_read_hatter_mail_after")),
                Parameter("well_read_dialogue_event_seen_before", BoolText(book, "well_read_dialogue_event_seen_before")),
                Parameter("well_read_dialogue_event_seen_after", BoolText(book, "well_read_dialogue_event_seen_after")),
                Parameter("well_read_ui_sound_platform_callbacks", ReadString(book, "well_read_ui_sound_platform_callbacks")),
                Parameter("cooking_recipes_added_json", recipesJson),
                Parameter("cooking_recipes_added_count", ReadInt(book, "cooking_recipes_added_count").ToString())
            };
        }

        private static bool TryReadBookRecipeProjection(JsonElement book, string serializedRecipes)
        {
            if (!TryReadExactStringArray(book, "cooking_recipes_added", serializedRecipes, out var recipes))
            {
                return false;
            }
            return recipes.Distinct(StringComparer.Ordinal).Count() == recipes.Length &&
                ReadInt(book, "cooking_recipes_added_count", -1) == recipes.Length;
        }

        private static bool TryReadExactStringArray(
            JsonElement source,
            string arrayProperty,
            string serializedRows,
            out string[] values)
        {
            values = Array.Empty<string>();
            if (!source.TryGetProperty(arrayProperty, out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            var transparent = rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.String)
                .Select(row => row.GetString() ?? string.Empty)
                .ToArray();
            if (transparent.Length != rows.GetArrayLength())
            {
                return false;
            }
            try
            {
                var serialized = JsonSerializer.Deserialize<string[]>(serializedRows);
                if (serialized is null || !transparent.SequenceEqual(serialized, StringComparer.Ordinal))
                {
                    return false;
                }
                values = transparent;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadExactJsonArray(JsonElement source, string arrayProperty, string serializedRows)
        {
            if (!source.TryGetProperty(arrayProperty, out var rows) || rows.ValueKind != JsonValueKind.Array ||
                string.IsNullOrWhiteSpace(serializedRows))
            {
                return false;
            }
            try
            {
                using var document = JsonDocument.Parse(serializedRows);
                return document.RootElement.ValueKind == JsonValueKind.Array &&
                    string.Equals(
                        JsonSerializer.Serialize(rows),
                        JsonSerializer.Serialize(document.RootElement),
                        StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadBookLevelProjection(
            JsonElement book,
            string levelDeltasJson,
            string newLevelsBeforeJson,
            string newLevelsAfterJson,
            StructuredSkillExperienceDelta[] experienceDeltas)
        {
            if (!TryReadExactJsonArray(book, "skill_level_deltas", levelDeltasJson) ||
                !TryReadExactJsonArray(book, "new_levels_before", newLevelsBeforeJson) ||
                !TryReadExactJsonArray(book, "new_levels_after", newLevelsAfterJson) ||
                !book.TryGetProperty("skill_level_deltas", out var levelRows) ||
                !book.TryGetProperty("new_levels_before", out var beforeRows) ||
                !book.TryGetProperty("new_levels_after", out var afterRows) ||
                !TryParseBookLevelDeltas(levelRows, out var levels) ||
                !TryParseBookNewLevelQueue(beforeRows, out var before) ||
                !TryParseBookNewLevelQueue(afterRows, out var after) ||
                !levels.Select(level => level.SkillIndex).SequenceEqual(experienceDeltas.Select(delta => delta.SkillIndex)))
            {
                return false;
            }

            var expectedAfter = before
                .Concat(levels.SelectMany(level => level.NewLevelsQueued.Select(value =>
                    new BookNewLevelQueueProjection(level.SkillIndex, value))))
                .ToArray();
            return expectedAfter.SequenceEqual(after);
        }

        private static bool TryParseBookLevelDeltas(JsonElement rows, out BookLevelDeltaProjection[] levels)
        {
            levels = Array.Empty<BookLevelDeltaProjection>();
            if (rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            var parsed = new List<BookLevelDeltaProjection>();
            var seen = new HashSet<int>();
            foreach (var row in rows.EnumerateArray())
            {
                var skillId = ReadString(row, "SkillId", ReadString(row, "skillId"));
                var skillIndex = ReadInt(row, "SkillIndex", ReadInt(row, "skillIndex", -1));
                var before = ReadInt(row, "LevelBefore", ReadInt(row, "levelBefore", -1));
                var after = ReadInt(row, "LevelAfter", ReadInt(row, "levelAfter", -1));
                if (row.ValueKind != JsonValueKind.Object || skillIndex is < 0 or > 5 || before < 0 || after < before ||
                    !string.Equals(skillId, NativeSkillId(skillIndex), StringComparison.Ordinal) || !seen.Add(skillIndex) ||
                    !TryReadIntArray(row, "NewLevelsQueued", "newLevelsQueued", out var queued) ||
                    !queued.SequenceEqual(Enumerable.Range(before + 1, after - before)))
                {
                    return false;
                }
                parsed.Add(new BookLevelDeltaProjection(skillId, skillIndex, before, after, queued));
            }
            levels = parsed.ToArray();
            return true;
        }

        private static bool TryParseBookNewLevelQueue(JsonElement rows, out BookNewLevelQueueProjection[] queue)
        {
            queue = Array.Empty<BookNewLevelQueueProjection>();
            if (rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            var parsed = new List<BookNewLevelQueueProjection>();
            foreach (var row in rows.EnumerateArray())
            {
                var skillIndex = ReadInt(row, "SkillIndex", ReadInt(row, "skillIndex", -1));
                var level = ReadInt(row, "Level", ReadInt(row, "level", -1));
                if (row.ValueKind != JsonValueKind.Object || skillIndex is < 0 or > 5 || level < 0)
                {
                    return false;
                }
                parsed.Add(new BookNewLevelQueueProjection(skillIndex, level));
            }
            queue = parsed.ToArray();
            return true;
        }

        private static bool TryReadIntArray(
            JsonElement source,
            string property,
            string alternateProperty,
            out int[] values)
        {
            values = Array.Empty<int>();
            if ((!source.TryGetProperty(property, out var rows) && !source.TryGetProperty(alternateProperty, out rows)) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            var parsed = new List<int>();
            foreach (var row in rows.EnumerateArray())
            {
                if (!row.TryGetInt32(out var value))
                {
                    return false;
                }
                parsed.Add(value);
            }
            values = parsed.ToArray();
            return true;
        }

        private static bool KnownBookBranch(string branch) => branch is
            "skill_book" or
            "power_book_repeated_skill" or
            "power_book_repeated_all_skills" or
            "purple_book" or
            "power_book_first_read" or
            "queen_of_sauce_first_read";

        private static string NullableNumberText(JsonElement source, string property)
        {
            if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return string.Empty;
            }
            return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var parsed)
                ? parsed.ToString()
                : string.Empty;
        }

        private static bool TryReadBoolean(JsonElement source, string property, out bool value)
        {
            value = false;
            if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(property, out var field))
            {
                return false;
            }
            if (field.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }
            return field.ValueKind == JsonValueKind.False;
        }

        private static string BoolText(JsonElement source, string property) =>
            ReadBool(source, property) == true ? "true" : "false";

        private sealed record BookLevelDeltaProjection(
            string SkillId,
            int SkillIndex,
            int LevelBefore,
            int LevelAfter,
            int[] NewLevelsQueued);
        private sealed record BookNewLevelQueueProjection(int SkillIndex, int Level);
    }
}
