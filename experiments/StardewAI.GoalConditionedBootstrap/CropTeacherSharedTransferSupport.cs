using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Retains the unprepared source while the SAME owner holds the original preparation RPC.
public sealed class CropTeacherSharedTransferSupport : ICropTeacherSharedPortfolioPreparation
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly SnapshotEnvelope before;
    private readonly StrategyCommitmentLedger ledger;
    private readonly ReservationPortfolioCommitRequest request;
    private readonly string stepId;

    private CropTeacherSharedTransferSupport(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, ReservationPortfolioCommitRequest request,
        IReadOnlyDictionary<string, string> decisionInputs, bool partitionedSaleStock = false, bool inventoryLease = false)
    {
        var paths = new[] { runRequestPath, sourcePath, ledgerPath }.Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).Count() == paths.Length,
            "Shared preparation aliases an original source role.");
        inputs = new(paths.Concat(decisionInputs.Keys).Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray());
        var hashes = inputs.ReadHashes();
        Require(decisionInputs.All(pair => hashes.TryGetValue(Path.GetFullPath(pair.Key), out var actual) && actual == pair.Value),
            "Shared preparation changed its original selected decision.");
        var source = CropTeacherSupportingSource.Read(paths[0], paths[1], paths[2], commitmentIds, stepId);
        var preparationOnly = new ReservationPortfolioCommitRequest
        {
            StateHash = source.Snapshot.StateHash, ExpectedLedgerRevision = source.Ledger.Revision,
            PortfolioId = request.PortfolioId, SourceDecisionId = request.PortfolioId, GoalId = request.GoalId,
            MaterialTransferPreparation = request.MaterialTransferPreparation
        };
        if (partitionedSaleStock || inventoryLease)
        {
            Require(request.MaterialTransferPreparation is not null && request.MaterialClaims is { Length: > 0 },
                "Partitioned sale stock requires its explicit original allocation request.");
            var canonical = CropSaleStockTransferPreparation.Create(source.Snapshot, source.Ledger,
                MaterialTransferIntentBinder.ToParameters(request.MaterialTransferPreparation!.Intent), out var reasons);
            Require(canonical is not null && EqualJson(request, canonical.Request) &&
                    CropSaleStockTransferLeaseContract.IsLeaseRequest(request) == inventoryLease,
                "Partitioned sale stock differs from its original conserved allocation: " + string.Join(",", reasons));
        }
        else Require(request.MaterialTransferPreparation is not null && EqualJson(request, preparationOnly),
                "Shared preparation must be the exact non-consuming original transfer request.");
        before = source.Snapshot; ledger = source.Ledger; this.stepId = stepId;
        this.request = CropTeacherSeedSupportSource.Copy(request);
        var preview = new ReservationPortfolioLedgerService().Commit(ledger, before, this.request, ledger.UpdatedAt);
        _ = VerifyPreparationReply(preview);
        SnapshotPath = paths[1];
        TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "total_days");
        ValidateInputsUnchanged();
    }

    public static CropTeacherSharedTransferSupport Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, ReservationPortfolioCommitRequest request,
        IReadOnlyDictionary<string, string> decisionInputs) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, request, decisionInputs);

    public static CropTeacherSharedTransferSupport OpenPartitionedSaleStock(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, ReservationPortfolioCommitRequest request,
        IReadOnlyDictionary<string, string> decisionInputs) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, request, decisionInputs, partitionedSaleStock: true);

    public static CropTeacherSharedTransferSupport OpenInventoryTransferLease(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, ReservationPortfolioCommitRequest request,
        IReadOnlyDictionary<string, string> decisionInputs) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, request, decisionInputs, inventoryLease: true);

    public string SnapshotPath { get; }
    public int TotalDay { get; }
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(ledger);
    public ReservationPortfolioCommitRequest PreparationRequest { get { ValidateInputsUnchanged(); return CropTeacherSeedSupportSource.Copy(request); } }
    public void ValidateInputsUnchanged() => inputs.Validate();

    public StrategyCommitmentLedger VerifyPreparationReply(ReservationPortfolioCommitResult reply)
    {
        ValidateInputsUnchanged();
        var prepared = CropTeacherSharedTransferPreparation.VerifyReply(before, ledger, request, reply);
        CropTeacherSupportingSource.RequireNewMarker(prepared, stepId);
        return prepared;
    }

    public StrategyCommitmentLedger VerifyPreparationReference(CropTeacherSharedTransferPreparationReference reference)
    {
        ValidateInputsUnchanged();
        var (actual, reply) = CropTeacherSharedPortfolioMutation.Read<ReservationPortfolioCommitRequest, ReservationPortfolioCommitResult>(
            reference.RequestPath, reference.ReplyPath, reference.JournalPath, reference.BackendUrl, "commit");
        Require(EqualJson(actual, request), "Shared preparation changed its admitted original request.");
        return VerifyPreparationReply(reply);
    }

    StrategyCommitmentLedger ICropTeacherSharedPortfolioPreparation.VerifyPreparationReference(
        string requestPath, string replyPath, string journalPath, string backendUrl) =>
        VerifyPreparationReference(new(requestPath, replyPath, journalPath, backendUrl));
}
