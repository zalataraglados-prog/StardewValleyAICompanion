using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherWateringMaintenance
{
    public sealed class Preparation : ICropTeacherSupportingPreparation
    {
        private readonly string runRequestPath;
        private readonly string ledgerPath;
        private readonly string[] commitmentIds;
        private readonly string commitmentId;
        private readonly string stepId;
        private readonly CropTeacherRetainedInputs inputs;
        private readonly CropTeacherSupportingSource source;
        private readonly CropTeacherRefillApproachCompilation? refillApproach;
        private readonly bool selectedRefillResources;

        private Preparation(string runRequestPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string commitmentId, string stepId, string modelActionPath, string queuePath, bool refill = false,
            bool selectedRefillResources = false)
        {
            this.selectedRefillResources = selectedRefillResources;
            var paths = new[] { runRequestPath, sourcePath, ledgerPath, modelActionPath, queuePath }.Select(Path.GetFullPath)
                .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            inputs = new(paths);
            source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
            this.runRequestPath = Path.GetFullPath(runRequestPath); this.ledgerPath = Path.GetFullPath(ledgerPath);
            this.commitmentIds = commitmentIds.ToArray(); this.commitmentId = commitmentId; this.stepId = stepId;
            SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath);
            TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
            var crop = source.Ledger.CropPlantingCommitments.SingleOrDefault(row => row.CommitmentId == commitmentId &&
                row.Status == StrategyCommitmentStatuses.Active && commitmentIds.Contains(commitmentId, StringComparer.Ordinal));
            Require(crop is not null && crop.RequiredWateringTotalDays.Contains(TotalDay),
                "Owned crop watering is outside its original active obligation/day.");
            if (refill)
            {
                refillApproach = CropTeacherRefillApproachCompilation.Open(runRequestPath, sourcePath, ledgerPath,
                    commitmentIds, commitmentId, stepId, modelActionPath, queuePath);
                if (selectedRefillResources)
                    Require(refillApproach.SelectedResourceClearanceSupported,
                        "Owned refill repair has no supported selected-resource contract before dispatch.");
                else
                    Require(refillApproach.ClearanceQueueItemIds.Count == 0,
                        "Owned refill repair requires its separate exact resource settlement before dispatch.");
                CandidateId = refillApproach.CandidateId;
                Tiles = Array.AsReadOnly(Array.Empty<MoneyRevenueCropPlantingTile>());
                inputs.Validate();
                return;
            }
            var model = CurrentTeacherFrontierSupport.Read<SmallModelActionEnvelope>(modelActionPath, "original watering action");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "original watering compilation");
            Require(model.SchemaVersion == "small_model_action.v1" && model.SourceModel == "authoritative_crop_native_run_plan.v1" &&
                    model.StateHash == source.Snapshot.StateHash && model.Actions is { Length: >= 1 and <= 8 } &&
                    model.Actions.All(action => action is { OptionId: "executor.water_crop" }),
                "Owned crop watering compilation differs from its original action scope.");
            CropTeacherOriginalQueueCompilation.Verify(queue, new ActionQueueCompiler().Compile(model, source.Snapshot, source.Ledger), "Owned crop watering");
            Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items.Length == model.Actions.Length,
                "Owned crop watering requires the original bounded pending batch.");
            var layout = source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
            var tiles = queue.Items.Select(item => MoneyRevenueCropNativeLifecycleProofBuilder.ReadMaintenanceTarget(
                item, source.Snapshot, layout.LocationId, commitmentId)).ToArray();
            Require(tiles.Distinct().Count() == tiles.Length && tiles.All(tile => layout.Tiles.Contains(tile)),
                "Owned crop watering repeats a target or leaves its original layout.");
            Tiles = Array.AsReadOnly(tiles);
            var candidates = queue.Items.SelectMany(item => item.NormalizedCommand.Parameters.Where(row => row.Name == "precondition" &&
                row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Select(row => row.Value["candidate_id:".Length..]))
                .Distinct(StringComparer.Ordinal).ToArray();
            Require(candidates is [var candidate] && !string.IsNullOrWhiteSpace(candidate) && model.ModelOutputId == "model-output:" + candidate,
                "Owned crop watering lost its original candidate identity.");
            CandidateId = candidates.Single();
            inputs.Validate();
        }

        public static Preparation Open(string runRequestPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string commitmentId, string stepId, string modelActionPath, string queuePath) =>
            new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, modelActionPath, queuePath);
        public static Preparation OpenRefillApproach(string runRequestPath, string sourcePath, string ledgerPath,
            IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string modelPlanPath, string queuePath) =>
            new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, modelPlanPath, queuePath, refill: true);
        public static Preparation OpenRefillApproachWithSelectedResourceEffects(string runRequestPath, string sourcePath, string ledgerPath,
            IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string modelPlanPath, string queuePath) =>
            new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, modelPlanPath, queuePath,
                refill: true, selectedRefillResources: true);
        public bool IsRefillApproach => refillApproach is not null;
        public bool UsesSelectedRefillResourceEffects => selectedRefillResources;
        public string SourcePath { get; }
        public string QueuePath { get; }
        public int TotalDay { get; }
        public string CandidateId { get; }
        public IReadOnlyList<MoneyRevenueCropPlantingTile> Tiles { get; }
        public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(source.Ledger);
        public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
        public void ValidateInputsUnchanged() { inputs.Validate(); refillApproach?.ValidateInputsUnchanged(); }

        public CropTeacherWateringMaintenance VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        {
            ValidateInputsUnchanged();
            Require(transition.TransitionKind == "crop_actions" && transition.TotalDay == TotalDay && transition.SelectedCandidateId == CandidateId &&
                    CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.QueuePath), QueuePath) &&
                    CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), SourcePath),
                "Owned crop watering differs from its pinned original compilation/source.");
            if (refillApproach is not null)
                return new CropTeacherWateringMaintenance(refillApproach, transition, stepId, selectedRefillResources);
            var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath, "original watering outcome kind");
            var result = !receipt.Success && receipt.StepResults is [{ Status: "blocked" }]
                ? CropTeacherWateringMaintenance.OpenRefillControl(runRequestPath, SourcePath, ledgerPath, commitmentIds, commitmentId, transition, stepId)
                : CropTeacherWateringMaintenance.Open(runRequestPath, SourcePath, ledgerPath, commitmentIds, commitmentId, transition, stepId);
            inputs.Validate();
            return result;
        }

        ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(
            MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
    }
}
