using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

// Read-only preparation: neither dispatch nor an assertion about native effects.
public sealed partial class CropTeacherSiteTillCompilation : ICropTeacherSupportingPreparation
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly StrategyCommitmentLedger baseLedger;
    private readonly string stepId;

    private CropTeacherSiteTillCompilation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
        string siteRequestPath, string sitePlanPath, string originalModelPath, string modelPath, string queuePath)
    {
        var explicitPaths = new[] { runRequestPath, sourcePath, ledgerPath, observationPath, siteRequestPath,
            sitePlanPath, originalModelPath, modelPath, queuePath }.Select(Path.GetFullPath).ToArray();
        Check(explicitPaths.Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).Count() == explicitPaths.Length,
            "Till compilation roles must use separate input paths.");
        using var reads = InvocationReadSession.Start(explicitPaths);
        var explicitInputs = new CropTeacherRetainedInputs(explicitPaths);
        var run = Read<MoneyRevenueCropNativeProductionRunPlanRequest>(runRequestPath);
        var request = Read<MoneyRevenueCropSitePreparationPlanRequest>(siteRequestPath);
        Check(SamePath(run.CropProductionPlanPath, request.CropProductionPlanPath),
            "Till request changed its original production plan path.");
        inputs = RetainDependencies(explicitPaths, request.CropProductionPlanPath);
        var observation = CropTeacherSiteApproachObservation.Open(runRequestPath, sourcePath, ledgerPath,
            commitmentIds, commitmentId, stepId, observationPath);
        Check(observation.AlreadyAtStand, "Original site approach must finish before till preparation.");
        var layout = run.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
        Check(request.CommitmentId == commitmentId && request.LocationId == layout.LocationId &&
            SamePath(request.PreparationSnapshotPath, observationPath) &&
            BootstrapArtifactGuard.EqualJson(request.Tiles, layout.Tiles) &&
            BootstrapArtifactGuard.EqualJson(request.WateringRefillPreparationTiles,
                layout.WateringRefillPreparationTiles ?? Array.Empty<MoneyRevenueCropPlantingTile>()),
            "Till request changed its original production plan, commitment, layout or observation.");
        var retainedPlan = Read<MoneyRevenueCropSitePreparationPlan>(sitePlanPath);
        var plan = MoneyRevenueCropSitePreparationPlanBuilder.Build(siteRequestPath);
        Check(BootstrapArtifactGuard.EqualJson(retainedPlan, plan) && plan.PreparationTotalDay == observation.TotalDay &&
            plan.PreparationStateHash == observation.ObservationStateHash,
            "Till plan does not exactly rebuild from its original request/frame.");
        var step = MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(plan);
        Check(step is not null && step.PlanStatus == "direct_till_ready" && step.ActionOptionId == "executor.till_soil",
            "Original first frontier is not a direct till; clearance/no-op cannot be skipped.");
        CandidateId = step!.CandidateId;
        Target = new(step.TileX, step.TileY);
        var parameters = step.Parameters.Concat(new[] { new SmallModelActionParameter
            { Name = "precondition", Value = "candidate_id:" + CandidateId } }).ToArray();
        JsonObject Model(string hash) => CropMechanicalActionModelBuilder.Build(hash,
            "authoritative_crop_site_preparation_plan.v1", CandidateId, step.ActionOptionId, parameters,
            ExecutionTargetProfiles.TrainingSingleplayer,
            JsonSerializer.SerializeToNode(ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), JsonDefaults.Options)!);
        Check(JsonNode.DeepEquals(Model(observation.ObservationStateHash), Read<JsonNode>(originalModelPath)) &&
            JsonNode.DeepEquals(Model(observation.SourceStateHash), Read<JsonNode>(modelPath)),
            "Till whole original/full model differs from its shared recipe.");
        var source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
        baseLedger = CropTeacherSeedSupportSource.Copy(source.Ledger);
        this.stepId = stepId;
        var queue = Read<ActionQueueEnvelope>(queuePath);
        var rebuilt = new ActionQueueCompiler().Compile(Read<SmallModelActionEnvelope>(modelPath), source.Snapshot, source.Ledger);
        CropTeacherOriginalQueueCompilation.Verify(queue, rebuilt, "Selected site till");
        Check(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
            item.Status == "pending" && item.OptionId == "executor.till_soil" && item.BlockingReasons.Length == 0 &&
            item.MissingStateFactors.Length == 0 && item.NormalizedCommand.StateHash == observation.SourceStateHash,
            "Till requires exactly one original pending full-source compilation without missing factors.");
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
        observation.ValidateInputsUnchanged(); explicitInputs.Validate(); inputs.Validate();
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
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(baseLedger);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();

    public CropTeacherSiteTilling VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        inputs.Validate();
        Check(new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath,
            transition.AfterSnapshotPath }.All(Path.IsPathFullyQualified) &&
            transition.TransitionKind == "crop_actions" && transition.TotalDay == TotalDay &&
            transition.SelectedCandidateId == CandidateId && SamePath(transition.QueuePath, QueuePath) &&
            SamePath(transition.BeforeSnapshotPath, SourcePath),
            "Till outcome changed its pinned original compilation/source/day/candidate.");
        var outcome = new CropTeacherSiteTilling(this, transition, stepId);
        inputs.Validate();
        return outcome;
    }

    ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
}
