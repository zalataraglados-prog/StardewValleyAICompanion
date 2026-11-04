using System;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Execution;

public static class ActionQueueIdentity
{
    public static void Stabilize(ActionQueueEnvelope queue, string compilationIdentity, string prefix = "acquisition_route")
    {
        queue.QueueId = prefix + "_queue." + compilationIdentity;
        for (var index = 0; index < queue.Items.Length; index++)
        {
            var item = queue.Items[index];
            item.QueueItemId = prefix + "_queue_item." + compilationIdentity + "." + index;
            var steps = item.NormalizedCommand?.Steps ?? Array.Empty<CompiledActionStep>();
            for (var stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                steps[stepIndex].StepId = prefix + "_primitive." + compilationIdentity + "." + index + "." + stepIndex;
        }
    }
}
