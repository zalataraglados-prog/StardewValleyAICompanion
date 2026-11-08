using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// Consumes original route evidence; it neither plans nor executes another action.
public sealed partial class CropTeacherSupportingRoute
{
    private readonly SnapshotEnvelope after;
    private readonly StrategyCommitmentLedger ledger;
    private readonly ReservationPortfolioCommitRequest request;
    private readonly Dictionary<string, string> hashes;
    private readonly Dictionary<string, string> logicalHashes;

    private CropTeacherSupportingRoute(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId)
    {
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, transition.QueuePath, transition.BeforeSnapshotPath,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Select(Path.GetFullPath)
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        foreach (var path in paths) RequireRegularInput(path);
        // Retain only these explicit phase inputs. A fresh validation below also checks CAS blob content.
        using var reads = InvocationReadSession.Start(paths);
        hashes = paths.ToDictionary(path => path, CurrentTeacherFrontierSupport.HashFile, CropTeacherSeedTransferNativeOutcome.PathComparer);
        logicalHashes = paths.ToDictionary(path => path, LogicalHash, CropTeacherSeedTransferNativeOutcome.PathComparer);
        var source = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(sourcePath, "crop supporting route source");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(source, "crop supporting route source");
        ledger = AcquisitionStrategyLedgerReader.Read(ledgerPath, source).Ledger;
        Require(ledger.PreparedMaterialTransfer is null,
            "Crop supporting route cannot cross an unresolved prepared material transfer.");
        var supply = CropTeacherOwnedSeedSupply.Read(source, ledger, commitmentIds);
        var run = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlanRequest>(runRequestPath,
            "crop supporting route original run request");
        Require(run.SchemaVersion == "money_revenue_crop_native_production_run_plan_request.v2" && !run.FormalProductTrainingAuthorized,
            "Crop supporting route requires its original non-training run request.");
        var locations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var crop in supply.Commitments)
        {
            var layouts = run.LayoutIntents.Where(row => row.CommitmentId == crop.CommitmentId).ToArray();
            Require(layouts.Length == 1 && layouts[0].Tiles.Length == crop.TileCount &&
                    layouts[0].Tiles.Distinct().Count() == crop.TileCount && !string.IsNullOrWhiteSpace(layouts[0].LocationId),
                "Crop supporting route has no exact original commitment layout denominator.");
            locations.Add(layouts[0].LocationId);
        }
        Require(locations.Count == 1, "Crop supporting route cannot merge different crop locations.");
        Require(!string.IsNullOrWhiteSpace(stepId) && !ledger.History.Any(row => row.CommitmentId == stepId || row.SourceDecisionId == stepId) &&
                !ledger.CropPlantingCommitments.Any(row => row.CommitmentId == stepId || row.SourceDecisionId == stepId),
            "Crop supporting route marker identity is not new/distinct.");
        after = MoneyRevenueCropNativeLifecycleProofBuilder.VerifySupportingRoute(source, transition, locations.Single());
        request = new ReservationPortfolioCommitRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = stepId, SourceDecisionId = stepId, GoalId = "earn_money"
        };
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        ValidateInputsUnchanged();
    }

    public static CropTeacherSupportingRoute Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId);

    public string AfterSnapshotPath { get; }
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(ledger);
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public IReadOnlyDictionary<string, string> InputHashes => new Dictionary<string, string>(hashes, CropTeacherSeedTransferNativeOutcome.PathComparer);
    public ReservationPortfolioCommitRequest RebindRequest
    {
        get { ValidateInputsUnchanged(); return CropTeacherSeedSupportSource.Copy(request); }
    }

    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply)
    {
        ValidateInputsUnchanged();
        var reasons = new List<string>();
        Require(reply.Ledger is not null && ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                request, ledger, reply.Ledger, after, reply, reasons),
            "Crop supporting route rebind does not replay the original marker: " + string.Join(",", reasons));
        return CropTeacherSeedSupportSource.Copy(reply.Ledger!);
    }
}
