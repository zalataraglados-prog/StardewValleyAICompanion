using StardewAI.Core.Infrastructure;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

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

    public static CropTeacherSharedSourceReturn VerifyWithPreparedProductTransferYields(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyCore(transition, allowSettledTransfer: true, allowPreparation: true, allowPreparedYield: true, productTransfer: true);

    public static CropTeacherSharedSourceReturn VerifyWithTransferredSaleStock(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyCore(transition, allowSettledTransfer: true, allowPreparation: true, allowPreparedYield: true,
            productTransfer: transition.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor, transferredStockSource: true);

    public static CropTeacherSharedSourceReturn VerifyWithSharedInventoryEffects(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyCore(transition, allowSettledTransfer: true, allowPreparation: true, allowPreparedYield: true,
            productTransfer: transition.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor, transferredStockSource: true,
            allowConsumption: true);

    public static CropTeacherSharedSourceReturn VerifyWithNativeWork(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        => VerifyNativeWorkRetentionSource(transition, inventoryEffects: true, out _);

    internal static CropTeacherSharedSourceReturn VerifyNativeWorkRetentionSource(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        bool inventoryEffects, out CropTeacherSharedSourceReturn.VerifiedInventoryObservation observation) =>
        VerifyCore(transition, out observation, true, true, true,
            transition.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor, true, inventoryEffects, allowNativeWork: true);

    private static CropTeacherSharedSourceReturn VerifyCore(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        bool allowSettledTransfer, bool allowPreparation = false, bool allowPreparedYield = false, bool productTransfer = false,
        bool transferredStockSource = false, bool allowConsumption = false)
        => VerifyCore(transition, out _, allowSettledTransfer, allowPreparation, allowPreparedYield, productTransfer, transferredStockSource, allowConsumption);

    internal static CropTeacherSharedSourceReturn VerifyInventoryRetentionSource(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        bool inventoryEffects, out CropTeacherSharedSourceReturn.VerifiedInventoryObservation observation) =>
        VerifyCore(transition, out observation, true, true, true,
            transition.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor, true, inventoryEffects);

    private static CropTeacherSharedSourceReturn VerifyCore(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        out CropTeacherSharedSourceReturn.VerifiedInventoryObservation observation, bool allowSettledTransfer,
        bool allowPreparation, bool allowPreparedYield, bool productTransfer, bool transferredStockSource, bool allowConsumption,
        bool allowNativeWork = false)
    {
        var reference = transition.NativeSharedSource
            ?? throw new InvalidDataException("Shared native transition lacks its original source/ledger binding.");
        Require(allowNativeWork || !reference.AppliedNativeWorkQueue,
            "Applied native work requires its explicit source consumer.");
        Require(transition.TransitionKind == "shared_source_return" && transition.NativeRefillPreparation is null &&
                reference.InputArtifactHashes is { Count: > 0 } && reference.CoveredCommitmentIds is { Length: > 0 },
            "Shared native transition is not an explicit separately credited source return.");
        var consumption = reference.SettledInventoryConsumption;
        Require(allowConsumption || consumption is null && reference.SettledConsumptionReferencePath is null,
            "Inventory consumption requires its explicit shared inventory-effects consumer.");
        Require((consumption is not null) == (reference.SettledConsumptionReferencePath is not null) &&
                (consumption is null || reference.SettledMaterialTransfer is null && reference.SettledTransferReferencePath is null && reference.AcceptedMarker is null),
            "Shared inventory source mixes or loses its typed original settlement reference.");
        Require(allowSettledTransfer || reference.SettledMaterialTransfer is null,
            "Settled material transfer requires its explicit joint source consumer.");
        Require(allowPreparation || reference.SettledMaterialTransfer?.Preparation is null,
            "Transfer preparation requires its explicit original-source consumer.");
        Require(reference.SettledTransferReferencePath is null || allowPreparedYield && reference.SettledMaterialTransfer?.Preparation is not null,
            "Prepared transfer yield requires its explicit retained-reference consumer.");
        Require(transferredStockSource || reference.AcceptedMarker is null,
            "Accepted source markers require their explicit transferred-stock consumer.");
        var lease = reference.SettledMaterialTransfer?.Preparation is { } preparation &&
            CurrentTeacherFrontierSupport.Read<ReservationPortfolioCommitResult>(preparation.ReplyPath,
                "shared inventory-lease preparation hint").Ledger is { } ledger && CropSaleStockTransferLeaseContract.IsBound(ledger);
        Require(!lease || transferredStockSource && reference.AcceptedMarker is not null && reference.SettledTransferReferencePath is not null,
            "Inventory transfer lease requires its original accepted release marker.");
        Require(!productTransfer || transition.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor &&
                (consumption is not null || transferredStockSource && reference.SettledMaterialTransfer is null ||
                 reference.SettledTransferReferencePath is not null && reference.SettledMaterialTransfer?.Preparation is not null),
            "Product shared transfer requires the explicit Product prepared-yield source.");
        var scope = CropTeacherSharedSourceReturn.OpenInventoryObservation(reference, transition, productTransfer, transferredStockSource, lease, out observation);
        if (reference.AcceptedMarker is not null) scope.BindAcceptedMarker(reference.AcceptedMarker);
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
