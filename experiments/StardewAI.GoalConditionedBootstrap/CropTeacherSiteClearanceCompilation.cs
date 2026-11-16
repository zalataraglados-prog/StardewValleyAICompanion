using System.Globalization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Reuses the original frontier and compiler; dispatch remains owned by the supporting protocol.
public sealed class CropTeacherSiteClearanceCompilation : ICropTeacherSupportingPreparation
{
    private readonly CropTeacherSitePreparationCompilation preparation;
    private readonly string stepId;

    private CropTeacherSiteClearanceCompilation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
        string siteRequestPath, string sitePlanPath, string originalModelPath, string modelPath, string queuePath)
    {
        preparation = new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId,
            observationPath, siteRequestPath, sitePlanPath, originalModelPath, modelPath, queuePath);
        this.stepId = stepId;
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

    public CropTeacherSiteClearance VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        ValidateInputsUnchanged();
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        if (new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath,
                transition.AfterSnapshotPath }.Any(path => !Path.IsPathFullyQualified(path)) ||
            transition.TransitionKind != "crop_actions" || transition.TotalDay != TotalDay ||
            transition.SelectedCandidateId != CandidateId ||
            !comparer.Equals(Path.GetFullPath(transition.QueuePath), QueuePath) ||
            !comparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), SourcePath))
            throw new InvalidDataException("Clearance outcome changed its original compilation/source/day/candidate.");
        return new(this, transition, stepId);
    }

    ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
}
