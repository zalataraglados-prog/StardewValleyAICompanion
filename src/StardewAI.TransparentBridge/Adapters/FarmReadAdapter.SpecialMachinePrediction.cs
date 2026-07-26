using StardewValley;
using StardewValley.GameData.Machines;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static bool IsVettedSpecialOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return IsVettedCaskOutputMethod(machine, outputMethod) ||
            IsVettedDeconstructorOutputMethod(machine, outputMethod);
    }

    private static string ReadVettedSpecialOutputModelId(
        StardewValley.Object machine,
        string outputMethod)
    {
        if (IsVettedCaskOutputMethod(machine, outputMethod))
        {
            return CaskPredictionModelId;
        }
        if (IsVettedDeconstructorOutputMethod(machine, outputMethod))
        {
            return DeconstructorPredictionModelId;
        }
        return string.Empty;
    }

    private static bool TryReadVettedSpecialMachinePrediction(
        StardewValley.Object machine,
        Item inputItem,
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput outputData,
        out object prediction)
    {
        if (TryReadCaskPrediction(
                machine,
                inputItem,
                outputRule,
                triggerRule,
                outputData,
                out prediction))
        {
            return true;
        }

        return TryReadDeconstructorPrediction(
            machine,
            inputItem,
            outputRule,
            triggerRule,
            outputData,
            out prediction);
    }
}
