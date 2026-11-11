using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeClearanceConsumptionSummary(NativeOutputResourceEvidence ResourceEvidence,
    float BeforeEnergy, float AfterEnergy, long BeforeCaptureGapTicks, long AfterCaptureGapTicks)
{
    public int ToolCallCount => ResourceEvidence.ClearanceToolInputs!.Calls.Count;
    public bool StaminaContinuityVerified => true;
    public bool FullResourceVerified => false;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    // A flat, exact energy chain only; retained pickup observations still carry no producer credit.
    public static NativeClearanceConsumptionSummary ReadClearanceConsumption(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId)
    {
        var evidence = ReadResourceEvidence(request, receipt, before, after, locationId);
        var calls = evidence.ClearanceToolInputs?.Calls
            ?? throw new InvalidDataException("Native clearance consumption: explicit tool evidence is unavailable.");
        float first = Energy(before), last = Energy(after), cursor = first;
        for (int index = 0; index < calls.Count; index++)
        {
            var call = calls[index];
            if (call.Ordinal != index + 1)
                throw new InvalidDataException("Native clearance consumption: nested completion order is unsettled; flat ordinals required.");
            if (!Same(cursor, call.StaminaAtEntry) || !Same(call.StaminaAtEntry, call.Debit.Input.StaminaBefore) ||
                !Same(call.Debit.StaminaAfter, call.StaminaAtExit))
                throw new InvalidDataException("Native clearance consumption: entry/debit/exit energy chain is unsettled.");
            cursor = call.StaminaAtExit;
        }
        if (!Same(cursor, last))
            throw new InvalidDataException("Native clearance consumption: final snapshot energy differs from the exact call chain.");
        return new(evidence, first, last, receipt.BeforeGameTick - before.GameTick, after.GameTick - receipt.AfterGameTick);
    }

    private static float Energy(SnapshotEnvelope snapshot)
    {
        var value = NativeExecutionSourceSupport.Value(snapshot, "player", "energy");
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out var energy) || !float.IsFinite(energy))
            throw new InvalidDataException("Native clearance consumption: finite original player.energy is unavailable.");
        return energy;
    }

    private static bool Same(float left, float right) =>
        float.IsFinite(left) && float.IsFinite(right) && BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right);
}
