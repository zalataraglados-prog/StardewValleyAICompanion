using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    public static MoneyRevenueCropNativeLifecycleProof Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop native-lifecycle proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeLifecycleProofRequest>(
                fullRequestPath,
                "Money Revenue crop native-lifecycle proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_native_lifecycle_proof_request.v2" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                request.Transitions is { Length: > 0 } &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop native-lifecycle proof request is invalid.");

        var planPath = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production obligation plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planPath,
                "Money Revenue crop-production obligation plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            plan.RequestPath);
        Require(plan.SchemaVersion ==
                    "money_revenue_crop_production_obligation_plan.v2" &&
                BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan) &&
                plan.PendingEvidenceRequestIds.Contains(
                    "native-crop-lifecycle:" + request.CommitmentId,
                    StringComparer.Ordinal) &&
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");
        var commitment = plan.Commitments.Single(value =>
            value.CommitmentId == request.CommitmentId);
        var planSha256 = CurrentTeacherFrontierSupport.HashFile(planPath);

        var layoutPath = RequiredFile(
            request.PlantingLayoutProofPath,
            "Money Revenue crop planting-layout proof");
        var layout = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropPlantingLayoutProof>(
                layoutPath,
                "Money Revenue crop planting-layout proof");
        var rebuiltLayout = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
            layout.RequestPath);
        Require(layout.SchemaVersion ==
                    "money_revenue_crop_planting_layout_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(layout, rebuiltLayout) &&
                SamePath(layout.CropProductionPlanPath, planPath) &&
                layout.CropProductionPlanSha256 == planSha256 &&
                layout.CommitmentId == commitment.CommitmentId &&
                layout.SeedQualifiedItemId ==
                    commitment.SeedQualifiedItemId &&
                layout.TileCount == commitment.TileCount &&
                layout.PlantingLayoutVerified &&
                !layout.FormalProductTrainingAuthorized,
            "Money Revenue crop planting-layout proof is not exact.");

        var capacityPath = RequiredFile(
            request.DailyCapacityProofPath,
            "Money Revenue crop daily-capacity proof");
        var capacity = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropDailyCapacityProof>(
                capacityPath,
                "Money Revenue crop daily-capacity proof");
        var rebuiltCapacity = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            capacity.RequestPath);
        Require(capacity.SchemaVersion ==
                    "money_revenue_crop_daily_capacity_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(capacity, rebuiltCapacity) &&
                SamePath(capacity.CropProductionPlanPath, planPath) &&
                capacity.CropProductionPlanSha256 == planSha256 &&
                capacity.CommitmentId == commitment.CommitmentId &&
                capacity.LocationId == layout.LocationId &&
                capacity.DailyCapacityVerified &&
                !capacity.FormalProductTrainingAuthorized,
            "Money Revenue crop daily-capacity proof is not exact.");

        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            RequiredFile(plan.SnapshotPath, "Money Revenue crop source snapshot"),
            "Money Revenue crop source snapshot");
        var contexts = LoadLifecycleContexts(
            plan,
            planPath,
            planSha256,
            capacity,
            sourceSnapshot);
        var primaryContext = contexts.Single(value =>
            value.Commitment.CommitmentId == commitment.CommitmentId);
        Require(SamePath(primaryContext.LayoutPath, layoutPath),
            "Money Revenue crop lifecycle primary layout drifted from aggregate capacity support.");
        var transitions = request.Transitions.Select(LoadTransition).ToArray();
        ValidateTransitionChain(
            transitions,
            sourceSnapshot,
            contexts);

        var transitionProofs = transitions.Select(value =>
            new MoneyRevenueCropNativeLifecycleTransitionProof(
                value.Request.TransitionKind,
                value.Request.TotalDay,
                value.QueuePath,
                CurrentTeacherFrontierSupport.HashFile(value.QueuePath),
                value.BeforePath,
                CurrentTeacherFrontierSupport.HashFile(value.BeforePath),
                value.ReceiptPath,
                CurrentTeacherFrontierSupport.HashFile(value.ReceiptPath),
                value.AfterPath,
                CurrentTeacherFrontierSupport.HashFile(value.AfterPath),
                value.Queue.QueueId,
                value.Before.StateHash,
                value.After.StateHash,
                value.Queue.Items.Length,
                value.Request.TransitionKind == CropActionsKind
                    ? value.Queue.Items[0].OptionId
                    : "executor.sleep",
                ResolveActionCommitmentId(value, contexts),
                true)).ToArray();
        return new MoneyRevenueCropNativeLifecycleProof
        {
            Status = "verified_native_crop_lifecycle",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = planSha256,
            PlantingLayoutProofPath = layoutPath,
            PlantingLayoutProofSha256 =
                CurrentTeacherFrontierSupport.HashFile(layoutPath),
            DailyCapacityProofPath = capacityPath,
            DailyCapacityProofSha256 =
                CurrentTeacherFrontierSupport.HashFile(capacityPath),
            EvidenceRequestId =
                "native-crop-lifecycle:" + commitment.CommitmentId,
            CommitmentId = commitment.CommitmentId,
            LocationId = layout.LocationId,
            SeedQualifiedItemId = commitment.SeedQualifiedItemId,
            SharedTransitionDenominatorCommitmentIds = contexts.Select(value =>
                value.Commitment.CommitmentId).ToArray(),
            SharedNativeTrajectoryVerified = true,
            TransitionCount = transitions.Length,
            CropActionQueueCount = transitions.Count(value =>
                value.Request.TransitionKind == CropActionsKind),
            NativeDayRolloverCount = transitions.Count(value =>
                value.Request.TransitionKind == DayRolloverKind),
            VerifiedPlantActionCount = CountOption(
                transitions,
                PlantOption),
            VerifiedWaterActionCount = CountOption(
                transitions,
                WaterOption),
            VerifiedHarvestActionCount = CountOption(
                transitions,
                HarvestOption),
            Transitions = transitionProofs,
            NativeCropLifecycleVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static LoadedTransition LoadTransition(
        MoneyRevenueCropNativeLifecycleTransitionRequest request)
    {
        Require(request.TransitionKind is CropActionsKind or DayRolloverKind &&
                request.TotalDay >= 0 &&
                !string.IsNullOrWhiteSpace(request.RunId) &&
                !string.IsNullOrWhiteSpace(request.ExecutorVersion) &&
                !string.IsNullOrWhiteSpace(request.SelectedCandidateId),
            "Money Revenue crop native-lifecycle transition request is invalid.");
        var queuePath = RequiredFile(
            request.QueuePath,
            "Money Revenue crop lifecycle queue");
        var beforePath = RequiredFile(
            request.BeforeSnapshotPath,
            "Money Revenue crop lifecycle before snapshot");
        var receiptPath = RequiredFile(
            request.ExecutionReceiptPath,
            "Money Revenue crop lifecycle execution receipt");
        var afterPath = RequiredFile(
            request.AfterSnapshotPath,
            "Money Revenue crop lifecycle after snapshot");
        return new LoadedTransition(
            request,
            queuePath,
            beforePath,
            receiptPath,
            afterPath,
            CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
                queuePath,
                "Money Revenue crop lifecycle queue"),
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                beforePath,
                "Money Revenue crop lifecycle before snapshot"),
            CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(
                receiptPath,
                "Money Revenue crop lifecycle execution receipt"),
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                afterPath,
                "Money Revenue crop lifecycle after snapshot"));
    }

    private static int CountOption(
        IEnumerable<LoadedTransition> transitions,
        string optionId) => transitions.Sum(value =>
            value.Queue.Items.Count(item => item.OptionId == optionId));

    private const string CropActionsKind = "crop_actions";
    private const string DayRolloverKind = "native_day_rollover";
    private const string PlantOption = "executor.plant_seed";
    private const string WaterOption = "executor.water_crop";
    private const string HarvestOption = "executor.harvest_crop";

    private sealed record LoadedTransition(
        MoneyRevenueCropNativeLifecycleTransitionRequest Request,
        string QueuePath,
        string BeforePath,
        string ReceiptPath,
        string AfterPath,
        ActionQueueEnvelope Queue,
        SnapshotEnvelope Before,
        QueueExecutionReceiptEnvelope Receipt,
        SnapshotEnvelope After);

    private sealed record CropCatalogRow(
        bool NeedsWatering,
        int? RegrowDays);

    private sealed record LifecycleCommitmentContext(
        string LayoutPath,
        MoneyRevenueCropPlantingLayoutProof Layout,
        MoneyRevenueCropProductionCommitmentPlan Commitment,
        CropCatalogRow Catalog,
        HashSet<(int X, int Y)> Tiles,
        int FinalHarvestDay);

    private sealed record CropState(
        string SeedId,
        string HarvestQualifiedItemId,
        int[] PhaseDays,
        int CurrentPhase,
        int PhaseCount,
        int DayOfCurrentPhase,
        bool Dead,
        bool FullyGrown,
        bool ReadyForHarvest,
        int? DaysUntilNextHarvest,
        int? RegrowDays,
        bool Watered);

    private sealed record CalendarIdentity(
        int TotalDay,
        int WorldDateOffset);
}
