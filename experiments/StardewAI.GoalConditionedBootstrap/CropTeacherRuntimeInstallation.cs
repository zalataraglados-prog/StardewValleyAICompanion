using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherRuntimeInstallation
{
    private readonly Dictionary<string, string> inputHashes;
    private readonly SnapshotEnvelope snapshot;
    private readonly StrategyCommitmentLedger baseLedger;
    private readonly CropPlantingCommitmentUpsertRequest cropRequest;
    private readonly ReservationPortfolioCommitRequest resourceRequest;

    private CropTeacherRuntimeInstallation(string decisionPath,
        MoneyRevenueCropProductionDecisionReceipt decision,
        MoneyRevenueCropResourceReservationReceipt resources)
    {
        DecisionPath = Path.GetFullPath(decisionPath);
        Require(!string.IsNullOrWhiteSpace(decision.BaseStrategyLedgerPath) &&
                decision.Mutations is { Length: 1 },
            "Runtime crop installation requires one original v2 selection and its complete base ledger.");
        CropTeacherLedgerSourceBinder.ValidateCurrent(decision.SnapshotPath, decision.BaseStrategyLedgerPath);
        snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(decision.SnapshotPath, "crop installation source");
        baseLedger = AcquisitionStrategyLedgerReader.Read(decision.BaseStrategyLedgerPath, snapshot).Ledger;
        cropRequest = decision.Mutations[0].Request;
        resourceRequest = resources.ReservationRequest;
        Require(cropRequest.ExpectedLedgerRevision == baseLedger.Revision &&
                resourceRequest.ExpectedLedgerRevision == checked(baseLedger.Revision + 1),
            "Runtime crop installation requests do not advance the original ledger in order.");
        var teacher = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropProductionTeacherRequest>(
            decision.TeacherRequestPath, "crop installation Teacher request");
        inputHashes = new[] { DecisionPath, decision.SnapshotPath, decision.BaseStrategyLedgerPath,
                decision.TeacherRequestPath }.Concat(teacher.FarmLayoutSnapshotPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(Path.GetFullPath,
                CurrentTeacherFrontierSupport.HashFile, StringComparer.OrdinalIgnoreCase);
    }

    public string DecisionPath { get; }
    public StrategyCommitmentLedger BaseLedger => Copy(baseLedger);
    public CropPlantingCommitmentUpsertRequest CropRequest => Copy(cropRequest);
    public ReservationPortfolioCommitRequest ResourceRequest => Copy(resourceRequest);
    public string SourceStateHash => snapshot.StateHash;

    public static CropTeacherRuntimeInstallation Open(string decisionPath)
    {
        var (resources, decision) = MoneyRevenueCropResourceReservationBuilder.BuildWithDecision(decisionPath);
        return new CropTeacherRuntimeInstallation(decisionPath, decision, resources);
    }

    public void ValidateInputsUnchanged()
    {
        foreach (var (path, hash) in inputHashes)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash,
                "Runtime crop installation input changed: " + path);
    }

    public void VerifyBaseLedger(StrategyCommitmentLedger current)
    {
        ValidateInputsUnchanged();
        Require(EqualJson(baseLedger, current), "Runtime crop installation base ledger changed before mutation.");
    }

    public StrategyCommitmentLedger VerifyCropReply(StrategyCommitmentMutationResult result)
    {
        Require(result.Accepted && result.Errors is { Length: 0 } && result.Ledger is not null,
            "Runtime crop installation upsert was rejected or incomplete.");
        return VerifyCropLedger(result.Ledger!);
    }

    // A lost reply is recovered from an exact current ledger, never a fabricated API reply or a second upsert.
    public StrategyCommitmentLedger VerifyCropLedger(StrategyCommitmentLedger current)
    {
        ValidateInputsUnchanged();
        var markers = (current.History ?? Array.Empty<StrategyCommitmentHistoryEntry>()).Where(row =>
            row.LedgerRevision == checked(baseLedger.Revision + 1) && row.Operation == "upsert" &&
            row.CommitmentId == cropRequest.CommitmentId && row.SourceDecisionId == cropRequest.SourceDecisionId).ToArray();
        Require(markers.Length == 1 && current.Revision == checked(baseLedger.Revision + 1),
            "Runtime crop installation lacks the exact one-revision upsert marker.");
        var replay = new CropCommitmentLedgerService().Upsert(baseLedger, snapshot, cropRequest, markers[0].RecordedAt);
        Require(replay.Accepted && replay.Ledger is not null && EqualJson(replay.Ledger, current),
            "Runtime crop installation upsert does not independently replay exactly.");
        return current;
    }

    public StrategyCommitmentLedger VerifyResourceReply(StrategyCommitmentLedger cropLedger,
        ReservationPortfolioCommitResult result)
    {
        VerifyCropLedger(cropLedger);
        var reasons = new List<string>();
        Require(result.Ledger is not null && ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                resourceRequest, cropLedger, result.Ledger, snapshot, result, reasons),
            "Runtime crop resource installation does not replay exactly: " + string.Join(",", reasons));
        return result.Ledger!;
    }

    public void VerifyPersistedLedger(StrategyCommitmentLedger installed, StrategyCommitmentLedger readback)
    {
        ValidateInputsUnchanged();
        Require(EqualJson(installed, readback), "Runtime crop resource ledger was not read back exactly after commit.");
    }

    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(
        JsonSerializer.Serialize(value, JsonDefaults.Options), JsonDefaults.Options)!;
}
