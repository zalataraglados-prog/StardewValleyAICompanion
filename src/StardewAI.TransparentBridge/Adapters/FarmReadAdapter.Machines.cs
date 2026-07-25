using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter : ReadAdapterBase
{
    private static object[] ReadCachedMachineProbeRowsOrFallback(Farm farm)
    {
        var currentTick = unchecked((long)Game1.ticks);
        lock (MachineProbeCacheLock)
        {
            if (cachedMachineProbeRows.Length > 0 &&
                cachedMachineProbeTick >= 0 &&
                currentTick - cachedMachineProbeTick <= MachineProbeCacheMaxAgeTicks)
            {
                return cachedMachineProbeRows;
            }
        }

        return ReadMachines(farm, includeLoadableInputs: false, minimalMachineProfile: true, machineProbeCacheTick: -1);
    }

    private static void SetMachineProbeCache(object[] rows, long tick)
    {
        lock (MachineProbeCacheLock)
        {
            cachedMachineProbeRows = rows;
            cachedMachineProbeTick = tick;
        }
    }

    private static object[] ReadMachines(Farm farm)
    {
        var minimalMachineProfile = string.Equals(SnapshotProfileContext.Current, "machine", StringComparison.OrdinalIgnoreCase);
        return ReadMachines(farm, includeLoadableInputs: false, minimalMachineProfile, machineProbeCacheTick: -1);
    }

    private static object[] ReadMachines(Farm farm, bool includeLoadableInputs, bool minimalMachineProfile, long machineProbeCacheTick)
    {
        var player = Game1.player;
        if (player is null)
        {
            return Array.Empty<object>();
        }
        var currentLocationId = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var playerId = player.UniqueMultiplayerID;
        var machineRows = MachineLocationTopology.ReadPersistentLocations(farm, player)
            .SelectMany(location => location.Location.objects.Pairs
                .Where(pair => pair.Value.bigCraftable.Value &&
                    pair.Value.GetMachineData() is not null &&
                    (location.IsPlayerControlled || pair.Value.owner.Value == playerId))
                .Select(pair => new
                {
                    location.Location,
                    location.Kind,
                    location.IsPlayerControlled,
                    location.RootLocationId,
                    location.ParentBuildingRuntimeType,
                    Pair = pair
                }))
            .OrderBy(row => row.Location.NameOrUniqueName, StringComparer.Ordinal)
            .ThenBy(row => row.Pair.Key.Y)
            .ThenBy(row => row.Pair.Key.X)
            .ToArray();
        var probeEligibleRows = machineRows
            .Where(row => string.Equals(row.Location.NameOrUniqueName, currentLocationId, StringComparison.OrdinalIgnoreCase) &&
                row.Pair.Value.MinutesUntilReady <= 0 &&
                !row.Pair.Value.readyForHarvest.Value &&
                MachineDataHasEffectiveInput(row.Pair.Value.GetMachineData()))
            .ToArray();
        var probeRotationIndex = probeEligibleRows.Length == 0 || machineProbeCacheTick < 0
            ? 0
            : (int)((machineProbeCacheTick / 10) % probeEligibleRows.Length);
        var probeMachineKeys = probeEligibleRows
            .Skip(probeRotationIndex)
            .Concat(probeEligibleRows.Take(probeRotationIndex))
            .Take(MaxMachineInputProbeMachinesPerRefresh)
            .Select(row => MachineLocationTileKey(row.Location.NameOrUniqueName, row.Pair.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return machineRows
            .Select(row =>
            {
                var locationId = row.Location.NameOrUniqueName;
                var probeLocationIsCurrent = string.Equals(locationId, currentLocationId, StringComparison.OrdinalIgnoreCase);
                var probeWithinBudget = probeMachineKeys.Contains(MachineLocationTileKey(locationId, row.Pair.Key));
                var liveMachineData = row.Pair.Value.GetMachineData();
                var machineHasInput =
                    MachineDataHasEffectiveInput(liveMachineData);
                var machineHasOutput =
                    MachineDataHasEffectiveOutput(liveMachineData);
                var machineIsIdle = row.Pair.Value.MinutesUntilReady <= 0 && !row.Pair.Value.readyForHarvest.Value;
                var harvestExperience = ReadMachineHarvestExperience(liveMachineData, player);
                object machineData = minimalMachineProfile
                    ? new
                    {
                        status = "blocked",
                        reason = "machine_profile_minimal_skips_machine_data"
                    }
                    : ReadMachineDataSummary(liveMachineData);
                var loadableInputs = includeLoadableInputs && probeLocationIsCurrent && probeWithinBudget
                    ? ReadMachineLoadableInputs(row.Pair.Value)
                    : Array.Empty<object>();
                return new
                {
                    location_id = locationId,
                    location_kind = row.Kind,
                    location_is_player_controlled = row.IsPlayerControlled,
                    root_location_id = row.RootLocationId,
                    parent_building_runtime_type = row.ParentBuildingRuntimeType,
                    location_is_current = probeLocationIsCurrent,
                    tile_x = (int)row.Pair.Key.X,
                    tile_y = (int)row.Pair.Key.Y,
                    qualified_item_id = row.Pair.Value.QualifiedItemId,
                    display_name = row.Pair.Value.DisplayName,
                    owner_player_id = row.Pair.Value.owner.Value,
                    ready_for_harvest = row.Pair.Value.readyForHarvest.Value,
                    minutes_until_ready = row.Pair.Value.MinutesUntilReady,
                    machine_has_input = machineHasInput,
                    machine_has_output = machineHasOutput,
                    machine_row_count_total = machineRows.Length,
                    machine_row_snapshot_status = "complete_no_row_truncation",
                    machine_input_probe_machine_limit = MaxMachineInputProbeMachinesPerRefresh,
                    machine_input_probe_eligible_count = probeEligibleRows.Length,
                    machine_input_probe_rotation_index = probeRotationIndex,
                    loadable_input_probe_slot_limit = MaxMachineInputProbeSlotsPerMachine,
                    loadable_input_probe_status = includeLoadableInputs
                        ? !probeLocationIsCurrent
                            ? "blocked_machine_location_not_current_requires_route_and_fresh_snapshot"
                            : machineHasInput != true
                                ? "not_applicable_machine_has_no_manual_input"
                                : !machineIsIdle
                                    ? "not_applicable_machine_not_idle"
                                    : probeWithinBudget
                                        ? "available_main_thread_cache"
                                        : "blocked_main_thread_probe_budget_rotates_on_refresh"
                        : "blocked_requires_main_thread_cache",
                    machine_probe_cache_tick = machineProbeCacheTick,
                    machine_data = machineData,
                    harvest_experience_raw = harvestExperience.Raw,
                    harvest_experience_entries = harvestExperience.Entries,
                    harvest_experience_deltas = harvestExperience.Deltas,
                    harvest_experience_deltas_json = JsonSerializer.Serialize(harvestExperience.Deltas),
                    harvest_mastery_experience_delta = harvestExperience.MasteryExperienceDelta,
                    harvest_experience_projection_status = harvestExperience.Status,
                    harvest_experience_native_contract = "Object.CheckForActionOnMachine_pair_parse_then_Farmer.gainExperience",
                    held_item = SummarizeItem(row.Pair.Value.heldObject.Value),
                    loadable_inputs = loadableInputs
                };
            })
            .ToArray();
    }

    private static string MachineLocationTileKey(string locationId, Vector2 tile)
    {
        return locationId + ":" + (int)tile.X + "," + (int)tile.Y;
    }

    private static object ReadMachineDataSummary(object? machineData) => ReadMachineDataSummary(machineData, completeCatalog: false);

    internal static object ReadCompleteMachineDataSummary(object? machineData) => ReadMachineDataSummary(machineData, completeCatalog: true);

    private static object ReadMachineDataSummary(object? machineData, bool completeCatalog)
    {
        if (machineData is null)
        {
            return new
            {
                source = "Object.GetMachineData()",
                status = "unavailable"
            };
        }

        return new
        {
            source = "Object.GetMachineData()",
            status = "available",
            has_input = machineData is MachineData typedMachineData &&
                MachineDataHasEffectiveInput(typedMachineData),
            has_output = machineData is MachineData typedOutputData &&
                MachineDataHasEffectiveOutput(typedOutputData),
            has_input_forced = ReadBoolNullable(machineData, "HasInput"),
            has_output_forced = ReadBoolNullable(machineData, "HasOutput"),
            effective_capability_native_contract =
                "ItemContextTagManager:forced_flag_or_output_rule_trigger",
            additional_consumed_item_count = ReadCount(machineData, "AdditionalConsumedItems"),
            additional_consumed_items = ReadMachineAdditionalConsumedItems(ReadMemberValue(machineData, "AdditionalConsumedItems"), completeCatalog),
            prevent_time_pass_count = ReadCount(machineData, "PreventTimePass"),
            ready_time_modifier_count = ReadCount(machineData, "ReadyTimeModifiers"),
            only_complete_overnight = ReadBoolNullable(machineData, "OnlyCompleteOvernight"),
            output_rule_count = ReadCount(machineData, "OutputRules"),
            output_rule_snapshot_status = completeCatalog ? "complete_no_rule_or_output_truncation" : "bounded_runtime_machine_summary",
            output_rules = ReadMachineOutputRules(machineData, completeCatalog)
        };
    }

    private static object[] ReadMachineOutputRules(object machineData, bool completeCatalog)
    {
        var outputRules = ReadMemberValue(machineData, "OutputRules");
        if (outputRules is not System.Collections.IEnumerable enumerable)
        {
            return Array.Empty<object>();
        }

        var rules = enumerable
            .Cast<object?>()
            .Where(rule => rule is not null);
        if (!completeCatalog)
        {
            rules = rules.Take(12);
        }
        return rules
            .Select(rule =>
            {
                var outputItems = ReadMachineOutputItemList(ReadMemberValue(rule!, "OutputItem"), completeCatalog);
                var requiredItemId = ReadUniqueMachineRuleRequiredItemId(ReadMemberValue(rule!, "Triggers"));
                return new
                {
                    id = ReadString(rule!, "Id") ?? string.Empty,
                    required_item_id = requiredItemId,
                    required_item_id_summary_status = string.IsNullOrWhiteSpace(requiredItemId)
                        ? "none_or_multiple_use_triggers"
                        : "single_distinct_trigger_required_item_id",
                    use_first_valid_output = ReadBoolNullable(rule!, "UseFirstValidOutput"),
                    trigger_count = ReadCount(rule!, "Triggers"),
                    triggers = ReadMachineOutputTriggers(ReadMemberValue(rule!, "Triggers"), completeCatalog),
                    minutes_until_ready = ReadIntNullable(rule!, "MinutesUntilReady"),
                    days_until_ready = ReadIntNullable(rule!, "DaysUntilReady"),
                    invalid_count_message_present = !string.IsNullOrWhiteSpace(ReadString(rule!, "InvalidCountMessage")),
                    recalculate_on_collect = ReadBoolNullable(rule!, "RecalculateOnCollect"),
                    output_item = outputItems.Length == 1 ? outputItems[0] : null,
                    output_items = outputItems
                };
            })
            .ToArray();
    }

    private static bool MachineDataHasEffectiveInput(
        MachineData? machineData)
    {
        if (machineData is null)
        {
            return false;
        }
        return machineData.HasInput ||
            machineData.OutputRules?.Any(rule =>
                rule.Triggers?.Any(trigger =>
                    trigger.Trigger.HasFlag(
                        MachineOutputTrigger.ItemPlacedInMachine)) ==
                true) == true;
    }

    private static bool MachineDataHasEffectiveOutput(
        MachineData? machineData) =>
        machineData is not null &&
        (machineData.HasOutput ||
         machineData.OutputRules?.Count > 0);

    private static string ReadUniqueMachineRuleRequiredItemId(object? triggers)
    {
        if (triggers is not System.Collections.IEnumerable enumerable)
        {
            return string.Empty;
        }

        var itemIds = enumerable.Cast<object?>()
            .Where(trigger => trigger is not null &&
                (ReadString(trigger!, "Trigger") ?? string.Empty)
                    .Split(',')
                    .Any(value => string.Equals(value.Trim(), "ItemPlacedInMachine", StringComparison.Ordinal)))
            .Select(trigger => ReadString(trigger!, "RequiredItemId") ?? string.Empty)
            .Where(itemId => itemId.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return itemIds.Length == 1 ? itemIds[0] : string.Empty;
    }

    private static object[] ReadMachineOutputTriggers(object? triggers, bool completeCatalog)
    {
        if (triggers is not System.Collections.IEnumerable enumerable)
        {
            return Array.Empty<object>();
        }

        var rows = enumerable
            .Cast<object?>()
            .Where(trigger => trigger is not null);
        if (!completeCatalog)
        {
            rows = rows.Take(12);
        }
        return rows
            .Select(trigger => new
            {
                trigger = ReadString(trigger!, "Trigger") ?? string.Empty,
                condition = ReadString(trigger!, "Condition") ?? string.Empty,
                required_item_id = ReadString(trigger!, "RequiredItemId") ?? string.Empty,
                required_tags = ReadStringList(trigger!, "RequiredTags"),
                required_count = ReadIntNullable(trigger!, "RequiredCount") ?? 0
            })
            .ToArray();
    }

    private static object[] ReadMachineAdditionalConsumedItems(object? items, bool completeCatalog)
    {
        if (items is not System.Collections.IEnumerable enumerable)
        {
            return Array.Empty<object>();
        }

        var rows = enumerable
            .Cast<object?>()
            .Where(item => item is not null);
        if (!completeCatalog)
        {
            rows = rows.Take(8);
        }
        return rows
            .Select(item =>
            {
                var itemId = ReadString(item!, "ItemId") ?? string.Empty;
                var amount = ReadIntNullable(item!, "RequiredCount") ?? 1;
                var salePrice = ReadItemSalePrice(itemId);
                return new
                {
                    item_id = itemId,
                    qualified_item_id = NormalizeObjectQualifiedId(itemId),
                    amount,
                    sale_price = salePrice,
                    total_value = salePrice.HasValue ? salePrice.Value * Math.Max(1, amount) : (int?)null
                };
            })
            .ToArray();
    }

    private static object? ReadMachineOutputItem(object? output)
    {
        if (output is null)
        {
            return null;
        }

        var itemId = ReadString(output, "ItemId") ?? ReadString(output, "Item") ?? string.Empty;
        return new
        {
            id = ReadString(output, "Id") ?? string.Empty,
            item_id = itemId,
            qualified_item_id = NormalizeObjectQualifiedId(itemId),
            random_item_ids = ReadStringList(output, "RandomItemId"),
            condition = ReadString(output, "Condition") ?? string.Empty,
            per_item_condition = ReadString(output, "PerItemCondition") ?? string.Empty,
            output_method = ReadString(output, "OutputMethod") ?? string.Empty,
            stack = ReadIntNullable(output, "Stack"),
            min_stack = ReadIntNullable(output, "MinStack"),
            max_stack = ReadIntNullable(output, "MaxStack"),
            quality = ReadIntNullable(output, "Quality"),
            price = ReadIntNullable(output, "Price"),
            sale_price = ReadItemSalePrice(itemId),
            copy_price = ReadBoolNullable(output, "CopyPrice"),
            copy_quality = ReadBoolNullable(output, "CopyQuality"),
            copy_color = ReadBoolNullable(output, "CopyColor"),
            preserve_type = ReadString(output, "PreserveType") ?? string.Empty,
            preserve_id = ReadString(output, "PreserveId") ?? string.Empty
        };
    }

    private static object[] ReadMachineOutputItemList(object? outputs, bool completeCatalog)
    {
        if (outputs is not System.Collections.IEnumerable enumerable)
        {
            return Array.Empty<object>();
        }

        var rows = enumerable
            .Cast<object?>()
            .Where(output => output is not null);
        if (!completeCatalog)
        {
            rows = rows.Take(8);
        }
        return rows
            .Select(ReadMachineOutputItem)
            .Where(output => output is not null)
            .Cast<object>()
            .ToArray();
    }

    private static object[] ReadMachineLoadableInputs(StardewValley.Object machine)
    {
        if (Game1.player is null ||
            machine.GetMachineData() is null ||
            machine.readyForHarvest.Value ||
            machine.MinutesUntilReady > 0)
        {
            return Array.Empty<object>();
        }

        var predictedOutputCache = new Dictionary<string, object>();
        var inputs = new List<object>();
        for (var index = 0; index < Game1.player.Items.Count && index < MaxMachineInputProbeSlotsPerMachine; index++)
        {
            var item = Game1.player.Items[index];
            if (item is null || item is not StardewValley.Object)
            {
                continue;
            }

            bool accepts;
            try
            {
                accepts = machine.performObjectDropInAction(item, probe: true, Game1.player);
            }
            catch (Exception ex)
            {
                inputs.Add(new
                {
                    slot_index = index,
                    item_id = item.ItemId,
                    qualified_item_id = item.QualifiedItemId,
                    display_name = item.DisplayName,
                    stack = item.Stack,
                    quality = item.Quality,
                    sale_price = item.salePrice(),
                    predicted_output = new
                    {
                        status = "blocked",
                        reason = "machine_input_probe_exception",
                        exception_type = ex.GetType().Name
                    },
                    probe_source = "Object.performObjectDropInAction(probe:true)",
                    load_executor_status = "blocked_probe_exception"
                });
                continue;
            }

            if (!accepts)
            {
                continue;
            }

            inputs.Add(new
            {
                slot_index = index,
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                display_name = item.DisplayName,
                stack = item.Stack,
                quality = item.Quality,
                sale_price = item.salePrice(),
                predicted_output = ReadPredictedMachineOutputCached(machine, item, predictedOutputCache),
                probe_source = "Object.performObjectDropInAction(probe:true)",
                load_executor_status = "covered_for_runtime_load"
            });
        }

        return inputs.ToArray();
    }

    private static object ReadPredictedMachineOutputCached(StardewValley.Object machine, Item inputItem, IDictionary<string, object> cache)
    {
        var key = inputItem.QualifiedItemId + "|" + inputItem.ItemId + "|" + inputItem.Quality + "|" + inputItem.Stack + "|" + inputItem.salePrice();
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        object predicted;
        try
        {
            predicted = ReadPredictedMachineOutput(machine, inputItem);
        }
        catch (Exception ex)
        {
            predicted = new
            {
                status = "blocked",
                reason = "machine_native_output_probe_exception",
                exception_type = ex.GetType().Name
            };
        }
        cache[key] = predicted;
        return predicted;
    }

    internal static object ReadPredictedMachineOutput(StardewValley.Object machine, Item inputItem)
    {
        var machineData = machine.GetMachineData();
        if (machineData is null || Game1.player is null || machine.Location is null)
        {
            return new
            {
                status = "unavailable",
                reason = "machine_context_unavailable"
            };
        }

        if (!MachineDataUtility.TryGetMachineOutputRule(
            machine,
            machineData,
            MachineOutputTrigger.ItemPlacedInMachine,
            inputItem,
            Game1.player,
            machine.Location,
            out var outputRule,
            out var triggerRule,
            out _,
            out _))
        {
            return new
            {
                status = "unavailable",
                reason = "machine_output_rule_unavailable"
            };
        }

        var outputEntries = outputRule.OutputItem;
        if (outputEntries is null || outputEntries.Count == 0)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_item_unavailable"
            };
        }

        if (!outputRule.UseFirstValidOutput && outputEntries.Count > 1)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_random_choice_not_probed"
            };
        }

        var outputData = MachineDataUtility.GetOutputData(machine, machineData, outputRule, inputItem, Game1.player, machine.Location);
        if (outputData is null)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_data_unavailable"
            };
        }

        if (!string.IsNullOrWhiteSpace(outputData.OutputMethod))
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_custom_method_not_probed"
            };
        }

        var outputItem = MachineDataUtility.GetOutputItem(machine, outputData, inputItem, Game1.player, probe: true, out var overrideMinutesUntilReady);
        if (outputItem is null)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_probe_returned_null"
            };
        }

        var ruleMinutesUntilReady = ReadIntNullable(outputRule, "MinutesUntilReady") ?? -1;
        var ruleDaysUntilReady = ReadIntNullable(outputRule, "DaysUntilReady") ?? -1;
        var baseMinutesUntilReady = overrideMinutesUntilReady ??
            (ruleDaysUntilReady >= 0
                ? Utility.CalculateMinutesUntilMorning(Game1.timeOfDay, ruleDaysUntilReady)
                : ruleMinutesUntilReady);
        var effectiveMinutesUntilReady = baseMinutesUntilReady >= 0
            ? (int)Utility.ApplyQuantityModifiers(
                baseMinutesUntilReady,
                machineData.ReadyTimeModifiers,
                machineData.ReadyTimeModifierMode,
                machine.Location,
                Game1.player,
                outputItem,
                inputItem)
            : baseMinutesUntilReady;
        return new
        {
            status = "available",
            source = "MachineDataUtility.GetOutputItem(probe:true)",
            matched_rule_id = outputRule.Id ?? string.Empty,
            required_item_id = triggerRule?.RequiredItemId ?? string.Empty,
            required_tags = triggerRule?.RequiredTags?.ToArray() ?? Array.Empty<string>(),
            required_count = triggerRule?.RequiredCount ?? 0,
            use_first_valid_output = outputRule.UseFirstValidOutput,
            rule_minutes_until_ready = ruleMinutesUntilReady,
            rule_days_until_ready = ruleDaysUntilReady,
            base_minutes_until_ready = baseMinutesUntilReady,
            effective_minutes_until_ready = effectiveMinutesUntilReady,
            item = SummarizeItem(outputItem),
            sale_price = outputItem.salePrice(),
            stack = outputItem.Stack,
            quality = outputItem.Quality,
            preserve_type = outputItem is StardewValley.Object outputObject && outputObject.preserve.Value.HasValue
                ? outputObject.preserve.Value.Value.ToString()
                : string.Empty,
            preserved_item_id = outputItem is StardewValley.Object preservedObject
                ? preservedObject.GetPreservedItemId() ?? string.Empty
                : string.Empty,
            override_minutes_until_ready = overrideMinutesUntilReady
        };
    }

}
