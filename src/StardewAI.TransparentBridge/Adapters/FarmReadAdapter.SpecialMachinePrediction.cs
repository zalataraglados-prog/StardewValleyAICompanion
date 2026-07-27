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
            IsVettedDeconstructorOutputMethod(machine, outputMethod) ||
            IsVettedGeodeCrusherOutputMethod(machine, outputMethod) ||
            IsVettedAnvilOutputMethod(machine, outputMethod) ||
            IsVettedIncubatorOutputMethod(machine, outputMethod) ||
            IsVettedSeedMakerOutputMethod(machine, outputMethod);
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
        if (IsVettedGeodeCrusherOutputMethod(
                machine,
                outputMethod))
        {
            return GeodeCrusherPredictionModelId;
        }
        if (IsVettedAnvilOutputMethod(machine, outputMethod))
        {
            return AnvilPredictionModelId;
        }
        if (IsVettedIncubatorOutputMethod(machine, outputMethod))
        {
            return IncubatorPredictionModelId;
        }
        if (IsVettedSeedMakerOutputMethod(machine, outputMethod))
        {
            return SeedMakerPredictionModelId;
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

        if (TryReadDeconstructorPrediction(
                machine,
                inputItem,
                outputRule,
                triggerRule,
                outputData,
                out prediction))
        {
            return true;
        }

        if (TryReadGeodeCrusherPrediction(
                machine,
                inputItem,
                outputRule,
                triggerRule,
                outputData,
                out prediction))
        {
            return true;
        }

        if (TryReadAnvilPrediction(
                machine,
                inputItem,
                outputRule,
                triggerRule,
                outputData,
                out prediction))
        {
            return true;
        }

        if (TryReadIncubatorPrediction(
                machine,
                inputItem,
                outputRule,
                triggerRule,
                outputData,
                out prediction))
        {
            return true;
        }

        return TryReadSeedMakerPrediction(
                machine,
                inputItem,
                outputRule,
                triggerRule,
                outputData,
            out prediction);
    }

    private static bool
        VettedSpecialMachineInputPassesCallbackPreconditions(
            StardewValley.Object machine,
            Item inputItem)
    {
        return machine.QualifiedItemId switch
        {
            AnvilQualifiedItemId =>
                IsVettedAnvilInputSupported(inputItem),
            GeodeCrusherQualifiedItemId =>
                IsVettedGeodeCrusherInputSupported(
                    inputItem),
            _ => true
        };
    }

    private static object? ReadMachineSpecialState(
        StardewValley.Object machine,
        GameLocation location)
    {
        return ReadCaskSpecialState(machine) ??
            ReadIncubatorSpecialState(machine, location) ??
            ReadSolarPanelSpecialState(machine, location) ??
            ReadEndlessFortuneSpecialState(machine) ??
            ReadMushroomLogSpecialState(machine, location);
    }

    private static bool IsVettedSpecialStateOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return IsVettedSolarPanelOutputMethod(
                machine,
                outputMethod) ||
            IsVettedEndlessFortuneOutputMethod(
                machine,
                outputMethod) ||
            IsVettedMushroomLogOutputMethod(
                machine,
                outputMethod);
    }
}
