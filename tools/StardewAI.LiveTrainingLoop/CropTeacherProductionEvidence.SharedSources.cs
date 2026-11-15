using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal static partial class CropTeacherProductionEvidence
{
    private static CropTeacherManagedProductionTransition[] MergeSharedSources(CropTeacherManagedProductionTrace trace,
        IReadOnlyList<CropTeacherManagedSharedSourceReturn> shared, string rootSourcePath, string runRequestPath,
        bool transferredStock, bool inventoryEffects) =>
        MergeSharedSourcesCore(trace, shared, rootSourcePath, runRequestPath, transferredStock, inventoryEffects, appliedNativeWork: false);

    private static CropTeacherManagedProductionTransition[] MergeSharedSourcesCore(CropTeacherManagedProductionTrace trace,
        IReadOnlyList<CropTeacherManagedSharedSourceReturn> shared, string rootSourcePath, string runRequestPath,
        bool transferredStock, bool inventoryEffects, bool appliedNativeWork)
    {
        if (shared.Count == 0) return trace.Transitions.ToArray();
        if (trace.Transitions.Zip(trace.Transitions.Skip(1), (left, right) => right.OwnerSequence <= left.OwnerSequence).Any(value => value))
            throw new InvalidDataException("Joint production crop history is not in its accepted original order.");
        var artifacts = new CropSeedRuntimeArtifacts(trace.InputHashes);
        var root = artifacts.Read<SnapshotEnvelope>(rootSourcePath);
        var original = trace.Transitions.FirstOrDefault()?.NativeTransition
            ?? throw new InvalidDataException("Joint production has no original native source identity.");
        var added = new List<CropTeacherManagedProductionTransition>();
        foreach (var row in shared)
        {
            var reference = row.SourceReference ?? throw new InvalidDataException("Joint production lacks its accepted shared source binding.");
            if (!SamePath(reference.RunRequestPath, runRequestPath) || reference.StepId != row.SupportStepId ||
                row.NativeTransition.NativeRefillPreparation is not null ||
                row.NativeTransition.NativeSharedSource is not null)
                throw new InvalidDataException("Joint production shared source changed its original role/marker binding.");
            var transition = row.NativeTransition with { TransitionKind = "shared_source_return", NativeSharedSource = reference };
            var verified = appliedNativeWork ? MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithNativeWork(transition) :
                inventoryEffects ? MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithSharedInventoryEffects(transition) :
                transferredStock ? MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(transition) :
                MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition);
            var before = artifacts.Read<SnapshotEnvelope>(transition.BeforeSnapshotPath);
            if (before.SaveId.Value != root.SaveId.Value || before.PlayerId.Value != root.PlayerId.Value ||
                before.GameVersion != root.GameVersion || transition.RunId != original.RunId || transition.ExecutorVersion != original.ExecutorVersion)
                throw new InvalidDataException("Joint production shared source belongs to another original run/save/player/version.");
            foreach (var (path, hash) in verified.InputHashes)
                if (!trace.InputHashes.TryGetValue(Path.GetFullPath(path), out var expected) || expected != hash)
                    throw new InvalidDataException("Joint production shared source escaped its accepted owner inputs.");
            added.Add(new(row.OwnerSequence, "shared_source_return", transition));
        }
        var all = trace.Transitions.Concat(added).OrderBy(row => row.OwnerSequence).ToArray();
        if (all.Any(row => row.OwnerSequence < 1) || all.Select(row => row.OwnerSequence).Distinct().Count() != all.Length)
            throw new InvalidDataException("Joint production repeated or changed an accepted owner sequence.");
        for (var index = 1; index < all.Length; index++)
        {
            if (all[index - 1].OperationKind != "shared_source_return" && all[index].OperationKind != "shared_source_return") continue;
            var after = artifacts.Read<SnapshotEnvelope>(all[index - 1].NativeTransition.AfterSnapshotPath);
            var before = artifacts.Read<SnapshotEnvelope>(all[index].NativeTransition.BeforeSnapshotPath);
            if (after.StateHash != before.StateHash || after.GameTick != before.GameTick)
                throw new InvalidDataException("Joint production shared source is not contiguous with its original neighboring action.");
        }
        artifacts.Validate();
        return all;
    }

    private static JsonObject AddJointSourceManifest<T>(T original, IReadOnlyList<CropTeacherManagedSharedSourceReturn> shared,
        CropTeacherManagedProductionTransition[] ordered)
    {
        var result = JsonSerializer.SerializeToNode(original, CropTeacherManagedSeedOwner.JsonOptions)!.AsObject();
        result["owner_shared_source_returns"] = JsonSerializer.SerializeToNode(shared, CropTeacherManagedSeedOwner.JsonOptions);
        result["ordered_owner_transition_sequences"] = JsonSerializer.SerializeToNode(ordered.Select(row => row.OwnerSequence).ToArray());
        result["foreign_crop_production_credit_authorized"] = false;
        result["joint_native_lifecycle_source_consumer"] = "explicit_shared_source_v6";
        return result;
    }
}
