using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// A close-only leaf witness, not an executor or a whole lifecycle success claim.
public sealed partial class CropTeacherControlRecovery : ICropTeacherSupportingTransition
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly CropTeacherSupportingRebind rebind;

    private CropTeacherControlRecovery(CropTeacherSupportingSource source, string[] preparationPaths,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId, string kind)
    {
        var paths = preparationPaths.Concat(new[] { transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath })
            .Select(Path.GetFullPath).Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        inputs = new(paths);
        var verified = MoneyRevenueResourceNeutralTerminalRouteVerifier.VerifyRecoveryControl(source.Snapshot, new()
        {
            TransitionId = stepId, QueuePath = transition.QueuePath, BeforeSnapshotPath = transition.BeforeSnapshotPath,
            ExecutionReceiptPath = transition.ExecutionReceiptPath, AfterSnapshotPath = transition.AfterSnapshotPath,
            RunId = transition.RunId, ExecutorVersion = transition.ExecutorVersion, SelectedCandidateId = transition.SelectedCandidateId
        });
        var after = verified.TerminalActionBefore;
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath, "crop control recovery receipt");
        Require(receipt.StepResults.Length == 1, "Crop control recovery requires one original native close receipt.");
        CropTeacherNativeStepBoundary.Verify(source.Snapshot, after, source.Snapshot, receipt.StepResults[0], transition.TotalDay, "Crop control recovery");
        foreach (var field in new[] { "tile_x", "tile_y" })
            Require(MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "player", field) ==
                    MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "player", field),
                "Crop control recovery changed the original player tile.");
        Require(MoneyRevenueResourceNeutralTerminalRouteVerifier.TryStateFieldValue(after, "menus", "active_menu", out var menu) &&
                menu.ValueKind == JsonValueKind.Object && menu.TryGetProperty("is_open", out var open) && open.ValueKind == JsonValueKind.False,
            "Crop control recovery did not readably close the original dialogue menu.");
        Kind = kind;
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        rebind = new(after, source.Ledger, stepId, inputs.Validate, "Crop control recovery");
        inputs.Validate();
    }

    public string Kind { get; }
    public string AfterSnapshotPath { get; }
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();
}
