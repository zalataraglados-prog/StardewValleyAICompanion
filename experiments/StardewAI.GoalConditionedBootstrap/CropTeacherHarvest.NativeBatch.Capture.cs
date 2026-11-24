using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest
{
    public sealed partial class NativeBatch
    {
        private sealed record PickupCapture(string Path, SnapshotEnvelope Snapshot, CropTeacherRetainedInputs Input,
            IReadOnlyList<JsonObject> Drops, long GroundQuantity, long PassiveGain);

        public (Pickup? Command, CropTeacherHarvestPickupFiles? Observed, string StateHash) PreparePickupOrObserve(string path)
        {
            var capture = ReadPickupCapture(path);
            if (capture.PassiveGain == 0) return (PreparePickup(capture), null, capture.Snapshot.StateHash);
            var files = new CropTeacherHarvestPickupFiles(AfterSnapshotPath, string.Empty, string.Empty, path) { ObservedWithoutDispatch = true };
            RecordPassivePickup(capture, files);
            return (null, files, capture.Snapshot.StateHash);
        }

        internal void ReplayPickup(CropTeacherHarvestPickupFiles files)
        {
            if (files.ObservedWithoutDispatch) RecordPassivePickup(ReadPickupCapture(files.AfterSnapshotPath), files);
            else ObservePickup(PreparePickup(files.BeforeSnapshotPath), files);
        }

        private PickupCapture ReadPickupCapture(string path)
        {
            Require(!completed && NeedsPickup && Path.IsPathFullyQualified(path) && Comparer.Equals(path, Path.GetFullPath(path)) && !InputHashes.ContainsKey(path),
                "Owned harvest pickup requires a new capture and an outstanding inventory deficit.");
            var source = new CropTeacherRetainedInputs(new[] { path });
            var current = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "native harvest pickup capture");
            MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(current, "native harvest pickup capture");
            MoneyRevenueCropNativeLifecycleProofBuilder.VerifyMaintenanceLocation(After, current, Prepared.LocationId);
            var item = Prepared.HarvestItemQualifiedId;
            var priorGround = CropSeedResourceBoundary.CountHarvestDebris(After, item);
            var drops = CropSeedResourceBoundary.ReadHarvestDebris(current, item);
            var ground = CropSeedResourceBoundary.SumHarvestDebris(drops);
            var clock = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(current, "time", "time");
            Require(current.SaveId.Value == After.SaveId.Value && current.PlayerId.Value == After.PlayerId.Value && current.GameVersion == After.GameVersion &&
                    current.GameTick >= After.GameTick && MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(current, "time", "total_days") == Prepared.TotalDay &&
                    clock >= MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(After, "time", "time") &&
                    ground <= priorGround && priorGround >= (long)AppliedTileCount * Prepared.MinimumUnitsPerTile - HeldIncrease,
                "Owned harvest pickup capture does not continue the original crop drops.");
            var gained = 0L;
            if (ground < priorGround)
            {
                Require(FieldEnvelopeValidator.IsReadableStatus(current.InGameTime.Status) && current.InGameTime.Value == clock,
                    "Observed harvest pickup has an inconsistent game clock.");
                gained = VerifyPickupDelta(After, current, priorGround - ground);
            }
            else CropSeedResourceBoundary.VerifyUnconsumedCropAction(After, current, "Harvest pickup capture", requireSeedInventory: false);
            return new(path, current, source, drops, ground, gained);
        }

        private long VerifyPickupDelta(SnapshotEnvelope before, SnapshotEnvelope after, long groundDecrease)
        {
            var gained = CropSeedResourceBoundary.VerifyHarvestPickupStock(before, after, Prepared.HarvestItemQualifiedId);
            Require(groundDecrease == gained, "Owned harvest pickup did not conserve the crop drop quantity.");
            return gained;
        }

        private void RecordPassivePickup(PickupCapture capture, CropTeacherHarvestPickupFiles files)
        {
            Require(capture.PassiveGain > 0 && Comparer.Equals(AfterSnapshotPath, files.BeforeSnapshotPath),
                "Observed harvest pickup did not continue its preceding source.");
            capture.Input.Validate();
            inputs.Add(capture.Input);
            AdvancePickup(files, capture.Snapshot, capture.PassiveGain);
        }

        private void AdvancePickup(CropTeacherHarvestPickupFiles files, SnapshotEnvelope after, long gained)
        {
            pickups.Add(files);
            AfterSnapshotPath = files.AfterSnapshotPath;
            After = after;
            HeldIncrease = checked(HeldIncrease + gained);
        }
    }
}
