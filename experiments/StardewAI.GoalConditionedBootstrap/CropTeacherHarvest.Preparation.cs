using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest : ICropTeacherSupportingTransition
{
    public sealed class Preparation : ICropTeacherSupportingPreparation
    {
        private readonly CropTeacherRetainedInputs inputs;
        private readonly CropTeacherSupportingSource source;
        private readonly ActionQueueEnvelope queue;
        private readonly string stepId;

        private Preparation(string nativePlanPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string commitmentId, string stepId, int totalDay, int batchIndex, string modelActionPath, string queuePath)
        {
            var plan = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlan>(nativePlanPath, "original harvest run plan");
            var production = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropProductionObligationPlan>(plan.CropProductionPlanPath, "original harvest obligations");
            var productionRequest = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropProductionObligationPlanRequest>(production.RequestPath, "original harvest obligation source");
            var paths = new[] { nativePlanPath, plan.RequestPath, plan.CropProductionPlanPath, production.RequestPath,
                productionRequest.SnapshotPath, productionRequest.StrategyLedgerPath, sourcePath, ledgerPath, modelActionPath, queuePath }
                .Select(Path.GetFullPath).Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            inputs = new(paths);
            Require(EqualJson(plan, CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlan>(nativePlanPath, "retained harvest run plan")) &&
                    EqualJson(production, CurrentTeacherFrontierSupport.Read<MoneyRevenueCropProductionObligationPlan>(plan.CropProductionPlanPath, "retained harvest obligations")) &&
                    EqualJson(productionRequest, CurrentTeacherFrontierSupport.Read<MoneyRevenueCropProductionObligationPlanRequest>(production.RequestPath, "retained harvest obligation source")) &&
                    EqualJson(plan, MoneyRevenueCropNativeProductionRunPlanBuilder.Build(plan.RequestPath)) &&
                    plan.RequestSha256 == inputs.ReadHashes()[Path.GetFullPath(plan.RequestPath)] &&
                    plan.CropProductionPlanSha256 == inputs.ReadHashes()[Path.GetFullPath(plan.CropProductionPlanPath)],
                "Owned harvest requires the exact retained original run plan.");
            source = CropTeacherSupportingSource.Read(plan.RequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
            var originalLedger = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(production.StrategyLedgerPath, "original harvest owner ledger");
            var originalOwner = originalLedger.CropPlantingCommitments.SingleOrDefault(row => row.CommitmentId == commitmentId);
            this.stepId = stepId;
            SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath);
            TotalDay = totalDay; BatchIndex = batchIndex; CommitmentId = commitmentId;
            var commitment = production.Commitments.SingleOrDefault(row => row.CommitmentId == commitmentId);
            var active = source.Ledger.CropPlantingCommitments.SingleOrDefault(row => row.CommitmentId == commitmentId &&
                row.Status == StrategyCommitmentStatuses.Active && commitmentIds.Contains(commitmentId, StringComparer.Ordinal));
            var waves = commitment?.HarvestWaves.Where(row => row.HarvestTotalDay == totalDay).ToArray();
            var batch = plan.Days.SingleOrDefault(day => day.TotalDay == totalDay)?.ActionBatches.SingleOrDefault(row => row.BatchIndex == batchIndex);
            Require(commitment is not null && active is not null && waves is [var _] &&
                    source.Ledger.SaveId == originalLedger.SaveId && source.Ledger.PlayerId == originalLedger.PlayerId &&
                    active.SourceDecisionId == originalOwner?.SourceDecisionId &&
                    plan.CoveredCommitmentIds.Contains(commitmentId, StringComparer.Ordinal) &&
                    totalDay == MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days") &&
                    active.HarvestTotalDays.Contains(totalDay) && active.TileCount == commitment.TileCount &&
                    active.SeedQualifiedItemId == commitment.SeedQualifiedItemId && active.HarvestItemQualifiedId == commitment.HarvestItemQualifiedId &&
                    active.MinimumUnitsPerWave == waves[0].MinimumUnits && commitment.TileCount > 0 &&
                    waves[0].MinimumUnits > 0 && waves[0].MinimumUnits % commitment.TileCount == 0 &&
                    batch is { OptionId: "executor.harvest_crop", Tiles.Length: >= 1 and <= 8 } && batch.CommitmentId == commitmentId,
                "Owned harvest is outside its original active commitment, due batch or wave denominator.");
            var wave = waves![0];
            WaveIndex = wave.WaveIndex; WaveMinimumUnits = wave.MinimumUnits; OutputRetentionKey = wave.OutputRetentionKey;
            MinimumUnitsPerTile = wave.MinimumUnits / commitment!.TileCount; HarvestItemQualifiedId = commitment.HarvestItemQualifiedId;
            var model = CurrentTeacherFrontierSupport.Read<SmallModelActionEnvelope>(modelActionPath, "original harvest action");
            queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "original harvest compilation");
            Require(model.SchemaVersion == "small_model_action.v1" && model.SourceModel == "authoritative_crop_native_run_plan.v1" &&
                    model.StateHash == source.Snapshot.StateHash && model.Actions is { Length: >= 1 and <= 8 } &&
                    model.Actions.All(action => action is { OptionId: "executor.harvest_crop" }),
                "Owned harvest compilation differs from its original action scope.");
            CropTeacherOriginalQueueCompilation.Verify(queue, new ActionQueueCompiler().Compile(model, source.Snapshot, source.Ledger), "Owned harvest");
            Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items.Length == model.Actions.Length,
                "Owned harvest requires the original bounded pending batch.");
            var layout = source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
            var tiles = queue.Items.Select(item => MoneyRevenueCropNativeLifecycleProofBuilder.ReadMaintenanceTarget(
                item, source.Snapshot, layout.LocationId, commitmentId)).ToArray();
            Require(layout.LocationId == batch!.LocationId && tiles.Distinct().Count() == tiles.Length &&
                    tiles.SequenceEqual(batch.Tiles.Where(tile => tiles.Contains(tile))) && tiles.All(tile => layout.Tiles.Contains(tile)),
                "Owned harvest repeats a target or changes its original batch order/layout.");
            foreach (var tile in tiles)
                MoneyRevenueCropNativeLifecycleProofBuilder.VerifyOwnedHarvestSource(source.Snapshot, layout.LocationId, tile,
                    commitment.SeedQualifiedItemId, HarvestItemQualifiedId);
            LocationId = layout.LocationId; Tiles = Array.AsReadOnly(tiles);
            CandidateId = $"crop:{totalDay}:{commitmentId}:{batchIndex}";
            Require(model.ModelOutputId == "model-output:" + CandidateId && queue.Items.All(item =>
                    item.NormalizedCommand.Parameters.Where(row => row.Name == "precondition" && row.Value.StartsWith("candidate_id:", StringComparison.Ordinal))
                        .Select(row => row.Value).SequenceEqual(new[] { "candidate_id:" + CandidateId })),
                "Owned harvest lost its original candidate identity.");
            inputs.Validate();
        }

        public static Preparation Open(string nativePlanPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string commitmentId, string stepId, int totalDay, int batchIndex, string modelActionPath, string queuePath) =>
            new(nativePlanPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, totalDay, batchIndex, modelActionPath, queuePath);
        public string SourcePath { get; }
        public string QueuePath { get; }
        public int TotalDay { get; }
        public int BatchIndex { get; }
        public string CommitmentId { get; }
        public int WaveIndex { get; }
        public int WaveMinimumUnits { get; }
        public int MinimumUnitsPerTile { get; }
        public string OutputRetentionKey { get; }
        public string HarvestItemQualifiedId { get; }
        public string LocationId { get; }
        public string CandidateId { get; }
        public IReadOnlyList<MoneyRevenueCropPlantingTile> Tiles { get; }
        public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(source.Ledger);
        public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
        public void ValidateInputsUnchanged() => inputs.Validate();
        public CropTeacherHarvest VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition) =>
            new(this, source.Snapshot, queue, stepId, transition);
        ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
    }
}
