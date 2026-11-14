using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedTerminalEvidenceTests
{
    [Fact]
    public async Task NativeVariantAggregateRejectsSparseLegacyGoldenWithoutInventingNativeFields()
    {
        var golden = await Shared.Value;
        var f = new Case(golden);
        var initial = Read<StardewAI.Contracts.State.SnapshotEnvelope>(golden.ProductionFixture.SourcePath);
        var bin = initial.State["farm"].GetProperty("shipping_bins").GetProperty("value").EnumerateArray().Single(row =>
            row.GetProperty("tile_x").GetInt32() == f.Target.BinTileX && row.GetProperty("tile_y").GetInt32() == f.Target.BinTileY);
        f.Target = f.Target with { NativeVariants = new("crop_teacher_native_terminal_variants.v1",
            bin.GetProperty("bin_scope").GetString()!, new(f.Target.SlotIndex, f.Target.Quality, f.Target.UnitPrice, f.Target.InitialQuantity)) };
        // The legacy golden omits accessibility and full stock graphs; it is not a native-mode positive fixture.
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Build());
        Assert.Contains("observed stand accessibility", error.Message);
        Assert.False(Directory.Exists(f.Output));
        foreach (var row in golden.Hashes) Assert.Equal(row.Value, Hash(row.Key));
    }

    [Fact]
    public async Task OriginalProofClosesDeclaredGoldenWithoutSaveOrTrainingClaimsAndWithoutOverwrite()
    {
        var golden = await Shared.Value;
        var f = new Case(golden);
        var result = await f.Build();
        var proof = Read<MoneyRevenueCropTerminalSettlementProof>(result.TerminalProofPath);
        Assert.True(proof.NativeTerminalSettlementVerified);
        Assert.Equal(proof.SettledRevenue, result.SettledRevenue);
        Assert.Equal(golden.Target.RequiredQuantity, proof.SettledQuantity);
        Assert.Equal(Hash(result.ManifestPath), result.ManifestSha256);
        var manifest = Read<System.Text.Json.JsonElement>(result.ManifestPath);
        Assert.True(manifest.GetProperty("terminal_settlement_verified").GetBoolean());
        Assert.False(manifest.GetProperty("semantic_saved_content_verified").GetBoolean());
        Assert.False(manifest.GetProperty("formal_product_training_authorized").GetBoolean());
        foreach (var row in golden.Hashes) Assert.Equal(row.Value, Hash(row.Key));
        await Assert.ThrowsAsync<IOException>(() => f.Build());
    }

    [Theory]
    [InlineData("pending_only", "missing final native settlement/head")]
    [InlineData("cursor", "target differs")]
    [InlineData("missing_deposit", "suffix sequence/head/native identity differs")]
    [InlineData("foreign_transition", "unsupported or unconsumed")]
    [InlineData("head", "missing final native settlement/head")]
    [InlineData("manifest_hash", "production manifest hash differs")]
    [InlineData("target_price", "retained slot/variant/price differs")]
    public async Task DeclaredDriftsFailClosedAtTheirOwnBoundary(string fault, string reason)
    {
        var golden = await Shared.Value;
        var f = new Case(golden);
        switch (fault)
        {
            case "pending_only":
                f.Transitions.RemoveAt(f.Transitions.Count - 1);
                f.FinalPath = f.Transitions[^1].NativeTransition.AfterSnapshotPath;
                break;
            case "cursor": f.Target = f.Target with { AcceptedDepositCount = 1 }; break;
            case "missing_deposit":
                f.Transitions.RemoveAt(f.Transitions.FindLastIndex(row => row.OperationKind == "terminal_shipping"));
                break;
            case "foreign_transition":
                var index = f.Transitions.FindIndex(row => row.OperationKind == "supporting_route");
                f.Transitions[index] = f.Transitions[index] with { OperationKind = "control_recovery" };
                break;
            case "head": f.FinalPath = golden.ProductionFixture.SourcePath; break;
            case "manifest_hash": f.Production = f.Production with { ManifestSha256 = new string('0', 64) }; break;
            case "target_price": f.Target = f.Target with { UnitPrice = f.Target.UnitPrice + 1 }; break;
        }
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Build());
        Assert.Contains(reason, error.Message);
        Assert.False(Directory.Exists(f.Output));
    }
}
