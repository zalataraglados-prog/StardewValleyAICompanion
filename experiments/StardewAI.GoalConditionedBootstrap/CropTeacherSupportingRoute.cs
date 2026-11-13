using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// Consumes original route evidence; it neither plans nor executes another action.
public sealed partial class CropTeacherSupportingRoute : ICropTeacherSupportingTransition
{
    private readonly CropTeacherSupportingRebind rebind;
    private readonly CropTeacherRetainedInputs inputs;

    private CropTeacherSupportingRoute(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId,
        string? verifiedHopTarget = null, MoneyRevenueCropPlantingTile? siteApproachTarget = null, string? siteObservationPath = null)
    {
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, transition.QueuePath, transition.BeforeSnapshotPath,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Concat(siteObservationPath is null ? Array.Empty<string>() : new[] { siteObservationPath }).Select(Path.GetFullPath)
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        // Retain only these explicit phase inputs. A fresh validation below also checks CAS blob content.
        using var reads = InvocationReadSession.Start(paths);
        inputs = new(paths);
        var data = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
        var finalTarget = MoneyRevenueCropNativeLifecycleProofBuilder.SupportingRouteTarget(data.Snapshot, transition.TransitionKind, data.CropLocation);
        var after = MoneyRevenueCropNativeLifecycleProofBuilder.VerifySupportingRoute(data.Snapshot, transition, data.CropLocation, verifiedHopTarget);
        SiteApproachTarget = siteApproachTarget;
        if (IsSiteApproach)
            Require(MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "player", "tile_x") == siteApproachTarget!.TileX &&
                    MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "player", "tile_y") == siteApproachTarget.TileY,
                "Owned site approach did not reach its exact selected stand.");
        RouteTargetReached = !IsSiteApproach && (verifiedHopTarget is null || verifiedHopTarget == finalTarget);
        rebind = new(after, data.Ledger, stepId, inputs.Validate, "Crop supporting route");
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        ValidateInputsUnchanged();
    }

    public static CropTeacherSupportingRoute Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId);

    public string AfterSnapshotPath { get; }
    public bool RouteTargetReached { get; }
    public bool IsSiteApproach => SiteApproachTarget is not null;
    public MoneyRevenueCropPlantingTile? SiteApproachTarget { get; }
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
}
