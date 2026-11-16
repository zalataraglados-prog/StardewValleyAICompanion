using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSupportingRoute
{
    public sealed partial class Preparation : ICropTeacherSupportingPreparation
    {
        private static readonly HashSet<string> NeutralIngressConnectorKinds = new(new[]
        { "warp", "door", "building_door", "action_warp", "touch_action_warp", "locked_door_warp", "magic_warp" }, StringComparer.Ordinal);
        private readonly string runRequestPath;
        private readonly string ledgerPath;
        private readonly string[] commitmentIds;
        private readonly string stepId;
        private readonly CropTeacherRetainedInputs inputs;
        private readonly CropTeacherSupportingSource source;

        private Preparation(string runRequestPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string stepId, string kind, string modelPlanPath, string queuePath, bool nativeControlObservation)
        {
            var paths = new[] { runRequestPath, sourcePath, ledgerPath, modelPlanPath, queuePath }.Select(Path.GetFullPath)
                .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            inputs = new(paths);
            source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
            this.runRequestPath = Path.GetFullPath(runRequestPath); this.ledgerPath = Path.GetFullPath(ledgerPath);
            this.commitmentIds = commitmentIds.ToArray(); this.stepId = stepId;
            SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath); Kind = kind;
            FinalTargetLocation = MoneyRevenueCropNativeLifecycleProofBuilder.SupportingRouteTarget(source.Snapshot, kind, source.CropLocation);
            var plan = CurrentTeacherFrontierSupport.Read<SmallModelPlanEnvelope>(modelPlanPath, "original crop route plan");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "original crop route compilation");
            if (nativeControlObservation)
            {
                var observationCandidate = NativeControlSettleRecovery.TryCreate(source.Snapshot);
                Require(kind == "supporting_route_to_sleep" && observationCandidate is not null,
                    "Owned crop control observation requires its authoritative native-control gap and sleep-route scope.");
                Require(plan.SchemaVersion == "small_model_plan.v1" &&
                        plan.SourceModel == "StardewAI.Core.Training.DailyPlanCompiler" &&
                        plan.StateHash == source.Snapshot.StateHash && plan.PlanType == "daily_candidate_plan" &&
                        plan.ExecutionMode == ExecutionTargetProfiles.TrainingSingleplayer &&
                        EqualJson(plan.Actor, ExecutionTargetProfiles.CreateActor(plan.ExecutionMode)) &&
                        plan.CandidateAudit is [var audit] && audit.CandidateId == observationCandidate!.CandidateId &&
                        audit.Kind == observationCandidate.Kind && audit.Decision == "accepted" &&
                        audit.Reasons.SequenceEqual(new[] { "fits_aggregate_budget" }) &&
                        plan.Steps is [var wait] && wait.Kind == "wait_ticks" &&
                        wait.WaitTicks == NativeControlSettleRecovery.WaitTicks &&
                        wait.Preconditions.SequenceEqual(new[] { "candidate_id:" + observationCandidate.CandidateId }) &&
                        wait.ExpectedEffects.SequenceEqual(new[] { observationCandidate.ExpectedEffect }) &&
                        wait.SafetyConstraints.Contains("wait_only_recovery_candidate", StringComparer.Ordinal) &&
                        wait.FailurePolicy.SequenceEqual(new[] { "refresh_snapshot_and_replan" }),
                    "Owned crop control observation differs from its original wait-only plan/candidate/replan scope.");
                var observation = new ActionQueueCompiler().Compile(plan, source.Snapshot);
                CropTeacherOriginalQueueCompilation.Verify(queue, observation, "Owned crop control observation");
                Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 &&
                        queue.Items is [var observationItem] && observationItem.Status == "pending" &&
                        observationItem.BlockingReasons.Length == 0 && observationItem.MissingStateFactors.Length == 0 &&
                        observationItem.OptionId == "executor.wait_ticks" &&
                        observationItem.NormalizedCommand.StateHash == source.Snapshot.StateHash &&
                        observationItem.NormalizedCommand.Steps is [var nativeWait] &&
                        nativeWait.StepType == "wait_ticks" && nativeWait.Target == NativeControlSettleRecovery.WaitTicks.ToString(),
                    "Owned crop control observation requires its original single pending native wait compilation.");
                // This hop observes the current position; the original home remains the final target.
                Require(!string.IsNullOrWhiteSpace(observationCandidate!.LocationId), "Owned crop control observation lacks its source location.");
                ExpectedHopTargetLocation = observationCandidate.LocationId;
                CandidateId = observationCandidate.CandidateId;
                TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
                inputs.Validate();
                return;
            }
            Require(plan.SchemaVersion == "small_model_plan.v1" && plan.SourceModel == "authoritative_crop_native_route.v1" &&
                    plan.StateHash == source.Snapshot.StateHash && plan.PlanType == "mechanical_plan" && plan.Steps is [var move] &&
                    move.Kind == "move_to_tile" && move.TargetLocation == FinalTargetLocation,
                "Owned crop route compilation differs from its original target/plan scope.");
            var rebuilt = new ActionQueueCompiler().Compile(plan, source.Snapshot, source.Ledger);
            CropTeacherOriginalQueueCompilation.Verify(queue, rebuilt, "Owned crop route");
            Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
                    item.OptionId == "executor.traverse_connector" && item.NormalizedCommand.StateHash == source.Snapshot.StateHash,
                "Owned crop route requires the original single-connector compilation, not an interrupt/wait or resource action.");
            var command = queue.Items[0].NormalizedCommand;
            string Parameter(string name)
            {
                var rows = command.Parameters.Where(row => row.Name == name).ToArray();
                Require(rows.Length == 1 && !string.IsNullOrWhiteSpace(rows[0].Value), "Owned crop route lacks its original parameter: " + name);
                return rows[0].Value;
            }
            Require(Parameter("cross_location_move_target") == FinalTargetLocation && Parameter("fresh_snapshot_replan_required") == "true",
                "Owned crop route lost its final target or original one-connector replan boundary.");
            Require(NeutralIngressConnectorKinds.Contains(Parameter("connector_kind")),
                "Owned neutral crop ingress cannot dispatch a carrier without its separate resource/cost settlement scope.");
            ExpectedHopTargetLocation = Parameter("expected_target_location");
            var candidates = command.Parameters.Where(row => row.Name == "precondition" && row.Value.StartsWith("candidate_id:", StringComparison.Ordinal))
                .Select(row => row.Value["candidate_id:".Length..]).Distinct(StringComparer.Ordinal).ToArray();
            Require(candidates is [var candidate] && !string.IsNullOrWhiteSpace(candidate), "Owned crop route lacks its original candidate identity.");
            CandidateId = candidates.Single();
            TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
            inputs.Validate();
        }

        public static Preparation Open(string runRequestPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string stepId, string kind, string modelPlanPath, string queuePath, bool nativeControlObservation = false) =>
            new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, kind, modelPlanPath, queuePath, nativeControlObservation);
        public string SourcePath { get; }
        public string QueuePath { get; }
        public string Kind { get; }
        public int TotalDay { get; }
        public string CandidateId { get; }
        public string FinalTargetLocation { get; }
        public string ExpectedHopTargetLocation { get; }
        public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(source.Ledger);
        public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
        public void ValidateInputsUnchanged() => inputs.Validate();

        public CropTeacherSupportingRoute VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        {
            inputs.Validate();
            Require(transition.TransitionKind == Kind && transition.TotalDay == TotalDay && transition.SelectedCandidateId == CandidateId &&
                    CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.QueuePath), QueuePath) &&
                    CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), SourcePath),
                "Owned crop native route differs from its pinned original compilation/source.");
            var result = UsesNativeActionEffects
                ? new CropTeacherSupportingRoute(this, transition, stepId)
                : new CropTeacherSupportingRoute(runRequestPath, SourcePath, ledgerPath, commitmentIds, transition, stepId,
                    ExpectedHopTargetLocation, SiteApproachTarget, siteObservationPath);
            inputs.Validate();
            return result;
        }

        ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(
            MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);
    }
}
