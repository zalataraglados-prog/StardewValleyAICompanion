using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReplanBuilder
{
    private static bool PriorQueueInvalidated(
        AcquisitionRouteSupportingTransitionRequest request,
        AcquisitionRouteDispatchCompilation compilation,
        AcquisitionRouteSupportingTransitionFreshContext fresh)
    {
        var queue = compilation.ActionQueue;
        if (queue is null || queue.Items.Length == 0 ||
            compilation.SourceStateHash != request.SourceStateHash ||
            queue.StateHash != request.SourceStateHash ||
            fresh.StateHash == request.SourceStateHash)
        {
            return false;
        }
        var revision = PriorLedgerRevision(compilation);
        return revision > 0 && queue.Items.All(item =>
                int.TryParse(
                    UniqueParameter(
                        item,
                        "acquisition_reservation_ledger_revision"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var itemRevision) &&
                itemRevision == revision) &&
            revision != fresh.LedgerRevision;
    }

    private static int PriorLedgerRevision(
        AcquisitionRouteDispatchCompilation compilation)
    {
        var values = (compilation.ActionQueue?.Items ??
                Array.Empty<ActionQueueItem>())
            .Select(item => UniqueParameter(
                item,
                "acquisition_reservation_ledger_revision"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 1 && int.TryParse(
            values[0],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var revision)
                ? revision
                : 0;
    }

    private static string UniqueParameter(
        ActionQueueItem item,
        string name)
    {
        var values = (item.NormalizedCommand?.Parameters ??
                Array.Empty<SmallModelActionParameter>())
            .Where(value => value.Name == name)
            .Select(value => value.Value)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }


}
