using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherNativeVariantTerminalRequest(string SchemaVersion,
    CropTeacherTerminalShippingTarget Target, MoneyRevenueCropTerminalSettlementProofRequest Settlement)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MoneyRevenueCropNativeLifecycleTransitionRequest[]? NativeControlTail { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CropTeacherTerminalControlCheckpoint[]? NativeControlCheckpoints { get; init; }
}

// Explicit aggregate adapter; old scalar builders/readers never admit this request/version.
public static class CropTeacherNativeVariantTerminalProof
{
    public static MoneyRevenueCropTerminalSettlementProof Build(string requestPath)
    {
        var wrapper = Read<CropTeacherNativeVariantTerminalRequest>(requestPath);
        var target = wrapper.Target; var request = wrapper.Settlement;
        var interleaved = wrapper.SchemaVersion == "crop_teacher_native_variant_terminal_request.v2";
        Require((interleaved ? wrapper.NativeControlCheckpoints is { Length: > 0 } :
                wrapper.SchemaVersion == "crop_teacher_native_variant_terminal_request.v1" && wrapper.NativeControlCheckpoints is null) && target.NativeVariants is not null &&
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
        MoneyRevenueCropOutputRetentionProofBuilder.VerifyProducerTerminalTarget(retention, target, start);
        var transitions = request.ShippingDepositTransitions;
        Require(transitions is not null && transitions.Length == target.RequiredQuantity &&
            transitions.All(row => row is not null && !string.IsNullOrWhiteSpace(row.TransitionId)) &&
            transitions.Select(row => row.TransitionId).Distinct(StringComparer.Ordinal).Count() == transitions.Length &&
            transitions.Select(row => Path.GetFullPath(row.ExecutionReceiptPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == transitions.Length,
            "missing/replayed deposit evidence.");
        var expected = start;
        MoneyRevenueTerminalQueueTransitionEvidence[] controls = [];
        if (wrapper.NativeControlTail is { } controlTail)
        {
            Require(controlTail.Length > 0 && controlTail.All(row => row.TotalDay == target.TotalDay &&
                row.RunId == transitions![0].RunId && row.ExecutorVersion == transitions[0].ExecutorVersion),
                "control tail differs from terminal native identity/day.");
            controls = MoneyRevenueCropNativeLifecycleProofBuilder.VerifyNativeObservedControlTail(
                retention.AfterSnapshotPath, controlTail, controlTail[^1].AfterSnapshotPath);
            expected = Read<SnapshotEnvelope>(controlTail[^1].AfterSnapshotPath);
            target.VerifyNativeVariantSource(expected, 0, retention.MinimumGrossRevenue);
        }
        var deposits = new List<MoneyRevenueCropShippingDepositEvidence>();
        MoneyRevenueTerminalQueueTransitionEvidence[] route = [];
        var sequence = interleaved ? new CropTeacherTerminalControlSequence(wrapper, transitions![0], target.TotalDay,
            wrapper.NativeControlTail is { Length: > 0 } tail ? tail[^1].AfterSnapshotPath : retention.AfterSnapshotPath) : null;
        if (sequence is not null)
        {
            var identity = new MoneyRevenueTerminalContinuationIdentity("executor.ship_inventory_item_to_bin",
                target.QualifiedItemId, target.VariantAt(0).SlotIndex, 1, target.VariantAt(0).UnitPrice, string.Empty);
            var routing = new List<MoneyRevenueTerminalQueueTransitionEvidence>();
            foreach (var transition in request.RouteTransitions)
            {
                expected = sequence.ObserveControls(expected);
                var leg = MoneyRevenueResourceNeutralTerminalRouteVerifier.Verify(expected, [transition], identity);
                routing.AddRange(leg.Evidence.Select(row => row with { TransitionIndex = routing.Count + row.TransitionIndex }));
                expected = leg.TerminalActionBefore; sequence.Advance(transition.AfterSnapshotPath);
            }
            route = routing.ToArray();
        }
        var beforeHash = string.Empty;
        for (var index = 0; index < transitions!.Length; index++)
        {
            if (sequence is not null) expected = sequence.ObserveControls(expected);
            var selected = target.VariantAt(index); var transition = transitions[index];
            var batch = MoneyRevenueCropTerminalSettlementModeVerifier.VerifyShippingDepositBatch(expected,
                index == 0 && sequence is null ? request.RouteTransitions : [], [transition], target.QualifiedItemId,
                1, selected.UnitPrice, target.PendingValueAt(index), index);
            if (index == 0) { if (sequence is null) route = batch.RouteTransitions; beforeHash = batch.TerminalActionBefore.StateHash; }
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
            sequence?.Advance(transition.AfterSnapshotPath);
        }
        var homeEvidence = new List<MoneyRevenueTerminalQueueTransitionEvidence>();
        if (sequence is not null)
        {
            foreach (var transition in request.SettlementRouteTransitions)
            {
                expected = sequence.ObserveControls(expected);
                var leg = MoneyRevenueResourceNeutralTerminalRouteVerifier.VerifyHomeIngressSegment(expected, [transition]);
                homeEvidence.AddRange(leg.Evidence.Select(row => row with { TransitionIndex = homeEvidence.Count + row.TransitionIndex }));
                expected = leg.TerminalActionBefore; sequence.Advance(transition.AfterSnapshotPath);
            }
            expected = sequence.ObserveControls(expected);
            sequence.RequireConsumed();
        }
        var ingress = MoneyRevenueResourceNeutralTerminalRouteVerifier.VerifyHomeIngress(expected,
            sequence is null ? request.SettlementRouteTransitions : []);
        var recurrence = MoneyRevenueCropTerminalSettlementModeVerifier.VerifyShippingRecurrence(request.RevenueRecurrenceManifestPath,
            request.RevenueRecurrenceReceiptPath, sequence?.CurrentPath ?? (request.SettlementRouteTransitions.Length == 0 ? transitions[^1].AfterSnapshotPath :
                request.SettlementRouteTransitions[^1].AfterSnapshotPath), ingress.TerminalActionBefore.StateHash,
            target.PendingValueAt(target.RequiredQuantity));
        return new()
        {
            SchemaVersion = interleaved ? "crop_teacher_native_variant_terminal_proof.v2" : "crop_teacher_native_variant_terminal_proof.v1", Status = "verified_native_crop_terminal_settlement",
            RequestPath = Path.GetFullPath(requestPath), RequestSha256 = Hash(requestPath),
            CropProductionPlanPath = Path.GetFullPath(request.CropProductionPlanPath), CropProductionPlanSha256 = Hash(request.CropProductionPlanPath),
            OutputRetentionProofPath = Path.GetFullPath(target.RetentionProofPath), OutputRetentionProofSha256 = target.RetentionProofSha256,
            EvidenceRequestId = retention.EvidenceRequestId, CommitmentId = retention.CommitmentId, WaveIndex = retention.WaveIndex,
            HarvestItemQualifiedId = target.QualifiedItemId, MinimumUnits = retention.MinimumUnits, MinimumGrossRevenue = retention.MinimumGrossRevenue,
            TerminalKind = MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement, TerminalStartStateHash = start.StateHash,
            TerminalActionBeforeStateHash = beforeHash, TerminalActionAfterStateHash = recurrence.FinalStateHash,
            RouteTransitions = controls.Concat(route.Select(row => row with { TransitionIndex = row.TransitionIndex + controls.Length })).ToArray(),
            ShippingDeposits = deposits.ToArray(), SettlementRouteTransitions = sequence is null ? ingress.Evidence : homeEvidence.ToArray(),
            NativeControlCheckpoints = sequence?.Evidence.ToArray(),
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
