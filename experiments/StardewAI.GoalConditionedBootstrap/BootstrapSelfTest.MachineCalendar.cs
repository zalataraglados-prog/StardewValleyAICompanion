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
                  "UseFirstValidOutput": false,
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
                },
            "Flavored machine output identity did not resolve.");

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
}
