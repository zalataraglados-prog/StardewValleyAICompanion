using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedTerminalEvidenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NativeTerminalPreservesControlsAcrossDepositsAndHomeThenReplaysWithoutWriting(bool controls, bool closeFirst)
    {
        var golden = await (controls ? closeFirst ? CloseControlGolden.Value : ControlGolden.Value : PlainNativeGolden.Value);
        var f = new Case(golden);
        var result = await f.Build();
        var proof = Read<MoneyRevenueCropTerminalSettlementProof>(result.TerminalProofPath);
        Assert.Equal(controls ? "crop_teacher_native_variant_terminal_proof.v2" : "crop_teacher_native_variant_terminal_proof.v1", proof.SchemaVersion);
        Assert.Equal(golden.Target.RequiredQuantity, proof.SettledQuantity);
        Assert.Equal(golden.Target.PendingValueAt(golden.Target.RequiredQuantity), result.SettledRevenue);
        Assert.True(proof.NativeTerminalSettlementVerified);
        Assert.False(proof.FormalProductTrainingAuthorized);
        var rebuilt = typeof(MoneyRevenueCropProductionProofCheckpointBuilder).GetMethod("RebuildCollectedTerminal", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { proof, true });
        Assert.Equal(JsonSerializer.Serialize(proof, JsonOptions), JsonSerializer.Serialize(rebuilt, JsonOptions));
        Assert.Throws<TargetInvocationException>(() => typeof(MoneyRevenueCropProductionProofCheckpointBuilder)
            .GetMethod("RebuildCollectedTerminal", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { proof, false }));
        var manifest = Read<JsonElement>(result.ManifestPath);
        Assert.Equal(golden.Transitions.Count(row => row.OwnerSequence > golden.ProductionFixture.Transitions.Count),
            manifest.GetProperty("owner_terminal_transitions").GetArrayLength());
        if (controls)
        {
            var checkpoints = Assert.IsType<CropTeacherTerminalControlCheckpointEvidence[]>(proof.NativeControlCheckpoints);
            Assert.Equal(new[] { 0, 1, 2, 3 }, checkpoints.Select(row => row.BeforeTransitionIndex));
            Assert.Equal(closeFirst ? 5 : 4, checkpoints.Sum(row => row.Transitions.Length));
            Assert.Single(proof.SettlementRouteTransitions); // Waits were not substituted for home movement.
            var request = Read<CropTeacherNativeVariantTerminalRequest>(proof.RequestPath);
            Assert.Equal(closeFirst ? "control_recovery" : "supporting_route_to_sleep",
                request.NativeControlCheckpoints![0].Transitions[0].TransitionKind);
        }
        else
        {
            Assert.Null(proof.NativeControlCheckpoints);
            Assert.DoesNotContain("native_control_checkpoints", File.ReadAllText(result.TerminalProofPath));
            Assert.DoesNotContain("nativeControlCheckpoints", File.ReadAllText(proof.RequestPath));
        }
        foreach (var row in golden.Hashes) Assert.Equal(row.Value, Hash(row.Key));
        var bytes = RetainedBytes(f.Output);
        Assert.Equal(result, await Replay(f, golden));
        AssertRetainedBytes(bytes, f.Output);
    }

    [Theory]
    [InlineData("old_version")]
    [InlineData("empty")]
    [InlineData("duplicate_position")]
    [InlineData("outside_position")]
    [InlineData("reordered")]
    [InlineData("dropped")]
    [InlineData("run")]
    [InlineData("day")]
    [InlineData("replayed_receipt")]
    [InlineData("wait_as_independent_close")]
    public async Task NativeTerminalCheckpointFaultsRejectWithoutChangingOldInputs(string fault)
    {
        var golden = await ControlGolden.Value;
        var f = new Case(golden);
        var built = await f.Build();
        var proof = Read<MoneyRevenueCropTerminalSettlementProof>(built.TerminalProofPath);
        var request = Read<CropTeacherNativeVariantTerminalRequest>(proof.RequestPath);
        var checkpoints = request.NativeControlCheckpoints!;
        switch (fault)
        {
            case "old_version": request = request with { SchemaVersion = "crop_teacher_native_variant_terminal_request.v1" }; break;
            case "empty": request = request with { NativeControlCheckpoints = [] }; break;
            case "duplicate_position": checkpoints[1] = checkpoints[1] with { BeforeTransitionIndex = 0 }; break;
            case "outside_position": checkpoints[3] = checkpoints[3] with { BeforeTransitionIndex = 4 }; break;
            case "reordered": request = request with { NativeControlCheckpoints = checkpoints.Reverse().ToArray() }; break;
            case "dropped": request = request with { NativeControlCheckpoints = checkpoints[1..] }; break;
            case "run": checkpoints[1].Transitions[0] = checkpoints[1].Transitions[0] with { RunId = "foreign-run" }; break;
            case "day": checkpoints[1].Transitions[0] = checkpoints[1].Transitions[0] with { TotalDay = golden.Target.TotalDay + 1 }; break;
            case "replayed_receipt": checkpoints[1].Transitions[0] = checkpoints[0].Transitions[0]; break;
            case "wait_as_independent_close": checkpoints[1].Transitions[0] = checkpoints[1].Transitions[0] with { TransitionKind = "control_recovery" }; break;
        }
        var path = Path.Combine(f.Output, "declared-invalid-" + fault + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(request, JsonOptions));
        Assert.Throws<InvalidDataException>(() => CropTeacherNativeVariantTerminalProof.Build(path));
        foreach (var row in golden.Hashes) Assert.Equal(row.Value, Hash(row.Key));
    }

    [Fact]
    public async Task RetainedNativeTerminalCheckpointMutationRejectsWithoutRepair()
    {
        var golden = await ControlGolden.Value;
        var f = new Case(golden);
        var built = await f.Build();
        var requestPath = Read<MoneyRevenueCropTerminalSettlementProof>(built.TerminalProofPath).RequestPath;
        EditRetained(requestPath, node => node["nativeControlCheckpoints"]![0]!["beforeTransitionIndex"] = 99);
        var before = RetainedBytes(f.Output);
        await Assert.ThrowsAsync<InvalidDataException>(() => Replay(f, golden));
        AssertRetainedBytes(before, f.Output);
    }
}
