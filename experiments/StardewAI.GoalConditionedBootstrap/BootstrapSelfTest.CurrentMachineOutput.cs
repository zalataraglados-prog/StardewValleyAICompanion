namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCurrentMachineOutputSingleDispatchSelection()
    {
        var twoInsufficientTargets = new[]
        {
            CurrentMachineOutputTarget(10, 20, stack: 1),
            CurrentMachineOutputTarget(11, 20, stack: 1)
        };
        Require(AcquisitionCurrentMachineOutputTargetSelector
                .SelectDispatchable(
                    twoInsufficientTargets,
                    "(O)725",
                    requiredAmount: 2,
                    minimumQuality: 0)
                .Length == 0,
            "Multiple insufficient current outputs were aggregated into one dispatch.");

        var oneSufficientTarget = twoInsufficientTargets
            .Append(CurrentMachineOutputTarget(12, 20, stack: 2))
            .ToArray();
        var selected = AcquisitionCurrentMachineOutputTargetSelector
            .SelectDispatchable(
                oneSufficientTarget,
                "(O)725",
                requiredAmount: 2,
                minimumQuality: 0);
        Require(selected.Length == 1 &&
                selected[0].TargetTileX == 12 &&
                selected[0].TargetTileY == 20,
            "A sufficient single current output target was not retained.");
    }

    private static AcquisitionFacilityTargetEvaluation
        CurrentMachineOutputTarget(int tileX, int tileY, int stack) => new(
            "Farm",
            null,
            "resolved_existing_current_machine_capacity_match",
            null,
            null,
            null,
            null,
            new[] { "state.farm.machines.value[]" },
            Array.Empty<string>(),
            MachineQualifiedItemId: "(BC)105",
            TargetTileX: tileX,
            TargetTileY: tileY,
            MachineSourceMatches: true,
            MachineCapacityState: "ready_output",
            MachineActiveOutputEvidenceAvailable: true,
            MachineActiveOutputQualifiedItemId: "(O)725",
            MachineActiveOutputStack: stack,
            MachineActiveOutputQuality: 0,
            MachineActiveOutputRouteMatches: true);
}
