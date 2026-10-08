namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionCurrentMachineOutputTargetSelector
{
    internal static AcquisitionFacilityTargetEvaluation[] SelectMatching(
        IEnumerable<AcquisitionFacilityTargetEvaluation> targets,
        string qualifiedItemId) => targets
        .Where(value =>
            value.Status ==
                "resolved_existing_current_machine_capacity_match" &&
            value.MachineSourceMatches == true &&
            value.MachineActiveOutputEvidenceAvailable == true &&
            value.MachineActiveOutputRouteMatches == true &&
            value.MachineActiveOutputQualifiedItemId == qualifiedItemId &&
            value.MachineActiveOutputStack is > 0 &&
            value.MachineActiveOutputQuality is >= 0)
        .OrderBy(value => value.TargetLocationId, StringComparer.Ordinal)
        .ThenBy(value => value.TargetTileY)
        .ThenBy(value => value.TargetTileX)
        .ToArray();

    internal static AcquisitionFacilityTargetEvaluation[] SelectDispatchable(
        IEnumerable<AcquisitionFacilityTargetEvaluation> targets,
        string qualifiedItemId,
        int requiredAmount,
        int minimumQuality) => SelectMatching(targets, qualifiedItemId)
        .Where(value =>
            value.MachineActiveOutputStack >= requiredAmount &&
            value.MachineActiveOutputQuality >= minimumQuality)
        .ToArray();
}
