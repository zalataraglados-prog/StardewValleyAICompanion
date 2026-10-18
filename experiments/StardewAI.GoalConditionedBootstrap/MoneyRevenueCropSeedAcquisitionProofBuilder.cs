using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    private static readonly AsyncLocal<HashSet<string>?> BuildStack = new();

    public static MoneyRevenueCropSeedAcquisitionProof Build(
        string requestPath)
    {
        var identity = Path.GetFullPath(requestPath);
        var stack = BuildStack.Value ??= new HashSet<string>(PathComparer);
        Require(stack.Add(identity),
            "Money Revenue crop seed-acquisition proof lineage is cyclic.");
        try
        {
            return BuildCore(identity);
        }
        finally
        {
            stack.Remove(identity);
            if (stack.Count == 0)
                BuildStack.Value = null;
        }
    }

    private static MoneyRevenueCropSeedAcquisitionProof BuildCore(
        string requestPath)
    {
        var requestFile = RequiredFile(
            requestPath,
            "Money Revenue crop seed-acquisition proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSeedAcquisitionProofRequest>(
                requestFile,
                "Money Revenue crop seed-acquisition proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_seed_acquisition_proof_request.v2" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop seed-acquisition proof request is invalid.");

        var planFile = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planFile,
                "Money Revenue crop-production plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder
            .Build(plan.RequestPath);
        Require(plan.SchemaVersion ==
                    "money_revenue_crop_production_obligation_plan.v2" &&
                BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan),
            "Money Revenue crop-production plan is not exact.");
        var commitment = plan.Commitments.SingleOrDefault(value =>
            value.CommitmentId == request.CommitmentId);
        Require(commitment is not null &&
                commitment.SeedQuantityShortfall > 0,
            "Money Revenue crop commitment has no seed shortfall.");
        var exactCommitment = commitment ?? throw new InvalidDataException(
            "Money Revenue crop seed-acquisition commitment is missing.");
        var evidenceRequestId = EvidenceRequestId(exactCommitment);
        Require(plan.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal) &&
                exactCommitment.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal),
            "Money Revenue crop seed acquisition is outside the plan denominator.");

        var planSha256 = CurrentTeacherFrontierSupport.HashFile(planFile);
        var acquisitionCommitments = plan.Commitments.Where(value =>
                value.SeedQuantityShortfall > 0)
            .ToArray();
        var acquisitionIndex = Array.FindIndex(
            acquisitionCommitments,
            value => value.CommitmentId == exactCommitment.CommitmentId);
        Require(acquisitionIndex >= 0,
            "Money Revenue crop seed acquisition is outside the shortfall order.");

        MoneyRevenueCropSeedAcquisitionProof? priorProof = null;
        SnapshotEnvelope? priorBoundary = null;
        if (acquisitionIndex == 0)
        {
            Require(string.IsNullOrWhiteSpace(
                    request.PriorSeedAcquisitionProofPath),
                "The first Money Revenue crop seed acquisition has an unexpected prior proof.");
        }
        else
        {
            var priorPath = RequiredFile(
                request.PriorSeedAcquisitionProofPath,
                "Prior Money Revenue crop seed-acquisition proof");
            priorProof = CurrentTeacherFrontierSupport.Read<
                MoneyRevenueCropSeedAcquisitionProof>(
                    priorPath,
                    "Prior Money Revenue crop seed-acquisition proof");
            var rebuiltPrior = Build(priorProof.RequestPath);
            Require(priorProof.SchemaVersion ==
                        "money_revenue_crop_seed_acquisition_proof.v2" &&
                    BootstrapArtifactGuard.EqualJson(
                        priorProof,
                        rebuiltPrior) &&
                    SamePath(priorProof.CropProductionPlanPath, planFile) &&
                    priorProof.CropProductionPlanSha256 == planSha256 &&
                    priorProof.CommitmentId ==
                        acquisitionCommitments[acquisitionIndex - 1]
                            .CommitmentId &&
                    priorProof.SeedAcquisitionVerified &&
                    !priorProof.FormalProductTrainingAuthorized,
                "Prior Money Revenue crop seed-acquisition proof is not the exact preceding shortfall.");
            priorBoundary = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                RequiredFile(
                    priorProof.AfterSnapshotPath,
                    "Prior Money Revenue crop seed-acquisition terminal snapshot"),
                "Prior Money Revenue crop seed-acquisition terminal snapshot");
            ValidateSnapshot(priorBoundary, "prior terminal");
            Require(priorBoundary.StateHash == priorProof.AfterStateHash,
                "Prior Money Revenue crop seed-acquisition terminal boundary drifted.");
        }

        var manifestFile = RequiredFile(
            request.ExecutionManifestPath,
            "Money Revenue crop seed-acquisition execution manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSeedAcquisitionExecutionManifest>(
                manifestFile,
                "Money Revenue crop seed-acquisition execution manifest");
        Require(manifest.SchemaVersion ==
                    "money_revenue_crop_seed_acquisition_execution_manifest.v1" &&
                SamePath(manifest.CropProductionPlanPath, planFile) &&
                manifest.CropProductionPlanSha256 == planSha256 &&
                manifest.CommitmentId == exactCommitment.CommitmentId &&
                !string.IsNullOrWhiteSpace(manifest.SourceStateHash) &&
                !string.IsNullOrWhiteSpace(manifest.RunId) &&
                manifest.Transitions is { Length: > 0 } &&
                !manifest.FormalProductTrainingAuthorized,
            "Money Revenue crop seed-acquisition execution manifest is invalid.");

        var transitions = manifest.Transitions.Select(LoadTransition).ToArray();
        var verification = VerifyExecutionChain(
            plan,
            exactCommitment,
            manifest,
            transitions,
            priorBoundary);
        var first = transitions[0];
        var last = transitions[^1];
        return new MoneyRevenueCropSeedAcquisitionProof
        {
            Status = "verified_exact_seed_shortfall_native_acquisition",
            RequestPath = requestFile,
            RequestSha256 = Hash(requestFile),
            CropProductionPlanPath = planFile,
            CropProductionPlanSha256 = planSha256,
            EvidenceRequestId = evidenceRequestId,
            CommitmentId = exactCommitment.CommitmentId,
            ExecutionManifestPath = manifestFile,
            ExecutionManifestSha256 = Hash(manifestFile),
            PriorSeedAcquisitionProofPath = priorProof is null
                ? string.Empty
                : Path.GetFullPath(request.PriorSeedAcquisitionProofPath),
            PriorSeedAcquisitionProofSha256 = priorProof is null
                ? string.Empty
                : Hash(request.PriorSeedAcquisitionProofPath),
            PriorSeedAcquisitionEvidenceRequestId =
                priorProof?.EvidenceRequestId ?? string.Empty,
            BeforeSnapshotPath = first.BeforePath,
            BeforeSnapshotSha256 = Hash(first.BeforePath),
            BeforeStateHash = first.Before.StateHash,
            AfterSnapshotPath = last.AfterPath,
            AfterSnapshotSha256 = Hash(last.AfterPath),
            AfterStateHash = last.After.StateHash,
            SeedQualifiedItemId = exactCommitment.SeedQualifiedItemId,
            InitialSeedQuantity = verification.InitialQuantity,
            FinalSeedQuantity = verification.FinalQuantity,
            RequiredShortfallQuantity =
                exactCommitment.SeedQuantityShortfall,
            VerifiedQuantityIncrease = verification.VerifiedIncrease,
            SeedSupplyDeadlineTotalDay =
                exactCommitment.SeedSupplyDeadlineTotalDay,
            AcquisitionTotalDay = verification.TotalDay,
            TransitionCount = transitions.Length,
            SupportingTransitionCount = verification.TransitionProofs.Count(
                value => value.Stage == "supporting_route_or_shop"),
            PurchaseSliceCount = verification.TransitionProofs.Count(
                value => value.Stage == "purchase_slice"),
            Transitions = verification.TransitionProofs,
            SeedAcquisitionVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static LoadedTransition LoadTransition(
        MoneyRevenueCropNativeLifecycleTransitionRequest request)
    {
        Require(request.TransitionKind == "seed_acquisition_stage" &&
                request.TotalDay >= 0 &&
                !string.IsNullOrWhiteSpace(request.RunId) &&
                !string.IsNullOrWhiteSpace(request.ExecutorVersion) &&
                !string.IsNullOrWhiteSpace(request.SelectedCandidateId),
            "Money Revenue crop seed-acquisition transition request is invalid.");
        var queuePath = RequiredFile(
            request.QueuePath,
            "Money Revenue crop seed-acquisition queue");
        var beforePath = RequiredFile(
            request.BeforeSnapshotPath,
            "Money Revenue crop seed-acquisition before snapshot");
        var receiptPath = RequiredFile(
            request.ExecutionReceiptPath,
            "Money Revenue crop seed-acquisition execution receipt");
        var afterPath = RequiredFile(
            request.AfterSnapshotPath,
            "Money Revenue crop seed-acquisition after snapshot");
        return new LoadedTransition(
            request,
            queuePath,
            beforePath,
            receiptPath,
            afterPath,
            CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
                queuePath,
                "Money Revenue crop seed-acquisition queue"),
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                beforePath,
                "Money Revenue crop seed-acquisition before snapshot"),
            CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(
                receiptPath,
                "Money Revenue crop seed-acquisition execution receipt"),
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                afterPath,
                "Money Revenue crop seed-acquisition after snapshot"));
    }

    private static string EvidenceRequestId(
        MoneyRevenueCropProductionCommitmentPlan commitment) =>
        "seed-acquisition:" + commitment.CommitmentId + ":" +
        commitment.SeedQuantityShortfall;

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(path);

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static bool SamePath(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            PathComparison);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

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

    private sealed record PurchaseBinding(
        string ShopId,
        string StockId,
        string QualifiedItemId,
        int OutputStack,
        int OutputQuality,
        int MaxUnitPrice,
        int Quantity);

    private sealed record ChainVerification(
        int InitialQuantity,
        int FinalQuantity,
        int VerifiedIncrease,
        int TotalDay,
        MoneyRevenueCropSeedAcquisitionTransitionProof[] TransitionProofs);
}
