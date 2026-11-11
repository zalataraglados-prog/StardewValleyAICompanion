using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeClearanceConsumptionBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactFlatChainRetainsEvidenceGapsAndNoCredit(bool multiple)
    {
        var (request, receipt, before, after) = Fixture();
        if (multiple)
        {
            var second = Copy(receipt.ClearanceToolCalls![0]);
            second.Ordinal = 2;
            second.StaminaAtEntry = second.StaminaBeforeDebit = 12f;
            second.StaminaAfterDebit = second.StaminaAtExit = 10f;
            receipt.ClearanceToolCalls = [receipt.ClearanceToolCalls[0], second];
            SetEnergy(after, 10f);
        }
        var result = Read(request, receipt, before, after);
        Assert.True(result.StaminaContinuityVerified);
        Assert.Equal(multiple ? 2 : 1, result.ToolCallCount);
        Assert.Equal(14f, result.BeforeEnergy);
        Assert.Equal(multiple ? 10f : 12f, result.AfterEnergy);
        Assert.Equal(1L, result.BeforeCaptureGapTicks);
        Assert.Equal(1L, result.AfterCaptureGapTicks);
        Assert.Equal(6L, result.ResourceEvidence.ObservedPickupUnits);
        Assert.False(result.FullResourceVerified);
        Assert.False(result.ProducerProvenanceVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        receipt.ClearanceToolCalls![0].StaminaBeforeDebit = 999f;
        receipt.Collections[0].InventoryAdmissions[0].InventoryAfter = 999;
        SetEnergy(before, 999f);
        Assert.Equal(14f, result.ResourceEvidence.ClearanceToolInputs!.Calls[0].Debit.Input.StaminaBefore);
        Assert.Equal(6L, result.ResourceEvidence.ObservedPickupUnits);
        Assert.Equal(14f, result.BeforeEnergy);
    }

    [Theory]
    [InlineData(true, false, 0, 5, 14f)]
    [InlineData(false, false, 0, 5, 5f)]
    [InlineData(false, true, 1, 270, 14f)]
    [InlineData(false, false, 100, 270, -16f)]
    public void ReusesEfficientProtectionAndNativeSetterClipping(bool efficient, bool protection, int power, int max, float end)
    {
        var (request, receipt, before, after) = Fixture();
        var call = receipt.ClearanceToolCalls![0];
        call.IsEfficient = efficient; call.HasStaminaDecreaseProtection = protection;
        call.ArgumentPower = call.NativePower = power; call.MaxStamina = max;
        call.StaminaSetterInvoked = !efficient;
        call.StaminaAfterDebit = call.StaminaAtExit = end;
        SetEnergy(after, end);
        var result = Read(request, receipt, before, after);
        Assert.Equal(end, result.AfterEnergy);
        Assert.Equal(!efficient, result.ResourceEvidence.ClearanceToolInputs!.Calls[0].Debit.StaminaSetterInvoked);
    }

    [Fact]
    public void EmptyObservedCallsOnlySettleUnchangedEnergy()
    {
        var (request, receipt, before, after) = Fixture();
        receipt.ClearanceToolCalls = [];
        SetEnergy(after, 14f);
        Assert.Equal(0, Read(request, receipt, before, after).ToolCallCount);
        SetEnergy(after, 12f);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt, before, after));
    }

    [Theory]
    [InlineData("before-endpoint")]
    [InlineData("after-endpoint")]
    [InlineData("entry")]
    [InlineData("debit")]
    [InlineData("exit")]
    [InlineData("between")]
    [InlineData("one-bit")]
    [InlineData("missing-calls")]
    [InlineData("not-requested")]
    [InlineData("missing-energy")]
    [InlineData("unavailable-energy")]
    [InlineData("null-energy")]
    [InlineData("nonfinite-energy")]
    [InlineData("overflow-energy")]
    [InlineData("nan-entry")]
    [InlineData("infinite-exit")]
    [InlineData("actor")]
    [InlineData("save")]
    [InlineData("request-hash")]
    [InlineData("raw-hash")]
    public void RefusesUnsettledOrUnavailableEvidence(string change)
    {
        var (request, receipt, before, after) = Fixture();
        var row = receipt.ClearanceToolCalls![0];
        switch (change)
        {
            case "before-endpoint": SetEnergy(before, 15f); break;
            case "after-endpoint": SetEnergy(after, 13f); break;
            case "entry": row.StaminaAtEntry = 13f; break;
            case "debit": row.StaminaBeforeDebit = 15f; row.StaminaAfterDebit = 13f; break;
            case "exit": row.StaminaAtExit = 13f; break;
            case "between":
                var next = Copy(row); next.Ordinal = 2;
                receipt.ClearanceToolCalls = [row, next]; break;
            case "one-bit": SetEnergy(after, float.BitIncrement(12f)); break;
            case "missing-calls": receipt.ClearanceToolCalls = null; break;
            case "not-requested": request.ActionOriginToolInputs = false; break;
            case "missing-energy": ChangeEnergy(before, field => field.Remove("value")); break;
            case "unavailable-energy": ChangeEnergy(after, field => field["status"] = "unavailable"); break;
            case "null-energy": ChangeEnergy(after, field => field["value"] = null); break;
            case "nonfinite-energy": ChangeEnergy(after, field => field["value"] = "NaN"); break;
            case "overflow-energy": ChangeEnergy(after, field => field["value"] = JsonNode.Parse("1e100")); break;
            case "nan-entry": row.StaminaAtEntry = float.NaN; break;
            case "infinite-exit": row.StaminaAtExit = float.PositiveInfinity; break;
            case "actor": after.PlayerId.Value = "other"; break;
            case "save": after.SaveId.Value = "other"; break;
            case "request-hash": request.BeforeStateHash = "wrong"; break;
            case "raw-hash": before.StateHash = "wrong"; break;
        }
        if (change is not "request-hash" and not "raw-hash") Bind(request, receipt, before);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt, before, after));
    }

    [Fact]
    public void SignedZeroIsNotAFloatToleranceMatch()
    {
        var (request, receipt, before, after) = Fixture();
        var row = receipt.ClearanceToolCalls![0];
        row.IsEfficient = true; row.StaminaSetterInvoked = false;
        row.StaminaAtEntry = row.StaminaBeforeDebit = row.StaminaAfterDebit = row.StaminaAtExit = 0f;
        SetEnergy(before, 0f); SetEnergy(after, -0f); Bind(request, receipt, before);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt, before, after));
    }

    [Fact]
    public void DefaultCalibrationAndNestedCompletionRemainValidOldButUnsettledNew()
    {
        var (request, receipt, before, after) = Fixture();
        var original = NativeClearanceToolObservationBoundaryTests.Fixture().Receipt.ClearanceToolCalls![0];
        receipt.ClearanceToolCalls = [original];
        Assert.Single(NativeClearanceToolObservationBoundary.Read(request, receipt, 99, 111, "Farm", "123").Calls);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt, before, after));
        var inner = Copy(original); inner.Ordinal = 2;
        receipt.ClearanceToolCalls = [inner, original];
        Assert.Equal(new[] { 2, 1 }, NativeClearanceToolObservationBoundary.Read(request, receipt, 99, 111, "Farm", "123").Calls.Select(call => call.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => Read(request, receipt, before, after));
        Assert.Contains("nested completion order is unsettled", error.Message);
    }

    private static NativeClearanceConsumptionSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt,
        SnapshotEnvelope before, SnapshotEnvelope after) => NativeOutputObservationBoundary.ReadClearanceConsumption(request, receipt, before, after, "Farm");

    internal static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt, SnapshotEnvelope Before, SnapshotEnvelope After) Fixture()
    {
        var (request, receipt) = NativeClearanceToolObservationBoundaryTests.Fixture();
        var before = NativeOutputObservationSnapshotBoundaryTests.Snapshot(99, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 10)]);
        var after = NativeOutputObservationSnapshotBoundaryTests.Snapshot(111, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 16)]);
        before.PlayerId.Value = after.PlayerId.Value = "123";
        SetEnergy(before, 14f); SetEnergy(after, 12f);
        receipt.ClearanceToolCalls![0].StaminaAtEntry = 14f;
        receipt.ClearanceToolCalls[0].StaminaAtExit = 12f;
        Bind(request, receipt, before);
        return (request, receipt, before, after);
    }

    private static ActionOriginClearanceToolCall Copy(ActionOriginClearanceToolCall row) =>
        JsonSerializer.Deserialize<ActionOriginClearanceToolCall>(JsonSerializer.Serialize(row))!;
    private static void Bind(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt, SnapshotEnvelope before) =>
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
    private static void SetEnergy(SnapshotEnvelope snapshot, float energy)
    {
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
        player["energy"] = new JsonObject { ["status"] = "available", ["value"] = energy };
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }
    private static void ChangeEnergy(SnapshotEnvelope snapshot, Action<JsonObject> change)
    {
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
        change(player["energy"]!.AsObject());
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }
}
