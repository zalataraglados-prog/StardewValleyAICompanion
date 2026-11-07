using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedTransferSettlement
{
    private readonly StrategyCommitmentLedger committed;
    private readonly SnapshotEnvelope after;
    private readonly CropTeacherSeedTransferProof proof;
    private readonly ReservationPortfolioSupportingTransitionSettlementRequest request;
    private readonly Dictionary<string, string> artifactHashes;

    internal CropTeacherSeedTransferSettlement(CropTeacherSeedTransferSupport scope, StrategyCommitmentLedger committed,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string proofPath, ActionQueueEnvelope queue,
        string actualQueueHash, string preparedQueueHash, string? expectedProofHash)
    {
        this.committed = CropTeacherSeedSupportSource.Copy(committed);
        var outcome = CropTeacherSeedTransferNativeOutcome.Verify(scope, this.committed, transition, queue,
            actualQueueHash, preparedQueueHash);
        proof = outcome.Proof;
        after = outcome.After;
        artifactHashes = new Dictionary<string, string>(scope.InputHashes, CropTeacherSeedTransferNativeOutcome.PathComparer);
        foreach (var (path, value) in outcome.ArtifactHashes)
        {
            Require(!artifactHashes.TryGetValue(path, out var prior) || prior == value,
                "Owned seed transfer artifact has conflicting source bindings.");
            artifactHashes[path] = value;
        }
        ProofPath = Path.GetFullPath(proofPath);
        Require(!artifactHashes.ContainsKey(ProofPath),
            "Owned seed transfer proof overlaps an input artifact.");
        var hash = CanonicalSha256(proof, out _);
        var prepared = this.committed.PreparedMaterialTransfer!;
        var affected = prepared.ReservationProjection.ReservationTransitions.Select(row => row.ReservationId).ToHashSet(StringComparer.Ordinal);
        var owned = scope.Source.Supply.Commitments.Single();
        var rebind = owned.Slots.Where(row => !affected.Contains(row.ReservationId)).Select(row => row.ReservationId).ToList();
        if (owned.PurchaseBudget > 0) rebind.Add(scope.CommitmentId + ":seed-purchase:money");
        request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = this.committed.Revision,
            PortfolioId = prepared.PortfolioId, RouteSourceDecisionId = prepared.RouteSourceDecisionId,
            GoalId = "earn_money", PreparedMaterialTransferSha256 = prepared.BindingSha256,
            SupportingTransitionReceiptSha256 = hash,
            MaterialRelocations = CropTeacherSeedSupportSource.Copy(prepared.MaterialRelocations),
            RebindActiveReservationIds = rebind.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            Reason = "verified_crop_seed_transfer:" + prepared.PortfolioId
        };
        // Pure original-service preflight on a clone; only the actual durable reply establishes settlement.
        var preview = new ReservationPortfolioLedgerService().SettleSupportingTransition(this.committed, after, request, this.committed.UpdatedAt);
        Require(preview.Accepted && preview.Ledger is not null,
            "Owned seed transfer settlement preflight rejected: " + string.Join(",", preview.Errors));
        CropTeacherOwnedSeedSupply.Read(after, preview.Ledger!, new[] { scope.CommitmentId });
        ValidateArtifactsUnchanged();
        if (expectedProofHash is not null)
        {
            Require(expectedProofHash == hash && CurrentTeacherFrontierSupport.HashFile(ProofPath) == expectedProofHash &&
                    EqualJson(CurrentTeacherFrontierSupport.Read<CropTeacherSeedTransferProof>(ProofPath, "retained owned seed transfer proof"), proof),
                "Owned seed transfer retained proof differs from its expected hash/native rebuild.");
        }
        else
        {
            using var stream = new FileStream(ProofPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, proof, JsonDefaults.Compact);
            stream.Flush(flushToDisk: true);
        }
        Require(CurrentTeacherFrontierSupport.HashFile(ProofPath) == hash, "Owned seed transfer proof write differs from its binding.");
        artifactHashes[ProofPath] = hash;
    }

    public string ProofPath { get; }
    public CropTeacherSeedTransferProof Proof => CropTeacherSeedSupportSource.Copy(proof);
    public ReservationPortfolioSupportingTransitionSettlementRequest SettlementRequest
    {
        get { ValidateArtifactsUnchanged(); return CropTeacherSeedSupportSource.Copy(request); }
    }

    public StrategyCommitmentLedger VerifySettlementReply(ReservationPortfolioSupportingTransitionSettlementResult result)
    {
        ValidateArtifactsUnchanged();
        Require(result.Accepted && result.Errors is { Length: 0 } && result.Ledger is not null,
            "Owned seed transfer settlement reply was rejected/incomplete.");
        var replay = new ReservationPortfolioLedgerService().SettleSupportingTransition(committed, after, request, result.Ledger!.UpdatedAt);
        Require(replay.Accepted && EqualJson(replay, result), "Owned seed transfer complete settlement reply does not replay exactly.");
        CropTeacherOwnedSeedSupply.Read(after, result.Ledger!, new[] { proof.CommitmentId });
        return CropTeacherSeedSupportSource.Copy(result.Ledger!);
    }

    private void ValidateArtifactsUnchanged()
    {
        foreach (var (path, hash) in artifactHashes)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Owned seed transfer artifact changed: " + path);
    }
}
