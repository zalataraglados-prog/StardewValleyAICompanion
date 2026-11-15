using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// An observed shared-source return, not authority to execute foreign work or credit crop production.
public sealed partial class CropTeacherSharedSourceReturn : ICropTeacherSupportingTransition
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly Dictionary<string, string> requestBlobs = new(CropTeacherSeedTransferNativeOutcome.PathComparer);
    private readonly CropTeacherSupportingRebind rebind;
    internal VerifiedNativeExecutionQueue NativeTransition { get; }
    internal StrategyCommitmentLedger SourceLedger { get; }
    internal PreparedMaterialTransfer? PreparedTransfer { get; }
    internal StrategyCommitmentLedger SettledLedger { get; }

    private CropTeacherSharedSourceReturn(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId,
        CropTeacherSharedTransferSettlementReference? transfer = null, bool includePreparation = false, string? retainedReferencePath = null,
        bool productTransfer = false, bool productSource = false, bool inventoryLease = false)
        : this(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId, transfer, includePreparation,
            retainedReferencePath, productTransfer, productSource, inventoryLease, null)
    { }

    private CropTeacherSharedSourceReturn(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId,
        CropTeacherSharedTransferSettlementReference? transfer, bool includePreparation, string? retainedReferencePath,
        bool productTransfer, bool productSource, bool inventoryLease, CropTeacherSharedConsumptionSettlementReference? consumption)
    {
        Require((transfer is null || consumption is null) && includePreparation == (transfer?.Preparation is not null || consumption is not null) &&
                (!includePreparation || (consumption is not null ? consumption.Preparation is not null && consumption.Preparation.BackendUrl == consumption.BackendUrl :
                    transfer!.Preparation!.BackendUrl == transfer.BackendUrl)),
            "Shared transfer return changed its explicit preparation mode or original backend.");
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, transition.QueuePath,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath };
        if (transfer is not null) paths = paths.Concat(new[] { transfer.RequestPath, transfer.ReplyPath, transfer.JournalPath }).ToArray();
        if (consumption is not null) paths = paths.Concat(new[] { consumption.RequestPath, consumption.ReplyPath, consumption.JournalPath }).ToArray();
        if (consumption is not null) paths = paths.Concat(new[] { consumption.Preparation!.RequestPath, consumption.Preparation.ReplyPath,
            consumption.Preparation.JournalPath }).ToArray();
        else if (includePreparation) paths = paths.Concat(new[] { transfer!.Preparation!.RequestPath, transfer.Preparation.ReplyPath,
            transfer.Preparation.JournalPath }).ToArray();
        if (retainedReferencePath is not null) paths = paths.Append(retainedReferencePath).ToArray();
        Require(paths.All(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)) &&
                !string.IsNullOrWhiteSpace(transition.BeforeSnapshotPath) && Path.IsPathFullyQualified(transition.BeforeSnapshotPath),
            "Shared source return requires absolute original artifact paths.");
        paths = paths.Select(Path.GetFullPath).ToArray();
        if (transfer is not null) transfer = transfer with { RequestPath = paths[6], ReplyPath = paths[7], JournalPath = paths[8] };
        if (consumption is not null) consumption = consumption with
        {
            RequestPath = paths[6], ReplyPath = paths[7], JournalPath = paths[8],
            Preparation = consumption.Preparation with { RequestPath = paths[9], ReplyPath = paths[10], JournalPath = paths[11] }
        };
        else if (includePreparation) transfer = transfer! with
            { Preparation = transfer!.Preparation! with { RequestPath = paths[9], ReplyPath = paths[10], JournalPath = paths[11] } };
        Require(paths.Distinct(comparer).Count() == paths.Length &&
                comparer.Equals(paths[1], Path.GetFullPath(transition.BeforeSnapshotPath)),
            "Shared source return aliases an original role or has another source.");
        using var reads = InvocationReadSession.Start(paths);
        var original = new CropTeacherRetainedInputs(paths);
        if (retainedReferencePath is not null)
            Require(consumption is not null ? EqualJson(consumption,
                    CurrentTeacherFrontierSupport.Read<CropTeacherSharedConsumptionSettlementReference>(retainedReferencePath, "shared cooking retained reference")) :
                transfer?.Preparation is not null && EqualJson(transfer,
                    CurrentTeacherFrontierSupport.Read<CropTeacherSharedTransferSettlementReference>(retainedReferencePath, "shared transfer retained reference")),
                "Shared transfer retained reference differs from its original protocol.");
        var source = transfer is null || includePreparation
            ? CropTeacherSupportingSource.Read(paths[0], paths[1], paths[2], commitmentIds, stepId)
            : CropTeacherSupportingSource.ReadBeforeSettledTransfer(paths[0], paths[1], paths[2], commitmentIds, stepId);
        Require(!productTransfer || productSource && transfer is null || includePreparation && retainedReferencePath is not null,
            "Product shared transfer requires its explicit original prepared-yield boundary.");
        var native = productTransfer ? ReadProductNativeTransition(source.Snapshot, transition) : ReadNativeTransition(source.Snapshot, transition);
        Require(native.Actions.Any(row => PreparedCookingConsumptionBinding.IsPresent(row.Item.NormalizedCommand.Parameters)) == (consumption is not null),
            "Prepared cooking requires its explicit original inventory-consumption source consumer.");

        // The explicit mode observes an already settled transfer; this adapter never performs that mutation.
        var preparedLedger = consumption is not null ? CropTeacherSharedConsumption.VerifyPreparation(source.Snapshot, source.Ledger, consumption.Preparation) :
            includePreparation ? CropTeacherSharedTransferPreparation.Verify(source.Snapshot, source.Ledger,
            transfer!.Preparation!) : source.Ledger;
        Require(CropSaleStockTransferLeaseContract.IsBound(preparedLedger) == inventoryLease &&
                (!inventoryLease || includePreparation && retainedReferencePath is not null),
            "Inventory transfer leases require their explicit original prepared-yield consumer.");
        var baseLedger = consumption is not null ? CropTeacherSharedConsumption.Verify(preparedLedger, native, consumption) :
            transfer is null ? source.Ledger : CropTeacherSharedTransferSettlement.Verify(preparedLedger, native, transfer);
        if (includePreparation) CropTeacherSupportingSource.RequireNewMarker(baseLedger, stepId);
        var projectedLedger = CropTeacherSeedSupportSource.Copy(baseLedger);
        projectedLedger.SourceStateHash = native.After.StateHash;
        _ = CropTeacherOwnedSeedSupply.Read(native.After, projectedLedger, commitmentIds);

        var retained = new Dictionary<string, string>(original.ReadHashes(), comparer);
        void Retain(string path, string expected)
        {
            path = Path.GetFullPath(path);
            Require(!retained.TryGetValue(path, out var prior) || prior == expected,
                "Shared source return changed a retained native artifact.");
            retained[path] = expected;
        }
        foreach (var (label, path) in new[] { ("queue", paths[3]), ("before", paths[1]), ("aggregate", paths[4]), ("after", paths[5]) })
            Retain(path, native.SourceSha256[label]);
        var rawReceipt = CurrentTeacherFrontierSupport.Read<JsonElement>(paths[4], "shared source native receipt");
        for (var index = 0; index < native.Actions.Length; index++)
        {
            var frame = native.Actions[index];
            var resultPath = CurrentTeacherFrontierSupport.RequiredString(rawReceipt.GetProperty("step_results")[index], "execution_path");
            var result = CurrentTeacherFrontierSupport.Read<JsonElement>(resultPath, "shared source native result");
            var journalPath = Path.GetFullPath(CurrentTeacherFrontierSupport.RequiredString(result, "execution_request_journal_path"));
            foreach (var (label, path) in new[] { ("before", frame.BeforeSnapshotPath), ("after", frame.AfterSnapshotPath),
                         ("result", resultPath), ("journal", journalPath) })
                Retain(path, frame.SourceSha256[label]);
            var blob = journalPath + ".gz";
            Require(!requestBlobs.TryGetValue(blob, out var previous) || previous == frame.SourceSha256["request_gzip"],
                "Shared source return reused an original request path with another hash.");
            requestBlobs[blob] = frame.SourceSha256["request_gzip"];
        }
        Require(requestBlobs.Keys.All(path => !retained.ContainsKey(path)),
            "Shared source return request blob aliases a JSON artifact.");
        inputs = new(retained.Keys.ToArray());
        Require(inputs.ReadHashes().All(pair => retained[pair.Key] == pair.Value),
            "Shared source return native artifacts changed during retention.");
        original.Validate();
        SourceStateHash = source.Snapshot.StateHash;
        AfterStateHash = native.After.StateHash;
        AfterSnapshotPath = paths[5];
        VerifiedNativeStepCount = native.Actions.Length;
        TransferSettlement = transfer;
        ConsumptionSettlement = consumption;
        ValidateInputsUnchanged();
        NativeTransition = native;
        SourceLedger = source.Ledger;
        PreparedTransfer = preparedLedger.PreparedMaterialTransfer;
        SettledLedger = baseLedger;
        rebind = new(native.After, baseLedger, stepId, ValidateInputsUnchanged, "Shared source return");
        ReleasesTransferLease = inventoryLease;
        if (inventoryLease) rebind.ReleaseTransferLease(preparedLedger.PreparedMaterialTransfer!.MaterialRelocations
            .Select(row => row.MaterialReservationId).ToArray());
    }

    public static CropTeacherSharedSourceReturn Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId);

    public static CropTeacherSharedSourceReturn OpenProductSource(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId, productTransfer: true, productSource: true);

    internal static VerifiedNativeExecutionQueue ReadNativeTransition(SnapshotEnvelope source,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition) => ReadNativeTransitionCore(source, transition, productTransfer: false);

    internal static VerifiedNativeExecutionQueue ReadProductNativeTransition(SnapshotEnvelope source,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition) => ReadNativeTransitionCore(source, transition, productTransfer: true);

    private static VerifiedNativeExecutionQueue ReadNativeTransitionCore(SnapshotEnvelope source,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, bool productTransfer)
    {
        Require(transition.ExecutorVersion == (productTransfer ? PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor) &&
                transition.TotalDay == MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source, "time", "total_days"),
            "Shared source return differs from the original executor/day boundary.");
        var native = productTransfer
            ? NativeExecutionActionSourceReader.ReadSelectedProductActionQueue(transition.QueuePath, transition.BeforeSnapshotPath,
                transition.ExecutionReceiptPath, transition.AfterSnapshotPath, transition.RunId, transition.SelectedCandidateId)
            : NativeExecutionActionSourceReader.ReadSelectedNativeActionQueue(transition.QueuePath,
                transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath,
                transition.RunId, transition.SelectedCandidateId);
        Require(native.Actions.Length > 0 && native.Before.StateHash == source.StateHash,
            "Shared source return has no original native source chain.");
        for (var index = 0; index < native.Actions.Length; index++)
            CropTeacherNativeStepBoundary.Verify(native.Actions[index].Before, native.Actions[index].After, source,
                native.Receipt.StepResults[index], transition.TotalDay, "Shared source return");
        return native;
    }

    public static CropTeacherSharedSourceReturn OpenAfterSettledMaterialTransfer(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId,
        CropTeacherSharedTransferSettlementReference transfer) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId,
            transfer ?? throw new ArgumentNullException(nameof(transfer)));

    public static CropTeacherSharedSourceReturn OpenAfterPreparedAndSettledMaterialTransfer(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId,
        CropTeacherSharedTransferSettlementReference transfer) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId,
            transfer ?? throw new ArgumentNullException(nameof(transfer)), includePreparation: true);

    public static CropTeacherSharedSourceReturn OpenAfterPreparedTransferYield(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId,
        CropTeacherSharedTransferSettlementReference transfer, string retainedReferencePath) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId, transfer, true, retainedReferencePath);

    public static CropTeacherSharedSourceReturn OpenAfterPreparedProductTransferYield(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId,
        CropTeacherSharedTransferSettlementReference transfer, string retainedReferencePath) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId, transfer, true, retainedReferencePath, productTransfer: true);

    public string SourceStateHash { get; }
    public string AfterStateHash { get; }
    public string AfterSnapshotPath { get; }
    public int VerifiedNativeStepCount { get; }
    public CropTeacherSharedTransferSettlementReference? TransferSettlement { get; }
    public bool NativeCropLifecycleVerified => false;
    public bool CropProductionCreditAuthorized => false;
    public bool TeacherPreferenceLabelEligible => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes().Concat(requestBlobs)
        .Concat(marker?.Hashes.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, string>>())
        .ToDictionary(pair => pair.Key, pair => pair.Value, CropTeacherSeedTransferNativeOutcome.PathComparer);

    public void ValidateInputsUnchanged()
    {
        inputs.Validate();
        marker?.Validate();
        foreach (var (path, hash) in requestBlobs)
            Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && CurrentTeacherFrontierSupport.HashFile(path) == hash,
                "Shared source return original compressed request changed.");
    }
}
