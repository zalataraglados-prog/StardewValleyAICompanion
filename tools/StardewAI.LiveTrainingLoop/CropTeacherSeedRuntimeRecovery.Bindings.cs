using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedRuntimeRecovery
{
    private sealed class CompilationMarkers
    {
        internal CropTeacherSeedTransferMarker? Transfer { get; set; }
        internal CropTeacherSeedPlantingMarker? Planting { get; set; }
        internal Action? BindRetainedPlantingQueue { get; set; }
    }

    internal static CropSeedRuntimeScopeBinding Bind(CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step) => Bind(scopes, step, null);

    private static CropSeedRuntimeScopeBinding Bind(CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step, CompilationMarkers? markers)
    {
        if (scopes.Purchase is { } purchase) return CropSeedRuntimeBindings.Purchase(purchase);
        if (scopes.Transfer is { } transfer) return CropSeedRuntimeBindings.Transfer(transfer,
            markers is null ? null : marker => markers.Transfer = marker);
        var planting = scopes.Planting ?? throw new InvalidDataException("Crop seed recovery lacks its original selected scope.");
        var batch = step.ActionBatch ?? throw new InvalidDataException("Crop seed recovery planting batch is absent.");
        var bounded = planting.ValidateExecutionPreflight(batch, step.SeedReservationId);
        return CropSeedRuntimeBindings.Planting(planting, batch, bounded, step.SeedReservationId,
            marker =>
            {
                if (markers is null) marker.BuildActions(batch, step.SeedReservationId);
                else
                {
                    markers.Planting = marker;
                    markers.BindRetainedPlantingQueue = () => marker.BuildActions(batch, step.SeedReservationId);
                }
            });
    }
}
