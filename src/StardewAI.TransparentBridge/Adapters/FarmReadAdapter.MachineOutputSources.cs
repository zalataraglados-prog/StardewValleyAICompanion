using Microsoft.Xna.Framework;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object AttachPredictedMachineOutputRouteSource(
        StardewValley.Object machine,
        MachineData data,
        MachineOutputRule outputRule,
        MachineItemOutput outputData,
        object prediction)
    {
        var document = JsonSerializer.SerializeToNode(prediction) as JsonObject;
        if (document is null)
            return prediction;

        var ruleIndex = data.OutputRules?.FindIndex(rule =>
            ReferenceEquals(rule, outputRule)) ?? -1;
        var outputIndex = outputRule.OutputItem?.FindIndex(output =>
            ReferenceEquals(output, outputData)) ?? -1;
        document["matched_rule_index"] = ruleIndex;
        document["matched_output_index"] = outputIndex;

        var status = document["status"]?.GetValue<string>() ?? string.Empty;
        var outputIdentity = document["item"] as JsonObject ??
            document["output_identity"] as JsonObject;
        var qualifiedItemId =
            outputIdentity?["qualified_item_id"]?.GetValue<string>() ??
            string.Empty;
        var sources = string.Equals(status, "available", StringComparison.Ordinal) &&
                      ruleIndex >= 0 &&
                      outputIndex >= 0 &&
                      !string.IsNullOrWhiteSpace(qualifiedItemId)
            ? PredictedMachineOutputRouteSources(
                machine.QualifiedItemId,
                outputRule,
                outputData,
                ruleIndex,
                outputIndex,
                qualifiedItemId)
            : Array.Empty<object>();
        document["authoritative_route_sources"] =
            JsonSerializer.SerializeToNode(sources);
        return document;
    }

    private static object[] PredictedMachineOutputRouteSources(
        string machineQualifiedItemId,
        MachineOutputRule outputRule,
        MachineItemOutput outputData,
        int ruleIndex,
        int outputIndex,
        string qualifiedItemId)
    {
        if (!string.IsNullOrWhiteSpace(outputRule.Id))
        {
            return new object[]
            {
                new
                {
                    route_kind = "machine_output",
                    source_id = "machine:" + machineQualifiedItemId +
                        ":rule:" + outputRule.Id,
                    qualified_item_id = qualifiedItemId
                }
            };
        }

        var query = outputData.ItemId ?? string.Empty;
        var outputItemId = UnqualifiedMachineOutputObjectId(qualifiedItemId);
        if (!MachineOutputQueryItemIds(query).Contains(
                outputItemId,
                StringComparer.Ordinal))
        {
            return Array.Empty<object>();
        }

        return new object[]
        {
            new
            {
                route_kind = query.StartsWith(
                    "FLAVORED_ITEM ",
                    StringComparison.Ordinal)
                        ? "native_machine_flavored_output"
                        : "native_machine_item_query_output",
                source_id = "machine:" + machineQualifiedItemId +
                    ":rule:" + ruleIndex +
                    ":output:" + outputIndex,
                qualified_item_id = qualifiedItemId
            }
        };
    }

    private static object[] ReadMachineOutputAuthoritativeRouteSources(
        GameLocation location,
        Vector2 tile,
        StardewValley.Object machine)
    {
        var specialSources = ReadSolarPanelOutputAuthoritativeRouteSources(
                machine)
            .Concat(ReadWildTreeTapperOutputAuthoritativeRouteSources(
                location,
                tile,
                machine))
            .ToArray();
        if (specialSources.Length > 0)
            return specialSources;

        return ReadOrdinaryMachineOutputAuthoritativeRouteSources(machine);
    }

    private static object[]
        ReadOrdinaryMachineOutputAuthoritativeRouteSources(
            StardewValley.Object machine) =>
        ReadOrdinaryMachineOutputAuthoritativeRouteSources(
            machine,
            requireReadyForHarvest: true);

    private static object[] ReadActiveMachineOutputAuthoritativeRouteSources(
        GameLocation location,
        Vector2 tile,
        StardewValley.Object machine)
    {
        var specialSources = ReadSolarPanelOutputAuthoritativeRouteSources(
                machine)
            .Concat(ReadWildTreeTapperOutputAuthoritativeRouteSources(
                location,
                tile,
                machine))
            .ToArray();
        return specialSources.Length > 0
            ? specialSources
            : ReadOrdinaryMachineOutputAuthoritativeRouteSources(
                machine,
                requireReadyForHarvest: false);
    }

    private static object[]
        ReadOrdinaryMachineOutputAuthoritativeRouteSources(
            StardewValley.Object machine,
            bool requireReadyForHarvest)
    {
        var output = machine.heldObject.Value;
        var data = machine.GetMachineData();
        if ((requireReadyForHarvest && !machine.readyForHarvest.Value) ||
            output is null ||
            data?.OutputRules is null)
        {
            return Array.Empty<object>();
        }

        var selectedRuleId = machine.lastOutputRuleId.Value;
        if (!string.IsNullOrWhiteSpace(selectedRuleId))
        {
            var selectedRules = data.OutputRules
                .Where(rule => string.Equals(
                    rule.Id,
                    selectedRuleId,
                    StringComparison.Ordinal))
                .ToArray();
            if (selectedRules.Length != 1)
                return Array.Empty<object>();

            return new object[]
            {
                new
                {
                    route_kind = "machine_output",
                    source_id = "machine:" + machine.QualifiedItemId +
                        ":rule:" + selectedRuleId,
                    qualified_item_id = output.QualifiedItemId
                }
            };
        }

        return ReadLegacyMachineOutputRowSources(
            machine.QualifiedItemId,
            data,
            output.QualifiedItemId);
    }

    private static object[] ReadLegacyMachineOutputRowSources(
        string machineQualifiedItemId,
        MachineData data,
        string outputQualifiedItemId)
    {
        if (!outputQualifiedItemId.StartsWith(
                "(O)",
                StringComparison.Ordinal))
        {
            return Array.Empty<object>();
        }

        var outputItemId = outputQualifiedItemId[3..];
        var sources = new List<object>();
        for (var ruleIndex = 0;
            ruleIndex < data.OutputRules.Count;
            ruleIndex++)
        {
            var outputs = data.OutputRules[ruleIndex].OutputItem;
            if (outputs is null)
                continue;

            for (var outputIndex = 0;
                outputIndex < outputs.Count;
                outputIndex++)
            {
                var query = outputs[outputIndex].ItemId ?? string.Empty;
                if (!MachineOutputQueryItemIds(query).Contains(
                        outputItemId,
                        StringComparer.Ordinal))
                {
                    continue;
                }

                sources.Add(new
                {
                    route_kind = query.StartsWith(
                        "FLAVORED_ITEM ",
                        StringComparison.Ordinal)
                            ? "native_machine_flavored_output"
                            : "native_machine_item_query_output",
                    source_id = "machine:" + machineQualifiedItemId +
                        ":rule:" + ruleIndex +
                        ":output:" + outputIndex,
                    qualified_item_id = outputQualifiedItemId
                });
            }
        }
        return sources.ToArray();
    }

    private static IEnumerable<string> MachineOutputQueryItemIds(
        string query)
    {
        const string flavoredPrefix = "FLAVORED_ITEM ";
        if (query.StartsWith(flavoredPrefix, StringComparison.Ordinal))
        {
            var preserveType = query[flavoredPrefix.Length..]
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? string.Empty;
            if (FlavoredMachineOutputItemIds.TryGetValue(
                    preserveType,
                    out var flavoredItemId))
            {
                yield return flavoredItemId;
            }
            yield break;
        }

        foreach (var token in query.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            var itemId = UnqualifiedMachineOutputObjectId(token);
            if (itemId.All(character =>
                    char.IsLetterOrDigit(character) || character == '_'))
            {
                yield return itemId;
            }
        }
    }

    private static readonly IReadOnlyDictionary<string, string>
        FlavoredMachineOutputItemIds =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AgedRoe"] = "447",
                ["Honey"] = "340",
                ["Jelly"] = "344",
                ["Juice"] = "350",
                ["Pickle"] = "342",
                ["Roe"] = "812",
                ["Wine"] = "348",
                ["Bait"] = "SpecificBait",
                ["DriedFruit"] = "DriedFruit",
                ["DriedMushroom"] = "DriedMushrooms",
                ["SmokedFish"] = "SmokedFish"
            };

    private static object[]
        ReadWildTreeTapperOutputAuthoritativeRouteSources(
            GameLocation location,
            Vector2 tile,
            StardewValley.Object machine)
    {
        var output = machine.heldObject.Value;
        if (!machine.readyForHarvest.Value ||
            !machine.IsTapper() ||
            output is null ||
            !location.terrainFeatures.TryGetValue(tile, out var feature) ||
            feature is not Tree tree ||
            tree.GetType() != typeof(Tree) ||
            !tree.tapped.Value)
        {
            return Array.Empty<object>();
        }

        var data = tree.GetData();
        if (data?.TapItems is null)
            return Array.Empty<object>();

        var expectedItemId = UnqualifiedMachineOutputObjectId(
            output.QualifiedItemId);
        var sources = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var rowIndex = 0;
            rowIndex < data.TapItems.Count;
            rowIndex++)
        {
            var row = data.TapItems[rowIndex];
            var sourcePrefix = "wild_tree:" + tree.treeType.Value +
                ":" + rowIndex;
            AddWildTreeTapperSource(
                sources,
                seen,
                sourcePrefix,
                row.ItemId,
                expectedItemId);
            if (row.RandomItemId is null)
                continue;
            for (var randomIndex = 0;
                randomIndex < row.RandomItemId.Count;
                randomIndex++)
            {
                AddWildTreeTapperSource(
                    sources,
                    seen,
                    sourcePrefix + ":random:" + randomIndex,
                    row.RandomItemId[randomIndex],
                    expectedItemId);
            }
        }
        return sources.ToArray();
    }

    private static void AddWildTreeTapperSource(
        ICollection<object> sources,
        ISet<string> seen,
        string sourceId,
        string? rawItemIds,
        string expectedItemId)
    {
        if (string.IsNullOrWhiteSpace(rawItemIds))
            return;

        foreach (var itemId in rawItemIds.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries)
                 .Select(UnqualifiedMachineOutputObjectId)
                 .Where(itemId => string.Equals(
                     itemId,
                     expectedItemId,
                     StringComparison.Ordinal))
                 .Distinct(StringComparer.Ordinal))
        {
            if (!seen.Add(sourceId + "|" + itemId))
                continue;
            sources.Add(new
            {
                route_kind = "native_wild_tree_tapper_output",
                source_id = sourceId,
                qualified_item_id = "(O)" + itemId
            });
        }
    }

    private static string UnqualifiedMachineOutputObjectId(
        string? itemId) =>
        itemId?.StartsWith("(O)", StringComparison.Ordinal) == true
            ? itemId[3..]
            : itemId ?? string.Empty;
}
