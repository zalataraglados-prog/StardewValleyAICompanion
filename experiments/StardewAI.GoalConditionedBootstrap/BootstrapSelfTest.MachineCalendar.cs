using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineCalendarResolution()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "(BC)114": {
                "OutputRules": [{
                  "Id": "Default",
                  "Condition": null,
                  "Triggers": [{
                    "Id": "ItemPlacedInMachine",
                    "Trigger": 1,
                    "RequiredItemId": "(O)388",
                    "RequiredTags": null,
                    "RequiredCount": 10,
                    "Condition": null
                  }],
                  "UseFirstValidOutput": true,
                  "OutputItem": [{
                    "ItemId": "(O)382",
                    "OutputMethod": null,
                    "Condition": "PLAYER_HAS_PROFESSION Current 4",
                    "PerItemCondition": null,
                    "RandomItemId": null,
                    "MinStack": 1,
                    "MaxStack": 1,
                    "Quality": 0,
                    "CopyQuality": false,
                    "StackModifierMode": 0,
                    "StackModifiers": null,
                    "QualityModifierMode": 0,
                    "QualityModifiers": null
                  }, {
                    "ItemId": "(O)334",
                    "OutputMethod": null,
                    "Condition": null,
                    "PerItemCondition": null,
                    "RandomItemId": null,
                    "MinStack": 1,
                    "MaxStack": 1,
                    "Quality": 0,
                    "CopyQuality": false,
                    "StackModifierMode": 0,
                    "StackModifiers": null,
                    "QualityModifierMode": 0,
                    "QualityModifiers": null
                  }],
                  "MinutesUntilReady": 30,
                  "DaysUntilReady": -1,
                  "RecalculateOnCollect": false
                }],
                "AdditionalConsumedItems": [{
                  "ItemId": "(O)382",
                  "RequiredCount": 1
                }],
                "ReadyTimeModifiers": null,
                "ReadyTimeModifierMode": 0,
                "OnlyCompleteOvernight": false
              },
              "(BC)90": {
                "OutputRules": [{
                  "Id": "Default",
                  "Condition": null,
                  "Triggers": [{
                    "Id": "ItemPlacedInMachine",
                    "Trigger": 1,
                    "RequiredItemId": null,
                    "RequiredTags": ["bone_item"],
                    "RequiredCount": 1,
                    "Condition": null
                  }],
                  "UseFirstValidOutput": true,
                  "OutputItem": [{
                    "ItemId": "(O)466",
                    "OutputMethod": null,
                    "Condition": "RANDOM .1",
                    "PerItemCondition": null,
                    "RandomItemId": null,
                    "MinStack": 3,
                    "MaxStack": 6,
                    "Quality": -1,
                    "CopyQuality": false,
                    "StackModifierMode": 0,
                    "StackModifiers": [{
                      "Id": "RareDouble",
                      "Condition": "RANDOM .1",
                      "Modification": 2,
                      "Amount": 2,
                      "RandomAmount": null
                    }],
                    "QualityModifierMode": 0,
                    "QualityModifiers": null
                  }, {
                    "ItemId": "(O)465",
                    "OutputMethod": null,
                    "Condition": null,
                    "PerItemCondition": null,
                    "RandomItemId": null,
                    "MinStack": 1,
                    "MaxStack": 1,
                    "Quality": -1,
                    "CopyQuality": false,
                    "StackModifierMode": 0,
                    "StackModifiers": null,
                    "QualityModifierMode": 0,
                    "QualityModifiers": null
                  }],
                  "MinutesUntilReady": 240,
                  "DaysUntilReady": -1,
                  "RecalculateOnCollect": false
                }],
                "AdditionalConsumedItems": null,
                "ReadyTimeModifiers": null,
                "ReadyTimeModifierMode": 0,
                "OnlyCompleteOvernight": false
              },
              "(BC)10": {
                "OutputRules": [{
                  "Id": "Default",
                  "Condition": null,
                  "Triggers": [{
                    "Id": "OutputCollected",
                    "Trigger": 2,
                    "RequiredItemId": null,
                    "RequiredTags": null,
                    "RequiredCount": 1,
                    "Condition": "!LOCATION_SEASON Target Winter"
                  }],
                  "UseFirstValidOutput": true,
                  "OutputItem": [{
                    "ItemId": "FLAVORED_ITEM Honey NEARBY_FLOWER_ID",
                    "OutputMethod": null,
                    "Condition": null,
                    "PerItemCondition": null,
                    "RandomItemId": null,
                    "MinStack": 1,
                    "MaxStack": 1,
                    "Quality": -1,
                    "CopyQuality": false,
                    "StackModifierMode": 0,
                    "StackModifiers": null,
                    "QualityModifierMode": 0,
                    "QualityModifiers": null
                  }],
                  "MinutesUntilReady": 1440,
                  "DaysUntilReady": -1,
                  "RecalculateOnCollect": true
                }],
                "AdditionalConsumedItems": null,
                "ReadyTimeModifiers": [{
                  "Id": "Fast",
                  "Condition": "PLAYER_HAS_PROFESSION Current 4",
                  "Modification": 2,
                  "Amount": 0.5,
                  "RandomAmount": null
                }],
                "ReadyTimeModifierMode": 0,
                "OnlyCompleteOvernight": false
              }
            }
            """);
        var machines = document.RootElement;

        var deterministic = AcquisitionRouteCalendarResolutionBuilder
            .ResolveMachineWindows(
                "(O)382",
                MachineRoute(
                    "machine_output",
                    "machine:(BC)114:rule:Default",
                    "payload.(BC)114.OutputRules[0].OutputItem[0].ItemId"),
                machines,
                337);
        Require(deterministic.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                deterministic.MachineSource is
                {
                    MachineQualifiedItemId: "(BC)114",
                    MinutesUntilReady: 30,
                    StochasticOutcome: false
                } &&
                deterministic.MachineSource.Triggers.Length == 1 &&
                deterministic.MachineSource.AdditionalConsumedItems.Length == 1 &&
                deterministic.Windows.Single().DynamicConditions.SequenceEqual(
                    new[] { "PLAYER_HAS_PROFESSION Current 4" }),
            "Deterministic machine calendar evidence drifted.");

        var orderedOutputBlocked = AcquisitionRouteCalendarResolutionBuilder
            .ResolveMachineWindows(
                "(O)334",
                MachineRoute(
                    "native_machine_item_query_output",
                    "machine:(BC)114:rule:0:output:1",
                    "payload.(BC)114.OutputRules[0].OutputItem[1].ItemId"),
                machines,
                337);
        Require(orderedOutputBlocked.Status ==
                    "blocked_machine_output_selection_order_unresolved" &&
                orderedOutputBlocked.BlockingReasons.Contains(
                    "machine_first_valid_output_preceding_condition_probability_unresolved:0",
                    StringComparer.Ordinal),
            "A later first-valid machine output row with an unresolved preceding condition was admitted.");

        var stochastic = AcquisitionRouteCalendarResolutionBuilder
            .ResolveMachineWindows(
                "(O)466",
                MachineRoute(
                    "native_machine_item_query_output",
                    "machine:(BC)90:rule:0:output:0",
                    "payload.(BC)90.OutputRules[0].OutputItem[0].ItemId"),
                machines,
                337);
        Require(stochastic.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                stochastic.MachineSource is
                {
                    OutputSelectionCount: 2,
                    StochasticOutcome: true
                } &&
                stochastic.Windows.Single().DynamicConditions.Length == 0 &&
                stochastic.Windows.Single().StochasticOutcome,
            "Stochastic machine output evidence drifted.");

        var stochasticFallback = AcquisitionRouteCalendarResolutionBuilder
            .ResolveMachineWindows(
                "(O)465",
                MachineRoute(
                    "native_machine_item_query_output",
                    "machine:(BC)90:rule:0:output:1",
                    "payload.(BC)90.OutputRules[0].OutputItem[1].ItemId"),
                machines,
                337);
        Require(stochasticFallback.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                stochasticFallback.MachineSource is
                {
                    OutputIndex: 1,
                    UseFirstValidOutput: true,
                    StochasticOutcome: true
                } &&
                stochasticFallback.Windows.Single().DynamicConditions.Length == 0 &&
                AcquisitionRouteTargetDateStochasticRetryBuilder
                    .TryMachineSingleAttemptProbability(
                        MachineCalendarRoute(stochasticFallback),
                        out var fallbackProbability,
                        out _) &&
                Math.Abs(fallbackProbability - 0.9d) < 1e-12,
            "A probabilistically exact later first-valid machine output did not preserve native selection order.");

        var flavored = AcquisitionRouteCalendarResolutionBuilder
            .ResolveMachineWindows(
                "(O)340",
                MachineRoute(
                    "native_machine_flavored_output",
                    "machine:(BC)10:rule:0:output:0",
                    "payload.(BC)10.OutputRules[0].OutputItem[0].ItemId"),
                machines,
                337);
        Require(flavored.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                flavored.MachineSource is
                {
                    OutputItemQuery: "FLAVORED_ITEM Honey NEARBY_FLOWER_ID",
                    RecalculateOnCollect: true
                } &&
                flavored.MachineSource.TriggerConditionSet is
                {
                    CombinationMode: "or",
                    Alternatives:
                    [
                        {
                            LocationConditions:
                            ["!LOCATION_SEASON Target Winter"]
                        }
                    ]
                } &&
                flavored.Windows.Single().DynamicConditions.SequenceEqual(
                    new[] { "!LOCATION_SEASON Target Winter" },
                    StringComparer.Ordinal),
            "Flavored machine output identity did not resolve.");

        var targetDate = AcquisitionRouteTargetDateCalendarBuilder.Evaluate(
            MachineCalendarRoute(flavored),
            targetTotalDay: 111);
        Require(targetDate.CalendarAxisResolved &&
                targetDate.StaticWindowMatchesTargetDate &&
                targetDate.PendingDynamicConditions.SequenceEqual(
                    new[] { "!LOCATION_SEASON Target Winter" },
                    StringComparer.Ordinal) &&
                AcquisitionUnlockConditionEvaluator.Classify(
                    targetDate.PendingDynamicConditions.Single()) ==
                    AcquisitionUnlockConditionEvaluator.LocationAxis,
            "Bee House trigger season did not survive into the target-date axis.");

        var winter = AcquisitionLocationConditionEvaluator.Evaluate(
            targetDate.PendingDynamicConditions,
            "Farm",
            "Winter");
        var island = AcquisitionLocationConditionEvaluator.Evaluate(
            targetDate.PendingDynamicConditions,
            "IslandWest",
            "Summer");
        var missingSeason = AcquisitionLocationConditionEvaluator.Evaluate(
            targetDate.PendingDynamicConditions,
            "Farm",
            string.Empty);
        Require(winter is { Resolved: true, Matches: false } &&
                island is { Resolved: true, Matches: true } &&
                !missingSeason.Resolved &&
                missingSeason.BlockingReasons.Contains(
                    "target_location_effective_season_missing:Farm",
                    StringComparer.Ordinal),
            "Target-location season did not preserve valley/Island semantics.");

        var triggerOr = AcquisitionRouteCalendarResolutionBuilder
            .ProjectMachineTriggerConditions(new[]
            {
                MachineTrigger("spring", "LOCATION_SEASON Target Spring"),
                MachineTrigger("summer", "LOCATION_SEASON Target Summer")
            });
        Require(!triggerOr.Supported &&
                triggerOr.ConditionSet.CombinationMode == "or" &&
                triggerOr.BlockingReasons.Contains(
                    "machine_trigger_condition_or_semantics_not_supported",
                    StringComparer.Ordinal),
            "Cross-axis machine trigger alternatives were flattened or admitted.");

        var unsupportedTrigger = AcquisitionRouteCalendarResolutionBuilder
            .ProjectMachineTriggerConditions(new[]
            {
                MachineTrigger("unknown", "PLAYER_HAS_PROFESSION Current 4")
            });
        Require(!unsupportedTrigger.Supported &&
                unsupportedTrigger.BlockingReasons.Contains(
                    "machine_trigger_condition_unsupported:unknown",
                    StringComparer.Ordinal),
            "Unsupported machine trigger predicate did not fail closed.");

        var itemTrigger = AcquisitionRouteCalendarResolutionBuilder
            .ProjectMachineTriggerConditions(new[]
            {
                MachineTrigger(
                    "item",
                    "ITEM_CONTEXT_TAG Input bone_item",
                    trigger: 1)
            });
        Require(itemTrigger.Supported &&
                itemTrigger.PendingLocationConditions.Length == 0 &&
                itemTrigger.ConditionSet.Alternatives.Single()
                    .ResourceConditions.SequenceEqual(
                        new[] { "ITEM_CONTEXT_TAG Input bone_item" },
                        StringComparer.Ordinal),
            "Existing item-input trigger ownership drifted from the resource axis.");

        var randomTrigger = AcquisitionRouteCalendarResolutionBuilder
            .ProjectMachineTriggerConditions(new[]
            {
                MachineTrigger("random", "RANDOM 0.02", trigger: 1)
            });
        Require(randomTrigger.Supported &&
                randomTrigger.ConditionSet.Alternatives.Single()
                    .StochasticConditions.SequenceEqual(
                        new[] { "RANDOM 0.02" },
                        StringComparer.Ordinal),
            "Supported trigger randomness drifted from the retry axis.");

        var repeatedRandomTrigger = AcquisitionRouteCalendarResolutionBuilder
            .ProjectMachineTriggerConditions(new[]
            {
                MachineTrigger(
                    "repeated-random",
                    "RANDOM 0.5, RANDOM 0.5, RANDOM 0.5",
                    trigger: 1)
            });
        Require(repeatedRandomTrigger.Supported &&
                repeatedRandomTrigger.ConditionSet.Alternatives.Single()
                    .StochasticConditions.SequenceEqual(
                        new[]
                        {
                            "RANDOM 0.5",
                            "RANDOM 0.5",
                            "RANDOM 0.5"
                        },
                        StringComparer.Ordinal),
            "Repeated trigger randomness lost source multiplicity or order.");

        var mismatch = AcquisitionRouteCalendarResolutionBuilder
            .ResolveMachineWindows(
                "(O)999",
                MachineRoute(
                    "machine_output",
                    "machine:(BC)114:rule:Default",
                    "payload.(BC)114.OutputRules[0].OutputItem[0].ItemId"),
                machines,
                337);
        Require(mismatch.Status == "blocked_authoritative_machine_item_mismatch",
            "Machine target mismatch did not fail closed.");
    }

    private static AcquisitionRequirementRouteLowering MachineRoute(
        string routeKind,
        string sourceId,
        string sourcePath) => new(
            routeKind,
            sourceId,
            "Data/Machines",
            sourcePath,
            "teacher_and_runtime",
            "deterministic_or_explicit_stochastic",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            true,
            true);

    private static AcquisitionRouteCalendarResolution MachineCalendarRoute(
        AcquisitionRouteCalendarResolutionBuilder.CalendarSourceResolution
            source) => new(
        "machine-calendar-bee",
        "machine-calendar-test",
        "bee-house-honey",
        0,
        0,
        "340",
        "(O)340",
        "exact_item_id",
        1,
        0,
        "native_machine_flavored_output",
        "deterministic_or_explicit_stochastic",
        "machine:(BC)10:rule:0:output:0",
        "Data/Machines",
        "payload.(BC)10.OutputRules[0].OutputItem[0].ItemId",
        source.Status,
        source.EvidenceClass,
        source.Windows,
        source.BlockingReasons,
        MachineSource: source.MachineSource);

    private static AcquisitionMachineTriggerEvidence MachineTrigger(
        string id,
        string condition,
        int trigger = 2) => new(
        id,
        trigger,
        string.Empty,
        Array.Empty<string>(),
        1,
        condition);
}
