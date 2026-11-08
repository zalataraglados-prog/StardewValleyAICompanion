using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed class CropTeacherSeedCompilationContext
{
    internal CropTeacherSeedCompilationContext(string outputDirectory, CropTeacherRunStep step,
        CropTeacherSeedTransferMarker? transfer, CropTeacherSeedPlantingMarker? planting,
        CropTeacherSeedTransferExecutionPlan? transferPlan)
    { OutputDirectory = outputDirectory; Step = step; Transfer = transfer; Planting = planting; TransferPlan = transferPlan; }

    public string OutputDirectory { get; }
    public CropTeacherRunStep Step { get; }
    public CropTeacherSeedTransferMarker? Transfer { get; }
    public CropTeacherSeedTransferExecutionPlan? TransferPlan { get; }
    public CropTeacherSeedPlantingMarker? Planting { get; }
}
