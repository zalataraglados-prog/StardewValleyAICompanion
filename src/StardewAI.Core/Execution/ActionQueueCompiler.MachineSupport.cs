using System;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static bool MachineSupportContinuationMatches(
        SmallModelAction action,
        MachineSupportContinuation expected)
    {
        return MachineSupportIntentProjection.Parameters(expected)
            .All(parameter => string.Equals(
                ReadParameter(action, parameter.Name),
                parameter.Value,
                StringComparison.Ordinal));
    }
}
