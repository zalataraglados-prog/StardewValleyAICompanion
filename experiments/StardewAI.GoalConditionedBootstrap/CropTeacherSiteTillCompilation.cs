using System.Globalization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Read-only preparation: neither dispatch nor an assertion about native effects.
public sealed partial class CropTeacherSiteTillCompilation : ICropTeacherSupportingPreparation
{
    private readonly CropTeacherSitePreparationCompilation preparation;
    private readonly string stepId;

    private CropTeacherSiteTillCompilation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
        string siteRequestPath, string sitePlanPath, string originalModelPath, string modelPath, string queuePath)
    {
        preparation = new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId,
            observationPath, siteRequestPath, sitePlanPath, originalModelPath, modelPath, queuePath);
        var observation = preparation.Observation;
        Check(observation.AlreadyAtStand, "Original site approach must finish before till preparation.");
        var step = preparation.Step;
        Check(step.PlanStatus == "direct_till_ready" && step.ActionOptionId == "executor.till_soil",
            "Original first frontier is not a direct till; clearance/no-op cannot be skipped.");
        CandidateId = step.CandidateId;
        Target = new(step.TileX, step.TileY);
        this.stepId = stepId;
        var observed = Read<StardewAI.Contracts.State.SnapshotEnvelope>(observationPath);
        var context = NativeExecutionSourceSupport.Value(observed, "current_location", "site_preparation_context");
        var row = context.GetProperty("tiles").EnumerateArray().Single(tile =>
            tile.GetProperty("tile_x").GetInt32() == Target.TileX && tile.GetProperty("tile_y").GetInt32() == Target.TileY);
        Check(int.TryParse(step.Parameters.Single(p => p.Name == "tool_slot_index").Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out var slot) && slot >= 0 &&
            row.GetProperty("hoe_tool_slot_index").GetInt32() == slot,
            "Till projected Hoe slot differs from the selected original tool.");
        ToolSlotIndex = slot;
        ToolQualifiedItemId = row.GetProperty("hoe_tool_qualified_item_id").GetString() ?? string.Empty;
        ProjectedEnergyCost = row.GetProperty("hoe_energy_cost").GetDouble();
        Check(context.GetProperty("hoe_tool_slot_index").GetInt32() == slot &&
            context.GetProperty("hoe_tool_qualified_item_id").GetString() == ToolQualifiedItemId &&
            context.GetProperty("hoe_energy_cost_per_single_tile_action").GetDouble() == ProjectedEnergyCost &&
            !string.IsNullOrWhiteSpace(ToolQualifiedItemId) && double.IsFinite(ProjectedEnergyCost) && ProjectedEnergyCost >= 0,
            "Till projected Hoe identity/energy is unavailable.");
        SourcePath = Path.GetFullPath(sourcePath); ObservationPath = Path.GetFullPath(observationPath);
        QueuePath = Path.GetFullPath(queuePath); SourceStateHash = observation.SourceStateHash;
        ObservationStateHash = observation.ObservationStateHash; TotalDay = observation.TotalDay;
        preparation.ValidateInputsUnchanged();
    }

    public static CropTeacherSiteTillCompilation Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
        string siteRequestPath, string sitePlanPath, string originalModelPath, string modelPath, string queuePath) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, observationPath,
            siteRequestPath, sitePlanPath, originalModelPath, modelPath, queuePath);

    public string SourcePath { get; }
    public string ObservationPath { get; }
    public string QueuePath { get; }
    public string SourceStateHash { get; }
    public string ObservationStateHash { get; }
    public string CandidateId { get; }
    public int TotalDay { get; }
    public MoneyRevenueCropPlantingTile Target { get; }
    public int ToolSlotIndex { get; }
    public string ToolQualifiedItemId { get; }
    public double ProjectedEnergyCost { get; }
    public bool NativeOutcomeVerified => false;
    public bool ResourceBoundaryVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => preparation.BaseLedger;
    public IReadOnlyDictionary<string, string> InputHashes => preparation.InputHashes;
    public void ValidateInputsUnchanged() => preparation.ValidateInputsUnchanged();

    public CropTeacherSiteTilling VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        preparation.ValidateInputsUnchanged();
        Check(new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath,
            transition.AfterSnapshotPath }.All(Path.IsPathFullyQualified) &&
            transition.TransitionKind == "crop_actions" && transition.TotalDay == TotalDay &&
            transition.SelectedCandidateId == CandidateId && SamePath(transition.QueuePath, QueuePath) &&
            SamePath(transition.BeforeSnapshotPath, SourcePath),
            "Till outcome changed its pinned original compilation/source/day/candidate.");
        var outcome = new CropTeacherSiteTilling(this, transition, stepId);
        preparation.ValidateInputsUnchanged();
        return outcome;
    }

    ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
}
