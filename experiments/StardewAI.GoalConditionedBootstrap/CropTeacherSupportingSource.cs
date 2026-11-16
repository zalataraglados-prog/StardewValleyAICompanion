using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record CropTeacherSupportingSource(SnapshotEnvelope Snapshot, StrategyCommitmentLedger Ledger,
    string CropLocation, MoneyRevenueCropNativeProductionRunPlanRequest RunRequest)
{
    internal static CropTeacherSupportingSource Read(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId) =>
        ReadCore(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, settledTransfer: false);

    internal static CropTeacherSupportingSource ReadBeforeSettledTransfer(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId) =>
        ReadCore(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, settledTransfer: true);

    private static CropTeacherSupportingSource ReadCore(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, bool settledTransfer)
    {
        var source = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(sourcePath, "crop supporting route source");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(source, "crop supporting route source");
        var ledger = AcquisitionStrategyLedgerReader.Read(ledgerPath, source).Ledger;
        Require(ledger.PreparedInventoryConsumption is null,
            "Crop supporting source cannot implicitly cross a prepared inventory consumption.");
        Require(settledTransfer ? ledger.PreparedMaterialTransfer is not null : ledger.PreparedMaterialTransfer is null,
            settledTransfer ? "Settled transfer source requires its original preparation." :
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
        RequireNewMarker(ledger, stepId);
        return new(source, ledger, locations.Single(), run);
    }

    internal static void RequireNewMarker(StrategyCommitmentLedger ledger, string stepId) =>
        Require(!string.IsNullOrWhiteSpace(stepId) && !ledger.History.Any(row => row.CommitmentId == stepId || row.SourceDecisionId == stepId) &&
                !ledger.CropPlantingCommitments.Any(row => row.CommitmentId == stepId || row.SourceDecisionId == stepId),
            "Crop supporting route marker identity is not new/distinct.");
}
