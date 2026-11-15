using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropNativeSharedSourceVerifier
{
    public static CropTeacherSharedSourceReturn Verify(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyCore(transition, allowSettledTransfer: false);

    public static CropTeacherSharedSourceReturn VerifyWithSettledMaterialTransfers(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyCore(transition, allowSettledTransfer: true);

    public static CropTeacherSharedSourceReturn VerifyWithPreparedMaterialTransfers(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyCore(transition, allowSettledTransfer: true, allowPreparation: true);

    public static CropTeacherSharedSourceReturn VerifyWithPreparedTransferYields(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyCore(transition, allowSettledTransfer: true, allowPreparation: true, allowPreparedYield: true);

    private static CropTeacherSharedSourceReturn VerifyCore(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        bool allowSettledTransfer, bool allowPreparation = false, bool allowPreparedYield = false)
    {
        var reference = transition.NativeSharedSource
            ?? throw new InvalidDataException("Shared native transition lacks its original source/ledger binding.");
        Require(transition.TransitionKind == "shared_source_return" && transition.NativeRefillPreparation is null &&
                reference.InputArtifactHashes is { Count: > 0 } && reference.CoveredCommitmentIds is { Length: > 0 },
            "Shared native transition is not an explicit separately credited source return.");
        Require(allowSettledTransfer || reference.SettledMaterialTransfer is null,
            "Settled material transfer requires its explicit joint source consumer.");
        Require(allowPreparation || reference.SettledMaterialTransfer?.Preparation is null,
            "Transfer preparation requires its explicit original-source consumer.");
        Require(reference.SettledTransferReferencePath is null || allowPreparedYield && reference.SettledMaterialTransfer?.Preparation is not null,
            "Prepared transfer yield requires its explicit retained-reference consumer.");
        var scope = reference.SettledTransferReferencePath is { } retainedReferencePath
            ? CropTeacherSharedSourceReturn.OpenAfterPreparedTransferYield(reference.RunRequestPath, transition.BeforeSnapshotPath,
                reference.LedgerPath, reference.CoveredCommitmentIds, transition, reference.StepId, reference.SettledMaterialTransfer!, retainedReferencePath)
            : reference.SettledMaterialTransfer?.Preparation is not null
            ? CropTeacherSharedSourceReturn.OpenAfterPreparedAndSettledMaterialTransfer(reference.RunRequestPath, transition.BeforeSnapshotPath,
                reference.LedgerPath, reference.CoveredCommitmentIds, transition, reference.StepId, reference.SettledMaterialTransfer)
            : reference.SettledMaterialTransfer is { } transfer
            ? CropTeacherSharedSourceReturn.OpenAfterSettledMaterialTransfer(reference.RunRequestPath, transition.BeforeSnapshotPath,
                reference.LedgerPath, reference.CoveredCommitmentIds, transition, reference.StepId, transfer)
            : CropTeacherSharedSourceReturn.Open(reference.RunRequestPath, transition.BeforeSnapshotPath,
                reference.LedgerPath, reference.CoveredCommitmentIds, transition, reference.StepId);
        var expected = new Dictionary<string, string>(CropTeacherSeedTransferNativeOutcome.PathComparer);
        foreach (var (path, hash) in reference.InputArtifactHashes)
        {
            Require(Path.IsPathFullyQualified(path) && !string.IsNullOrWhiteSpace(hash) &&
                    expected.TryAdd(Path.GetFullPath(path), hash),
                "Shared native transition input bindings are relative, missing or duplicated.");
        }
        var actual = scope.InputHashes;
        Require(expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var hash) && hash == pair.Value),
            "Shared native transition changed its complete original input denominator or hashes.");
        scope.ValidateInputsUnchanged();
        return scope;
    }

    internal static Action RetainInputs(MoneyRevenueCropNativeSharedSourceReference reference)
    {
        var expected = reference.InputArtifactHashes.ToDictionary(pair => Path.GetFullPath(pair.Key), pair => pair.Value,
            CropTeacherSeedTransferNativeOutcome.PathComparer);
        var json = new CropTeacherRetainedInputs(expected.Keys.Where(path => !path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)).ToArray());
        void Validate()
        {
            using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
            json.Validate();
            foreach (var (path, hash) in expected)
                Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && CurrentTeacherFrontierSupport.HashFile(path) == hash,
                    "Shared native transition retained input changed.");
        }
        Validate();
        return Validate;
    }
}
