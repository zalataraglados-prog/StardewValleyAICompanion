using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed record MachineCropWaveProjection(
    int Days,
    int Units,
    int ServiceIntervalDays,
    int ProcessMinutes,
    string Source,
    string[] CommitmentIds);

internal static class MachineCropWaveProjectionEvaluator
{
    public static MachineCropWaveProjection Evaluate(
        SnapshotEnvelope snapshot,
        JsonElement recipe,
        IReadOnlyCollection<string> acceptedInputIds,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var acceptedIds = acceptedInputIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staticRules = ReadStaticMachineRules(recipe);
        var cropTags = ReadCropCatalogTags(snapshot);
        var crops = ReadStateFieldValue(snapshot, "farm", "crops");
        var matching = !crops.HasValue || crops.Value.ValueKind != JsonValueKind.Array
            ? Array.Empty<CropWaveRow>()
            : crops.Value.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object && ReadBool(row, "dead") != true &&
                    ReadInt(row, "days_until_next_harvest_if_watered", -1) >= 0)
                .Select(row =>
                {
                    var itemId = ReadString(row, "harvest_item_id");
                    var qualifiedId = ReadString(row, "harvest_item_qualified_id");
                    var units = Math.Max(1, ReadInt(row, "harvest_min_stack", 1));
                    cropTags.TryGetValue(ItemIdentity(itemId, qualifiedId), out var tags);
                    var processMinutes = StaticProcessMinutes(staticRules, itemId, qualifiedId, tags ?? Array.Empty<string>(), units);
                    var accepted = acceptedIds.Contains(itemId) || acceptedIds.Contains(qualifiedId) || processMinutes > 0;
                    return new CropWaveRow(
                        accepted,
                        ReadInt(row, "days_until_next_harvest_if_watered", -1),
                        units,
                        Math.Max(0, ReadInt(row, "regrow_days")),
                        processMinutes);
                })
                .Where(row => row.Accepted)
                .ToArray();
        var live = matching.Length == 0
            ? Empty()
            : new MachineCropWaveProjection(
                matching.Min(row => row.Days),
                0,
                0,
                0,
                "live_crop_wave",
                Array.Empty<string>());
        if (live.Days >= 0)
        {
            var selected = matching.Where(row => row.Days == live.Days).ToArray();
            live = live with
            {
                Units = selected.Sum(row => row.Units),
                ServiceIntervalDays = MinimumPositive(selected.Select(row => row.RegrowDays)),
                ProcessMinutes = selected.Max(row => row.ProcessMinutes)
            };
        }

