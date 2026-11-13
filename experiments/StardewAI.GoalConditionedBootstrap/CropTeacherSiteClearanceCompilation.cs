using System.Globalization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Read-only first-frontier preparation, deliberately not ICropTeacherSupportingPreparation.
public sealed class CropTeacherSiteClearanceCompilation
{
    private readonly CropTeacherSitePreparationCompilation preparation;

    private CropTeacherSiteClearanceCompilation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
        string siteRequestPath, string sitePlanPath, string originalModelPath, string modelPath, string queuePath)
    {
        preparation = new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId,
            observationPath, siteRequestPath, sitePlanPath, originalModelPath, modelPath, queuePath);
        var step = preparation.Step;
        if (step.ActionOptionId != "executor.clear_obstacle" || step.PlanStatus is not
            ("clear_then_fresh_snapshot_rebind" or "clear_access_then_fresh_snapshot_rebind"))
            throw new InvalidDataException("Original first frontier is not an obstacle clearance; till/no-op/foreign actions cannot be substituted.");
        PreparationTarget = new(step.TileX, step.TileY);
        Target = new(Coordinate("target_tile_x"), Coordinate("target_tile_y"));
        Stand = new(Coordinate("stand_tile_x"), Coordinate("stand_tile_y"));
        ClearSourceId = Parameter("clear_source_id");
        preparation.ValidateInputsUnchanged();

        string Parameter(string name)
        {
            var values = step.Parameters.Where(row => row.Name == name).ToArray();
            if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0].Value))
                throw new InvalidDataException("Original clearance parameter is missing or ambiguous: " + name);
            return values[0].Value;
        }
        int Coordinate(string name) => int.TryParse(Parameter(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value : throw new InvalidDataException("Original clearance coordinate is invalid: " + name);
    }

    public static CropTeacherSiteClearanceCompilation Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
        string siteRequestPath, string sitePlanPath, string originalModelPath, string modelPath, string queuePath) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, observationPath,
            siteRequestPath, sitePlanPath, originalModelPath, modelPath, queuePath);

    public string SourcePath => preparation.SourcePath;
    public string ObservationPath => preparation.ObservationPath;
    public string QueuePath => preparation.QueuePath;
    public string SourceStateHash => preparation.SourceStateHash;
    public string ObservationStateHash => preparation.ObservationStateHash;
    public string CandidateId => preparation.Step.CandidateId;
    public int TotalDay => preparation.TotalDay;
    public MoneyRevenueCropPlantingTile PreparationTarget { get; }
    public MoneyRevenueCropPlantingTile Target { get; }
    public MoneyRevenueCropPlantingTile Stand { get; }
    public string ClearSourceId { get; }
    public bool NativeOutcomeVerified => false;
    public bool ResourceBoundaryVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => preparation.BaseLedger;
    public IReadOnlyDictionary<string, string> InputHashes => preparation.InputHashes;
    public void ValidateInputsUnchanged() => preparation.ValidateInputsUnchanged();
}
