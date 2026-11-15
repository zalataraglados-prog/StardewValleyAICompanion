using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.WorldModel;

namespace StardewAI.GoalConditionedBootstrap;

// Holds the crop source while the global caller, not the crop executor, owns selected foreign work.
public sealed class CropTeacherSharedSourcePreparation : ICropTeacherSupportingPreparation
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly string runRequestPath;
    private readonly string ledgerPath;
    private readonly string[] commitmentIds;
    private readonly string stepId;
    private readonly MoneyRevenueCropNativeLifecycleTransitionRequest boundary;
    private readonly SnapshotEnvelope snapshot;
    private readonly CropTeacherSharedTransferPreparationReference? transferPreparation;
    private readonly string? settlementReferencePath;

    private CropTeacherSharedSourcePreparation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, string modelPlanPath,
        MoneyRevenueCropNativeLifecycleTransitionRequest boundary, IReadOnlyDictionary<string, string> selectionHashes,
        CropTeacherSharedTransferPreparationReference? transferPreparation = null, string? settlementReferencePath = null)
    {
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, modelPlanPath, boundary.QueuePath };
        Require(paths.All(Path.IsPathFullyQualified) && Path.IsPathFullyQualified(boundary.BeforeSnapshotPath) &&
                Path.IsPathFullyQualified(boundary.ExecutionReceiptPath) && Path.IsPathFullyQualified(boundary.AfterSnapshotPath),
            "Shared source preparation requires absolute original paths.");
        paths = paths.Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(comparer).Count() == paths.Length && comparer.Equals(paths[1], Path.GetFullPath(boundary.BeforeSnapshotPath)),
            "Shared source preparation aliases an input role or uses another source.");
        var retained = paths.Concat(selectionHashes.Keys);
        if (transferPreparation is not null)
        {
            var preparationPaths = new[] { transferPreparation.RequestPath, transferPreparation.ReplyPath, transferPreparation.JournalPath };
            Require(preparationPaths.All(Path.IsPathFullyQualified) && Path.IsPathFullyQualified(settlementReferencePath!) &&
                    preparationPaths.Select(Path.GetFullPath).Distinct(comparer).Count() == preparationPaths.Length &&
                    preparationPaths.All(path => !retained.Contains(Path.GetFullPath(path), comparer)),
                "Shared prepared transfer aliases an input role or lacks its retained settlement output.");
            retained = retained.Concat(preparationPaths);
        }
        Require((transferPreparation is null) == (settlementReferencePath is null), "Shared transfer preparation mode is incomplete.");
        inputs = new(retained.Distinct(comparer).ToArray());
        var hashes = inputs.ReadHashes();
        Require(selectionHashes.All(pair => hashes.TryGetValue(Path.GetFullPath(pair.Key), out var hash) && hash == pair.Value),
            "Shared source preparation changed its selected decision inputs.");
        var source = CropTeacherSupportingSource.Read(paths[0], paths[1], paths[2], commitmentIds, stepId);
        var ledger = transferPreparation is null ? source.Ledger :
            CropTeacherSharedTransferPreparation.Verify(source.Snapshot, source.Ledger, transferPreparation);
        if (transferPreparation is not null) CropTeacherSupportingSource.RequireNewMarker(ledger, stepId);
        var model = CurrentTeacherFrontierSupport.Read<SmallModelPlanEnvelope>(paths[3], "shared source selected plan");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(paths[4], "shared source selected queue");
        Require(model.StateHash == source.Snapshot.StateHash && queue.StateHash == source.Snapshot.StateHash &&
                model.SourceModel == "StardewAI.Core.Training.DailyPlanCompiler" && model.PlanType == "daily_candidate_plan" &&
                queue.Status == "pending" && queue.Items.Length > 0 && queue.Items.All(item => item.Status == "pending"),
            "Shared source preparation requires a fresh executable original selected plan.");
        CropTeacherOriginalQueueCompilation.Verify(queue,
            new ActionQueueCompiler().Compile(model, source.Snapshot, ledger), "Shared source selected queue");
        var budget = new TimeBudgetValidator().Validate(new WorldModelProjector().Project(source.Snapshot, queue.GoalId, queue.ExecutionMode), queue);
        Require(budget.FitsRequiredPlusOptional, "Shared source selected queue exceeds its original time budget: " + string.Join(",", budget.BlockReasons));
        TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
        Require(boundary.TotalDay == TotalDay && boundary.ExecutorVersion == PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor &&
                !string.IsNullOrWhiteSpace(boundary.RunId) && !string.IsNullOrWhiteSpace(boundary.SelectedCandidateId),
            "Shared source preparation differs from its original day/run/executor/candidate.");
        var outputs = new[] { Path.GetFullPath(boundary.ExecutionReceiptPath), Path.GetFullPath(boundary.AfterSnapshotPath) };
        if (settlementReferencePath is not null) outputs = outputs.Append(Path.GetFullPath(settlementReferencePath)).ToArray();
        Require(outputs.Distinct(comparer).Count() == outputs.Length && outputs.All(path => !hashes.ContainsKey(path)),
            "Shared source preparation outputs overlap retained inputs.");
        this.runRequestPath = paths[0]; SourcePath = paths[1]; this.ledgerPath = paths[2]; QueuePath = paths[4];
        this.commitmentIds = commitmentIds.ToArray(); this.stepId = stepId; this.boundary = boundary;
        CandidateId = boundary.SelectedCandidateId; BaseLedger = ledger; snapshot = source.Snapshot;
        this.transferPreparation = transferPreparation;
        this.settlementReferencePath = settlementReferencePath is null ? null : Path.GetFullPath(settlementReferencePath);
        ValidateInputsUnchanged();
    }

    public static CropTeacherSharedSourcePreparation Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, string modelPlanPath,
        MoneyRevenueCropNativeLifecycleTransitionRequest boundary, IReadOnlyDictionary<string, string> selectionHashes) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, modelPlanPath, boundary, selectionHashes);

    public static CropTeacherSharedSourcePreparation OpenPreparedTransfer(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, string modelPlanPath,
        MoneyRevenueCropNativeLifecycleTransitionRequest boundary, IReadOnlyDictionary<string, string> selectionHashes,
        CropTeacherSharedTransferPreparationReference preparation, string settlementReferencePath) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, modelPlanPath, boundary, selectionHashes,
            preparation ?? throw new ArgumentNullException(nameof(preparation)), settlementReferencePath);

    public string SourcePath { get; }
    public string QueuePath { get; }
    public int TotalDay { get; }
    public string CandidateId { get; }
    public StrategyCommitmentLedger BaseLedger { get; }
    public bool RequiresTransferSettlement => transferPreparation is not null;
    public CropTeacherSharedTransferPreparationReference? TransferPreparation => transferPreparation;
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();
    public ICropTeacherSupportingTransition VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        ValidateInputsUnchanged();
        Require(transition == boundary, "Shared source outcome differs from its pre-native selected boundary.");
        CropTeacherSharedSourceReturn result;
        if (transferPreparation is null)
            result = CropTeacherSharedSourceReturn.Open(runRequestPath, SourcePath, ledgerPath, commitmentIds, transition, stepId);
        else
        {
            var reference = CurrentTeacherFrontierSupport.Read<CropTeacherSharedTransferSettlementReference>(settlementReferencePath!,
                "shared prepared transfer settlement");
            Require(reference.Preparation == transferPreparation && reference.BackendUrl == transferPreparation.BackendUrl,
                "Shared prepared transfer changed its original preparation or backend.");
            result = CropTeacherSharedSourceReturn.OpenAfterPreparedTransferYield(runRequestPath, SourcePath,
                ledgerPath, commitmentIds, transition, stepId, reference, settlementReferencePath!);
        }
        ValidateInputsUnchanged();
        return result;
    }

    public ReservationPortfolioSupportingTransitionSettlementRequest ReadTransferSettlementRequest()
    {
        ValidateInputsUnchanged();
        Require(RequiresTransferSettlement, "Shared source yield has no prepared material transfer.");
        return CropTeacherSharedTransferSettlement.CreateRequest(BaseLedger,
            CropTeacherSharedSourceReturn.ReadNativeTransition(snapshot, boundary));
    }

    public StrategyCommitmentLedger VerifyTransferSettlementReply(ReservationPortfolioSupportingTransitionSettlementRequest request,
        ReservationPortfolioSupportingTransitionSettlementResult reply)
    {
        ValidateInputsUnchanged();
        Require(RequiresTransferSettlement, "Shared source yield has no prepared material transfer.");
        var native = CropTeacherSharedSourceReturn.ReadNativeTransition(snapshot, boundary);
        Require(EqualJson(request, CropTeacherSharedTransferSettlement.CreateRequest(BaseLedger, native)),
            "Shared transfer settlement changed its original native-bound request.");
        var result = CropTeacherSharedTransferSettlement.VerifyReply(BaseLedger, native, request, reply);
        ValidateInputsUnchanged();
        return result;
    }
}