        var committed = ReadCommittedCropWave(snapshot, acceptedIds, staticRules, commitmentLedger);
        if (live.Days < 0)
        {
            return committed;
        }
        if (committed.Days < 0 || live.Days < committed.Days)
        {
            return live;
        }
        if (committed.Days < live.Days)
        {
            return committed;
        }
        return new MachineCropWaveProjection(
            live.Days,
            live.Units + committed.Units,
            MinimumPositive(new[] { live.ServiceIntervalDays, committed.ServiceIntervalDays }),
            Math.Max(live.ProcessMinutes, committed.ProcessMinutes),
            "live_and_committed_crop_wave",
            committed.CommitmentIds);
    }

    private static MachineCropWaveProjection ReadCommittedCropWave(
        SnapshotEnvelope snapshot,
        HashSet<string> acceptedIds,
        IReadOnlyCollection<StaticMachineRule> staticRules,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var currentTotalDay = ReadStateFieldIntOptional(snapshot, "time", "total_days");
        if (!currentTotalDay.HasValue || commitmentLedger is null ||
            !string.Equals(commitmentLedger.SchemaVersion, "strategy_commitment_ledger.v1", StringComparison.Ordinal))
        {
            return Empty();
        }

        var rows = commitmentLedger.CropPlantingCommitments
            .Where(row => string.Equals(row.Status, StrategyCommitmentStatuses.Active, StringComparison.Ordinal))
            .Select(row => new
            {
                Row = row,
                NextDay = NextCommittedHarvestDay(row, currentTotalDay.Value),
                ProcessMinutes = StaticProcessMinutes(
                    staticRules,
                    row.HarvestItemId,
                    row.HarvestItemQualifiedId,
                    row.HarvestContextTags ?? Array.Empty<string>(),
                    row.MinimumUnitsPerWave),
                AcceptedByCurrentProbe = acceptedIds.Contains(row.HarvestItemId) ||
                    acceptedIds.Contains(row.HarvestItemQualifiedId) ||
                    acceptedIds.Contains(Unqualify(row.HarvestItemQualifiedId))
            })
            .Where(row => row.NextDay.HasValue && (row.AcceptedByCurrentProbe || row.ProcessMinutes > 0))
            .ToArray();
        if (rows.Length == 0)
        {
            return Empty();
        }

        var nextTotalDay = rows.Min(row => row.NextDay!.Value);
        var selected = rows.Where(row => row.NextDay == nextTotalDay).ToArray();
        return new MachineCropWaveProjection(
            nextTotalDay - currentTotalDay.Value,
            selected.Sum(row => Math.Max(0, row.Row.MinimumUnitsPerWave)),
            MinimumPositive(selected.Select(row => row.Row.RegrowDays ?? 0)),
            selected.Max(row => row.ProcessMinutes),
            "committed_strategy_ledger",
            selected.Select(row => row.Row.CommitmentId).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    private static StaticMachineRule[] ReadStaticMachineRules(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("output_machine_data", out var machineData) || machineData.ValueKind != JsonValueKind.Object ||
            !machineData.TryGetProperty("output_rules", out var rules) || rules.ValueKind != JsonValueKind.Array ||
            ReadInt(machineData, "additional_consumed_item_count") > 0 ||
            ReadInt(machineData, "prevent_time_pass_count") > 0 ||
            ReadInt(machineData, "ready_time_modifier_count") > 0 ||
            ReadBool(machineData, "only_complete_overnight") == true)
        {
            return Array.Empty<StaticMachineRule>();
        }

        return rules.EnumerateArray()
            .Where(rule => rule.ValueKind == JsonValueKind.Object &&
                StaticOutputAvailable(rule) &&
                RuleProcessMinutes(rule) > 0 &&
                rule.TryGetProperty("triggers", out var triggers) && triggers.ValueKind == JsonValueKind.Array)
            .Select(rule => new StaticMachineRule(
                RuleProcessMinutes(rule),
                rule.GetProperty("triggers").EnumerateArray()
                    .Where(trigger => trigger.ValueKind == JsonValueKind.Object)
                    .Select(trigger => new StaticMachineTrigger(
                        ReadString(trigger, "trigger"),
                        ReadString(trigger, "condition"),
                        ReadString(trigger, "required_item_id"),
                        ReadStringArray(trigger, "required_tags"),
                        Math.Max(0, ReadInt(trigger, "required_count"))))
                    .ToArray()))
            .Where(rule => rule.Triggers.Length > 0)
            .ToArray();
    }

    private static int RuleProcessMinutes(JsonElement rule)
    {
        var minutes = ReadInt(rule, "minutes_until_ready", -1);
        if (minutes >= 0)
        {
            return minutes;
        }
        var days = ReadInt(rule, "days_until_ready", -1);
        return days > 0 ? checked(days * 1600) : 0;
    }

    private static bool StaticOutputAvailable(JsonElement rule)
    {
        if (rule.TryGetProperty("output_item", out var single) && single.ValueKind == JsonValueKind.Object &&
            StaticOutputItem(single))
        {
            return true;
        }
        return rule.TryGetProperty("output_items", out var items) && items.ValueKind == JsonValueKind.Array &&
            items.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object && StaticOutputItem(item));
    }

    private static bool StaticOutputItem(JsonElement item) =>
        string.IsNullOrWhiteSpace(ReadString(item, "condition")) &&
        string.IsNullOrWhiteSpace(ReadString(item, "per_item_condition")) &&
        string.IsNullOrWhiteSpace(ReadString(item, "output_method")) &&
        (!string.IsNullOrWhiteSpace(ReadString(item, "item_id")) ||
         ReadStringArray(item, "random_item_ids").Length > 0);

    private static int StaticProcessMinutes(
        IReadOnlyCollection<StaticMachineRule> rules,
        string itemId,
        string qualifiedItemId,
        IReadOnlyCollection<string> contextTags,
        int stack)
    {
        var matches = rules
            .Where(rule => rule.Triggers.Any(trigger =>
                HasItemPlacedTrigger(trigger.Trigger) &&
                string.IsNullOrWhiteSpace(trigger.Condition) &&
                (string.IsNullOrWhiteSpace(trigger.RequiredItemId) ||
                 SameItem(trigger.RequiredItemId, qualifiedItemId, itemId)) &&
                RequiredTagsMatch(trigger.RequiredTags, contextTags) &&
                trigger.RequiredCount <= stack))
            .Select(rule => rule.ProcessMinutes)
            .Where(minutes => minutes > 0)
            .ToArray();
        return matches.Length == 0 ? 0 : matches.Max();
    }

    private static bool HasItemPlacedTrigger(string trigger) =>
        trigger.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(value => string.Equals(value.Trim(), "ItemPlacedInMachine", StringComparison.Ordinal));

    private static bool RequiredTagsMatch(
        IReadOnlyCollection<string> requiredTags,
        IReadOnlyCollection<string> actualTags)
    {
        if (requiredTags.Count == 0)
        {
            return true;
        }
        var tags = actualTags.ToHashSet(StringComparer.Ordinal);
        foreach (var rawRequiredTag in requiredTags)
        {
            if (rawRequiredTag is null)
            {
                return false;
            }
            var requiredTag = rawRequiredTag.Trim();
            var expectedPresent = true;
            if (requiredTag.StartsWith("!", StringComparison.Ordinal))
            {
                requiredTag = requiredTag.Substring(1).TrimStart();
                expectedPresent = false;
            }
            if (requiredTag.Length == 0 || tags.Contains(requiredTag) != expectedPresent)
            {
                return false;
            }
        }
        return true;
    }

    private static Dictionary<string, string[]> ReadCropCatalogTags(SnapshotEnvelope snapshot)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var catalog = ReadStateFieldValue(snapshot, "farm", "crop_catalog");
        if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Array)
        {
            return result;
        }
        foreach (var row in catalog.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object))
        {
            result[ItemIdentity(ReadString(row, "harvest_item_id"), ReadString(row, "harvest_item_qualified_id"))] =
                ReadStringArray(row, "harvest_context_tags");
        }
        return result;
    }

    private static int? NextCommittedHarvestDay(CropPlantingCommitment row, int currentTotalDay)
    {
        if (currentTotalDay <= row.FirstHarvestTotalDay)
        {
            return row.FirstHarvestTotalDay;
        }
        if (!row.RegrowDays.HasValue || row.RegrowDays.Value <= 0 || currentTotalDay > row.LastInSeasonHarvestTotalDay)
        {
            return null;
        }
        var elapsed = currentTotalDay - row.FirstHarvestTotalDay;
        var wave = DivideRoundUp(elapsed, row.RegrowDays.Value);
        var next = row.FirstHarvestTotalDay + wave * row.RegrowDays.Value;
        return next <= row.LastInSeasonHarvestTotalDay ? next : null;
    }

    private static bool SameItem(string requiredId, string qualifiedId, string itemId) =>
        string.Equals(requiredId, qualifiedId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(requiredId, itemId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Unqualify(requiredId), Unqualify(itemId), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Unqualify(requiredId), Unqualify(qualifiedId), StringComparison.OrdinalIgnoreCase);

    private static string Unqualify(string itemId)
    {
        var close = itemId.IndexOf(')');
        return itemId.StartsWith("(", StringComparison.Ordinal) && close >= 0 && close + 1 < itemId.Length
            ? itemId.Substring(close + 1)
            : itemId;
    }

    private static string ItemIdentity(string itemId, string qualifiedItemId) =>
        !string.IsNullOrWhiteSpace(qualifiedItemId) ? qualifiedItemId : itemId;

    private static int MinimumPositive(IEnumerable<int> values)
    {
        var positive = values.Where(value => value > 0).ToArray();
        return positive.Length == 0 ? 0 : positive.Min();
    }

    private static string[] ReadStringArray(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray()
            : Array.Empty<string>();

    private static int DivideRoundUp(int value, int divisor) => divisor <= 0 ? 0 : (value + divisor - 1) / divisor;

    private static MachineCropWaveProjection Empty() => new(-1, 0, 0, 0, "none", Array.Empty<string>());

    private sealed record StaticMachineRule(int ProcessMinutes, StaticMachineTrigger[] Triggers);
    private sealed record StaticMachineTrigger(
        string Trigger,
        string Condition,
        string RequiredItemId,
        string[] RequiredTags,
        int RequiredCount);
    private sealed record CropWaveRow(bool Accepted, int Days, int Units, int RegrowDays, int ProcessMinutes);
}
