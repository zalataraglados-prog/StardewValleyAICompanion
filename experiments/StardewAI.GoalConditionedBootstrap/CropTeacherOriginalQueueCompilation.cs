using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CropTeacherOriginalQueueCompilation
{
    internal static void Verify(ActionQueueEnvelope queue, ActionQueueEnvelope rebuilt, string label)
    {
        Require(queue.Items is { Length: > 0 } && queue.Items.All(item => item is not null && item.NormalizedCommand is not null),
            label + " compiled items are unavailable.");
        var original = CropTeacherSeedSupportSource.Copy(queue);
        Require(!string.IsNullOrWhiteSpace(original.QueueId) && original.Items.All(row => !string.IsNullOrWhiteSpace(row.QueueItemId)) &&
                original.Items.Select(row => row.QueueItemId).Distinct(StringComparer.Ordinal).Count() == original.Items.Length,
            label + " compiler queue identities are invalid.");
        foreach (var value in new[] { original, rebuilt })
        {
            value.QueueId = string.Empty;
            foreach (var item in value.Items)
            {
                item.QueueItemId = string.Empty;
                Require(item.NormalizedCommand.Steps.All(step => !string.IsNullOrWhiteSpace(step.StepId)) &&
                        item.NormalizedCommand.Steps.Select(step => step.StepId).Distinct(StringComparer.Ordinal).Count() == item.NormalizedCommand.Steps.Length,
                    label + " native step identities are invalid.");
                foreach (var step in item.NormalizedCommand.Steps) step.StepId = string.Empty;
            }
        }
        Require(EqualJson(original, rebuilt), label + " does not replay its original compiler (except opaque generated IDs).");
    }
}
