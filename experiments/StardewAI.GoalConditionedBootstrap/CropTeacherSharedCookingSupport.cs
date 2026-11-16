using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSharedCookingSupport : ICropTeacherSharedPortfolioPreparation
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly SnapshotEnvelope before;
    private readonly StrategyCommitmentLedger ledger;
    private readonly ReservationPortfolioCommitRequest request;
    private readonly string stepId;

    private CropTeacherSharedCookingSupport(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, ReservationPortfolioCommitRequest request,
        IReadOnlyDictionary<string, string> decisionInputs)
    {
        var paths = new[] { runRequestPath, sourcePath, ledgerPath }.Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).Count() == paths.Length,
            "Shared cooking preparation aliases an original source role.");
        inputs = new(paths.Concat(decisionInputs.Keys).Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray());
        var hashes = inputs.ReadHashes();
        Require(decisionInputs.All(pair => hashes.TryGetValue(Path.GetFullPath(pair.Key), out var actual) && actual == pair.Value),
            "Shared cooking preparation changed its original selected decision.");
        var source = CropTeacherSupportingSource.Read(paths[0], paths[1], paths[2], commitmentIds, stepId);
        var canonical = new ReservationPortfolioCommitRequest
        {
            StateHash = source.Snapshot.StateHash, ExpectedLedgerRevision = source.Ledger.Revision,
            PortfolioId = request.PortfolioId, SourceDecisionId = request.PortfolioId, GoalId = request.GoalId,
            InventoryConsumptionPreparation = request.InventoryConsumptionPreparation
        };
        Require(request.InventoryConsumptionPreparation is { ConsumerOptionId: PreparedInventoryConsumptionContract.Consumer } &&
                EqualJson(request, canonical), "Shared cooking requires its exact non-consuming original preparation request.");
        before = source.Snapshot; ledger = source.Ledger; this.stepId = stepId;
        this.request = CropTeacherSeedSupportSource.Copy(request);
        _ = VerifyPreparationReply(new ReservationPortfolioLedgerService().Commit(ledger, before, this.request, ledger.UpdatedAt));
        ValidateInputsUnchanged();
    }

    public static CropTeacherSharedCookingSupport Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, ReservationPortfolioCommitRequest request,
        IReadOnlyDictionary<string, string> decisionInputs) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, request, decisionInputs);

    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(ledger);
    public ReservationPortfolioCommitRequest PreparationRequest { get { ValidateInputsUnchanged(); return CropTeacherSeedSupportSource.Copy(request); } }
    public void ValidateInputsUnchanged() => inputs.Validate();

    public StrategyCommitmentLedger VerifyPreparationReply(ReservationPortfolioCommitResult reply)
    {
        ValidateInputsUnchanged();
        var prepared = CropTeacherSharedConsumption.VerifyPreparationReply(before, ledger, request, reply);
        CropTeacherSupportingSource.RequireNewMarker(prepared, stepId);
        return prepared;
    }

    public StrategyCommitmentLedger VerifyPreparationReference(string requestPath, string replyPath, string journalPath, string backendUrl)
    {
        ValidateInputsUnchanged();
        var (actual, reply) = CropTeacherSharedPortfolioMutation.Read<ReservationPortfolioCommitRequest, ReservationPortfolioCommitResult>(
            requestPath, replyPath, journalPath, backendUrl, "commit");
        Require(EqualJson(actual, request), "Shared cooking preparation changed its admitted original request.");
        return VerifyPreparationReply(reply);
    }
}
