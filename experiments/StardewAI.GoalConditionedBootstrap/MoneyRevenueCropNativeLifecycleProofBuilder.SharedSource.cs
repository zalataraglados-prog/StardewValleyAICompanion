namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private const string SharedSourceReturnKind = "shared_source_return";

    private static LoadedTransition LoadJointObservedTransition(MoneyRevenueCropNativeLifecycleTransitionRequest request) =>
        LoadJointObservedTransitionCore(request, transferredStock: false);

    private static LoadedTransition LoadTransferredStockObservedTransition(MoneyRevenueCropNativeLifecycleTransitionRequest request) =>
        LoadJointObservedTransitionCore(request, transferredStock: true);

    private static LoadedTransition LoadSharedInventoryObservedTransition(MoneyRevenueCropNativeLifecycleTransitionRequest request) =>
        LoadJointObservedTransitionCore(request, transferredStock: true, inventoryEffects: true);

    private static LoadedTransition LoadNativeWorkObservedTransition(MoneyRevenueCropNativeLifecycleTransitionRequest request) =>
        LoadJointObservedTransitionCore(request, transferredStock: true, inventoryEffects: true, appliedNativeWork: true);

    private static LoadedTransition LoadJointObservedTransitionCore(MoneyRevenueCropNativeLifecycleTransitionRequest request,
        bool transferredStock, bool inventoryEffects = false, bool appliedNativeWork = false)
    {
        if (request.NativeSharedSource is null && request.TransitionKind != SharedSourceReturnKind)
            return LoadNativeObservedTransition(request) with { JointObserved = true };
        var scope = appliedNativeWork ? MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithNativeWork(request) :
            inventoryEffects ? MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithSharedInventoryEffects(request) :
            transferredStock ? MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(request) :
            MoneyRevenueCropNativeSharedSourceVerifier.Verify(request);
        var loaded = LoadTransitionCore(request, allowSharedSource: true);
        Require(loaded.Before.StateHash == scope.SourceStateHash && loaded.After.StateHash == scope.AfterStateHash,
            "Joint crop lifecycle changed the verified foreign source boundary.");
        scope.ValidateInputsUnchanged();
        return loaded with { NativeObserved = true, NativeSharedSource = true, JointObserved = true,
            SharedInputGuard = MoneyRevenueCropNativeSharedSourceVerifier.RetainInputs(request.NativeSharedSource!) };
    }

    private static void ValidateSharedCropBoundary(LoadedTransition transition, LifecycleCommitmentContext[] contexts)
    {
        Require(transition.NativeSharedSource && transition.Request.TransitionKind == SharedSourceReturnKind &&
                ReadCalendar(transition.Before).TotalDay == transition.Request.TotalDay &&
                ReadCalendar(transition.After).TotalDay == transition.Request.TotalDay,
            "Joint crop lifecycle foreign source crossed its declared day.");
        foreach (var context in contexts)
            foreach (var tile in context.Tiles)
                Require(BootstrapArtifactGuard.EqualJson(
                        ReadCrop(transition.Before, context.Layout.LocationId, tile.X, tile.Y),
                        ReadCrop(transition.After, context.Layout.LocationId, tile.X, tile.Y)),
                    "Foreign work changed an owned crop tile without its original crop action attribution.");
    }
}
