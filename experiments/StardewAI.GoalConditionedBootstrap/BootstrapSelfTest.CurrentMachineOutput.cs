namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCurrentMachineOutputSingleDispatchSelection()
    {
        var multiSourceOutput = new AcquisitionMachineActiveOutputState(
            "(O)725",
            2,
            0,
            new[]
            {
                new AcquisitionMachineActiveOutputRouteSource(
                    "machine_output",
                    "machine:(BC)105:rule:Default",
                    "(O)725"),
                new AcquisitionMachineActiveOutputRouteSource(
                    "native_machine_item_query_output",
                    "machine:(BC)105:rule:0:output:1",
                    "(O)725")
            });
        Require(AcquisitionOutputProof.MatchesAuthoritativeRoute(
                    multiSourceOutput,
                    "native_machine_item_query_output",
                    "machine:(BC)105:rule:0:output:1",
                    "(O)725") &&
                !AcquisitionOutputProof.MatchesAuthoritativeRoute(
                    multiSourceOutput,
                    "native_machine_item_query_output",
                    "machine:(BC)105:rule:0:output:2",
                    "(O)725"),
            "Multi-source machine output attribution did not match exactly.");

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
