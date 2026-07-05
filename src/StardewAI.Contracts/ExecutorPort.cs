namespace StardewAI.Contracts
{
    using StardewAI.Contracts.Execution;

    public interface IExecutorPort
    {
        bool ExecutionEnabled { get; }

        ExecutionBatchResult Execute(ActionQueueEnvelope queue);
    }
}
