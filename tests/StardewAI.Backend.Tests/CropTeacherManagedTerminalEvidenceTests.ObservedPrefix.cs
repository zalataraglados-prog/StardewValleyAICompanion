using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedTerminalEvidenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservedTerminalProofCombinesOriginalPrefixAndRemainingDepositsThenReplaysWithoutWriting(bool allDeposited)
    {
        var golden = await (allDeposited ? CompleteObservedGolden.Value : PartialObservedGolden.Value);
        var f = new Case(golden); var built = await f.Build();
        var proof = Read<MoneyRevenueCropTerminalSettlementProof>(built.TerminalProofPath);
        var wrapper = Read<CropTeacherNativeVariantTerminalRequest>(proof.RequestPath);
        Assert.Equal("crop_teacher_native_variant_terminal_proof.v3", proof.SchemaVersion);
        Assert.Equal("crop_teacher_native_variant_terminal_request.v3", wrapper.SchemaVersion);
        Assert.NotNull(wrapper.ObservedShippingPrefix);
        Assert.Equal(allDeposited ? 0 : 1, wrapper.Settlement.ShippingDepositTransitions.Length);
        Assert.Equal(2, proof.SettledQuantity); Assert.Equal(2, proof.ShippingDeposits.Length);
        Assert.Equal(new[] { 0, 1 }, proof.ShippingDeposits.Select(row => row.TransitionIndex));
        Assert.Equal(golden.Target.PendingValueAt(2), proof.SettledRevenue);
        Assert.Equal(Read<SnapshotEnvelope>(golden.FinalPath).StateHash, proof.TerminalActionAfterStateHash);
        Assert.False(proof.FormalProductTrainingAuthorized);
        Assert.Single(proof.SettlementRouteTransitions);
        var rebuilt = typeof(MoneyRevenueCropProductionProofCheckpointBuilder).GetMethod("RebuildCollectedTerminal", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [proof, true]);
        Assert.Equal(JsonSerializer.Serialize(proof, JsonOptions), JsonSerializer.Serialize(rebuilt, JsonOptions));
        Assert.Throws<TargetInvocationException>(() => typeof(MoneyRevenueCropProductionProofCheckpointBuilder)
            .GetMethod("RebuildCollectedTerminal", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [proof, false]));
        var bytes = RetainedBytes(f.Output);
        Assert.Equal(built, await Replay(f, golden)); AssertRetainedBytes(bytes, f.Output);
        foreach (var input in golden.Hashes) Assert.Equal(input.Value, Hash(input.Key));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("missing_prefix")]
    [InlineData("replay_prefix")]
    [InlineData("run")]
    [InlineData("whole_quantity")]
    [InlineData("dropped_control")]
    public async Task ObservedTerminalProofRejectsLostOrReplayedOriginalWorkWithoutModifyingInputs(string fault)
    {
        var golden = await PartialObservedGolden.Value;
        var f = new Case(golden); var built = await f.Build();
        var proof = Read<MoneyRevenueCropTerminalSettlementProof>(built.TerminalProofPath);
        var wrapper = Read<CropTeacherNativeVariantTerminalRequest>(proof.RequestPath);
        switch (fault)
        {
            case "legacy": wrapper = wrapper with { SchemaVersion = "crop_teacher_native_variant_terminal_request.v2" }; break;
            case "missing_prefix": wrapper = wrapper with { ObservedShippingPrefix = null }; break;
            case "replay_prefix": wrapper.Settlement.ShippingDepositTransitions[0].ExecutionReceiptPath = wrapper.ObservedShippingPrefix!.Transitions[0].ExecutionReceiptPath; break;
            case "run": wrapper.Settlement.ShippingDepositTransitions[0].RunId = "foreign-run"; break;
            case "whole_quantity": wrapper = wrapper with { Target = wrapper.Target with { RequiredQuantity = 1, AcceptedDepositCount = 1 } }; break;
            case "dropped_control": wrapper = wrapper with { NativeControlCheckpoints = null }; break;
        }
        var path = Path.Combine(f.Output, "declared-invalid-" + fault + ".json"); File.WriteAllText(path, JsonSerializer.Serialize(wrapper, JsonOptions));
        Assert.Throws<InvalidDataException>(() => CropTeacherNativeVariantTerminalProof.Build(path));
        foreach (var input in golden.Hashes) Assert.Equal(input.Value, Hash(input.Key));
    }
}
