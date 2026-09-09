using System.Globalization;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static WildTreeChopVerification VerifyWildTreeChop(
        ActiveClearObstacle active,
        IReadOnlyList<string> treeToolTrace)
    {
        var chop = active.WildTreeChop!;
        var after = CaptureWildTreeProductOutputs(active.Location);
        var deltas = after.Keys.Union(chop.OutputCountsBefore.Keys, StringComparer.Ordinal)
            .ToDictionary(
                key => key,
                key => after.GetValueOrDefault(key) - chop.OutputCountsBefore.GetValueOrDefault(key),
                StringComparer.Ordinal);
        var outputReason = WildTreeChopOutputMismatch(chop.Projection, deltas);
        var treeRemoved = !active.Location.terrainFeatures.ContainsKey(active.Target.ToVector2());
        var experienceAfter = Game1.player.experiencePoints[Farmer.foragingSkill];
        var treesAfter = (long)Game1.player.stats.Get("TreesChopped");
        var traceMatched = treeToolTrace.Count(row => row.StartsWith("before:", StringComparison.Ordinal) && row.Contains(":type=Axe:", StringComparison.Ordinal)) == active.SwingCount &&
            treeToolTrace.Count(row => row.StartsWith("removed:", StringComparison.Ordinal)) == 1 &&
            treeToolTrace.All(row => !row.Contains("GodTool", StringComparison.OrdinalIgnoreCase));
        var reason = !treeRemoved
            ? "wild_tree_chop_tree_still_present"
            : experienceAfter != active.BeforeForagingExperience + 16
                ? "wild_tree_chop_foraging_experience_mismatch"
                : treesAfter != chop.TreesChoppedBefore + 1
                    ? "wild_tree_chop_trees_chopped_stat_mismatch"
                    : outputReason ?? (!traceMatched ? "wild_tree_chop_native_tool_trace_mismatch" : string.Empty);
        var changes = new List<SimulatedFactChange>
        {
            new() { Path = "current_location.terrain_features[" + active.Target.X + "," + active.Target.Y + "].present", Before = "true", After = treeRemoved ? "false" : "true" },
            new() { Path = "player.stats.TreesChopped", Before = chop.TreesChoppedBefore.ToString(CultureInfo.InvariantCulture), After = treesAfter.ToString(CultureInfo.InvariantCulture) }
        };
        foreach (var row in deltas.Where(row => row.Value != 0).OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            changes.Add(new SimulatedFactChange
            {
                Path = "combined_inventory_debris_output[" + row.Key + "]",
                Before = chop.OutputCountsBefore.GetValueOrDefault(row.Key).ToString(CultureInfo.InvariantCulture),
                After = after.GetValueOrDefault(row.Key).ToString(CultureInfo.InvariantCulture)
            });
        }
        if (Game1.player.mailReceived.Contains("GotWoodcuttingBook") != chop.WoodcuttingBookMailBefore)
        {
            changes.Add(new SimulatedFactChange
            {
                Path = "player.mail_received.GotWoodcuttingBook",
                Before = chop.WoodcuttingBookMailBefore.ToString().ToLowerInvariant(),
                After = Game1.player.mailReceived.Contains("GotWoodcuttingBook").ToString().ToLowerInvariant()
            });
        }
        return new WildTreeChopVerification(
            string.IsNullOrEmpty(reason),
            string.IsNullOrEmpty(reason) ? "verified" : reason,
            string.Join(",", deltas.Where(row => row.Value != 0).OrderBy(row => row.Key).Select(row => row.Key + "=" + row.Value)),
            changes.ToArray());
    }

    private static string? WildTreeChopOutputMismatch(
        RuntimeWildTreeChopProjection projection,
        IReadOnlyDictionary<string, int> deltas)
    {
        if (deltas.Any(row => row.Value < 0))
        {
            return "wild_tree_chop_unexpected_output_consumption";
        }
        foreach (var minimum in projection.Minimums)
        {
            if (deltas.GetValueOrDefault(minimum.Key) < minimum.QuantityMin)
            {
                return "wild_tree_chop_guaranteed_minimum_missing:" + minimum.Key;
            }
        }
        foreach (var row in deltas.Where(row => row.Value > 0))
        {
            var separator = row.Key.LastIndexOf('|');
            if (separator <= 0 || !int.TryParse(row.Key[(separator + 1)..], out var quality))
            {
                return "wild_tree_chop_output_key_invalid:" + row.Key;
            }
            var minimum = projection.Minimums.FirstOrDefault(value => value.Key == row.Key)?.QuantityMin ?? 0;
            var excess = row.Value - minimum;
            if (excess <= 0)
            {
                continue;
            }
            var matching = projection.Rules.Where(rule => rule.Matches(row.Key[..separator], quality)).ToArray();
            if (matching.Length == 0 || matching.All(rule => rule.QuantityMax.HasValue) &&
                excess > matching.Sum(rule => rule.QuantityMax!.Value))
            {
                return "wild_tree_chop_output_outside_native_domain:" + row.Key;
            }
        }
        return null;
    }

    private static bool WildTreeChopWaitingForFall(ActiveClearObstacle active) =>
        active.WildTreeChop?.Tree.falling.Value == true;

    private static bool WildTreeChopCompleted(ActiveClearObstacle active) =>
        active.WildTreeChop is not null &&
        !active.Location.terrainFeatures.ContainsKey(active.Target.ToVector2());

    private sealed record WildTreeChopVerification(
        bool Verified,
        string Reason,
        string OutputDeltas,
        SimulatedFactChange[] ChangedFacts);
}
