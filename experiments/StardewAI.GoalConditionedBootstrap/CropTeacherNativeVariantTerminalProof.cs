using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherNativeVariantTerminalRequest(string SchemaVersion,
    CropTeacherTerminalShippingTarget Target, MoneyRevenueCropTerminalSettlementProofRequest Settlement);

// Explicit aggregate adapter; old scalar builders/readers never admit this request/version.
public static class CropTeacherNativeVariantTerminalProof
{
    public static MoneyRevenueCropTerminalSettlementProof Build(string requestPath)
    {
        var wrapper = Read<CropTeacherNativeVariantTerminalRequest>(requestPath);
        var target = wrapper.Target; var request = wrapper.Settlement;
        Require(wrapper.SchemaVersion == "crop_teacher_native_variant_terminal_request.v1" && target.NativeVariants is not null &&
            target.AcceptedDepositCount == target.RequiredQuantity && !request.FormalProductTrainingAuthorized &&
            request.SchemaVersion == "money_revenue_crop_terminal_settlement_proof_request.v3" &&
            string.IsNullOrWhiteSpace(request.ShopSaleEpisodePath), "invalid explicit variant request.");
        var plan = Read<MoneyRevenueCropProductionObligationPlan>(request.CropProductionPlanPath);
        Require(BootstrapArtifactGuard.EqualJson(plan, MoneyRevenueCropProductionObligationPlanBuilder.Build(plan.RequestPath)) &&
            !plan.FormalProductTrainingAuthorized, "production obligation differs.");
        var retention = Read<MoneyRevenueCropOutputRetentionProof>(request.OutputRetentionProofPath);
        Require(BootstrapArtifactGuard.EqualJson(retention, MoneyRevenueCropOutputRetentionProofBuilder.Build(retention.RequestPath)) &&
            retention.NativeOutputRetentionVerified && !retention.FormalProductTrainingAuthorized &&
            Same(retention.CropProductionPlanPath, request.CropProductionPlanPath) &&
            retention.CropProductionPlanSha256 == Hash(request.CropProductionPlanPath) &&
            plan.PendingEvidenceRequestIds.Contains(retention.EvidenceRequestId, StringComparer.Ordinal) &&
            Same(target.RetentionProofPath, request.OutputRetentionProofPath) && target.RetentionProofSha256 == Hash(target.RetentionProofPath) &&
            target.RequiredQuantity == retention.MinimumUnits && retention.MinimumGrossRevenue > 0 &&
            target.QualifiedItemId == retention.HarvestItemQualifiedId && target.TotalDay == retention.HarvestTotalDay,
            "retained whole-wave obligation differs.");
        var start = Read<SnapshotEnvelope>(retention.AfterSnapshotPath);
        Require(Hash(retention.AfterSnapshotPath) == retention.AfterSnapshotSha256 && start.StateHash == retention.AfterStateHash,
            "retained start differs.");
        target.VerifyNativeVariantSource(start, 0, retention.MinimumGrossRevenue);
        var transitions = request.ShippingDepositTransitions;
        Require(transitions is not null && transitions.Length == target.RequiredQuantity &&
            transitions.All(row => row is not null && !string.IsNullOrWhiteSpace(row.TransitionId)) &&
            transitions.Select(row => row.TransitionId).Distinct(StringComparer.Ordinal).Count() == transitions.Length &&
            transitions.Select(row => Path.GetFullPath(row.ExecutionReceiptPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == transitions.Length,
            "missing/replayed deposit evidence.");
        var expected = start;
        var deposits = new List<MoneyRevenueCropShippingDepositEvidence>();
        MoneyRevenueTerminalQueueTransitionEvidence[] route = [];
        var beforeHash = string.Empty;
        for (var index = 0; index < transitions!.Length; index++)
        {
            var selected = target.VariantAt(index); var transition = transitions[index];
            var batch = MoneyRevenueCropTerminalSettlementModeVerifier.VerifyShippingDepositBatch(expected,
                index == 0 ? request.RouteTransitions : [], [transition], target.QualifiedItemId,
                1, selected.UnitPrice, target.PendingValueAt(index), index);
            if (index == 0) { route = batch.RouteTransitions; beforeHash = batch.TerminalActionBefore.StateHash; }
            var before = Read<SnapshotEnvelope>(transition.BeforeSnapshotPath);
            target.VerifyNativeVariantSource(before, index);
            var effect = CropTeacherShippingDepositResources.Verify(before, batch.After,
                Read<ActionQueueEnvelope>(transition.QueuePath), Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath),
                new("shipping_deposit", target.TotalDay, transition.QueuePath, transition.BeforeSnapshotPath,
                    transition.ExecutionReceiptPath, transition.AfterSnapshotPath, transition.RunId, transition.ExecutorVersion, transition.SelectedCandidateId),
                target.QualifiedItemId);
            Require(effect.SlotIndex == selected.SlotIndex && effect.Quality == selected.Quality && effect.UnitPrice == selected.UnitPrice &&
                effect.BinTileX == target.BinTileX && effect.BinTileY == target.BinTileY &&
                effect.PendingValueBefore == target.PendingValueAt(index) && effect.PendingValueAfter == target.PendingValueAt(index + 1),
                "native deposit differs from chosen variant/prefix/bin.");
            target.VerifyNativeVariantSource(batch.After, index + 1);
            deposits.AddRange(batch.Deposits); expected = batch.After;
        }
        var ingress = MoneyRevenueResourceNeutralTerminalRouteVerifier.VerifyHomeIngress(expected, request.SettlementRouteTransitions);
        var recurrence = MoneyRevenueCropTerminalSettlementModeVerifier.VerifyShippingRecurrence(request.RevenueRecurrenceManifestPath,
            request.RevenueRecurrenceReceiptPath, request.SettlementRouteTransitions.Length == 0 ? transitions[^1].AfterSnapshotPath :
                request.SettlementRouteTransitions[^1].AfterSnapshotPath, ingress.TerminalActionBefore.StateHash,
            target.PendingValueAt(target.RequiredQuantity));
        return new()
        {
            SchemaVersion = "crop_teacher_native_variant_terminal_proof.v1", Status = "verified_native_crop_terminal_settlement",
            RequestPath = Path.GetFullPath(requestPath), RequestSha256 = Hash(requestPath),
            CropProductionPlanPath = Path.GetFullPath(request.CropProductionPlanPath), CropProductionPlanSha256 = Hash(request.CropProductionPlanPath),
            OutputRetentionProofPath = Path.GetFullPath(target.RetentionProofPath), OutputRetentionProofSha256 = target.RetentionProofSha256,
            EvidenceRequestId = retention.EvidenceRequestId, CommitmentId = retention.CommitmentId, WaveIndex = retention.WaveIndex,
            HarvestItemQualifiedId = target.QualifiedItemId, MinimumUnits = retention.MinimumUnits, MinimumGrossRevenue = retention.MinimumGrossRevenue,
            TerminalKind = MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement, TerminalStartStateHash = start.StateHash,
            TerminalActionBeforeStateHash = beforeHash, TerminalActionAfterStateHash = recurrence.FinalStateHash,
            RouteTransitions = route, ShippingDeposits = deposits.ToArray(), SettlementRouteTransitions = ingress.Evidence,
            // There is no scalar slot/price for this version. Each verified unit carries its own exact fields.
            SlotIndex = -1, UnitPrice = 0, SettledQuantity = deposits.Count,
            SettledRevenue = checked((int)target.PendingValueAt(target.RequiredQuantity)),
            RevenueRecurrenceManifestPath = recurrence.ManifestPath, RevenueRecurrenceManifestSha256 = recurrence.ManifestSha256,
            RevenueRecurrenceReceiptPath = recurrence.ReceiptPath, RevenueRecurrenceReceiptSha256 = recurrence.ReceiptSha256,
            NativeTerminalSettlementVerified = true, FormalProductTrainingAuthorized = false
        };
    }

    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "native variant terminal");
    private static string Hash(string path) => CurrentTeacherFrontierSupport.HashFile(path);
    private static bool Same(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException("Native variant terminal: " + reason); }
}
