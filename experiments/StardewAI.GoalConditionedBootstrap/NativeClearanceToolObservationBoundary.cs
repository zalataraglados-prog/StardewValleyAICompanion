using StardewAI.Contracts.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedClearanceToolCall(int Ordinal, string ToolReferenceId,
    string QualifiedToolId, int X, int Y, long GameTick, float StaminaAtEntry,
    float StaminaAtExit, NativeClearanceToolCostProjection Debit)
{
    public int NativePower => Debit.Input.Tool == NativeClearanceToolKind.Axe
        ? Debit.Input.ArgumentPower : Debit.Input.FarmerToolPower;
}

public sealed record NativeClearanceToolInputSummary(string ScopeId, string CaptureSessionId,
    IReadOnlyList<NativeObservedClearanceToolCall> Calls)
{
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

// Verifies recorded per-call debit inputs, not all effects of the surrounding action.
public static class NativeClearanceToolObservationBoundary
{
    public static NativeClearanceToolInputSummary Read(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, long earliestTick, long latestTick,
        string locationId, string actorId)
    {
        Require(request.ActionOriginToolInputs && request.ActionOriginSnapshotAnchor,
            "tool input observation was not explicitly requested with a snapshot anchor");
        var original = NativeOutputObservationBoundary.Read(request, receipt, earliestTick, latestTick, locationId);
        Require(Text(actorId) && receipt.ClearanceToolCalls is { Length: <= 256 },
            "actor or complete bounded tool call list is unavailable");
        var result = new List<NativeObservedClearanceToolCall>();
        var previousTick = receipt.BeforeGameTick;
        var completed = new HashSet<int>();
        var toolTypes = new Dictionary<Guid, string>();
        var active = new Stack<int>();
        var nextEntry = 1;
        foreach (var row in receipt.ClearanceToolCalls!)
        {
            Require(row is not null && row.SchemaVersion == "native_clearance_tool_call.v1" &&
                    row.Ordinal >= 1 && row.Ordinal <= receipt.ClearanceToolCalls.Length && completed.Add(row.Ordinal) &&
                    Guid.TryParseExact(row.ToolReferenceId, "N", out _) &&
                    Text(row.QualifiedToolId) && row.ActorId == actorId && row.LocationId == locationId,
                "tool call schema, ordinal, reference or actor/location differs");
            var reference = Guid.ParseExact(row!.ToolReferenceId, "N");
            Require(!toolTypes.TryGetValue(reference, out var previousType) || previousType == row.ToolRuntimeType,
                "one native tool reference changed its CLR runtime type");
            toolTypes[reference] = row.ToolRuntimeType;
            // Ordinals are entry order; rows retain finalizer callback order, including nesting.
            while (nextEntry <= row.Ordinal) active.Push(nextEntry++);
            Require(active.Count != 0 && active.Pop() == row.Ordinal,
                "tool completion order cannot belong to the recorded synchronous invocation stack");
            var kind = row.ToolRuntimeType switch
            {
                "StardewValley.Tools.Axe" => NativeClearanceToolKind.Axe,
                "StardewValley.Tools.Pickaxe" => NativeClearanceToolKind.Pickaxe,
                _ => throw new InvalidDataException("Native clearance tool input: unsupported native tool type.")
            };
            Require(row.X.HasValue && row.Y.HasValue && row.ArgumentPower.HasValue && row.NativePower.HasValue &&
                    row.BeforeGameTick.HasValue && row.InputGameTick.HasValue && row.DebitGameTick.HasValue &&
                    row.AfterGameTick.HasValue && row.BeforeGameTick >= previousTick &&
                    row.InputGameTick == row.BeforeGameTick && row.DebitGameTick == row.InputGameTick &&
                    row.AfterGameTick == row.DebitGameTick && row.AfterGameTick <= receipt.AfterGameTick,
                "actual call arguments or same synchronous native tick are unavailable");
            Require(Finite(row.StaminaAtEntry) && Finite(row.StaminaBeforeDebit) &&
                    Finite(row.StaminaAfterDebit) && Finite(row.StaminaAtExit) &&
                    row.MaxStamina is >= 0 && row.EffectiveSkillLevel is >= 0 && row.IsEfficient.HasValue &&
                    row.HasStaminaDecreaseProtection.HasValue && row.StaminaSetterInvoked.HasValue,
                "actual effective debit inputs or observations are unavailable");
            Require(kind != NativeClearanceToolKind.Axe || row.NativePower == row.ArgumentPower,
                "axe native argument differs from its retained by-value argument");
            var input = new NativeClearanceToolCostInput(kind, row.StaminaBeforeDebit!.Value,
                row.MaxStamina!.Value, row.IsEfficient!.Value, row.HasStaminaDecreaseProtection!.Value,
                row.ArgumentPower!.Value, row.NativePower!.Value, row.EffectiveSkillLevel!.Value);
            var projection = NativeClearanceToolCostProjector.Project(input);
            Require(projection.StaminaSetterInvoked == row.StaminaSetterInvoked &&
                    BitConverter.SingleToInt32Bits(projection.StaminaAfter) ==
                    BitConverter.SingleToInt32Bits(row.StaminaAfterDebit!.Value),
                "actual explicit setter/skip outcome differs from the original per-call native projection");
            result.Add(new(row.Ordinal, row.ToolReferenceId, row.QualifiedToolId, row.X!.Value, row.Y!.Value,
                row.BeforeGameTick!.Value, row.StaminaAtEntry!.Value, row.StaminaAtExit!.Value, projection));
            previousTick = row.AfterGameTick!.Value;
        }
        return new(original.ScopeId, original.CaptureSessionId, result.AsReadOnly());
    }

    private static bool Text(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && !value.Any(char.IsControl);
    private static bool Finite(float? value) => value.HasValue && float.IsFinite(value.Value);
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Native clearance tool input: " + reason + ".");
    }
}
