using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropNativeSharedSourceVerifier
{
    public static CropTeacherSharedSourceReturn Verify(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        var reference = transition.NativeSharedSource
            ?? throw new InvalidDataException("Shared native transition lacks its original source/ledger binding.");
        Require(transition.TransitionKind == "shared_source_return" && transition.NativeRefillPreparation is null &&
                reference.InputArtifactHashes is { Count: > 0 } && reference.CoveredCommitmentIds is { Length: > 0 },
            "Shared native transition is not an explicit separately credited source return.");
        var scope = CropTeacherSharedSourceReturn.Open(reference.RunRequestPath, transition.BeforeSnapshotPath,
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
