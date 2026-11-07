using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedPlantingSettlement
{
    private readonly StrategyCommitmentLedger committed;
    private readonly SnapshotEnvelope after;
    private readonly CropTeacherSeedPlantingProof proof;
    private readonly ReservationPortfolioSupportingTransitionSettlementRequest request;
    private readonly Dictionary<string, string> artifactHashes;

    internal CropTeacherSeedPlantingSettlement(CropTeacherSeedPlantingSupport scope, StrategyCommitmentLedger ledger,
        MoneyRevenueCropNativeProductionActionBatch batch, MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        ActionQueueEnvelope queue, string queueHash, string proofPath, string? expectedProofHash)
    {
        committed = CropTeacherSeedSupportSource.Copy(ledger);
        var outcome = CropTeacherSeedPlantingNativeOutcome.Verify(scope, committed, batch, transition, queue, queueHash);
        proof = outcome.Proof;
        after = outcome.After;
        artifactHashes = new Dictionary<string, string>(scope.Source.InputHashes, CropTeacherSeedTransferNativeOutcome.PathComparer);
        foreach (var (path, hash) in outcome.ArtifactHashes)
        {
            Require(!artifactHashes.TryGetValue(path, out var prior) || prior == hash, "Owned planting artifacts have conflicting source bindings.");
            artifactHashes[path] = hash;
        }
        ProofPath = Path.GetFullPath(proofPath);
        Require(!artifactHashes.ContainsKey(ProofPath), "Owned planting proof overlaps an input artifact.");
        var proofHash = CanonicalSha256(proof, out _);
        var owned = scope.Source.Supply.Commitments.Single();
        var claim = committed.MaterialReservations.Single(row => row.ReservationId == proof.ReservationId);
        var rebind = owned.Slots.Where(row => row.ReservationId != claim.ReservationId).Select(row => row.ReservationId).ToList();
        if (owned.PurchaseBudget > 0) rebind.Add(scope.CommitmentId + ":seed-purchase:money");
        request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = committed.Revision,
            PortfolioId = proof.PortfolioId, RouteSourceDecisionId = claim.SourceDecisionId, GoalId = "earn_money",
            SupportingTransitionReceiptSha256 = proofHash,
            MaterialConsumptions = new[] { new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = claim.ReservationId, NodeId = claim.NodeId, SlotIndex = claim.SlotIndex,
                QualifiedItemId = claim.QualifiedItemId, ConsumedQuantity = proof.ConsumedQuantity
            } },
            RebindActiveReservationIds = rebind.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            Reason = "verified_crop_seed_planting:" + proof.PortfolioId
        };
        // A pure replay preflight does not replace the original durable repository mutation/readback.
        var preview = new ReservationPortfolioLedgerService().SettleSupportingTransition(committed, after, request, committed.UpdatedAt);
        Require(preview.Accepted && preview.Ledger is not null,
            "Owned planting settlement preflight rejected: " + string.Join(",", preview.Errors));
        CropTeacherOwnedSeedSupply.Read(after, preview.Ledger!, new[] { scope.CommitmentId });
        ValidateArtifactsUnchanged();
        if (expectedProofHash is not null)
            Require(expectedProofHash == proofHash && CurrentTeacherFrontierSupport.HashFile(ProofPath) == expectedProofHash &&
                    EqualJson(CurrentTeacherFrontierSupport.Read<CropTeacherSeedPlantingProof>(ProofPath, "retained owned planting proof"), proof),
                "Owned planting retained proof differs from its expected hash/native rebuild.");
        else
        {
            using var stream = new FileStream(ProofPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, proof, JsonDefaults.Compact);
            stream.Flush(flushToDisk: true);
        }
        Require(CurrentTeacherFrontierSupport.HashFile(ProofPath) == proofHash, "Owned planting proof write differs from its binding.");
        artifactHashes[ProofPath] = proofHash;
    }

    public string ProofPath { get; }
    public CropTeacherSeedPlantingProof Proof => CropTeacherSeedSupportSource.Copy(proof);
    public ReservationPortfolioSupportingTransitionSettlementRequest SettlementRequest
    {
        get { ValidateArtifactsUnchanged(); return CropTeacherSeedSupportSource.Copy(request); }
    }

    public StrategyCommitmentLedger VerifySettlementReply(ReservationPortfolioSupportingTransitionSettlementResult result)
    {
        ValidateArtifactsUnchanged();
        Require(result.Accepted && result.Errors is { Length: 0 } && result.Ledger is not null,
            "Owned planting settlement reply is rejected/incomplete.");
        var replay = new ReservationPortfolioLedgerService().SettleSupportingTransition(committed, after, request, result.Ledger!.UpdatedAt);
        Require(replay.Accepted && EqualJson(replay, result), "Owned planting complete settlement reply does not replay exactly.");
        CropTeacherOwnedSeedSupply.Read(after, result.Ledger!, new[] { proof.CommitmentId });
        return CropTeacherSeedSupportSource.Copy(result.Ledger!);
    }

    private void ValidateArtifactsUnchanged()
    {
        foreach (var (path, hash) in artifactHashes)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Owned planting artifact changed: " + path);
    }
}
