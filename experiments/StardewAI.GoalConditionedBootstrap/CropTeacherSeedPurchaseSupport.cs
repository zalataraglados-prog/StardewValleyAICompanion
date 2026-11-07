using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Mechanical settlement scope only. The original planner/compiler still owns dispatch.
public sealed partial class CropTeacherSeedPurchaseSupport
{
    private readonly SnapshotEnvelope before;
    private readonly StrategyCommitmentLedger ledger;
    private readonly CropTeacherOwnedSeedSupply supply;
    private readonly MoneyRevenueCropNativeSeedAcquisitionIntent intent;
    private readonly Dictionary<string, string> inputHashes;
    private readonly ReservationPortfolioCommitRequest marker;

    private CropTeacherSeedPurchaseSupport(string runRequestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string supportStepId, string? priorVerifiedSnapshotPath)
    {
        inputHashes = new[] { runRequestPath, snapshotPath, ledgerPath }
            .Concat(priorVerifiedSnapshotPath is null ? Array.Empty<string>() : new[] { priorVerifiedSnapshotPath }).Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path,
                CurrentTeacherFrontierSupport.HashFile, StringComparer.OrdinalIgnoreCase);
        before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(snapshotPath, "crop seed support source");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(before, "support source");
        ledger = AcquisitionStrategyLedgerReader.Read(ledgerPath, before).Ledger;
        Require(!string.IsNullOrWhiteSpace(supportStepId) &&
                !ledger.History.Any(row => row.CommitmentId == supportStepId) &&
                !ledger.CropPlantingCommitments.Any(row => row.CommitmentId == supportStepId || row.SourceDecisionId == supportStepId),
            "Crop seed support marker must be new and distinct from the Teacher owner.");
        CommitmentId = commitmentId;
        marker = new ReservationPortfolioCommitRequest
        {
            StateHash = before.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = supportStepId, SourceDecisionId = supportStepId, GoalId = "earn_money"
        };
        supply = CropTeacherOwnedSeedSupply.Read(before, ReadSupplyLedger(priorVerifiedSnapshotPath), new[] { commitmentId });
        var owned = supply.Commitments.Single();
        var request = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlanRequest>(
            runRequestPath, "crop seed support run request");
        var intents = request.SeedAcquisitionIntents.Where(row => row.CommitmentId == commitmentId).ToArray();
        var layouts = request.LayoutIntents.Where(row => row.CommitmentId == commitmentId).ToArray();
        Require(request.SchemaVersion == "money_revenue_crop_native_production_run_plan_request.v2" &&
                !request.FormalProductTrainingAuthorized && intents.Length == 1 && layouts.Length == 1 &&
                layouts[0].Tiles.Length == owned.TileCount,
            "Crop seed support lacks the original commitment layout/acquisition scope.");
        intent = intents[0];
        Require(intent.SeedQualifiedItemId == owned.SeedQualifiedItemId && !string.IsNullOrWhiteSpace(intent.ShopId) &&
                intent.MaxUnitPrice > 0 && owned.ExternalQuantity == 0 && owned.PlayerQuantity < owned.TileCount &&
                (long)owned.PurchaseBudget >= (long)(owned.TileCount - owned.PlayerQuantity) * intent.MaxUnitPrice,
            "Crop seed support requires a funded shortfall after owned chest staging.");
        var day = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "total_days");
        Require(day >= 0 && day <= owned.PlantingTotalDay,
            "Crop seed support source is past its planting deadline.");
        SnapshotPath = Path.GetFullPath(snapshotPath);
        TotalDay = day;
        ValidateInputsUnchanged();
    }

    public string CommitmentId { get; }
    public string SnapshotPath { get; }
    public int TotalDay { get; }
    public IReadOnlyDictionary<string, string> InputHashes => new Dictionary<string, string>(inputHashes, StringComparer.OrdinalIgnoreCase);
    public StrategyCommitmentLedger BaseLedger => Copy(ledger);
    public ReservationPortfolioCommitRequest MarkerRequest
    {
        get { ValidateInputsUnchanged(); return Copy(marker); }
    }

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
        ValidateInputsUnchanged();
        var reasons = new List<string>();
        Require(markerResult.Ledger is not null && ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                marker, ledger, markerResult.Ledger, before, markerResult, reasons),
            "Crop seed support marker does not replay exactly: " + string.Join(",", reasons));
        return new CropTeacherSeedPurchaseMarker(this, Copy(markerResult.Ledger!));
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
        var result = new CropTeacherSeedPurchaseSettlement(CommitmentId, marker.PortfolioId,
            committed, supply, verified.Proof, verified.After, proofPath, expectedProofSha256);
        ValidateInputsUnchanged();
        return result;
    }

    public void ValidateInputsUnchanged()
    {
        foreach (var (path, hash) in inputHashes)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Crop seed support input changed: " + path);
    }

    internal static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(
        JsonSerializer.Serialize(value, JsonDefaults.Options), JsonDefaults.Options)!;
}
