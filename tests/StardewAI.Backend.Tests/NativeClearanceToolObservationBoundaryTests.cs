using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeClearanceToolObservationBoundaryTests
{
    [Fact]
    public void ActualPostBaseInputIsDistinctFromParentEntryAndExit()
    {
        var (request, receipt) = Fixture();
        var result = Read(request, receipt);
        var call = Assert.Single(result.Calls);
        Assert.Equal(10f, call.StaminaAtEntry);
        Assert.Equal(1, call.NativePower);
        Assert.Equal(14f, call.Debit.Input.StaminaBefore);
        Assert.Equal(12f, call.Debit.StaminaAfter);
        Assert.Equal(13f, call.StaminaAtExit);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        receipt.ClearanceToolCalls![0].StaminaBeforeDebit = 999f;
        Assert.Equal(14f, call.Debit.Input.StaminaBefore);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void UsesActualPickaxeLocalPowerAndDistinguishesEfficientAndProtection(bool efficient, bool protectedDecrease)
    {
        var (request, receipt) = Fixture();
        var row = receipt.ClearanceToolCalls![0];
        row.ToolRuntimeType = "StardewValley.Tools.Pickaxe";
        row.QualifiedToolId = "(T)Pickaxe";
        row.ArgumentPower = 99; row.NativePower = 2; row.EffectiveSkillLevel = 12;
        row.IsEfficient = efficient; row.HasStaminaDecreaseProtection = protectedDecrease;
        row.StaminaSetterInvoked = !efficient;
        row.StaminaAfterDebit = efficient || protectedDecrease ? 14f : 14f - (6f - 12f * 0.1f);
        var call = Assert.Single(Read(request, receipt).Calls);
        Assert.Equal(99, call.Debit.Input.ArgumentPower);
        Assert.Equal(2, call.Debit.Input.FarmerToolPower);
        Assert.Equal(2, call.NativePower);
        Assert.Equal(!efficient, call.Debit.StaminaSetterInvoked);
        Assert.Equal(efficient, call.Debit.RequestedStamina is null);
    }

    [Theory]
    [InlineData(false, 5f)]
    [InlineData(true, 14f)]
    public void ZeroDebitStillRunsNativeSetterWhileEfficientSkipsIt(bool efficient, float actualAfter)
    {
        var (request, receipt) = Fixture();
        var row = receipt.ClearanceToolCalls![0];
        row.ArgumentPower = row.NativePower = 0; row.MaxStamina = 5;
        row.IsEfficient = efficient; row.StaminaSetterInvoked = !efficient;
        row.StaminaAfterDebit = actualAfter;
        Assert.Equal(actualAfter, Assert.Single(Read(request, receipt).Calls).Debit.StaminaAfter);
    }

    [Fact]
    public void EmptyObservedListDoesNotInventAnyToolCallOrWholeActionCredit()
    {
        var (request, receipt) = Fixture();
        receipt.ClearanceToolCalls = [];
        var result = Read(request, receipt);
        Assert.Empty(result.Calls);
        Assert.False(result.ResourceCreditAuthorized);
        receipt.ClearanceToolCalls = null;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("schema_version")]
    [InlineData("x")]
    [InlineData("y")]
    [InlineData("argument_power")]
    [InlineData("native_power")]
    [InlineData("before_game_tick")]
    [InlineData("input_game_tick")]
    [InlineData("debit_game_tick")]
    [InlineData("after_game_tick")]
    [InlineData("stamina_at_entry")]
    [InlineData("stamina_before_debit")]
    [InlineData("stamina_after_debit")]
    [InlineData("stamina_at_exit")]
    [InlineData("max_stamina")]
    [InlineData("effective_skill_level")]
    [InlineData("is_efficient")]
    [InlineData("has_stamina_decrease_protection")]
    [InlineData("stamina_setter_invoked")]
    public void MissingWireInputIsNotReplacedWithZeroOrFalse(string field)
    {
        var (request, receipt) = Fixture();
        var json = JsonSerializer.SerializeToNode(receipt.ClearanceToolCalls![0])!.AsObject();
        json.Remove(field);
        receipt.ClearanceToolCalls = [json.Deserialize<ActionOriginClearanceToolCall>()!];
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("anchor")]
    [InlineData("schema")]
    [InlineData("reference")]
    [InlineData("type")]
    [InlineData("tool-id")]
    [InlineData("actor")]
    [InlineData("location")]
    [InlineData("ordinal")]
    [InlineData("begin-tick")]
    [InlineData("input-tick")]
    [InlineData("debit-tick")]
    [InlineData("end-tick")]
    [InlineData("power")]
    [InlineData("setter")]
    [InlineData("debit")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("max")]
    [InlineData("skill")]
    [InlineData("null-row")]
    [InlineData("limit")]
    [InlineData("foreign-scope")]
    public void RejectsUnboundOrIncompleteNativeInputs(string change)
    {
        var (request, receipt) = Fixture();
        var row = receipt.ClearanceToolCalls![0];
        switch (change)
        {
            case "request": request.ActionOriginToolInputs = false; break;
            case "anchor": request.ActionOriginSnapshotAnchor = false; break;
            case "schema": row.SchemaVersion = "v2"; break;
            case "reference": row.ToolReferenceId = "absent"; break;
            case "type": row.ToolRuntimeType = "Custom.Axe"; break;
            case "tool-id": row.QualifiedToolId = ""; break;
            case "actor": row.ActorId = "other"; break;
            case "location": row.LocationId = "Town"; break;
            case "ordinal": row.Ordinal = 2; break;
            case "begin-tick": row.BeforeGameTick = 99; break;
            case "input-tick": row.InputGameTick = 106; break;
            case "debit-tick": row.DebitGameTick = 106; break;
            case "end-tick": row.AfterGameTick = 106; break;
            case "power": row.NativePower = 2; break;
            case "setter": row.StaminaSetterInvoked = false; break;
            case "debit": row.StaminaAfterDebit = float.BitIncrement(12f); break;
            case "nan": row.StaminaAtEntry = float.NaN; break;
            case "infinity": row.StaminaAtExit = float.PositiveInfinity; break;
            case "max": row.MaxStamina = -1; break;
            case "skill": row.EffectiveSkillLevel = -1; break;
            case "null-row": receipt.ClearanceToolCalls = [null!]; break;
            case "limit": receipt.ClearanceToolCalls = Enumerable.Repeat(row, 257).ToArray(); break;
            case "foreign-scope": receipt.RequestNonce += "other"; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameNativeReferenceCannotChangeClrToolType(bool upperCaseReference)
    {
        var (request, receipt) = Fixture();
        var first = receipt.ClearanceToolCalls![0];
        var second = JsonSerializer.Deserialize<ActionOriginClearanceToolCall>(JsonSerializer.Serialize(first))!;
        second.Ordinal = 2;
        if (upperCaseReference) second.ToolReferenceId = first.ToolReferenceId.ToUpperInvariant();
        second.ToolRuntimeType = "StardewValley.Tools.Pickaxe";
        second.QualifiedToolId = "(T)Pickaxe";
        second.ArgumentPower = 99; second.NativePower = 0;
        receipt.ClearanceToolCalls = [first, second];
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void RetainsCompletionOrderAndEntryOrdinalsForNestedCallsWithoutSorting()
    {
        var (request, receipt) = Fixture();
        var first = receipt.ClearanceToolCalls![0];
        var second = JsonSerializer.Deserialize<ActionOriginClearanceToolCall>(JsonSerializer.Serialize(first))!;
        second.Ordinal = 2;
        receipt.ClearanceToolCalls = [first, second];
        Assert.Equal(2, Read(request, receipt).Calls.Count);
        receipt.ClearanceToolCalls = [second, first];
        Assert.Equal(new[] { 2, 1 }, Read(request, receipt).Calls.Select(call => call.Ordinal));
        var third = JsonSerializer.Deserialize<ActionOriginClearanceToolCall>(JsonSerializer.Serialize(first))!;
        third.Ordinal = 3;
        receipt.ClearanceToolCalls = [third, first, second];
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        receipt.ClearanceToolCalls = [first, first];
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    private static NativeClearanceToolInputSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt) =>
        NativeClearanceToolObservationBoundary.Read(request, receipt, 99, 111, "Farm", "123");

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) Fixture()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        request.ActionOriginToolInputs = request.ActionOriginSnapshotAnchor = true;
        receipt.ClearanceToolCalls = [new()
        {
            SchemaVersion = "native_clearance_tool_call.v1", Ordinal = 1,
            ToolReferenceId = Guid.NewGuid().ToString("N"), ToolRuntimeType = "StardewValley.Tools.Axe",
            QualifiedToolId = "(T)Axe", ActorId = "123", LocationId = "Farm", X = 640, Y = 704,
            ArgumentPower = 1, NativePower = 1, BeforeGameTick = 105, InputGameTick = 105,
            DebitGameTick = 105, AfterGameTick = 105, StaminaAtEntry = 10f, StaminaBeforeDebit = 14f,
            StaminaAfterDebit = 12f, StaminaAtExit = 13f, MaxStamina = 270, EffectiveSkillLevel = 0,
            IsEfficient = false, HasStaminaDecreaseProtection = false, StaminaSetterInvoked = true
        }];
        return (request, receipt);
    }
}
