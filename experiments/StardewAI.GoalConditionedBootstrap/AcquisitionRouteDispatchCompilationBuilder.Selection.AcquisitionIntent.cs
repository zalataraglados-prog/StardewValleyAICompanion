using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    private static bool HasValidTerminalAcquisitionIntent(
        AcquisitionRouteTargetDateUnlock requirement,
        PolicyEventCandidatePrediction candidate)
    {
        var acquisitionParameters = (candidate.Parameters ??
                Array.Empty<SmallModelActionParameter>())
            .Where(parameter => parameter.Name.StartsWith(
                "acquisition_",
                StringComparison.Ordinal))
            .ToArray();
        if (acquisitionParameters.Length == 0)
            return true;
        if (requirement.RouteKind != "native_monster_drop_table" ||
            acquisitionParameters.Length != 3)
        {
            return false;
        }

        return HasUniqueValue(
                acquisitionParameters,
                "acquisition_target_route_kind",
                requirement.RouteKind) &&
            HasUniqueValue(
                acquisitionParameters,
                "acquisition_target_source_id",
                requirement.SourceId) &&
            HasUniqueValue(
                acquisitionParameters,
                "acquisition_target_qualified_item_id",
                requirement.QualifiedItemId);
    }

    private static bool HasUniqueValue(
        IEnumerable<SmallModelActionParameter> parameters,
        string name,
        string expected)
    {
        var values = parameters
            .Where(parameter => parameter.Name == name)
            .Select(parameter => parameter.Value)
            .ToArray();
        return values.Length == 1 && string.Equals(
            values[0], expected, StringComparison.Ordinal);
    }
}
