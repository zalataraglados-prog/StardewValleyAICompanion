using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Mechanical settlement scope only. The original planner/compiler still owns dispatch.
public sealed class CropTeacherSeedPurchaseSupport
{
    private readonly CropTeacherSeedSupportSource source;
    private SnapshotEnvelope before => source.Before;
    private CropTeacherOwnedSeedSupply supply => source.Supply;
    private readonly MoneyRevenueCropNativeSeedAcquisitionIntent intent;

    private CropTeacherSeedPurchaseSupport(string runRequestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string supportStepId, string? priorVerifiedSnapshotPath)
    {
        source = new(runRequestPath, snapshotPath, ledgerPath, commitmentId, supportStepId, priorVerifiedSnapshotPath);
        var owned = supply.Commitments.Single();
        var request = source.RunRequest;
        var intents = request.SeedAcquisitionIntents.Where(row => row.CommitmentId == commitmentId).ToArray();
        Require(intents.Length == 1,
            "Crop seed support lacks the original commitment layout/acquisition scope.");
        intent = intents[0];
        Require(intent.SeedQualifiedItemId == owned.SeedQualifiedItemId && !string.IsNullOrWhiteSpace(intent.ShopId) &&
                intent.MaxUnitPrice > 0 && owned.ExternalQuantity == 0 && owned.PlayerQuantity < owned.TileCount &&
                (long)owned.PurchaseBudget >= (long)(owned.TileCount - owned.PlayerQuantity) * intent.MaxUnitPrice,
            "Crop seed support requires a funded shortfall after owned chest staging.");
        ValidateInputsUnchanged();
    }

    public string CommitmentId => source.CommitmentId;
    public string SnapshotPath => source.SnapshotPath;
    public int TotalDay => source.TotalDay;
    public MoneyRevenueCropNativeSeedAcquisitionIntent AcquisitionIntent => Copy(intent);
    public int MaximumPurchaseQuantity
    {
        get
        {
            var owned = supply.Commitments.Single();
            return owned.TileCount - owned.PlayerQuantity;
        }
    }
    public IReadOnlyDictionary<string, string> InputHashes => source.InputHashes;
    public StrategyCommitmentLedger BaseLedger => Copy(source.Ledger);
    public ReservationPortfolioCommitRequest MarkerRequest => source.MarkerRequest;

    public static CropTeacherSeedPurchaseSupport Open(string runRequestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string supportStepId, string? priorVerifiedSnapshotPath = null) =>
        new(runRequestPath, snapshotPath, ledgerPath, commitmentId, supportStepId, priorVerifiedSnapshotPath);

    public CropTeacherSeedPurchaseSettlement VerifyTransition(ReservationPortfolioCommitResult markerResult,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string proofOutputPath)
    {
        var verified = VerifyMarkerReply(markerResult);
        return verified.VerifyTransition(transition, proofOutputPath, verified.ValidateQueue(transition.QueuePath));
    }

    public CropTeacherSeedPurchaseSettlement ResumeTransition(ReservationPortfolioCommitResult markerResult,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string proofPath, string expectedProofSha256)
    {
        var verified = VerifyMarkerReply(markerResult);
        return verified.ResumeTransition(transition, proofPath, verified.ValidateQueue(transition.QueuePath), expectedProofSha256);
    }

    public CropTeacherSeedPurchaseMarker VerifyMarkerReply(ReservationPortfolioCommitResult markerResult)
    {
        return new CropTeacherSeedPurchaseMarker(this, source.VerifyCommit(MarkerRequest, markerResult));
    }

    internal string ValidateQueue(string path)
    {
        ValidateInputsUnchanged();
        var hash = CurrentTeacherFrontierSupport.HashFile(path);
        var queue = CurrentTeacherFrontierSupport.Read<StardewAI.Contracts.Execution.ActionQueueEnvelope>(path,
            "compiled crop seed support queue");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSupportQueue(queue, before,
            intent.SeedQualifiedItemId, intent.ShopId, intent.MaxUnitPrice);
        Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Crop seed support compiled queue changed during binding.");
        return hash;
    }

    internal CropTeacherSeedPurchaseSettlement VerifyTransitionCore(StrategyCommitmentLedger committed,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string proofPath, string queueSha256,
        string? expectedProofSha256)
    {
        ValidateInputsUnchanged();
        Require(IsLowerSha256(queueSha256) && CurrentTeacherFrontierSupport.HashFile(transition.QueuePath) == queueSha256,
            "Crop seed support native queue differs from its prepared compilation.");
        var owned = supply.Commitments.Single();
        var verified = MoneyRevenueCropSeedAcquisitionProofBuilder.VerifySupportSlice(transition,
            intent.SeedQualifiedItemId, intent.ShopId, intent.MaxUnitPrice, owned.PlantingTotalDay);
        Require(EqualJson(before, verified.Before), "Crop seed support native before-state differs from its marker source.");
        Require(verified.Proof.QueueSha256 == queueSha256, "Crop seed support native queue changed during verification.");
        var result = new CropTeacherSeedPurchaseSettlement(CommitmentId, source.MarkerRequest.PortfolioId,
            committed, supply, verified.Proof, verified.After, proofPath, expectedProofSha256);
        ValidateInputsUnchanged();
        return result;
    }

    public void ValidateInputsUnchanged() => source.ValidateInputsUnchanged();

    internal static T Copy<T>(T value) => CropTeacherSeedSupportSource.Copy(value);
}
