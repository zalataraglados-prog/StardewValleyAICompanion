using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherObservedShippingPrefixVerification(string AfterSnapshotPath, int DepositedQuantity,
    MoneyRevenueCropShippingDepositEvidence[] Deposits, IReadOnlyDictionary<string, string> InputHashes)
{
    public bool NativeShippingPrefixVerified => true;
    public bool OutputRetentionVerified => false;
    public bool OwnerAcceptanceVerified => false;
    public bool TerminalSettlementVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

// Consumes immutable original work; certificate and owner-history binding belong to its caller.
public static class CropTeacherObservedShippingPrefixVerifier
{
    public static CropTeacherObservedShippingPrefixVerification Verify(CropTeacherObservedShippingPrefix prefix,
        CropTeacherTerminalShippingTarget target, string expectedHeadPath)
    {
        Require(prefix.SchemaVersion == "crop_teacher_observed_shipping_prefix.v1" && prefix.Transitions is { Length: > 0 } &&
            target.NativeVariants?.SchemaVersion == "crop_teacher_native_terminal_variants.v2" &&
            (prefix.OutputRetentionProofPath == target.RetentionProofPath ||
             !string.IsNullOrWhiteSpace(prefix.OutputRetentionProofPath) && !string.IsNullOrWhiteSpace(target.RetentionProofPath) &&
             CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(prefix.OutputRetentionProofPath), Path.GetFullPath(target.RetentionProofPath))),
            "Missing explicit observed-shipping prefix/ordered target binding.");
        var pins = new Dictionary<string, string>(CropTeacherSeedTransferNativeOutcome.PathComparer);
        void Pin(string path, string? expected = null)
        {
            path = Path.GetFullPath(path);
            var actual = CurrentTeacherFrontierSupport.HashFile(path);
            Require((expected is null || actual == expected) && (!pins.TryGetValue(path, out var prior) || prior == actual),
                "Observed shipping changed an original artifact.");
            pins[path] = actual;
        }
        SnapshotEnvelope Snapshot(string path)
        {
            Pin(path);
            return CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "observed shipping original snapshot");
        }
        var currentPath = prefix.RetentionSnapshotPath;
        var current = Snapshot(currentPath);
        target.VerifyNativeVariantSource(current, 0);
        var identity = prefix.Transitions[0];
        var receipts = new HashSet<string>(CropTeacherSeedTransferNativeOutcome.PathComparer);
        var deposits = new List<MoneyRevenueCropShippingDepositEvidence>();
        foreach (var transition in prefix.Transitions)
        {
            Require(transition.TotalDay == target.TotalDay && transition.RunId == identity.RunId &&
                transition.ExecutorVersion == identity.ExecutorVersion && !string.IsNullOrWhiteSpace(identity.RunId) &&
                !string.IsNullOrWhiteSpace(identity.ExecutorVersion) && receipts.Add(Path.GetFullPath(transition.ExecutionReceiptPath)),
                "Observed shipping changed its native identity/day or replayed a receipt.");
            var before = Snapshot(transition.BeforeSnapshotPath);
            Require(BootstrapArtifactGuard.EqualJson(current, before), "Observed shipping lost the original contiguous source.");
            if (transition.NativeSharedSource is null)
            {
                _ = MoneyRevenueCropNativeLifecycleProofBuilder.VerifyNativeObservedControlTail(currentPath, [transition], transition.AfterSnapshotPath);
                foreach (var path in new[] { transition.QueuePath, transition.ExecutionReceiptPath }) Pin(path);
                currentPath = transition.AfterSnapshotPath; current = Snapshot(currentPath);
                target.VerifyNativeVariantSource(current, deposits.Count);
                continue;
            }
            var scope = MoneyRevenueCropNativeSharedSourceVerifier.VerifyNativeWorkRetentionSource(transition, true, out var observation);
            // The original verifier has already read/validated the complete queue and every wire frame.
            // Its same-invocation observation avoids another full read of that input graph.
            var native = observation.Native;
            foreach (var (path, hash) in scope.InputHashes) Pin(path, hash);
            foreach (var frame in native.Actions)
            {
                if (frame.Item.OptionId != "executor.ship_inventory_item_to_bin") continue;
                Require(deposits.Count < target.RequiredQuantity, "Observed shipping exceeds the whole obligation.");
                var deposit = NativeShippingDepositEvidenceVerifier.VerifyNativeWorkFrame(frame, target.QualifiedItemId);
                Require(deposit.Verified, "Observed native shipping rejected: " + string.Join(",", deposit.BlockingReasons));
                var chosen = target.VariantAt(deposits.Count);
                Require(deposit.SlotIndex == chosen.SlotIndex && deposit.Quality == chosen.Quality && deposit.UnitPrice == chosen.UnitPrice &&
                    deposit.BinTileX == target.BinTileX && deposit.BinTileY == target.BinTileY &&
                    deposit.BeforePendingSettlementValue == target.PhysicalPendingValueAt(deposits.Count) &&
                    deposit.AfterPendingSettlementValue == target.PhysicalPendingValueAt(deposits.Count + 1),
                    "Observed shipping differs from the retained variant/order/bin.");
                // Only the shipping frame is shipping-neutral; preceding movement may have legitimate effects.
                CropSeedResourceBoundary.VerifyShippingStock(frame.Before, frame.After, deposit);
                CropSeedResourceBoundary.VerifyShippingBinsAndPersistentState(frame.Before, frame.After, deposit);
                deposits.Add(new(deposits.Count, "observed-shipping-" + deposits.Count,
                    CurrentTeacherFrontierSupport.HashFile(transition.QueuePath), CurrentTeacherFrontierSupport.HashFile(transition.BeforeSnapshotPath),
                    CurrentTeacherFrontierSupport.HashFile(transition.ExecutionReceiptPath), CurrentTeacherFrontierSupport.HashFile(transition.AfterSnapshotPath),
                    native.Before.StateHash, native.After.StateHash, deposit.QueueItemId, chosen.SlotIndex, 1, chosen.UnitPrice,
                    deposit.BeforePendingSettlementValue!.Value, deposit.AfterPendingSettlementValue!.Value,
                    native.Queue.Items.Select(item => item.OptionId).ToArray()));
            }
            scope.ValidateInputsUnchanged();
            currentPath = transition.AfterSnapshotPath; current = native.After;
            target.VerifyNativeVariantSource(current, deposits.Count);
        }
        Require(deposits.Count > 0 && CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(currentPath),
            Path.GetFullPath(expectedHeadPath)), "Observed shipping does not reach the exact accepted head.");
        foreach (var (path, hash) in pins) Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Observed shipping input changed during verification.");
        return new(Path.GetFullPath(currentPath), deposits.Count, deposits.ToArray(),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(pins));
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }
}
