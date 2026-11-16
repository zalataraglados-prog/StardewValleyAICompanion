namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    internal bool HasAcceptedOriginalRefillControl(CropTeacherManagedSeedOperation operation)
    {
        if (operation.Watering?.RefillControlVerified != true) return false;
        gate.Wait();
        try
        {
            ValidateState();
            return ReferenceEquals(operation.Owner, this) && ReferenceEquals(operation, lastAcceptedOperation) &&
                Pending is null && !HeadRecoveryRequired && SamePath(operation.Watering.AfterSnapshotPath, headSnapshotPath);
        }
        finally { gate.Release(); }
    }
}
