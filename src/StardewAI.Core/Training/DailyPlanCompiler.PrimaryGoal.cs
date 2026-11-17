using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    public SmallModelPlanEnvelope CompileWithPrimaryGoal(
        IEnumerable<PolicyEventCandidatePrediction> candidates,
        string stateHash,
        int currentTime,
        IReadOnlyCollection<string> requiredCandidateIds,
        IReadOnlyCollection<string> primaryCandidateIds,
        string goalId = "daily.closed_loop",
        string executionMode = "training_singleplayer",
        int maxCandidates = 4,
        int? availableMinutes = null,
        int? energyBudget = null)
    {
        var rows = candidates.ToArray();
        var required = RequiredCandidateSet(rows, requiredCandidateIds);
        var primary = new HashSet<string>(primaryCandidateIds, StringComparer.Ordinal);
        // A primary group contains alternatives. Blocked alternatives remain ordinary
        // audited candidates; unlike required work, not every one must be executable.
        if (primary.Count != primaryCandidateIds.Count || primary.Any(string.IsNullOrWhiteSpace) ||
            primary.Any(id => rows.Count(row => row.CandidateId == id) != 1))
            throw new ArgumentException("Primary candidate IDs must name distinct, uniquely materialized candidates.");
        return CompileCore(rows, stateHash, goalId, executionMode, maxCandidates, availableMinutes, energyBudget,
            new JointCalendarBudget(currentTime), required, primary);
    }
}
