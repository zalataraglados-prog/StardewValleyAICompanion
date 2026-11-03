using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

internal static class ActionQueueDispatchReadinessEndpoints
{
    public static void MapActionQueueDispatchReadinessEndpoints(this WebApplication app)
    {
        const string route = "/api/v1/action-queues/{queueId}/items/{queueItemId}/dispatch-readiness";
        app.MapGet(route, (
            string queueId, string queueItemId, string? stateHash,
            StateStore store, IStrategyCommitmentRepository repository,
            ActionQueueDispatchReadinessService service) =>
        {
            if (!store.ActionQueues.TryGetValue(queueId, out var queue))
                return Results.NotFound(new { detail = "queue not found" });
            var item = queue.Items.SingleOrDefault(row => row.QueueItemId == queueItemId);
            return item is null
                ? Results.NotFound(new { detail = "queue item not found" })
                : Evaluate(queue, item, stateHash, store, repository, service);
        });

        // Local/imported queues must check the exact dispatched item, not a stale stored copy.
        app.MapPost(route, (
            string queueId, string queueItemId, string? stateHash, ActionQueueItem item,
            StateStore store, IStrategyCommitmentRepository repository,
            ActionQueueDispatchReadinessService service) =>
        {
            if (string.IsNullOrWhiteSpace(queueId) ||
                string.IsNullOrWhiteSpace(queueItemId) || item.QueueItemId != queueItemId ||
                item.NormalizedCommand is null || item.NormalizedCommand.Parameters is null ||
                item.NormalizedCommand.Parameters.Any(row => row is null || string.IsNullOrWhiteSpace(row.Name)))
                return Results.UnprocessableEntity(new { detail = "dispatch queue item identity or command is invalid" });
            var queue = new ActionQueueEnvelope { QueueId = queueId };
            return Evaluate(queue, item, stateHash, store, repository, service);
        });
    }

    private static IResult Evaluate(
        ActionQueueEnvelope queue, ActionQueueItem item, string? stateHash,
        StateStore store, IStrategyCommitmentRepository repository,
        ActionQueueDispatchReadinessService service)
    {
        if (string.IsNullOrWhiteSpace(stateHash) ||
            !store.Snapshots.TryGetValue(stateHash, out var snapshot))
            return Results.UnprocessableEntity(new { detail = "state_hash does not match an ingested snapshot" });

        var result = service.Evaluate(queue, item, repository.Get(snapshot), snapshot.StateHash);
        store.AppendAudit(
            result.Ready ? "ActionQueueDispatchReady" : "ActionQueueDispatchBlocked",
            snapshot.GameTick, snapshot.StateHash);
        return Results.Ok(result);
    }
}
