using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.WorldModel;

namespace StardewAI.GoalConditionedBootstrap;

// Holds the crop source while the global caller, not the crop executor, owns selected foreign work.
public sealed class CropTeacherSharedSourcePreparation : ICropTeacherSupportingPreparation
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly string runRequestPath;
    private readonly string ledgerPath;
    private readonly string[] commitmentIds;
    private readonly string stepId;
    private readonly MoneyRevenueCropNativeLifecycleTransitionRequest boundary;

    private CropTeacherSharedSourcePreparation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, string modelPlanPath,
        MoneyRevenueCropNativeLifecycleTransitionRequest boundary, IReadOnlyDictionary<string, string> selectionHashes)
    {
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, modelPlanPath, boundary.QueuePath };
        Require(paths.All(Path.IsPathFullyQualified) && Path.IsPathFullyQualified(boundary.BeforeSnapshotPath) &&
                Path.IsPathFullyQualified(boundary.ExecutionReceiptPath) && Path.IsPathFullyQualified(boundary.AfterSnapshotPath),
            "Shared source preparation requires absolute original paths.");
        paths = paths.Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(comparer).Count() == paths.Length && comparer.Equals(paths[1], Path.GetFullPath(boundary.BeforeSnapshotPath)),
            "Shared source preparation aliases an input role or uses another source.");
        inputs = new(paths.Concat(selectionHashes.Keys).Distinct(comparer).ToArray());
        var hashes = inputs.ReadHashes();
        Require(selectionHashes.All(pair => hashes.TryGetValue(Path.GetFullPath(pair.Key), out var hash) && hash == pair.Value),
            "Shared source preparation changed its selected decision inputs.");
        var source = CropTeacherSupportingSource.Read(paths[0], paths[1], paths[2], commitmentIds, stepId);
        var model = CurrentTeacherFrontierSupport.Read<SmallModelPlanEnvelope>(paths[3], "shared source selected plan");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(paths[4], "shared source selected queue");
        Require(model.StateHash == source.Snapshot.StateHash && queue.StateHash == source.Snapshot.StateHash &&
                model.SourceModel == "StardewAI.Core.Training.DailyPlanCompiler" && model.PlanType == "daily_candidate_plan" &&
                queue.Status == "pending" && queue.Items.Length > 0 && queue.Items.All(item => item.Status == "pending"),
            "Shared source preparation requires a fresh executable original selected plan.");
        CropTeacherOriginalQueueCompilation.Verify(queue,
            new ActionQueueCompiler().Compile(model, source.Snapshot, source.Ledger), "Shared source selected queue");
        var budget = new TimeBudgetValidator().Validate(new WorldModelProjector().Project(source.Snapshot, queue.GoalId, queue.ExecutionMode), queue);
        Require(budget.FitsRequiredPlusOptional, "Shared source selected queue exceeds its original time budget: " + string.Join(",", budget.BlockReasons));
        TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
        Require(boundary.TotalDay == TotalDay && boundary.ExecutorVersion == PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor &&
                !string.IsNullOrWhiteSpace(boundary.RunId) && !string.IsNullOrWhiteSpace(boundary.SelectedCandidateId),
            "Shared source preparation differs from its original day/run/executor/candidate.");
        var outputs = new[] { Path.GetFullPath(boundary.ExecutionReceiptPath), Path.GetFullPath(boundary.AfterSnapshotPath) };
        Require(outputs.Distinct(comparer).Count() == outputs.Length && outputs.All(path => !hashes.ContainsKey(path)),
            "Shared source preparation outputs overlap retained inputs.");
        this.runRequestPath = paths[0]; SourcePath = paths[1]; this.ledgerPath = paths[2]; QueuePath = paths[4];
        this.commitmentIds = commitmentIds.ToArray(); this.stepId = stepId; this.boundary = boundary;
        CandidateId = boundary.SelectedCandidateId; BaseLedger = source.Ledger;
        ValidateInputsUnchanged();
    }

    public static CropTeacherSharedSourcePreparation Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string stepId, string modelPlanPath,
        MoneyRevenueCropNativeLifecycleTransitionRequest boundary, IReadOnlyDictionary<string, string> selectionHashes) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, modelPlanPath, boundary, selectionHashes);

    public string SourcePath { get; }
    public string QueuePath { get; }
    public int TotalDay { get; }
    public string CandidateId { get; }
    public StrategyCommitmentLedger BaseLedger { get; }
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();
    public ICropTeacherSupportingTransition VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        ValidateInputsUnchanged();
        Require(transition == boundary, "Shared source outcome differs from its pre-native selected boundary.");
        var result = CropTeacherSharedSourceReturn.Open(runRequestPath, SourcePath, ledgerPath, commitmentIds, transition, stepId);
        ValidateInputsUnchanged();
        return result;
    }
}
