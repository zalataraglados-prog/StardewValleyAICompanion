using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherOvernight : ICropTeacherSupportingTransition
{
    public sealed class Preparation : ICropTeacherSupportingPreparation
    {
        private readonly CropTeacherRetainedInputs inputs;
        private readonly CropTeacherSupportingSource source;
        private readonly string[] commitmentIds;
        private readonly string stepId;

        private Preparation(string runRequestPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string stepId, string modelPlanPath, string queuePath)
        {
            var paths = new[] { runRequestPath, sourcePath, ledgerPath, modelPlanPath, queuePath }.Select(Path.GetFullPath)
                .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            inputs = new(paths);
            source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
            this.commitmentIds = commitmentIds.ToArray(); this.stepId = stepId;
            SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath);
            TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
            var model = CurrentTeacherFrontierSupport.Read<SmallModelPlanEnvelope>(modelPlanPath, "original crop sleep plan");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "original crop sleep queue");
            Require(model.SchemaVersion == "small_model_plan.v1" && model.SourceModel == "StardewAI.Core.Training.DailyPlanCompiler" &&
                    model.PlanType == "daily_candidate_plan" && model.StateHash == source.Snapshot.StateHash && model.Steps is [{ Kind: "sleep" }],
                "Owned crop sleep differs from the original single-sleep daily compilation: schema=" + model.SchemaVersion +
                "; source=" + model.SourceModel + "; type=" + model.PlanType + "; steps=" + string.Join(",", model.Steps.Take(4).Select(step => step.Kind)) +
                "; state_match=" + (model.StateHash == source.Snapshot.StateHash));
            CropTeacherOriginalQueueCompilation.Verify(queue, new ActionQueueCompiler().Compile(model, source.Snapshot, source.Ledger), "Owned crop sleep");
            Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && NativeDaySettlementSupport.IsSingleNativeSleepQueue(queue),
                "Owned crop sleep requires its original single pending native sleep queue.");
            var candidates = queue.Items[0].NormalizedCommand.Parameters.Where(row => row.Name == "precondition" &&
                row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Select(row => row.Value["candidate_id:".Length..]).ToArray();
            Require(candidates is [var candidate] && !string.IsNullOrWhiteSpace(candidate), "Owned crop sleep lacks one original candidate identity.");
            CandidateId = candidates[0];
            inputs.Validate();
        }

        public static Preparation Open(string runRequestPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string stepId, string modelPlanPath, string queuePath) => new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, modelPlanPath, queuePath);
        public string SourcePath { get; }
        public string QueuePath { get; }
        public int TotalDay { get; }
        public string CandidateId { get; }
        public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(source.Ledger);
        public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
        public void ValidateInputsUnchanged() => inputs.Validate();
        public CropTeacherOvernight VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition) =>
            new(this, source, commitmentIds, stepId, transition);
        ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
    }
}
