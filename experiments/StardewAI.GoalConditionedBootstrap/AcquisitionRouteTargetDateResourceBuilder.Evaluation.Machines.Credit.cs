namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private static bool TryMachineExistingOutputCredit(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionRouteCalendarResolution staticRoute,
        out int creditedQuantity,
        out string[] blockingReasons)
    {
        creditedQuantity = 0;
        blockingReasons = Array.Empty<string>();
        try
        {
            foreach (var target in route.TargetEvaluations.Where(value =>
                         value.MachineActiveOutputRouteMatches == true))
            {
                if (target.MachineActiveOutputEvidenceAvailable != true ||
                    target.MachineActiveOutputQualifiedItemId !=
                        staticRoute.QualifiedItemId ||
                    target.MachineActiveOutputStack is not > 0 ||
                    target.MachineActiveOutputQuality is not >= 0 ||
                    target.MachineCapacityState is not
                        ("processing" or "ready_output"))
                {
                    blockingReasons = new[]
                    {
                        "machine_existing_output_credit_evidence_invalid"
                    };
                    return false;
                }
                if (target.MachineActiveOutputQuality >=
                    staticRoute.MinimumQuality)
                {
                    creditedQuantity = checked(
                        creditedQuantity +
                        target.MachineActiveOutputStack.Value);
                }
            }
            creditedQuantity = Math.Min(
                creditedQuantity,
                staticRoute.RequiredAmount);
            return true;
        }
        catch (OverflowException)
        {
            blockingReasons = new[]
            {
                "machine_existing_output_credit_quantity_overflow"
            };
            return false;
        }
    }

    private static AcquisitionRouteTargetDateResource
        ExistingMachineOutputSatisfiesRequirement(
            AcquisitionRouteTargetDateFacility route,
            AcquisitionRouteCalendarResolution staticRoute,
            int creditedQuantity)
    {
        var evaluation = new AcquisitionResourceInputEvaluation(
            "machine_existing_output_credit",
            staticRoute.QualifiedItemId,
            0,
            creditedQuantity,
            "resolved_resource_input_match",
            new[]
            {
                "target_date_facility_capacity.routes[].target_evaluations[].machine_active_output_route_matches",
                "state.farm.machines.value[].held_item"
            },
            Array.Empty<string>(),
            new AcquisitionMachineResourceBinding(
                "existing_output_credit",
                string.Empty,
                Array.Empty<string>(),
                null,
                null,
                0,
                0,
                Array.Empty<AcquisitionMachineResourceSlot>())
            {
                CreditedExistingOutputQuantity = creditedQuantity
            });
        return Result(
            route,
            "resolved_resource_inputs_match",
            true,
            true,
            MachineInput,
            new[] { evaluation },
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
