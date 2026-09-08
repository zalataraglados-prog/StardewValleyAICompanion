using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MasterAnglerTargetDateIntentBuilder
{
    private static HashSet<string> ReadReadyCrabPotOutputs(JsonElement state)
    {
        if (!TryReadFieldValue(state, "current_location", "objects", out var objects) ||
            objects.ValueKind != JsonValueKind.Array)
            return new HashSet<string>(StringComparer.Ordinal);

        return objects.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("crab_pot_collect_status", out var status) &&
                status.ValueKind == JsonValueKind.String && status.GetString() == "ready" &&
                item.TryGetProperty("crab_pot_fish_collection_eligible", out var eligible) &&
                eligible.ValueKind == JsonValueKind.True &&
                item.TryGetProperty("crab_pot_output_qualified_item_id", out var output) &&
                output.ValueKind == JsonValueKind.String)
            .Select(item => item.GetProperty("crab_pot_output_qualified_item_id").GetString() ?? string.Empty)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ReadRuntimeEligibleLocationRules(JsonElement state)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!TryReadFieldValue(state, "fishing", "rod_contexts", out var contexts) ||
            contexts.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var context in contexts.EnumerateArray())
        {
            if (context.ValueKind != JsonValueKind.Object ||
                !context.TryGetProperty("complete", out var complete) ||
                complete.ValueKind != JsonValueKind.True ||
                !context.TryGetProperty("spawn_rules", out var spawnRules) ||
                spawnRules.ValueKind != JsonValueKind.Object ||
                !spawnRules.TryGetProperty("item_query_resolution_complete", out var resolutionComplete) ||
                resolutionComplete.ValueKind != JsonValueKind.True ||
                !spawnRules.TryGetProperty("rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object ||
                    !rule.TryGetProperty("condition_met", out var conditionMet) ||
                    conditionMet.ValueKind != JsonValueKind.True ||
                    !rule.TryGetProperty("eligible_before_random_rolls", out var eligible) ||
                    eligible.ValueKind != JsonValueKind.True ||
                    !rule.TryGetProperty("source", out var source) ||
                    source.ValueKind != JsonValueKind.String ||
                    !rule.TryGetProperty("source_index", out var sourceIndex) ||
                    !sourceIndex.TryGetInt32(out var index) ||
                    !rule.TryGetProperty("outputs", out var outputs) ||
                    outputs.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                const string prefix = "Data/Locations:";
                var sourceText = source.GetString() ?? string.Empty;
                if (!sourceText.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var sourceKey = sourceText[prefix.Length..] + ":" +
                    index.ToString(CultureInfo.InvariantCulture);
                foreach (var output in outputs.EnumerateArray())
                {
                    if (output.ValueKind == JsonValueKind.Object &&
                        output.TryGetProperty("resolution_complete", out var outputResolution) &&
                        outputResolution.ValueKind == JsonValueKind.True &&
                        output.TryGetProperty("output_eligible_before_random_rolls", out var outputEligible) &&
                        outputEligible.ValueKind == JsonValueKind.True &&
                        output.TryGetProperty("qualified_item_id", out var qid) &&
                        qid.ValueKind == JsonValueKind.String)
                    {
                        result.Add(RuntimeLocationRuleKey(
                            sourceKey,
                            qid.GetString() ?? string.Empty));
                    }
                }
            }
        }
        return result;
    }

    private static string RuntimeLocationRuleKey(
        string sourceKey,
        string qualifiedItemId) =>
        sourceKey + "|" + qualifiedItemId;

    private static bool TryReadFieldValue(
        JsonElement state,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return state.ValueKind == JsonValueKind.Object &&
            state.TryGetProperty(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("value", out value);
    }

    private static HashSet<string> ReadExactMissingSpecies(
        JsonElement state,
        int expectedDenominator)
    {
        var progress = RequiredFieldValue(
            state,
            "world_progress",
            "fish_collection_progress");
        var caughtCount = -1;
        var missingCount = -1;
        var items = default(JsonElement);
        Require(progress.ValueKind == JsonValueKind.Object &&
                progress.TryGetProperty("eligible_species_count", out var eligible) &&
                eligible.TryGetInt32(out var eligibleCount) &&
                eligibleCount == expectedDenominator &&
                progress.TryGetProperty("caught_eligible_species_count", out var caught) &&
                caught.TryGetInt32(out caughtCount) && caughtCount >= 0 &&
                progress.TryGetProperty("missing_species_count", out var missingCountValue) &&
                missingCountValue.TryGetInt32(out missingCount) && missingCount >= 0 &&
                caughtCount + missingCount == eligibleCount &&
                progress.TryGetProperty("items", out items) &&
                items.ValueKind == JsonValueKind.Array &&
                items.GetArrayLength() == expectedDenominator,
            "Snapshot Master Angler denominator is missing or inconsistent.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var observedCaught = 0;
        foreach (var item in items.EnumerateArray())
        {
            var qid = RequiredString(item, "qualified_item_id");
            var caughtFlag = default(JsonElement);
            Require(seen.Add(qid) &&
                    item.TryGetProperty("caught", out caughtFlag) &&
                    caughtFlag.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "Snapshot Master Angler item rows are invalid or duplicated.");
            if (caughtFlag.GetBoolean())
                observedCaught++;
            else
                missing.Add(qid);
        }
        Require(observedCaught == caughtCount && missing.Count == missingCount,
            "Snapshot Master Angler item counts disagree with the native summary.");
        return missing;
    }

    private static int ReadFishingLevel(JsonElement state)
    {
        var skills = RequiredFieldValue(state, "player", "skills_detail");
        var rows = default(JsonElement);
        Require(skills.ValueKind == JsonValueKind.Object &&
                skills.TryGetProperty("skills", out rows) &&
                rows.ValueKind == JsonValueKind.Array,
            "Snapshot fishing skill detail is unavailable.");
        var fishing = rows.EnumerateArray().SingleOrDefault(row =>
            row.ValueKind == JsonValueKind.Object &&
            RequiredString(row, "skill_id") == "fishing");
        var result = -1;
        Require(fishing.ValueKind == JsonValueKind.Object &&
                fishing.TryGetProperty("effective_level", out var level) &&
                level.TryGetInt32(out result),
            "Snapshot fishing effective level is unavailable.");
        return result;
    }

    private static bool AnyRodContextFlag(
        JsonElement state,
        string name,
        bool expected = true)
    {
        var contexts = RequiredFieldValue(state, "fishing", "rod_contexts");
        return contexts.ValueKind == JsonValueKind.Array &&
            contexts.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object &&
                row.TryGetProperty(name, out var value) &&
                value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                value.GetBoolean() == expected);
    }

    private static JsonElement RequiredFieldValue(
        JsonElement state,
        string section,
        string field)
    {
        var sectionValue = RequiredObject(state, section);
        var value = default(JsonElement);
        Require(sectionValue.TryGetProperty(field, out var envelope) &&
                envelope.ValueKind == JsonValueKind.Object &&
                envelope.TryGetProperty("value", out value),
            $"Snapshot field {section}.{field} is unavailable.");
        return value;
    }

    private static int RequiredFieldInt(
        JsonElement state,
        string section,
        string field)
    {
        var value = RequiredFieldValue(state, section, field);
        var result = -1;
        Require(value.TryGetInt32(out result),
            $"Snapshot field {section}.{field} is not an integer.");
        return result;
    }

    private static string RequiredFieldString(
        JsonElement state,
        string section,
        string field)
    {
        var value = RequiredFieldValue(state, section, field);
        Require(value.ValueKind == JsonValueKind.String,
            $"Snapshot field {section}.{field} is not a string.");
        return value.GetString() ?? string.Empty;
    }

    private static JsonElement RequiredObject(JsonElement source, string name)
    {
        var value = default(JsonElement);
        Require(source.ValueKind == JsonValueKind.Object &&
                source.TryGetProperty(name, out value) &&
                value.ValueKind == JsonValueKind.Object,
            "Required object is unavailable: " + name);
        return value;
    }

    private static string RequiredString(JsonElement source, string name)
    {
        var value = default(JsonElement);
        Require(source.ValueKind == JsonValueKind.Object &&
                source.TryGetProperty(name, out value) &&
                value.ValueKind == JsonValueKind.String,
            "Required string is unavailable: " + name);
        return value.GetString() ?? string.Empty;
    }

    private static SmallModelActionParameter Parameter(string name, object value) => new()
    {
        Name = name,
        Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static void Require(
        [DoesNotReturnIf(false)] bool condition,
        string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
