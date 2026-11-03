using System;

namespace StardewAI.RuntimePrimitives
{
    // SMAPI checks Type.IsPublic, which rejects even public nested API classes.
    public sealed class ExecutorPlanningPauseReadApi
    {
        private readonly Func<string> read;

        public ExecutorPlanningPauseReadApi(Func<string> read) =>
            this.read = read ?? throw new ArgumentNullException(nameof(read));

        public string ReadPlanningPauseEvidence() => read();
    }
}
