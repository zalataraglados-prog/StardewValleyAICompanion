using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunGoalMethodFrontierReuse(
        GoalMethodFrontierBuildInputs inputs)
    {
        using var session = InvocationDeterministicBuildSession.Start();
        var first = BuildGoalMethodFrontier(inputs);
        var second = BuildGoalMethodFrontier(inputs);
        var firstJson = JsonSerializer.Serialize(
            first,
            JsonDefaults.Compact);
        var secondJson = JsonSerializer.Serialize(
            second,
            JsonDefaults.Compact);
        var snapshot = session.Snapshot();
        Require(
            firstJson == secondJson &&
            !ReferenceEquals(first, second) &&
            snapshot.BuildCount == 1 &&
            snapshot.CacheHitCount == 1 &&
            snapshot.CachedBytes > 0,
            "Goal-method frontier was not reused as one isolated exact build.");
    }

    private static GoalMethodFrontierReport BuildGoalMethodFrontier(
        GoalMethodFrontierBuildInputs inputs) =>
        GoalMethodFrontierBuilder.Build(
            inputs.ExpansionPath,
            inputs.DependencyExpansionPath,
            inputs.IsolatedTrainingAuthorizationPath,
            inputs.RequirementInventoryPath,
            inputs.AcquisitionLoweringPath,
            inputs.AcquisitionLoweringCatalogPath,
            inputs.KnowledgePath,
            inputs.OptionMatrixPath,
            inputs.ClaimLedgerPath,
            inputs.DirectionCatalogSourcePath);
}
