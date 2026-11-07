using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedRuntimeRecovery
{
    private static CropSeedRuntimeScopeBinding Bind(CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step)
    {
        if (scopes.Purchase is { } purchase) return CropSeedRuntimeBindings.Purchase(purchase);
        if (scopes.Transfer is { } transfer) return CropSeedRuntimeBindings.Transfer(transfer);
        var planting = scopes.Planting ?? throw new InvalidDataException("Crop seed recovery lacks its original selected scope.");
        var batch = step.ActionBatch ?? throw new InvalidDataException("Crop seed recovery planting batch is absent.");
        var bounded = planting.ValidateExecutionPreflight(batch, step.SeedReservationId);
        return CropSeedRuntimeBindings.Planting(planting, batch, bounded, step.SeedReservationId,
            marker => marker.BuildActions(batch, step.SeedReservationId));
    }
}
