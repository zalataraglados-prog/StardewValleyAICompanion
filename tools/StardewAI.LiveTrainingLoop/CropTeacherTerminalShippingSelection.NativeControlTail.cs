using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherTerminalShippingSelection
{
    internal static CropTeacherManagedProductionTransition[]? ReadNativeRetentionControlTail(CropSeedRuntimeArtifacts inputs,
        MoneyRevenueCropOutputRetentionProof retention, JsonObject manifest, string headPath)
    {
        if (SamePath(retention.AfterSnapshotPath, headPath)) return [];
        var rows = manifest["owner_transitions"]?.Deserialize<CropTeacherManagedProductionTransition[]>(CropTeacherManagedSeedOwner.JsonOptions)
            ?? throw new InvalidDataException("Native terminal lacks its original owner control tail.");
        var anchors = rows.Select((row, index) => (row, index)).Where(value =>
            SamePath(value.row.NativeTransition.AfterSnapshotPath, retention.AfterSnapshotPath)).ToArray();
        if (anchors is not [var anchor] || anchor.index == rows.Length - 1 ||
            !SamePath(rows[^1].NativeTransition.AfterSnapshotPath, headPath)) return null;
        var tail = rows.Skip(anchor.index + 1).ToArray();
        var prior = anchor.row;
        foreach (var row in tail)
        {
            var transition = row.NativeTransition;
            if (row.OperationKind != "supporting_route" || transition.TransitionKind != "supporting_route_to_sleep" ||
                row.OwnerSequence <= prior.OwnerSequence ||
                transition.RunId != prior.NativeTransition.RunId || transition.ExecutorVersion != prior.NativeTransition.ExecutorVersion ||
                transition.TotalDay != retention.HarvestTotalDay ||
                !SamePath(transition.BeforeSnapshotPath, prior.NativeTransition.AfterSnapshotPath))
                return null;
            foreach (var path in new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath })
                if (!inputs.Hashes.ContainsKey(Path.GetFullPath(path)))
                    throw new InvalidDataException("Native terminal control tail escaped its retained inputs.");
            prior = row;
        }
        var harvest = inputs.Read<SnapshotEnvelope>(retention.AfterSnapshotPath);
        if (harvest.StateHash != retention.AfterStateHash || inputs.Hashes[Path.GetFullPath(retention.AfterSnapshotPath)] != retention.AfterSnapshotSha256)
            throw new InvalidDataException("Native terminal control tail changed its retained harvest source.");
        // Reuse the original exact control/resource/step checks, not a generic neutral-route allowance.
        MoneyRevenueCropNativeLifecycleProofBuilder.VerifyNativeObservedControlTail(retention.AfterSnapshotPath,
            tail.Select(row => row.NativeTransition).ToArray(), headPath);
        return tail;
    }
}
