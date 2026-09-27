namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRouteSupportingTransitionKinds
{
    public const string CropPlanting = "crop_planting";
    public const string MachineInputLoad = "machine_input_load";
    public const string MachineInputMaterialTransfer =
        "machine_input_material_transfer";
    public const string MachineInputPurchase = "machine_input_purchase";
    public const string MachineCapacityEstablishment =
        "machine_capacity_establishment";

    public static readonly string[] All =
    [
        CropPlanting,
        MachineInputLoad,
        MachineInputMaterialTransfer,
        MachineInputPurchase,
        MachineCapacityEstablishment
    ];

    public static bool IsKnown(string value) => All.Contains(
        value,
        StringComparer.Ordinal);
}
