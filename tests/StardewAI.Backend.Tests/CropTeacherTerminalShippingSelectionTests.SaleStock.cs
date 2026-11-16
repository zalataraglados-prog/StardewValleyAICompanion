using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherTerminalShippingSelectionTests
{
    [Fact]
    public async Task ReservedSelectionUsesOnlyOriginalWaveSlotsAndRestoresTheSameExplicitTarget()
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture(2, "declared.reserved.selection", true, 1);
        var evidence = await f.Build();
        var selection = OpenReserved(f, evidence);
        var target = Target(selection, 0);
        Assert.True(target.RequireSaleStockReservations);
        var variant = Assert.Single(target.ReadNativeVariants());
        Assert.Equal(1, variant.SlotIndex); Assert.Equal(2, variant.PlannedQuantity); Assert.Equal(0, variant.Quality);
        Assert.Equal(target, Target(OpenReserved(f, evidence), 0));
        Assert.Equal(target with { AcceptedDepositCount = 1 }, Target(selection, 1));
        var source = Read(f.SourcePath); Move(source, 70, 14);
        var model = Build(selection, source, 0)!;
        Assert.Contains(Assert.Single(model.Actions).Parameters, row => row.Name == "slot_index" && row.Value == "1");
        var old = Target(Unwrap(() => Selection.GetMethod("OpenNativeVariants", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [evidence, f.NativePlanPath, f.SourcePath, f.LedgerPath]))!, 0);
        Assert.False(old.RequireSaleStockReservations);
        Assert.DoesNotContain("require_sale_stock_reservations", JsonSerializer.Serialize(old, JsonOptions));
        Assert.Contains(old.ReadNativeVariants(), row => row.SlotIndex == 3);
        var roundtrip = JsonSerializer.Deserialize<CropTeacherTerminalShippingTarget>(JsonSerializer.Serialize(target, JsonOptions), JsonOptions);
        Assert.Equal(target, roundtrip);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("insufficient")]
    [InlineData("wrong_wave")]
    [InlineData("wrong_variant")]
    [InlineData("wrong_actor")]
    [InlineData("stale_ledger")]
    public async Task ReservedSelectionRejectsUnavailableOwnedStockEvenWhenShippableInventoryExists(string fault)
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture(2, "declared.reserved.negative." + fault, true, 1);
        var evidence = await f.Build();
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(f.LedgerPath), JsonOptions)!;
        var claim = Assert.Single(ledger.MaterialReservations);
        CropSaleStockReservationContract.TryRead(claim, out var purpose);
        if (fault == "missing") ledger.MaterialReservations = [];
        if (fault == "insufficient") claim.Quantity = 1;
        if (fault == "wrong_wave") claim.Purpose = CropSaleStockReservationContract.Purpose(purpose!.CommitmentId, "other-wave", purpose.InventoryIdentitySha256);
        if (fault == "wrong_variant") claim.Purpose = CropSaleStockReservationContract.Purpose(purpose!.CommitmentId, purpose.OutputRetentionKey, new string('a', 64));
        if (fault == "wrong_actor") { claim.OwnerPlayerId = 456; claim.NodeId = "player:456"; }
        if (fault == "stale_ledger") ledger.SourceStateHash = new string('0', 64);
        // Keep the original accepted fixture immutable; the negative binds its own declared ledger bytes.
        var ledgerPath = Path.Combine(f.Output, "negative-ledger.json");
        await File.WriteAllTextAsync(ledgerPath, JsonSerializer.Serialize(ledger, JsonOptions));
        var manifest = JsonNode.Parse(File.ReadAllText(evidence.ManifestPath))!.AsObject();
        manifest["accepted_ledger_path"] = ledgerPath;
        manifest["input_artifact_hashes"]![ledgerPath] = Hash(ledgerPath);
        var manifestPath = Path.Combine(f.Output, "negative-manifest.json");
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString(JsonOptions));
        var negative = evidence with { ManifestPath = manifestPath, ManifestSha256 = Hash(manifestPath) };
        var error = Assert.Throws<InvalidDataException>(() => OpenReserved(f, negative, ledgerPath));
        Assert.Contains(fault switch
        {
            "wrong_variant" => "original slot variant",
            "wrong_actor" => "unavailable material projection",
            "stale_ledger" => "current material graph",
            _ => "whole obligation"
        }, error.Message);
    }

    private static object OpenReserved(CropTeacherProductionEvidenceTests.Fixture f, CropTeacherManagedProductionEvidence evidence,
        string? ledger = null) => Unwrap(() => Selection.GetMethod("OpenNativeVariantsConsumingSaleStock", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [evidence, f.NativePlanPath, f.SourcePath, ledger ?? f.LedgerPath]))!;

    private static CropTeacherTerminalShippingTarget Target(object selection, int cursor) =>
        (CropTeacherTerminalShippingTarget)Unwrap(() => Selection.GetMethod("CreateTarget", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(selection, [cursor]))!;

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
