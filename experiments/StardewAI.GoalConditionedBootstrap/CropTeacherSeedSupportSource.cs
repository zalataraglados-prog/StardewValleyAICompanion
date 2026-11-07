using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class CropTeacherSeedSupportSource
{
    private readonly SnapshotEnvelope before;
    private readonly StrategyCommitmentLedger ledger;
    private readonly ReservationPortfolioCommitRequest marker;
    private readonly Dictionary<string, string> inputHashes;

    internal CropTeacherSeedSupportSource(string runRequestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string supportStepId, string? priorVerifiedSnapshotPath)
    {
        inputHashes = new[] { runRequestPath, snapshotPath, ledgerPath }
            .Concat(priorVerifiedSnapshotPath is null ? Array.Empty<string>() : new[] { priorVerifiedSnapshotPath })
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path,
                CurrentTeacherFrontierSupport.HashFile, StringComparer.OrdinalIgnoreCase);
        before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(snapshotPath, "crop seed support source");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(before, "support source");
        ledger = AcquisitionStrategyLedgerReader.Read(ledgerPath, before).Ledger;
        Require(!string.IsNullOrWhiteSpace(supportStepId) && !ledger.History.Any(row => row.CommitmentId == supportStepId) &&
                !ledger.CropPlantingCommitments.Any(row => row.CommitmentId == supportStepId || row.SourceDecisionId == supportStepId),
            "Crop seed support marker must be new and distinct from the Teacher owner.");
        CommitmentId = commitmentId;
        marker = new ReservationPortfolioCommitRequest
        {
            StateHash = before.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = supportStepId, SourceDecisionId = supportStepId, GoalId = "earn_money"
        };
        Supply = CropTeacherOwnedSeedSupply.Read(before, ReadSupplyLedger(priorVerifiedSnapshotPath), new[] { commitmentId });
        RunRequest = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlanRequest>(
            runRequestPath, "crop seed support run request");
        var owned = Supply.Commitments.Single();
        var layouts = RunRequest.LayoutIntents.Where(row => row.CommitmentId == commitmentId).ToArray();
        Require(RunRequest.SchemaVersion == "money_revenue_crop_native_production_run_plan_request.v2" &&
                !RunRequest.FormalProductTrainingAuthorized && layouts.Length == 1 && layouts[0].Tiles.Length == owned.TileCount,
            "Crop seed support lacks the original commitment layout/acquisition scope.");
        TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "total_days");
        Require(TotalDay >= 0 && TotalDay <= owned.PlantingTotalDay,
            "Crop seed support source is past its planting deadline.");
        SnapshotPath = Path.GetFullPath(snapshotPath);
        ValidateInputsUnchanged();
    }

    internal string CommitmentId { get; }
    internal string SnapshotPath { get; }
    internal int TotalDay { get; }
    internal SnapshotEnvelope Before => before;
    internal StrategyCommitmentLedger Ledger => ledger;
    internal CropTeacherOwnedSeedSupply Supply { get; }
    internal MoneyRevenueCropNativeProductionRunPlanRequest RunRequest { get; }
    internal IReadOnlyDictionary<string, string> InputHashes => new Dictionary<string, string>(inputHashes, StringComparer.OrdinalIgnoreCase);
    internal ReservationPortfolioCommitRequest MarkerRequest
    {
        get { ValidateInputsUnchanged(); return Copy(marker); }
    }

    internal StrategyCommitmentLedger VerifyCommit(ReservationPortfolioCommitRequest request, ReservationPortfolioCommitResult result)
    {
        ValidateInputsUnchanged();
        var reasons = new List<string>();
        Require(result.Ledger is not null && ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                request, ledger, result.Ledger, before, result, reasons),
            "Crop seed support marker does not replay exactly: " + string.Join(",", reasons));
        return Copy(result.Ledger!);
    }

    internal void ValidateInputsUnchanged()
    {
        foreach (var (path, hash) in inputHashes)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Crop seed support input changed: " + path);
    }

    internal static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(
        JsonSerializer.Serialize(value, JsonDefaults.Options), JsonDefaults.Options)!;
}
