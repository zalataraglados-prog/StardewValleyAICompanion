using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

// Shared original-input binding only: no dispatch, owner admission or resource claims.
internal sealed class CropTeacherSitePreparationCompilation
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly StrategyCommitmentLedger baseLedger;

    internal CropTeacherSitePreparationCompilation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
        string siteRequestPath, string sitePlanPath, string originalModelPath, string modelPath, string queuePath)
    {
        var explicitPaths = new[] { runRequestPath, sourcePath, ledgerPath, observationPath, siteRequestPath,
            sitePlanPath, originalModelPath, modelPath, queuePath }.Select(Path.GetFullPath).ToArray();
        Check(explicitPaths.Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).Count() == explicitPaths.Length,
            "Site compilation roles must use separate input paths.");
        using var reads = InvocationReadSession.Start(explicitPaths);
        var explicitInputs = new CropTeacherRetainedInputs(explicitPaths);
        var run = Read<MoneyRevenueCropNativeProductionRunPlanRequest>(runRequestPath);
        var request = Read<MoneyRevenueCropSitePreparationPlanRequest>(siteRequestPath);
        Check(SamePath(run.CropProductionPlanPath, request.CropProductionPlanPath),
            "Site request changed its original production plan path.");
        inputs = RetainDependencies(explicitPaths, request.CropProductionPlanPath);
        Observation = CropTeacherSiteApproachObservation.Open(runRequestPath, sourcePath, ledgerPath,
            commitmentIds, commitmentId, stepId, observationPath);
        var layout = run.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
        Check(request.CommitmentId == commitmentId && request.LocationId == layout.LocationId &&
            SamePath(request.PreparationSnapshotPath, observationPath) &&
            BootstrapArtifactGuard.EqualJson(request.Tiles, layout.Tiles) &&
            BootstrapArtifactGuard.EqualJson(request.WateringRefillPreparationTiles,
                layout.WateringRefillPreparationTiles ?? Array.Empty<MoneyRevenueCropPlantingTile>()),
            "Site request changed its original production plan, commitment, layout or observation.");
        var retainedPlan = Read<MoneyRevenueCropSitePreparationPlan>(sitePlanPath);
        var plan = MoneyRevenueCropSitePreparationPlanBuilder.Build(siteRequestPath);
        Check(BootstrapArtifactGuard.EqualJson(retainedPlan, plan) && plan.PreparationTotalDay == Observation.TotalDay &&
            plan.PreparationStateHash == Observation.ObservationStateHash,
            "Site plan does not exactly rebuild from its original request/frame.");
        Step = MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(plan)
            ?? throw new InvalidDataException("Original first frontier is a site preparation no-op.");
        var parameters = Step.Parameters.Concat(new[] { new SmallModelActionParameter
            { Name = "precondition", Value = "candidate_id:" + Step.CandidateId } }).ToArray();
        JsonObject Model(string hash) => CropMechanicalActionModelBuilder.Build(hash,
            "authoritative_crop_site_preparation_plan.v1", Step.CandidateId, Step.ActionOptionId, parameters,
            ExecutionTargetProfiles.TrainingSingleplayer,
            JsonSerializer.SerializeToNode(ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), JsonDefaults.Options)!);
        Check(JsonNode.DeepEquals(Model(Observation.ObservationStateHash), Read<JsonNode>(originalModelPath)) &&
            JsonNode.DeepEquals(Model(Observation.SourceStateHash), Read<JsonNode>(modelPath)),
            "Site whole original/full model differs from its shared recipe.");
        var source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
        baseLedger = CropTeacherSeedSupportSource.Copy(source.Ledger);
        var queue = Read<ActionQueueEnvelope>(queuePath);
        var rebuilt = new ActionQueueCompiler().Compile(Read<SmallModelActionEnvelope>(modelPath), source.Snapshot, source.Ledger);
        CropTeacherOriginalQueueCompilation.Verify(queue, rebuilt, "Selected site preparation");
        Check(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
            item.Status == "pending" && item.OptionId == Step.ActionOptionId && item.BlockingReasons.Length == 0 &&
            item.MissingStateFactors.Length == 0 && item.NormalizedCommand.StateHash == Observation.SourceStateHash,
            "Site preparation requires exactly one original pending full-source compilation without missing factors.");
        SourcePath = Path.GetFullPath(sourcePath); ObservationPath = Path.GetFullPath(observationPath);
        QueuePath = Path.GetFullPath(queuePath);
        Observation.ValidateInputsUnchanged(); explicitInputs.Validate(); inputs.Validate();
    }

    internal CropTeacherSiteApproachObservation Observation { get; }
    internal MoneyRevenueCropSitePreparationStep Step { get; }
    internal string SourcePath { get; }
    internal string ObservationPath { get; }
    internal string QueuePath { get; }
    internal string SourceStateHash => Observation.SourceStateHash;
    internal string ObservationStateHash => Observation.ObservationStateHash;
    internal int TotalDay => Observation.TotalDay;
    internal StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(baseLedger);
    internal IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    internal void ValidateInputsUnchanged() => inputs.Validate();

    private static CropTeacherRetainedInputs RetainDependencies(string[] explicitPaths, string productionPath)
    {
        // Pin each discovery document before following it, then validate discovery again.
        var planPath = Path.GetFullPath(productionPath);
        var planPin = new CropTeacherRetainedInputs(new[] { planPath });
        var plan = Read<MoneyRevenueCropProductionObligationPlan>(planPath);
        var requestPath = Path.GetFullPath(plan.RequestPath);
        var requestPin = new CropTeacherRetainedInputs(new[] { requestPath });
        var request = Read<MoneyRevenueCropProductionObligationPlanRequest>(requestPath);
        var paths = explicitPaths.Concat(new[] { planPath, requestPath, plan.SnapshotPath, plan.StrategyLedgerPath,
            request.SnapshotPath, request.StrategyLedgerPath }).Select(Path.GetFullPath)
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        var retained = new CropTeacherRetainedInputs(paths);
        planPin.Validate(); requestPin.Validate();
        return retained;
    }

    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "selected original site input");
    private static bool SamePath(string left, string right) => CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(
        Path.GetFullPath(left), Path.GetFullPath(right));
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }
}
