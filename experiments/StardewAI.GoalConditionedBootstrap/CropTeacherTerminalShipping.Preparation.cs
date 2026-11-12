using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherTerminalShipping
{
    public sealed class Preparation : ICropTeacherSupportingPreparation
    {
        private readonly CropTeacherRetainedInputs inputs;
        private readonly CropTeacherSupportingSource source;
        private readonly string stepId;

        private Preparation(string runRequestPath, string sourcePath, string ledgerPath,
            IReadOnlyCollection<string> commitmentIds, string stepId, CropTeacherTerminalShippingTarget target,
            string modelActionPath, string queuePath)
        {
            Target = target;
            this.stepId = stepId;
            SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath);
            var proof = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropOutputRetentionProof>(target.RetentionProofPath, "terminal retention certificate");
            var run = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlanRequest>(runRequestPath, "terminal original run");
            var paths = new[] { runRequestPath, sourcePath, ledgerPath, modelActionPath, queuePath,
                target.RetentionProofPath, proof.AfterSnapshotPath, run.CropProductionPlanPath }
                .Select(Path.GetFullPath).Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            inputs = new(paths);
            source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
            proof = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropOutputRetentionProof>(target.RetentionProofPath, "terminal pinned retention certificate");
            var hashes = inputs.ReadHashes();
            Require(hashes[Path.GetFullPath(target.RetentionProofPath)] == target.RetentionProofSha256 &&
                    proof.SchemaVersion == "money_revenue_crop_output_retention_proof.v1" &&
                    proof.Status == "verified_native_crop_output_retention" && proof.NativeOutputRetentionVerified &&
                    !proof.FormalProductTrainingAuthorized && commitmentIds.Contains(proof.CommitmentId, StringComparer.Ordinal) &&
                    source.Ledger.CropPlantingCommitments.Any(row => row.CommitmentId == proof.CommitmentId &&
                        row.Status == StrategyCommitmentStatuses.Active && row.HarvestItemQualifiedId == target.QualifiedItemId) &&
                    SamePath(proof.CropProductionPlanPath, source.RunRequest.CropProductionPlanPath) &&
                    proof.CropProductionPlanSha256 == hashes[Path.GetFullPath(source.RunRequest.CropProductionPlanPath)] &&
                    proof.HarvestItemQualifiedId == target.QualifiedItemId && proof.HarvestTotalDay == target.TotalDay &&
                    target.RequiredQuantity == proof.MinimumUnits && target.RequiredQuantity > 0 && proof.MinimumGrossRevenue > 0 &&
                    target.UnitPrice > 0 && checked((long)target.UnitPrice * target.RequiredQuantity) >= proof.MinimumGrossRevenue &&
                    target.InitialQuantity >= target.RequiredQuantity && target.SlotIndex >= 0 && target.Quality >= 0 &&
                    target.AcceptedDepositCount >= 0 && target.AcceptedDepositCount < target.RequiredQuantity &&
                    new[] { target.BinTileX, target.BinTileY, target.StandTileX, target.StandTileY }.All(value => value >= 0),
                "Terminal target differs from its pinned original retained wave/active commitment/production plan.");
            Require(hashes[Path.GetFullPath(proof.AfterSnapshotPath)] == proof.AfterSnapshotSha256,
                "Terminal initial retained snapshot bytes changed.");
            var initial = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(proof.AfterSnapshotPath, "terminal initial retained snapshot");
            Require(initial.StateHash == proof.AfterStateHash, "Terminal initial retained state changed.");
            VerifySource(initial, target, 0);
            VerifySource(source.Snapshot, target, target.AcceptedDepositCount);
            var initialBin = ReadBin(initial, target);
            var currentBin = ReadBin(source.Snapshot, target);
            Require(Text(initialBin, "bin_scope") == Text(currentBin, "bin_scope"), "Terminal bin custody scope changed.");
            var approach = !AtStand(source.Snapshot, target);
            Kind = approach ? "shipping_approach" : "shipping_deposit";
            CandidateId = FormattableString.Invariant($"ship:Farm:{target.BinTileX},{target.BinTileY}:{target.SlotIndex}:{target.QualifiedItemId}") +
                (approach ? ":approach" : ":deposit:" + (target.AcceptedDepositCount + 1).ToString(CultureInfo.InvariantCulture));
            var model = CurrentTeacherFrontierSupport.Read<SmallModelActionEnvelope>(modelActionPath, "terminal original model action");
            Require(BootstrapArtifactGuard.EqualJson(model, BuildOriginalAction(source.Snapshot, target)),
                "Terminal model differs from the entire original single-action continuation recipe.");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "terminal original compilation");
            CropTeacherOriginalQueueCompilation.Verify(queue, new ActionQueueCompiler().Compile(model, source.Snapshot, source.Ledger), "Terminal shipping");
            Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
                    item.Status == "pending" && item.BlockingReasons.Length == 0 && item.MissingStateFactors.Length == 0 &&
                    item.OptionId == (approach ? "executor.move_to_tile" : "executor.ship_inventory_item_to_bin") &&
                    item.NormalizedCommand.StateHash == source.Snapshot.StateHash,
                "Terminal shipping requires one original pending unblocked action.");
            inputs.Validate();
        }

        public static Preparation Open(string runRequestPath, string sourcePath, string ledgerPath,
            IReadOnlyCollection<string> commitmentIds, string stepId, CropTeacherTerminalShippingTarget target,
            string modelActionPath, string queuePath) =>
            new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, target, modelActionPath, queuePath);
        public CropTeacherTerminalShippingTarget Target { get; }
        public string Kind { get; }
        public string SourcePath { get; }
        public string QueuePath { get; }
        public int TotalDay => Target.TotalDay;
        public string CandidateId { get; }
        public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(source.Ledger);
        public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
        public void ValidateInputsUnchanged() => inputs.Validate();

        public CropTeacherTerminalShipping VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        {
            inputs.Validate();
            Require(transition.TransitionKind == Kind && transition.TotalDay == TotalDay && transition.SelectedCandidateId == CandidateId &&
                    SamePath(transition.QueuePath, QueuePath) && SamePath(transition.BeforeSnapshotPath, SourcePath),
                "Terminal transition differs from the pinned original kind/day/candidate/queue/source.");
            var result = new CropTeacherTerminalShipping(source, stepId, Target, Kind, inputs.ReadHashes().Keys, transition);
            inputs.Validate();
            return result;
        }

        ICropTeacherSupportingTransition ICropTeacherSupportingPreparation.VerifyTransition(
            MoneyRevenueCropNativeLifecycleTransitionRequest transition) => VerifyTransition(transition);

        // Exact original RuntimeMoneyCropNative/terminal-selection recipe, for admission comparison only.
        public static SmallModelActionEnvelope BuildOriginalAction(SnapshotEnvelope snapshot, CropTeacherTerminalShippingTarget target)
        {
            var approach = !AtStand(snapshot, target);
            var candidate = FormattableString.Invariant($"ship:Farm:{target.BinTileX},{target.BinTileY}:{target.SlotIndex}:{target.QualifiedItemId}") +
                (approach ? ":approach" : ":deposit:" + (target.AcceptedDepositCount + 1).ToString(CultureInfo.InvariantCulture));
            return ExpectedOriginalModel(snapshot, target, candidate, approach);
        }

        private static SmallModelActionEnvelope ExpectedOriginalModel(SnapshotEnvelope snapshot,
            CropTeacherTerminalShippingTarget target, string candidate, bool approach)
        {
            var parameters = new List<SmallModelActionParameter>();
            void Add(string name, object value) => parameters.Add(new() { Name = name, Value = Convert.ToString(value, CultureInfo.InvariantCulture)! });
            if (approach) { Add("target_tile_x", target.StandTileX); Add("target_tile_y", target.StandTileY); }
            else
            {
                Add("target_location", "Farm"); Add("target_tile_x", target.BinTileX); Add("target_tile_y", target.BinTileY);
                Add("stand_tile_x", target.StandTileX); Add("stand_tile_y", target.StandTileY); Add("slot_index", target.SlotIndex);
                Add("qualified_item_id", target.QualifiedItemId); Add("quantity", 1); Add("expected_unit_price", target.UnitPrice);
            }
            Add("continuation.option_id", "executor.ship_inventory_item_to_bin"); Add("continuation.target_location", "Farm");
            Add("continuation.qualified_item_id", target.QualifiedItemId); Add("continuation.slot_index", target.SlotIndex);
            Add("continuation.quantity", 1); Add("continuation.expected_unit_price", target.UnitPrice); Add("precondition", "candidate_id:" + candidate);
            return new()
            {
                ModelOutputId = "model-output:" + candidate, SourceModel = "authoritative_crop_terminal_continuation.v1",
                StateHash = snapshot.StateHash, GoalId = "daily.closed_loop", ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
                Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer),
                Actions = new[] { new SmallModelAction { ActionId = candidate + ":action",
                    OptionId = approach ? "executor.move_to_tile" : "executor.ship_inventory_item_to_bin",
                    Rationale = "authoritative_crop_terminal_continuation", Parameters = parameters.ToArray() } }
            };
        }
    }
}
