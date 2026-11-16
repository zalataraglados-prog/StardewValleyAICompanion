using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherControlRecovery
{
    public sealed class Preparation : ICropTeacherSupportingPreparation
    {
        private readonly string[] paths;
        private readonly string stepId;
        private readonly CropTeacherRetainedInputs inputs;
        private readonly CropTeacherSupportingSource source;

        private Preparation(string runRequestPath, string sourcePath, string ledgerPath,
            IReadOnlyCollection<string> commitmentIds, string stepId, string kind, string modelPlanPath, string queuePath)
        {
            paths = new[] { runRequestPath, sourcePath, ledgerPath, modelPlanPath, queuePath }.Select(Path.GetFullPath)
                .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            inputs = new(paths);
            source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
            Require(kind == "safe_dialogue_recovery" && SafeSpeakerlessDialogueRecovery.HasSafeSpeakerlessDialogue(source.Snapshot),
                "Crop control recovery requires its original safe speakerless dialogue source/kind.");
            this.stepId = stepId;
            SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath); Kind = kind;
            var plan = CurrentTeacherFrontierSupport.Read<SmallModelPlanEnvelope>(modelPlanPath, "original crop dialogue recovery plan");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "original crop dialogue recovery compilation");
            const string prefix = "plan:crop-dialogue-recovery:";
            Require(plan.PlanId.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(plan.PlanId[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var iteration) && iteration > 0,
                "Crop control recovery lacks its original dialogue candidate identity.");
            CandidateId = plan.PlanId["plan:".Length..];
            var expectedStep = new SmallModelPlanStep
            {
                StepId = CandidateId + ":close", Kind = "close_menu", EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + CandidateId, "menus.active_menu.is_open=true" },
                ExpectedEffects = new[] { "menus.active_menu.is_open=false" },
                SafetyConstraints = new[] { "close_only_verified_supported_menu", "recovery_menu_close" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" }
            };
            Require(plan.SchemaVersion == "small_model_plan.v1" && plan.SourceModel == "authoritative_crop_native_recovery.v1" &&
                    plan.StateHash == source.Snapshot.StateHash && plan.GoalId == "daily.closed_loop" && plan.PlanType == "mechanical_plan" &&
                    plan.ExecutionMode == ExecutionTargetProfiles.TrainingSingleplayer &&
                    EqualJson(plan.Actor, ExecutionTargetProfiles.CreateActor(plan.ExecutionMode)) &&
                    plan.CandidateAudit.Length == 0 && plan.Steps is [var close] && EqualJson(close, expectedStep),
                "Crop control recovery differs from its original singleplayer close-only model/replan scope.");
            var rebuilt = new ActionQueueCompiler().Compile(plan, source.Snapshot);
            CropTeacherOriginalQueueCompilation.Verify(queue, rebuilt, "Crop control recovery");
            Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
                    item.Status == "pending" && item.BlockingReasons.Length == 0 && item.MissingStateFactors.Length == 0 &&
                    item.OptionId == "executor.close_menu" && item.NormalizedCommand.StateHash == source.Snapshot.StateHash &&
                    item.NormalizedCommand.Steps is [var nativeClose] && nativeClose.StepType == "close_menu",
                "Crop control recovery requires its original single pending unblocked close-menu compilation.");
            TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
            inputs.Validate();
        }

        public static Preparation Open(string runRequestPath, string sourcePath, string ledgerPath,
            IReadOnlyCollection<string> commitmentIds, string stepId, string kind, string modelPlanPath, string queuePath) =>
            new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, kind, modelPlanPath, queuePath);

        public string SourcePath { get; }
        public string QueuePath { get; }
        public string Kind { get; }
        public int TotalDay { get; }
        public string CandidateId { get; }
        public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(source.Ledger);
        public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
        public void ValidateInputsUnchanged() => inputs.Validate();

        public CropTeacherControlRecovery VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        {
            inputs.Validate();
            Require(transition.TransitionKind == "control_recovery" && transition.TotalDay == TotalDay &&
                    transition.SelectedCandidateId == CandidateId &&
                    CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.QueuePath), QueuePath) &&
                    CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), SourcePath),
                "Crop control recovery differs from its pinned original source/queue/candidate/day.");
            var result = new CropTeacherControlRecovery(source, paths, transition, stepId, Kind);
            inputs.Validate();
            return result;
        }

        ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(
            MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
    }
}
