using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteFreshTerminalReceiptBuilder
{
    public static AcquisitionRouteFreshTerminalReceiptAdmission Build(
        AcquisitionRouteExecutionBindingInputs inputs,
        string executionBindingPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string runId,
        string executorVersion) => BuildVerifiedBinding(
            inputs,
            executionBindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            runId,
            executorVersion,
            AcquisitionRouteExecutionBindingBuilder.Build(inputs));

    private static AcquisitionRouteFreshTerminalReceiptAdmission
        BuildVerifiedBinding(
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string runId,
            string executorVersion,
            AcquisitionRouteExecutionBinding expectedBinding)
    {
        var bindingPath = Path.GetFullPath(executionBindingPath);
        var receiptPath = Path.GetFullPath(executionReceiptPath);
        var afterPath = Path.GetFullPath(afterSnapshotPath);
        var beforePath = Path.GetFullPath(inputs.BeforeSnapshotPath);
        var queuePath = Path.GetFullPath(inputs.ActionQueuePath);
        var binding = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBinding>(
            bindingPath,
            "Acquisition route execution binding");
        Require(EqualJson(binding, expectedBinding),
            "Route execution binding drifted from deterministic source compilation.");

        var result = new AcquisitionRouteFreshTerminalReceiptAdmission
        {
            GoalId = binding.GoalId,
            GameVersion = binding.GameVersion,
            RouteOccurrenceId = binding.RouteOccurrenceId,
            ExecutionBindingSha256 = CurrentTeacherFrontierSupport.HashFile(
                bindingPath),
            ExecutionReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                receiptPath),
            AfterSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                afterPath),
            TerminalReceiptKind = binding.TerminalReceiptKind
        };
        if (!string.Equals(binding.SchemaVersion,
                "acquisition_route_execution_binding.v1",
                StringComparison.Ordinal) ||
            !string.Equals(binding.Status,
                "ready_for_exact_route_dispatch",
                StringComparison.Ordinal) ||
            !binding.DispatchBindingReady ||
            binding.FormalTrainingAuthorized ||
            binding.BlockingReasons.Length != 0)
        {
            result.Status = "blocked_route_execution_binding_not_ready";
            result.BlockingReasons = new[]
            {
                "fresh_terminal_receipt_execution_binding_not_ready"
            };
            return result;
        }

        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            queuePath,
            "Selected route action queue");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "After snapshot");
        var receipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
            receiptPath,
            "Queue execution receipt");
        var queueReasons = QueueExecutionReceiptValidator.Validate(
            queue,
            before,
            receipt,
            after,
            runId,
            executorVersion,
            binding.SelectedCandidateId,
            binding.BeforeStateHash,
            requireTeacherPreferenceStateRebound: false);
        if (!SameTargetDay(after, binding.GameVersion, binding.TargetTotalDay))
        {
            queueReasons = queueReasons
                .Append("fresh_terminal_receipt_after_snapshot_target_day_mismatch")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        var transition = VerifyTerminalTransition(binding, before, after);
        var transitionReasons = transition.BlockingReasons;
        if (!transition.Verified)
        {
            transitionReasons = transitionReasons
                .Append("fresh_terminal_transition_not_verified")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        var reasons = queueReasons
            .Concat(transitionReasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        result.TerminalTransition = transition;
        result.QueueExecutionVerified = queueReasons.Length == 0;
        result.FreshTerminalReceiptVerified = reasons.Length == 0;
        result.RouteTrainingEvidenceEligible = reasons.Length == 0;
        result.FormalTrainingAuthorized = false;
        result.Status = reasons.Length == 0
            ? "verified_fresh_terminal_receipt"
            : "blocked_fresh_terminal_receipt";
        result.BlockingReasons = reasons;
        return result;
    }

    private static AcquisitionTerminalTransitionEvidence VerifyTerminalTransition(
        AcquisitionRouteExecutionBinding binding,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        if (string.Equals(
                binding.TerminalReceiptKind,
                "exact_player_inventory_quantity_increase",
                StringComparison.Ordinal))
        {
            var evidence = ExactInventoryReceiptVerifier.Verify(
                before,
                after,
                binding.QualifiedItemId,
                binding.RequiredAmount,
                binding.MinimumQuality);
            return new AcquisitionTerminalTransitionEvidence
            {
                TransitionKind = binding.TerminalReceiptKind,
                QualifiedItemId = binding.QualifiedItemId,
                RequiredAmount = binding.RequiredAmount,
                MinimumQuality = binding.MinimumQuality,
                BeforeQuantity = evidence.BeforeQuantity,
                AfterQuantity = evidence.AfterQuantity,
                QuantityIncrease = evidence.QuantityIncrease,
                Resolved = evidence.Resolved,
                Verified = evidence.Verified,
                BlockingReasons = evidence.BlockingReasons
            };
        }
        if (string.Equals(
                binding.TerminalReceiptKind,
                "native_community_center_payment_completion",
                StringComparison.Ordinal))
        {
            return ExactCommunityCenterPaymentReceiptVerifier.Verify(
                before,
                after,
                binding.RequirementId,
                binding.AlternativeIndex,
                binding.RequiredAmount);
        }
        return new AcquisitionTerminalTransitionEvidence
        {
            TransitionKind = binding.TerminalReceiptKind,
            QualifiedItemId = binding.QualifiedItemId,
            RequiredAmount = binding.RequiredAmount,
            MinimumQuality = binding.MinimumQuality,
            BlockingReasons = new[]
            {
                "fresh_terminal_receipt_kind_unsupported"
            }
        };
    }

    private static bool SameTargetDay(
        SnapshotEnvelope snapshot,
        string gameVersion,
        int targetTotalDay) =>
        string.Equals(snapshot.SchemaVersion, "snapshot.v1",
            StringComparison.Ordinal) &&
        string.Equals(snapshot.GameVersion, gameVersion,
            StringComparison.Ordinal) &&
        TryStateInt(snapshot, "time", "total_days", out var totalDay) &&
        totalDay == targetTotalDay;

    private static bool TryStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out int result)
    {
        result = 0;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out var value) &&
            value.TryGetInt32(out result);
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
