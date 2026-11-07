using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using static StardewAI.GoalConditionedBootstrap.CropTeacherSeedPurchaseSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSeedPurchaseSettlement
{
    private readonly string commitmentId;
    private readonly string supportStepId;
    private readonly StrategyCommitmentLedger committed;
    private readonly SnapshotEnvelope after;
    private readonly CropTeacherOwnedSeedSupply beforeSupply;
    private readonly MoneyRevenueCropSeedAcquisitionTransitionProof proof;
    private readonly ReservationPortfolioSupportingTransitionSettlementRequest request;
    private readonly Dictionary<string, string> artifactHashes;

    internal CropTeacherSeedPurchaseSettlement(string commitmentId, string supportStepId,
        StrategyCommitmentLedger committed, CropTeacherOwnedSeedSupply beforeSupply,
        MoneyRevenueCropSeedAcquisitionTransitionProof proof, SnapshotEnvelope after, string proofOutputPath,
        string? expectedProofSha256)
    {
        this.commitmentId = commitmentId;
        this.supportStepId = supportStepId;
        this.committed = Copy(committed);
        this.beforeSupply = beforeSupply;
        this.proof = proof;
        this.after = after;
        var crop = committed.CropPlantingCommitments.Single(row => row.CommitmentId == commitmentId);
        var owned = beforeSupply.Commitments.Single();
        var cashId = commitmentId + ":seed-purchase:money";
        var price = proof.ExpectedUnitPrice ?? 0;
        Require(price >= 0 && price <= owned.PurchaseBudget &&
                (proof.Stage == "purchase_slice" ? price > 0 : price == 0),
            "Crop seed support purchase exceeds its owned budget.");
        var rebind = owned.Slots.Select(row => row.ReservationId).ToList();
        if (owned.PurchaseBudget > price) rebind.Add(cashId);
        ProofPath = Path.GetFullPath(proofOutputPath);
        if (expectedProofSha256 is not null)
        {
            Require(CurrentTeacherFrontierSupport.HashFile(ProofPath) == expectedProofSha256 &&
                    EqualJson(CurrentTeacherFrontierSupport.Read<MoneyRevenueCropSeedAcquisitionTransitionProof>(
                        ProofPath, "retained crop seed support proof"), proof),
                "Crop seed support retained proof differs from its expected hash or native rebuild.");
        }
        else
        {
            using var output = new FileStream(ProofPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(output, proof, JsonDefaults.Compact);
            output.Flush(flushToDisk: true);
        }
        artifactHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [proof.QueuePath] = proof.QueueSha256, [proof.BeforeSnapshotPath] = proof.BeforeSnapshotSha256,
            [proof.ExecutionReceiptPath] = proof.ExecutionReceiptSha256, [proof.AfterSnapshotPath] = proof.AfterSnapshotSha256,
            [ProofPath] = CurrentTeacherFrontierSupport.HashFile(ProofPath)
        };
        request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = committed.Revision,
            PortfolioId = supportStepId, RouteSourceDecisionId = crop.SourceDecisionId, GoalId = "earn_money",
            SupportingTransitionReceiptSha256 = artifactHashes[ProofPath],
            CurrencyConsumptions = price > 0 ? new[] { new ReservationPortfolioCurrencyConsumption
                { CurrencyReservationId = cashId, CurrencyId = 0, ConsumedAmount = price } } : Array.Empty<ReservationPortfolioCurrencyConsumption>(),
            RebindActiveReservationIds = rebind.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            Reason = "verified_crop_seed_support:" + supportStepId
        };
    }

    public MoneyRevenueCropSeedAcquisitionTransitionProof Proof => Copy(proof);
    public string ProofPath { get; }
    public ReservationPortfolioSupportingTransitionSettlementRequest SettlementRequest
    {
        get { ValidateArtifactsUnchanged(); return Copy(request); }
    }

    public CropTeacherSeedPurchaseCustody VerifySettlementReply(ReservationPortfolioSupportingTransitionSettlementResult result)
    {
        ValidateArtifactsUnchanged();
        Require(result.Accepted && result.Errors is { Length: 0 } && result.Ledger is not null,
            "Crop seed support settlement was rejected or incomplete.");
        var replay = new ReservationPortfolioLedgerService().SettleSupportingTransition(
            committed, after, request, result.Ledger!.UpdatedAt);
        Require(replay.Accepted && EqualJson(replay, result), "Crop seed support settlement does not replay exactly.");
        var supply = CropTeacherOwnedSeedSupply.Read(after, result.Ledger!, new[] { commitmentId });
        var acquisition = proof.Stage == "purchase_slice" ? AcquiredSeedRequest(result.Ledger!, supply) : null;
        return new CropTeacherSeedPurchaseCustody(commitmentId, result.Ledger!, after, acquisition, ValidateArtifactsUnchanged);
    }

    private void ValidateArtifactsUnchanged()
    {
        foreach (var (path, hash) in artifactHashes)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Crop seed support artifact changed: " + path);
    }
}
